using System.Data;
using Annap.CoffeeQrOrdering.Domain;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Extensions;

internal static class OrderWorkflowEndpoints
{
    internal sealed record MarkOrderPaidRequest(string? PaymentMethod);

    internal sealed record MarkItemPreparedRequest(int? PreparedQuantity, bool? IsPrepared);

    public static void MapOrderWorkflowEndpoints(this WebApplication app)
    {
        app.MapPost("/api/staff/orders/{orderId:guid}/mark-paid", async (
            HttpContext http,
            Guid orderId,
            MarkOrderPaidRequest? body,
            IOrderPaymentWorkflowService paymentWorkflow,
            CancellationToken ct) =>
        {
            if (!StaffAuthorizationHelper.CanMarkPaid(http.User))
                return Results.Forbid();

            var (confirmerName, accountId) = StaffPaymentConfirmerHelper.ResolveConfirmer(http.User);
            var result = await paymentWorkflow.MarkPaidAsync(
                orderId,
                confirmerName,
                body?.PaymentMethod,
                accountId,
                ct);

            return result.Outcome switch
            {
                MarkPaidOutcome.NotFound => Results.NotFound(),
                MarkPaidOutcome.Cancelled => Results.BadRequest(new { error = "Order is cancelled." }),
                MarkPaidOutcome.Completed => Results.BadRequest(new { error = "Order is already completed." }),
                MarkPaidOutcome.InvalidState => Results.BadRequest(new { error = "Order cannot be marked paid from its current state." }),
                MarkPaidOutcome.SerializationConflict => Results.Json(
                    new { error = "Another hand moved this ticket first—refresh the board." },
                    statusCode: StatusCodes.Status409Conflict),
                MarkPaidOutcome.Success => Results.Ok(new
                {
                    result.Order!.Id,
                    staffStatus = StaffOrderStatusHelper.ToStaffStatus(result.Order.Status),
                    boardColumn = StaffOrderBoardColumnHelper.ToColumn(result.Order.Status),
                    result.Order.PaidAtUtc,
                    result.Order.BillNumber,
                    bill = result.Bill,
                    replay = result.Replay
                }),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        }).RequireAuthorization("StaffCheckout");

        app.MapPost("/api/staff/orders/{orderId:guid}/complete", async (
            HttpContext http,
            Guid orderId,
            AppDbContext db,
            IOrderStatusNotifier notifier,
            CancellationToken ct) =>
        {
            if (!StaffAuthorizationHelper.CanComplete(http.User))
                return Results.Forbid();

            var (actorName, actorAccountId) = StaffBaristaActorHelper.ResolveActor(http.User);

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var order = await db.Orders
                    .Include(o => o.Items)
                    .ThenInclude(i => i.MenuItem)
                    .FirstOrDefaultAsync(o => o.Id == orderId, ct);
                if (order is null)
                {
                    await tx.RollbackAsync(ct);
                    return Results.NotFound();
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    await tx.RollbackAsync(ct);
                    return Results.BadRequest(new { error = "Order is cancelled." });
                }

                if (order.Status == OrderStatus.Completed)
                {
                    await tx.RollbackAsync(ct);
                    var billDone = OrderBillHelper.BuildPaidReceipt(order);
                    return Results.Ok(new
                    {
                        order.Id,
                        staffStatus = StaffOrderStatusHelper.ToStaffStatus(order.Status),
                        boardColumn = StaffOrderBoardColumnHelper.Completed,
                        order.CompletedAtUtc,
                        bill = billDone,
                        replay = true
                    });
                }

                if (!StaffOrderBoardColumnHelper.CanComplete(order.Status))
                {
                    await tx.RollbackAsync(ct);
                    return Results.BadRequest(new { error = "Only paid orders can be completed." });
                }

                if (!OrderItemPreparationHelper.IsOrderFullyPrepared(order))
                {
                    await tx.RollbackAsync(ct);
                    return Results.BadRequest(new
                    {
                        error = "items_not_prepared",
                        message = "Vui lòng tick đủ món đã pha chế trước khi hoàn thành đơn.",
                        messageVi = "Vui lòng tick đủ món đã pha chế trước khi hoàn thành đơn."
                    });
                }

                var now = DateTimeOffset.UtcNow;
                order.Status = OrderStatus.Completed;
                order.StatusChangedAtUtc = now;
                order.CompletedAtUtc = now;
                order.CompletedBy = actorName;
                order.CompletedByAccountId = actorAccountId;
                if (order.PaidAtUtc is null)
                    order.PaidAtUtc = now;
                order.BillNumber = OrderBillHelper.EnsureBillNumber(order);

                await OperationalAudit.AppendAsync(db, "order.complete", actorName, order.Id, "", ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                var bill = OrderBillHelper.BuildPaidReceipt(order);
                var pulse = BuildWorkflowPulse(order, bill);
                await notifier.NotifyGuestOrderWorkflowAsync(order.Id, pulse, ct);
                await notifier.NotifyStaffBoardWorkflowAsync(pulse, ct);

                return Results.Ok(new
                {
                    order.Id,
                    staffStatus = StaffOrderStatusHelper.ToStaffStatus(order.Status),
                    boardColumn = StaffOrderBoardColumnHelper.Completed,
                    order.CompletedAtUtc,
                    completedBy = order.CompletedBy,
                    bill
                });
            }
            catch (DbUpdateException ex) when (OrderSubmitHelper.IsSerializationConflict(ex))
            {
                await tx.RollbackAsync(ct);
                return Results.Json(
                    new { error = "Another hand moved this ticket first—refresh the board." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        }).RequireAuthorization("StaffBarista");

        app.MapPost("/api/staff/orders/{orderId:guid}/items/{itemId:guid}/prepared", async (
            HttpContext http,
            Guid orderId,
            Guid itemId,
            MarkItemPreparedRequest? body,
            AppDbContext db,
            IOrderStatusNotifier notifier,
            CancellationToken ct) =>
        {
            if (!StaffAuthorizationHelper.CanPrepareItems(http.User))
                return Results.Forbid();

            var (actorName, actorAccountId) = StaffBaristaActorHelper.ResolveActor(http.User);

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var order = await db.Orders
                    .Include(o => o.Items)
                    .ThenInclude(i => i.MenuItem)
                    .FirstOrDefaultAsync(o => o.Id == orderId, ct);
                if (order is null)
                {
                    await tx.RollbackAsync(ct);
                    return Results.NotFound();
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    await tx.RollbackAsync(ct);
                    return Results.BadRequest(new { error = "Order is cancelled." });
                }

                if (order.Status == OrderStatus.Completed)
                {
                    await tx.RollbackAsync(ct);
                    return Results.BadRequest(new { error = "Preparation is locked after order completion." });
                }

                if (!OrderItemPreparationHelper.CanEditPreparation(order.Status))
                {
                    await tx.RollbackAsync(ct);
                    return Results.BadRequest(new { error = "Only paid orders can be prepared." });
                }

                var item = order.Items.FirstOrDefault(i => i.Id == itemId);
                if (item is null)
                {
                    await tx.RollbackAsync(ct);
                    return Results.NotFound();
                }

                var now = DateTimeOffset.UtcNow;
                var changed = false;
                if (body?.IsPrepared is true)
                    changed = OrderItemPreparationHelper.MarkItemFullyPrepared(item, true, actorName, actorAccountId, now);
                else if (body?.IsPrepared is false)
                    changed = OrderItemPreparationHelper.MarkItemFullyPrepared(item, false, actorName, actorAccountId, now);
                else
                {
                    if (body?.PreparedQuantity is null)
                    {
                        await tx.RollbackAsync(ct);
                        return Results.BadRequest(new { error = "preparedQuantity or isPrepared is required." });
                    }

                    changed = OrderItemPreparationHelper.SetPreparedQuantity(
                        item, body.PreparedQuantity.Value, actorName, actorAccountId, now);
                }

                if (changed)
                {
                    await OperationalAudit.AppendAsync(db, "order.item_prepared", actorName, order.Id,
                        $"item={item.Id};qty={item.PreparedQuantity}/{item.Quantity}", ct);
                    await db.SaveChangesAsync(ct);
                }

                await tx.CommitAsync(ct);

                var pulse = BuildWorkflowPulse(order, OrderBillHelper.BuildPaidReceipt(order));
                await notifier.NotifyStaffBoardWorkflowAsync(pulse, ct);

                var progress = OrderItemPreparationHelper.CountProgress(order);
                return Results.Ok(new
                {
                    orderId = order.Id,
                    item = ProjectStaffBoardItem(item),
                    preparationDone = progress.Done,
                    preparationTotal = progress.Total,
                    allItemsPrepared = OrderItemPreparationHelper.IsOrderFullyPrepared(order)
                });
            }
            catch (DbUpdateException ex) when (OrderSubmitHelper.IsSerializationConflict(ex))
            {
                await tx.RollbackAsync(ct);
                return Results.Json(
                    new { error = "Another hand moved this ticket first—refresh the board." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        }).RequireAuthorization("StaffBarista");

        app.MapGet("/api/staff/orders/{orderId:guid}/bill", async (
            HttpContext http,
            Guid orderId,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!StaffAuthorizationHelper.CanViewBill(http.User))
                return Results.Forbid();

            var order = await db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null)
                return Results.NotFound();

            if (!OrderBillHelper.CanExposeBillToGuest(order.Status))
            {
            if (StaffOrderBoardColumnHelper.IsAwaitingPayment(order.Status))
            {
                return Results.Ok(new
                {
                    pendingPayment = true,
                    summary = OrderBillHelper.BuildCheckBill(order)
                });
            }

                return Results.BadRequest(new { error = "Bill is available after payment is confirmed." });
            }

            return Results.Ok(OrderBillHelper.BuildPaidReceipt(order));
        }).RequireAuthorization("BillView");

        app.MapGet("/api/orders/{orderId:guid}/bill", async (
            HttpRequest http,
            Guid orderId,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var order = await db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null)
                return Results.NotFound();

            if (!GuestSessionTokens.Matches(order.GuestSessionToken, http.Query["token"].FirstOrDefault()))
                return Results.NotFound();

            if (StaffOrderBoardColumnHelper.IsAwaitingPayment(order.Status))
            {
                return Results.Ok(new
                {
                    pendingPayment = true,
                    summary = OrderBillHelper.BuildCheckBill(order),
                    messageVi = "Nhân viên sẽ đến kiểm tra lại đơn và hỗ trợ thanh toán.",
                    messageEn = "Staff will come to confirm your order and help with payment."
                });
            }

            if (!OrderBillHelper.CanExposeBillToGuest(order.Status))
                return Results.NotFound();

            return Results.Ok(OrderBillHelper.BuildPaidReceipt(order));
        }).AllowAnonymous();

        app.MapGet("/api/guest/bank-transfer", (BankTransferQrBuilder builder) =>
            Results.Ok(builder.BuildGuestAvailability())).AllowAnonymous();

        app.MapGet("/api/orders/{orderId:guid}/transfer-qr", async (
            HttpRequest http,
            Guid orderId,
            AppDbContext db,
            BankTransferQrBuilder builder,
            CancellationToken ct) =>
        {
            var order = await db.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null)
                return Results.NotFound();

            if (!GuestSessionTokens.Matches(order.GuestSessionToken, http.Query["token"].FirstOrDefault()))
                return Results.NotFound();

            return Results.Ok(builder.Build(order));
        }).AllowAnonymous();
    }

    internal static object BuildWorkflowPulse(Order order, OrderBillDto? bill = null)
    {
        var track = CustomerTrackStatusHelper.Resolve(order.Status);
        var pendingPayment = StaffOrderBoardColumnHelper.IsAwaitingPayment(order.Status);
        var exposeBill = track.showBill ? bill : null;
        var checkBill = pendingPayment ? OrderBillHelper.BuildCheckBill(order) : null;
        var (pendingStatusVi, pendingStatusEn) = OrderPaymentMethods.PendingStatusLabels(order.PaymentMethod);
        var (methodVi, methodEn) = OrderPaymentMethods.Labels(order.PaymentMethod);
        return new
        {
            atUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            orderId = order.Id,
            orderNumber = OrderBillHelper.EnsureBillNumber(order),
            status = order.Status.ToString(),
            boardColumn = StaffOrderBoardColumnHelper.ToColumn(order.Status),
            staffStatus = StaffOrderStatusHelper.ToStaffStatus(order.Status),
            tableCode = order.TableCode,
            paymentMethod = order.PaymentMethod,
            paymentMethodLabelVi = methodVi,
            paymentMethodLabelEn = methodEn,
            pendingStatusLabelVi = pendingStatusVi,
            pendingStatusLabelEn = pendingStatusEn,
            paidAtUtc = order.PaidAtUtc,
            completedAtUtc = order.CompletedAtUtc,
            phaseKey = track.key,
            titleVi = track.titleVi,
            lineVi = track.lineVi,
            titleEn = track.titleEn,
            lineEn = track.lineEn,
            isComplete = track.isComplete,
            pendingPayment,
            showBill = track.showBill,
            showCheckBill = pendingPayment,
            bill = exposeBill,
            checkBill
        };
    }

    internal static object ProjectStaffBoardItem(OrderItem i) =>
        new
        {
            i.Id,
            i.MenuItemId,
            name = i.MenuItemName ?? i.MenuItem?.Name ?? "—",
            temperature = i.Temperature,
            temperatureLabel = DrinkServingTemperature.DisplayVi(i.Temperature),
            i.Quantity,
            preparedQuantity = i.PreparedQuantity,
            isPrepared = OrderItemPreparationHelper.IsItemFullyPrepared(i),
            i.PreparedAtUtc,
            i.PreparedBy,
            preparedByAccountId = i.PreparedByAccountId,
            i.UnitPrice,
            i.Notes,
            customerNote = i.CustomerNote
        };

    internal static object ProjectStaffBoardOrder(Order o, BankTransferQrBuilder bankTransferQr)
    {
        var progress = OrderItemPreparationHelper.CountProgress(o);
        var reference = OrderBillHelper.EnsureBillNumber(o);
        string? transferMemo = null;
        if (string.Equals(o.PaymentMethod, OrderPaymentMethods.BankTransfer, StringComparison.Ordinal))
            transferMemo = bankTransferQr.BuildMemoForOrder(o);
        var (paymentMethodLabelVi, paymentMethodLabelEn) = OrderPaymentMethods.Labels(o.PaymentMethod);
        return new
        {
            o.Id,
            orderNumber = reference,
            transferMemo,
            o.TableCode,
            staffStatus = StaffOrderStatusHelper.ToStaffStatus(o.Status),
            boardColumn = StaffOrderBoardColumnHelper.ToColumn(o.Status),
            phaseLabelVi = CustomerTrackStatusHelper.Resolve(o.Status).titleVi,
            o.CreatedAtUtc,
            o.UpdatedAtUtc,
            o.PaidAtUtc,
            o.CompletedAtUtc,
            completedBy = o.CompletedBy,
            completedByAccountId = o.CompletedByAccountId,
            o.PaymentMethod,
            paymentMethodLabelVi,
            paymentMethodLabelEn,
            pendingPaymentBadgeVi = OrderPaymentMethods.SubmittedBadgeVi(o.PaymentMethod),
            paymentConfirmedBy = o.PaymentConfirmedBy,
            paymentConfirmedByAccountId = o.PaymentConfirmedByAccountId,
            autoPaymentConfirmed = IsAutoBankWebhookConfirmation(o.PaymentConfirmedBy),
            statusChangedAtUtc = o.StatusChangedAtUtc ?? o.UpdatedAtUtc ?? o.CreatedAtUtc,
            brewingOwner = o.BrewingOwnerStaffName,
            servingOwner = o.ServingOwnerStaffName,
            o.TotalAmount,
            totalCups = o.Items.Sum(i => (int)i.Quantity),
            preparationDone = progress.Done,
            preparationTotal = progress.Total,
            allItemsPrepared = OrderItemPreparationHelper.IsOrderFullyPrepared(o),
            pacing = StaffOrderPacingHelper.Resolve(o),
            guestNotes = StaffOrderBoardNotes.Format(o),
            items = o.Items.Select(ProjectStaffBoardItem).ToList()
        };
    }

    private static bool IsAutoBankWebhookConfirmation(string? paymentConfirmedBy) =>
        !string.IsNullOrWhiteSpace(paymentConfirmedBy)
        && paymentConfirmedBy.StartsWith("bank-webhook:", StringComparison.OrdinalIgnoreCase);
}
