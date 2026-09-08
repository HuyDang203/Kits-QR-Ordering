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

internal interface IOrderPaymentWorkflowService
{
    Task<MarkPaidWorkflowResult> MarkPaidAsync(
        Guid orderId,
        string actor,
        string? paymentMethodOverride,
        Guid? confirmedByAccountId = null,
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
}
