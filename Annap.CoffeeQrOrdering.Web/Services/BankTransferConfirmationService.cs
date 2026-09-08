using System.Text.Json;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

internal interface IBankTransferConfirmationService
{
    Task<BankTransferConfirmationResult> ProcessAsync(
        PaymentConfirmationInput input,
        CancellationToken cancellationToken = default);
}

internal sealed class BankTransferConfirmationService(
    AppDbContext db,
    BankTransferQrBuilder memoBuilder,
    IOrderPaymentWorkflowService paymentWorkflow) : IBankTransferConfirmationService
{
    public async Task<BankTransferConfirmationResult> ProcessAsync(
        PaymentConfirmationInput input,
        CancellationToken cancellationToken = default)
    {
        var provider = input.Provider.Trim();
        var transactionId = string.IsNullOrWhiteSpace(input.ProviderTransactionId)
            ? null
            : input.ProviderTransactionId.Trim();

        if (!string.IsNullOrWhiteSpace(transactionId))
        {
            var prior = await db.PaymentConfirmations.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Provider == provider && x.ProviderTransactionId == transactionId,
                    cancellationToken);
            if (prior is not null)
            {
                return new BankTransferConfirmationResult(
                    "duplicate",
                    MatchedOrderId: prior.MatchedOrderId,
                    ConfirmationId: prior.Id,
                    Reason: "Provider transaction already processed.");
            }
        }

        if (string.IsNullOrWhiteSpace(input.Memo))
        {
            var missingMemo = await SaveConfirmationAsync(
                input,
                PaymentConfirmationMatchStatus.MemoMissing,
                null,
                "Transfer memo is required.",
                cancellationToken);
            return new BankTransferConfirmationResult(
                "memo_missing",
                ConfirmationId: missingMemo.Id,
                Reason: "Transfer memo is required.");
        }

        var candidates = await LoadPendingBankTransferOrdersAsync(cancellationToken);
        var memoMatches = candidates
            .Where(o => BankTransferMemoMatcher.MemoMatches(input.Memo, memoBuilder.BuildMemoForOrder(o)))
            .ToList();

        if (memoMatches.Count == 0)
        {
            var unmatched = await SaveConfirmationAsync(
                input,
                PaymentConfirmationMatchStatus.Unmatched,
                null,
                "No pending BankTransfer order matched memo and amount.",
                cancellationToken);
            return new BankTransferConfirmationResult(
                "unmatched",
                ConfirmationId: unmatched.Id,
                Reason: "No pending BankTransfer order matched memo and amount.");
        }

        if (memoMatches.Count > 1)
        {
            var ambiguous = await SaveConfirmationAsync(
                input,
                PaymentConfirmationMatchStatus.Unmatched,
                null,
                $"Multiple pending orders ({memoMatches.Count}) matched memo; auto-confirm skipped.",
                cancellationToken);
            return new BankTransferConfirmationResult(
                "unmatched",
                ConfirmationId: ambiguous.Id,
                Reason: "Multiple pending BankTransfer orders matched memo; auto-confirm skipped.");
        }

        var order = memoMatches[0];
        if (!AmountsMatch(order.TotalAmount, input.Amount))
        {
            var amountMismatch = await SaveConfirmationAsync(
                input,
                PaymentConfirmationMatchStatus.AmountMismatch,
                order.Id,
                $"Expected {(long)order.TotalAmount}, received {(long)input.Amount}.",
                cancellationToken);
            return new BankTransferConfirmationResult(
                "amount_mismatch",
                OrderId: order.Id,
                BillNumber: OrderBillHelper.EnsureBillNumber(order),
                Amount: order.TotalAmount,
                ConfirmationId: amountMismatch.Id,
                Reason: "Amount does not match the pending order total.");
        }

        if (order.Status == OrderStatus.Paid
            || StaffOrderBoardColumnHelper.IsPaidForPrep(order.Status)
            || order.Status == OrderStatus.Completed)
        {
            var alreadyPaid = await SaveConfirmationAsync(
                input,
                PaymentConfirmationMatchStatus.OrderAlreadyPaid,
                order.Id,
                "Order is already paid.",
                cancellationToken);
            return new BankTransferConfirmationResult(
                "order_already_paid",
                OrderId: order.Id,
                BillNumber: OrderBillHelper.EnsureBillNumber(order),
                Amount: order.TotalAmount,
                ConfirmationId: alreadyPaid.Id,
                MatchedOrderId: order.Id,
                Reason: "Order is already paid.");
        }

        var actor = $"bank-webhook:{provider}";
        var markResult = await paymentWorkflow.MarkPaidAsync(
            order.Id,
            actor,
            OrderPaymentMethods.BankTransfer,
            confirmedByAccountId: null,
            cancellationToken);

        if (markResult.Outcome != MarkPaidOutcome.Success)
        {
            var notes = markResult.Outcome switch
            {
                MarkPaidOutcome.NotFound => "Order not found during mark-paid.",
                MarkPaidOutcome.Cancelled => "Order cancelled.",
                MarkPaidOutcome.Completed => "Order already completed.",
                MarkPaidOutcome.InvalidState => "Order cannot be marked paid from current state.",
                MarkPaidOutcome.SerializationConflict => "Concurrent update; retry webhook.",
                _ => "Mark-paid failed."
            };
            var failed = await SaveConfirmationAsync(
                input,
                PaymentConfirmationMatchStatus.Unmatched,
                order.Id,
                notes,
                cancellationToken);
            return new BankTransferConfirmationResult(
                markResult.Outcome == MarkPaidOutcome.SerializationConflict ? "conflict" : "unmatched",
                OrderId: order.Id,
                ConfirmationId: failed.Id,
                Reason: notes);
        }

        PaymentConfirmation matched;
        try
        {
            matched = await SaveConfirmationAsync(
                input,
                PaymentConfirmationMatchStatus.Matched,
                order.Id,
                null,
                cancellationToken);
        }
        catch (DbUpdateException ex) when (OrderSubmitHelper.IsSerializationConflict(ex))
        {
            return new BankTransferConfirmationResult(
                "duplicate",
                OrderId: markResult.Order!.Id,
                BillNumber: markResult.Order.BillNumber,
                Amount: markResult.Order.TotalAmount,
                MatchedOrderId: markResult.Order.Id,
                Reason: "Provider transaction already processed.");
        }

        var paidOrder = markResult.Order!;
        return new BankTransferConfirmationResult(
            "matched",
            OrderId: paidOrder.Id,
            BillNumber: paidOrder.BillNumber,
            Amount: paidOrder.TotalAmount,
            ConfirmationId: matched.Id);
    }

    private async Task<List<Order>> LoadPendingBankTransferOrdersAsync(CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.PaymentMethod == OrderPaymentMethods.BankTransfer)
            .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Completed)
            .Where(o => o.Status == OrderStatus.Submitted || o.Status == OrderStatus.Draft)
            .Where(o => o.BillNumber != null && o.BillNumber != "")
            .ToListAsync(cancellationToken);

    private static bool AmountsMatch(decimal orderTotal, decimal receivedAmount) =>
        (long)orderTotal == (long)receivedAmount;

    private async Task<PaymentConfirmation> SaveConfirmationAsync(
        PaymentConfirmationInput input,
        string matchStatus,
        Guid? matchedOrderId,
        string? notes,
        CancellationToken cancellationToken)
    {
        var row = new PaymentConfirmation
        {
            Provider = input.Provider.Trim(),
            ProviderTransactionId = string.IsNullOrWhiteSpace(input.ProviderTransactionId)
                ? null
                : input.ProviderTransactionId.Trim(),
            ReceivedAtUtc = input.ReceivedAtUtc,
            Amount = input.Amount,
            Memo = input.Memo.Trim(),
            AccountNumber = string.IsNullOrWhiteSpace(input.AccountNumber) ? null : input.AccountNumber.Trim(),
            BankCode = string.IsNullOrWhiteSpace(input.BankCode) ? null : input.BankCode.Trim(),
            RawPayloadJson = TruncateRawPayload(input.RawPayloadJson),
            MatchedOrderId = matchedOrderId,
            MatchStatus = matchStatus,
            ProcessedAtUtc = DateTimeOffset.UtcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
        await db.PaymentConfirmations.AddAsync(row, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    private static string? TruncateRawPayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var trimmed = raw.Trim();
        return trimmed.Length <= 8000 ? trimmed : trimmed[..7997] + "…";
    }
}
