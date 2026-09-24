using System.Data;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Extensions;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

internal enum MarkPaidOutcome
{
    Success,
    NotFound,
    Cancelled,
    Completed,
    InvalidState,
    SerializationConflict
}

internal sealed record MarkPaidWorkflowResult(
    MarkPaidOutcome Outcome,
    Order? Order = null,
    OrderBillDto? Bill = null,
    object? WorkflowPulse = null,
    bool Replay = false);

internal enum ChangePaymentOutcome
{
    Success,
    NotFound,
    Forbidden,
    InvalidPaymentMethod,
    Locked,
    SerializationConflict
}

internal sealed record ChangePaymentWorkflowResult(
    ChangePaymentOutcome Outcome,
    Order? Order = null,
    object? WorkflowPulse = null,
    bool Replay = false,
    string? PaymentMethod = null);

internal interface IOrderPaymentWorkflowService
{
    Task<MarkPaidWorkflowResult> MarkPaidAsync(
        Guid orderId,
        string actor,
        string? paymentMethodOverride,
        Guid? confirmedByAccountId = null,
        CancellationToken cancellationToken = default);

    Task<ChangePaymentWorkflowResult> ChangePaymentMethodAsync(
        Guid orderId,
        string token,
        string paymentMethod,
        CancellationToken cancellationToken = default);
}

internal sealed class OrderPaymentWorkflowService(
    AppDbContext db,
    IOrderStatusNotifier notifier) : IOrderPaymentWorkflowService
{
    public async Task<MarkPaidWorkflowResult> MarkPaidAsync(
        Guid orderId,
        string actor,
        string? paymentMethodOverride,
        Guid? confirmedByAccountId = null,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            if (order is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return new MarkPaidWorkflowResult(MarkPaidOutcome.NotFound);
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                await tx.RollbackAsync(cancellationToken);
                return new MarkPaidWorkflowResult(MarkPaidOutcome.Cancelled, order);
            }

            if (order.Status == OrderStatus.Completed)
            {
                await tx.RollbackAsync(cancellationToken);
                return new MarkPaidWorkflowResult(MarkPaidOutcome.Completed, order);
            }

            var alreadyPaid = order.Status == OrderStatus.Paid
                || StaffOrderBoardColumnHelper.IsPaidForPrep(order.Status);

            if (!alreadyPaid)
            {
                if (!StaffOrderBoardColumnHelper.CanMarkPaid(order.Status))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new MarkPaidWorkflowResult(MarkPaidOutcome.InvalidState, order);
                }

                var now = DateTimeOffset.UtcNow;
                order.Status = OrderStatus.Paid;
                order.StatusChangedAtUtc = now;
                order.PaidAtUtc = now;
                order.PaymentConfirmedBy = actor;
                order.PaymentConfirmedByAccountId = confirmedByAccountId;
                order.BillNumber = OrderBillHelper.EnsureBillNumber(order);
                if (!string.IsNullOrWhiteSpace(paymentMethodOverride))
                    order.PaymentMethod = paymentMethodOverride.Trim();
                else if (string.IsNullOrWhiteSpace(order.PaymentMethod))
                    order.PaymentMethod = OrderPaymentMethods.Cash;

                await OperationalAudit.AppendAsync(db, "order.mark_paid", actor, order.Id,
                    $"bill={order.BillNumber}", cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (string.IsNullOrWhiteSpace(order.BillNumber))
            {
                order.BillNumber = OrderBillHelper.EnsureBillNumber(order);
                await db.SaveChangesAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);

            var bill = OrderBillHelper.BuildPaidReceipt(order);
            var pulse = OrderWorkflowEndpoints.BuildWorkflowPulse(order, bill);
            await notifier.NotifyGuestOrderWorkflowAsync(order.Id, pulse, cancellationToken);
            await notifier.NotifyStaffBoardWorkflowAsync(pulse, cancellationToken);

            return new MarkPaidWorkflowResult(
                MarkPaidOutcome.Success,
                order,
                bill,
                pulse,
                alreadyPaid);
        }
        catch (DbUpdateException ex) when (OrderSubmitHelper.IsSerializationConflict(ex))
        {
            await tx.RollbackAsync(cancellationToken);
            return new MarkPaidWorkflowResult(MarkPaidOutcome.SerializationConflict);
        }
    }

    public async Task<ChangePaymentWorkflowResult> ChangePaymentMethodAsync(
        Guid orderId,
        string token,
        string paymentMethod,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new ChangePaymentWorkflowResult(ChangePaymentOutcome.Forbidden);

        var normalizedPayment = OrderPaymentMethods.Normalize(paymentMethod);
        if (normalizedPayment is null)
            return new ChangePaymentWorkflowResult(ChangePaymentOutcome.InvalidPaymentMethod);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            if (order is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return new ChangePaymentWorkflowResult(ChangePaymentOutcome.NotFound);
            }

            if (!GuestSessionTokens.Matches(order.GuestSessionToken, token))
            {
                await tx.RollbackAsync(cancellationToken);
                return new ChangePaymentWorkflowResult(ChangePaymentOutcome.Forbidden);
            }

            // Allowed: only Submitted. Draft is not guest-submittable; everything past Submitted
            // (Paid, InProgress, FinishingTouches, Ready, Completed, Cancelled) is locked.
            if (order.Status != OrderStatus.Submitted)
            {
                await tx.RollbackAsync(cancellationToken);
                return new ChangePaymentWorkflowResult(ChangePaymentOutcome.Locked, order);
            }

            var current = (order.PaymentMethod ?? string.Empty).Trim();
            if (string.Equals(current, normalizedPayment, StringComparison.Ordinal))
            {
                // Same-method replay: no audit row, no DB write, no notification pulse.
                await tx.CommitAsync(cancellationToken);
                return new ChangePaymentWorkflowResult(
                    ChangePaymentOutcome.Success,
                    order,
                    Replay: true,
                    PaymentMethod: normalizedPayment);
            }

            var previous = order.PaymentMethod;
            order.PaymentMethod = normalizedPayment;

            // Generate BillNumber lazily when the new payment method needs it
            // (matches existing application contract: BankTransfer submit ensures one).
            // Do NOT regenerate when one already exists.
            if (string.Equals(normalizedPayment, OrderPaymentMethods.BankTransfer, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(order.BillNumber))
            {
                order.BillNumber = OrderBillHelper.EnsureBillNumber(order);
            }

            await OperationalAudit.AppendAsync(db, "order.change_payment_method", null, order.Id,
                $"from={previous};to={normalizedPayment};by=guest", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            var pulse = OrderWorkflowEndpoints.BuildWorkflowPulse(order);
            await notifier.NotifyGuestOrderWorkflowAsync(order.Id, pulse, cancellationToken);
            await notifier.NotifyStaffBoardWorkflowAsync(pulse, cancellationToken);

            return new ChangePaymentWorkflowResult(
                ChangePaymentOutcome.Success,
                order,
                pulse,
                Replay: false,
                PaymentMethod: normalizedPayment);
        }
        catch (DbUpdateException ex) when (OrderSubmitHelper.IsSerializationConflict(ex))
        {
            await tx.RollbackAsync(cancellationToken);
            return new ChangePaymentWorkflowResult(ChangePaymentOutcome.SerializationConflict);
        }
    }
}
