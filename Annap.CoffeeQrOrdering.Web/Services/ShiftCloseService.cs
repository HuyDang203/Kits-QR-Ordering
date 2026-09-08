using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed record ShiftCloseWindow(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

public sealed record ShiftCloseBillRowVm(
    Guid OrderId,
    string BillNumber,
    string TableCode,
    DateTimeOffset PaidAtUtc,
    string PaymentMethod,
    string PaymentMethodLabelVi,
    decimal TotalAmount,
    string ConfirmedBy,
    Guid? ConfirmedByAccountId,
    string StatusLabelVi);

public sealed record ShiftCloseEmployeeRowVm(
    string DisplayName,
    Guid? AccountId,
    int OrderCount,
    decimal CashAmount,
    decimal CardAmount,
    decimal BankTransferAmount,
    decimal LegacyCashOrCardAmount,
    decimal UnknownAmount,
    decimal TotalAmount);

public sealed record ShiftClosePreviewVm(
    ShiftCloseWindow Window,
    string CurrentUserDisplayName,
    int TotalOrders,
    decimal TotalGrossAmount,
    decimal CashAmount,
    decimal CardAmount,
    decimal LegacyCashOrCardAmount,
    decimal BankTransferAmount,
    decimal UnknownPaymentAmount,
    int CashOrders,
    int CardOrders,
    int LegacyCashOrCardOrders,
    int BankTransferOrders,
    int UnknownPaymentOrders,
    IReadOnlyList<ShiftCloseEmployeeRowVm> Employees,
    IReadOnlyList<ShiftCloseBillRowVm> Bills,
    ShiftCloseLastCloseVm? LastClose,
    bool CanClose,
    string? EmptyMessage)
{
    public decimal CashOrCardAmount => CashAmount + CardAmount + LegacyCashOrCardAmount;
    public int CashOrCardOrders => CashOrders + CardOrders + LegacyCashOrCardOrders;
}

public sealed record ShiftCloseLastCloseVm(
    Guid Id,
    DateTimeOffset ClosedAtUtc,
    string ClosedBy,
    decimal TotalGrossAmount,
    int TotalOrders);

public sealed record ShiftCloseResultVm(
    bool Success,
    string? ErrorMessage,
    ShiftClose? Entity,
    ShiftClosePreviewVm? ClosedSummary);

public interface IShiftCloseService
{
    Task<ShiftClosePreviewVm> BuildPreviewAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<ShiftCloseResultVm> CloseShiftAsync(ClaimsPrincipal user, CancellationToken ct = default);
    string BuildCopyText(ShiftClosePreviewVm preview);
}

public sealed class ShiftCloseService(AppDbContext db) : IShiftCloseService
{
    private static readonly OrderStatus[] PaidStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.InProgress,
        OrderStatus.FinishingTouches,
        OrderStatus.Ready,
        OrderStatus.Completed
    ];

    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<ShiftClosePreviewVm> BuildPreviewAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var endUtc = DateTimeOffset.UtcNow;
        var window = await ResolveWindowAsync(endUtc, ct);
        var orders = await LoadShiftOrdersAsync(window.StartUtc, endUtc, ct);
        var lastClose = await LoadLastCloseAsync(ct);
        var preview = await BuildPreviewFromOrdersAsync(orders, window, endUtc, ResolveUserDisplayName(user), lastClose, ct);
        return preview;
    }

    internal const string ConcurrentCloseMessage =
        "Ca vừa được kết trên một thiết bị khác. Vui lòng xem lại tổng kết trước khi kết ca tiếp.";

    public async Task<ShiftCloseResultVm> CloseShiftAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        // Serializable: the window-start read (latest ShiftClose) and the new ShiftClose insert
        // must be atomic, or two devices closing at once each snapshot the same paid orders.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var endUtc = DateTimeOffset.UtcNow;
            var window = await ResolveWindowInTransactionAsync(endUtc, ct);
            var orders = await LoadShiftOrdersAsync(window.StartUtc, endUtc, ct);
            var lastClose = await LoadLastCloseAsync(ct);
            var displayName = ResolveUserDisplayName(user);
            var preview = await BuildPreviewFromOrdersAsync(orders, window, endUtc, displayName, lastClose, ct);

            if (!preview.CanClose)
            {
                await tx.RollbackAsync(ct);
                return new ShiftCloseResultVm(false, preview.EmptyMessage ?? "Không thể kết ca.", null, preview);
            }

            var (confirmerName, accountId) = StaffPaymentConfirmerHelper.ResolveConfirmer(user);
            var snapshot = BuildSnapshotJson(preview, confirmerName);
            var entity = new ShiftClose
            {
                OpenedAtUtc = window.StartUtc,
                ClosedAtUtc = endUtc,
                ClosedBy = confirmerName,
                ClosedByAccountId = accountId,
                TotalOrders = preview.TotalOrders,
                TotalGrossAmount = preview.TotalGrossAmount,
                CashOrCardAmount = preview.CashOrCardAmount,
                BankTransferAmount = preview.BankTransferAmount,
                UnknownPaymentAmount = preview.UnknownPaymentAmount,
                CashOrCardOrders = preview.CashOrCardOrders,
                BankTransferOrders = preview.BankTransferOrders,
                UnknownPaymentOrders = preview.UnknownPaymentOrders,
                SnapshotJson = snapshot,
                CreatedAtUtc = endUtc
            };

            db.ShiftCloses.Add(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var closedSummary = preview with { LastClose = null };
            return new ShiftCloseResultVm(true, null, entity, closedSummary);
        }
        catch (Exception ex) when (IsConcurrentCloseConflict(ex))
        {
            // A concurrent close won: unique window-start index (23505) or Serializable
            // conflict (40001/40P01), possibly wrapped by the Npgsql execution strategy.
            // The caller re-renders the fresh (now empty) window.
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return new ShiftCloseResultVm(false, ConcurrentCloseMessage, null, null);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static bool IsConcurrentCloseConflict(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is PostgresException pg && pg.SqlState is "40001" or "40P01" or "23505")
                return true;
        }

        return false;
    }

    public string BuildCopyText(ShiftClosePreviewVm preview)
    {
        var start = AnnapBusinessTime.FormatLocalDateTime(preview.Window.StartUtc);
        var end = AnnapBusinessTime.FormatLocalDateTime(preview.Window.EndUtc);
        var lines = new List<string>
        {
            "KẾT CA ANNAP",
            $"Từ: {start}",
            $"Đến: {end}",
            $"Người kết ca: {preview.CurrentUserDisplayName}",
            $"Tổng bill: {preview.TotalOrders}",
            $"Tổng doanh thu: {Money(preview.TotalGrossAmount)}",
            $"Tiền mặt: {Money(preview.CashAmount)}",
            $"Thẻ: {Money(preview.CardAmount)}",
            $"Chuyển khoản: {Money(preview.BankTransferAmount)}"
        };

        if (preview.LegacyCashOrCardAmount > 0)
            lines.Add($"Tiền mặt/thẻ cũ: {Money(preview.LegacyCashOrCardAmount)}");

        if (preview.UnknownPaymentAmount > 0)
            lines.Add($"Chưa rõ: {Money(preview.UnknownPaymentAmount)}");

        if (preview.Employees.Count > 0)
        {
            lines.Add("");
            lines.Add("Theo nhân viên:");
            foreach (var e in preview.Employees.OrderByDescending(x => x.TotalAmount))
            {
                var parts = new List<string> { $"{e.OrderCount} bill" };
                if (e.CashAmount > 0) parts.Add($"tiền mặt {Money(e.CashAmount)}");
                if (e.CardAmount > 0) parts.Add($"thẻ {Money(e.CardAmount)}");
                if (e.BankTransferAmount > 0) parts.Add($"chuyển khoản {Money(e.BankTransferAmount)}");
                if (e.LegacyCashOrCardAmount > 0) parts.Add($"tm/thẻ cũ {Money(e.LegacyCashOrCardAmount)}");
                if (e.UnknownAmount > 0) parts.Add($"chưa rõ {Money(e.UnknownAmount)}");
                parts.Add($"tổng {Money(e.TotalAmount)}");
                lines.Add($"* {e.DisplayName}: {string.Join(" · ", parts)}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    internal async Task<ShiftCloseWindow> ResolveWindowAsync(DateTimeOffset endUtc, CancellationToken ct) =>
        await ResolveWindowCoreAsync(endUtc, trackLatestClose: false, ct);

    private async Task<ShiftCloseWindow> ResolveWindowInTransactionAsync(DateTimeOffset endUtc, CancellationToken ct) =>
        await ResolveWindowCoreAsync(endUtc, trackLatestClose: true, ct);

    private async Task<ShiftCloseWindow> ResolveWindowCoreAsync(
        DateTimeOffset endUtc,
        bool trackLatestClose,
        CancellationToken ct)
    {
        var query = trackLatestClose
            ? db.ShiftCloses.AsQueryable()
            : db.ShiftCloses.AsNoTracking();

        var lastClose = await query
            .OrderByDescending(s => s.ClosedAtUtc)
            .Select(s => (DateTimeOffset?)s.ClosedAtUtc)
            .FirstOrDefaultAsync(ct);

        DateTimeOffset startUtc;
        if (lastClose is { } closedAt)
            startUtc = closedAt;
        else
        {
            var (dayStart, _) = AnnapBusinessTime.ToUtcRangeInclusive(
                AnnapBusinessTime.TodayLocal,
                AnnapBusinessTime.TodayLocal);
            startUtc = dayStart;
        }

        return new ShiftCloseWindow(startUtc, endUtc);
    }

    private async Task<List<Order>> LoadShiftOrdersAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken ct) =>
        await db.Orders.AsNoTracking()
            .Where(o => o.PaidAtUtc != null
                        && o.PaidAtUtc > startUtc
                        && o.PaidAtUtc <= endUtc
                        && PaidStatuses.Contains(o.Status))
            .OrderBy(o => o.PaidAtUtc)
            .ToListAsync(ct);

    private async Task<ShiftCloseLastCloseVm?> LoadLastCloseAsync(CancellationToken ct)
    {
        var row = await db.ShiftCloses.AsNoTracking()
            .OrderByDescending(s => s.ClosedAtUtc)
            .Select(s => new ShiftCloseLastCloseVm(
                s.Id,
                s.ClosedAtUtc,
                s.ClosedBy,
                s.TotalGrossAmount,
                s.TotalOrders))
            .FirstOrDefaultAsync(ct);
        return row;
    }

    private async Task<ShiftClosePreviewVm> BuildPreviewFromOrdersAsync(
        IReadOnlyList<Order> orders,
        ShiftCloseWindow window,
        DateTimeOffset endUtc,
        string currentUser,
        ShiftCloseLastCloseVm? lastClose,
        CancellationToken ct)
    {
        var accountIds = orders
            .Where(o => o.PaymentConfirmedByAccountId is not null)
            .Select(o => o.PaymentConfirmedByAccountId!.Value)
            .Distinct()
            .ToList();

        var accountNames = accountIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StaffAccounts.AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.DisplayName, ct);

        decimal cash = 0, card = 0, legacy = 0, bank = 0, unknown = 0;
        int cashCount = 0, cardCount = 0, legacyCount = 0, bankCount = 0, unknownCount = 0;
        var bills = new List<ShiftCloseBillRowVm>();
        var employeeMap = new Dictionary<string, ShiftCloseEmployeeRowVm>();

        foreach (var o in orders)
        {
            var bucket = OrderPaymentMethods.ClassifyForShiftClose(o.PaymentMethod);
            var amount = o.TotalAmount;
            switch (bucket)
            {
                case PaymentShiftBucket.Cash:
                    cash += amount;
                    cashCount++;
                    break;
                case PaymentShiftBucket.Card:
                    card += amount;
                    cardCount++;
                    break;
                case PaymentShiftBucket.LegacyCashOrCard:
                    legacy += amount;
                    legacyCount++;
                    break;
                case PaymentShiftBucket.BankTransfer:
                    bank += amount;
                    bankCount++;
                    break;
                default:
                    unknown += amount;
                    unknownCount++;
                    break;
            }

            var confirmedBy = ResolveConfirmerLabel(o, accountNames);
            var (methodVi, _) = OrderPaymentMethods.Labels(o.PaymentMethod);
            bills.Add(new ShiftCloseBillRowVm(
                o.Id,
                OrderBillHelper.EnsureBillNumber(o),
                o.TableCode,
                o.PaidAtUtc!.Value,
                o.PaymentMethod ?? "",
                methodVi,
                amount,
                confirmedBy,
                o.PaymentConfirmedByAccountId,
                StatusLabelVi(o.Status)));

            var empKey = o.PaymentConfirmedByAccountId?.ToString("D")
                         ?? (string.IsNullOrWhiteSpace(o.PaymentConfirmedBy)
                             ? "__unknown__"
                             : o.PaymentConfirmedBy.Trim().ToLowerInvariant());

            if (!employeeMap.TryGetValue(empKey, out var emp))
            {
                emp = new ShiftCloseEmployeeRowVm(
                    confirmedBy,
                    o.PaymentConfirmedByAccountId,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
            }

            emp = emp with
            {
                OrderCount = emp.OrderCount + 1,
                CashAmount = emp.CashAmount + (bucket == PaymentShiftBucket.Cash ? amount : 0),
                CardAmount = emp.CardAmount + (bucket == PaymentShiftBucket.Card ? amount : 0),
                LegacyCashOrCardAmount = emp.LegacyCashOrCardAmount + (bucket == PaymentShiftBucket.LegacyCashOrCard ? amount : 0),
                BankTransferAmount = emp.BankTransferAmount + (bucket == PaymentShiftBucket.BankTransfer ? amount : 0),
                UnknownAmount = emp.UnknownAmount + (bucket == PaymentShiftBucket.Unknown ? amount : 0),
                TotalAmount = emp.TotalAmount + amount
            };
            employeeMap[empKey] = emp;
        }

        var total = cash + card + legacy + bank + unknown;
        var canClose = orders.Count > 0;
        var emptyMessage = canClose
            ? null
            : "Chưa có bill thanh toán trong ca này.";

        return new ShiftClosePreviewVm(
            window with { EndUtc = endUtc },
            currentUser,
            orders.Count,
            total,
            cash,
            card,
            legacy,
            bank,
            unknown,
            cashCount,
            cardCount,
            legacyCount,
            bankCount,
            unknownCount,
            employeeMap.Values.OrderByDescending(e => e.TotalAmount).ThenBy(e => e.DisplayName).ToList(),
            bills,
            lastClose,
            canClose,
            emptyMessage);
    }

    private static string BuildSnapshotJson(ShiftClosePreviewVm preview, string closedByDisplayName)
    {
        var payload = new
        {
            windowStartUtc = preview.Window.StartUtc,
            windowEndUtc = preview.Window.EndUtc,
            closedByDisplayName = closedByDisplayName,
            preview.TotalOrders,
            preview.TotalGrossAmount,
            preview.CashAmount,
            preview.CardAmount,
            preview.LegacyCashOrCardAmount,
            preview.BankTransferAmount,
            preview.UnknownPaymentAmount,
            preview.CashOrders,
            preview.CardOrders,
            preview.LegacyCashOrCardOrders,
            preview.BankTransferOrders,
            preview.UnknownPaymentOrders,
            cashOrCardAmount = preview.CashOrCardAmount,
            cashOrCardOrders = preview.CashOrCardOrders,
            employees = preview.Employees,
            bills = preview.Bills
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string ResolveUserDisplayName(ClaimsPrincipal user)
    {
        var display = user.FindFirst(StaffClaimTypes.DisplayName)?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(display))
            return display;

        var (name, _) = StaffPaymentConfirmerHelper.ResolveConfirmer(user);
        return name;
    }

    private static string ResolveConfirmerLabel(Order order, IReadOnlyDictionary<Guid, string> accountNames)
    {
        if (order.PaymentConfirmedByAccountId is Guid id
            && accountNames.TryGetValue(id, out var displayName))
            return displayName;

        var text = order.PaymentConfirmedBy?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
            return text;

        return "Không rõ";
    }

    private static string StatusLabelVi(OrderStatus status) => status switch
    {
        OrderStatus.Completed => "Hoàn thành",
        OrderStatus.Paid => "Đã thanh toán",
        _ => "Đang pha chế"
    };

    private static string Money(decimal amount) => VndMoneyFormatter.Format(amount);
}
