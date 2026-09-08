using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Internal;

public sealed record AdminPaymentConfirmationRowVm(
    Guid Id,
    DateTimeOffset ReceivedAtUtc,
    string Provider,
    string? ProviderTransactionId,
    decimal Amount,
    string Memo,
    string MatchStatus,
    string StatusLabel,
    Guid? MatchedOrderId,
    string? BillNumber,
    string? Notes);

public sealed record AdminPaymentConfirmationDetailVm(
    Guid Id,
    string Provider,
    string? ProviderTransactionId,
    DateTimeOffset ReceivedAtUtc,
    decimal Amount,
    string Memo,
    string? AccountNumberMasked,
    string? BankCode,
    string MatchStatus,
    string StatusLabel,
    Guid? MatchedOrderId,
    string? BillNumber,
    string? Notes,
    string? RawPayloadJson);

public sealed record AdminPaymentConfirmationSummaryVm(
    int TotalReceived,
    int MatchedCount,
    int NeedsReviewCount,
    int AmountMismatchCount,
    int DuplicateCount);

public sealed record AdminPaymentConfirmationPageVm(
    DateTime FromLocalDate,
    DateTime ToLocalDate,
    IReadOnlyList<string> ProviderOptions,
    AdminPaymentConfirmationSummaryVm Summary,
    IReadOnlyList<AdminPaymentConfirmationRowVm> Rows,
    int TotalMatchingRows,
    int Page,
    int PageSize,
    int TotalPages);

internal static class AdminPaymentConfirmationQuery
{
    public const int DefaultPageSize = 50;

    public static IReadOnlyDictionary<string, string> StatusLabels { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PaymentConfirmationMatchStatus.Matched] = "Đã khớp",
            [PaymentConfirmationMatchStatus.Unmatched] = "Chưa khớp",
            [PaymentConfirmationMatchStatus.Duplicate] = "Trùng giao dịch",
            [PaymentConfirmationMatchStatus.AmountMismatch] = "Sai số tiền",
            [PaymentConfirmationMatchStatus.MemoMissing] = "Thiếu nội dung",
            [PaymentConfirmationMatchStatus.OrderAlreadyPaid] = "Đơn đã thanh toán",
            [PaymentConfirmationMatchStatus.Ignored] = "Đã bỏ qua"
        };

    public static string StatusLabel(string? status) =>
        status is not null && StatusLabels.TryGetValue(status, out var label) ? label : status ?? "—";

    public static async Task<AdminPaymentConfirmationPageVm> LoadPageAsync(
        IApplicationDbContext db,
        DateTime fromLocalInclusive,
        DateTime toLocalInclusive,
        string? matchStatus,
        string? provider,
        string? search,
        int page,
        CancellationToken ct = default)
    {
        var from = fromLocalInclusive.Date;
        var to = toLocalInclusive.Date;
        if (from > to)
            (from, to) = (to, from);

        var (utcStart, utcEndExclusive) = AnnapBusinessTime.ToUtcRangeInclusive(from, to);
        var pageSize = DefaultPageSize;
        var pageIndex = page < 1 ? 1 : page;

        var baseQuery = db.PaymentConfirmations.AsNoTracking()
            .Where(p => p.ReceivedAtUtc >= utcStart && p.ReceivedAtUtc < utcEndExclusive);

        if (!string.IsNullOrWhiteSpace(matchStatus)
            && !string.Equals(matchStatus, "all", StringComparison.OrdinalIgnoreCase))
        {
            var status = matchStatus.Trim();
            baseQuery = baseQuery.Where(p => p.MatchStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            var prov = provider.Trim();
            baseQuery = baseQuery.Where(p => p.Provider == prov);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var like = $"%{term}%";
            baseQuery = baseQuery.Where(p =>
                EF.Functions.ILike(p.Memo, like)
                || (p.ProviderTransactionId != null && EF.Functions.ILike(p.ProviderTransactionId, like))
                || (p.Notes != null && EF.Functions.ILike(p.Notes, like))
                || (p.MatchedOrderId != null && db.Orders.Any(o =>
                    o.Id == p.MatchedOrderId
                    && o.BillNumber != null
                    && EF.Functions.ILike(o.BillNumber, like))));
        }

        var summaryRows = await baseQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Matched = g.Count(p => p.MatchStatus == PaymentConfirmationMatchStatus.Matched),
                NeedsReview = g.Count(p =>
                    p.MatchStatus == PaymentConfirmationMatchStatus.Unmatched
                    || p.MatchStatus == PaymentConfirmationMatchStatus.MemoMissing),
                AmountMismatch = g.Count(p => p.MatchStatus == PaymentConfirmationMatchStatus.AmountMismatch),
                Duplicate = g.Count(p => p.MatchStatus == PaymentConfirmationMatchStatus.Duplicate)
            })
            .FirstOrDefaultAsync(ct);

        var summary = new AdminPaymentConfirmationSummaryVm(
            summaryRows?.Total ?? 0,
            summaryRows?.Matched ?? 0,
            summaryRows?.NeedsReview ?? 0,
            summaryRows?.AmountMismatch ?? 0,
            summaryRows?.Duplicate ?? 0);

        var totalMatching = summary.TotalReceived;
        var totalPages = totalMatching == 0 ? 1 : (int)Math.Ceiling(totalMatching / (double)pageSize);
        if (pageIndex > totalPages)
            pageIndex = totalPages;

        var rows = await baseQuery
            .OrderByDescending(p => p.ReceivedAtUtc)
            .ThenByDescending(p => p.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.ReceivedAtUtc,
                p.Provider,
                p.ProviderTransactionId,
                p.Amount,
                p.Memo,
                p.MatchStatus,
                p.MatchedOrderId,
                p.Notes
            })
            .ToListAsync(ct);

        var orderIds = rows
            .Where(r => r.MatchedOrderId.HasValue)
            .Select(r => r.MatchedOrderId!.Value)
            .Distinct()
            .ToList();

        var billByOrderId = orderIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Orders.AsNoTracking()
                .Where(o => orderIds.Contains(o.Id))
                .Select(o => new { o.Id, o.BillNumber })
                .ToDictionaryAsync(x => x.Id, x => x.BillNumber ?? "", ct);

        var providerOptions = await db.PaymentConfirmations.AsNoTracking()
            .Where(p => p.ReceivedAtUtc >= utcStart && p.ReceivedAtUtc < utcEndExclusive)
            .Select(p => p.Provider)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync(ct);

        var rowVms = rows.Select(r =>
        {
            string? bill = null;
            if (r.MatchedOrderId is { } oid && billByOrderId.TryGetValue(oid, out var bn))
                bill = string.IsNullOrWhiteSpace(bn) ? null : bn;
            return new AdminPaymentConfirmationRowVm(
                r.Id,
                r.ReceivedAtUtc,
                r.Provider,
                r.ProviderTransactionId,
                r.Amount,
                r.Memo,
                r.MatchStatus,
                StatusLabel(r.MatchStatus),
                r.MatchedOrderId,
                bill,
                r.Notes);
        }).ToList();

        return new AdminPaymentConfirmationPageVm(
            from,
            to,
            providerOptions,
            summary,
            rowVms,
            totalMatching,
            pageIndex,
            pageSize,
            totalPages);
    }

    public static async Task<AdminPaymentConfirmationDetailVm?> LoadDetailAsync(
        IApplicationDbContext db,
        Guid id,
        CancellationToken ct = default)
    {
        var row = await db.PaymentConfirmations.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (row is null)
            return null;

        string? billNumber = null;
        if (row.MatchedOrderId is { } orderId)
        {
            billNumber = await db.Orders.AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => o.BillNumber)
                .FirstOrDefaultAsync(ct);
        }

        return new AdminPaymentConfirmationDetailVm(
            row.Id,
            row.Provider,
            row.ProviderTransactionId,
            row.ReceivedAtUtc,
            row.Amount,
            row.Memo,
            MaskAccount(row.AccountNumber),
            row.BankCode,
            row.MatchStatus,
            StatusLabel(row.MatchStatus),
            row.MatchedOrderId,
            string.IsNullOrWhiteSpace(billNumber) ? null : billNumber,
            row.Notes,
            row.RawPayloadJson);
    }

    private static string? MaskAccount(string? accountNumber) =>
        string.IsNullOrWhiteSpace(accountNumber)
            ? null
            : BankTransferQrBuilder.MaskAccountNumber(accountNumber);
}
