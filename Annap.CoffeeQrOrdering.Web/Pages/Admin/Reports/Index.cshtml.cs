using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Reports;

public sealed record AdminReportBillLineVm(string Name, int Quantity, decimal LineTotal);

public sealed record AdminReportBillDisplayVm(
    string ShopName,
    string BillNumber,
    string TableCode,
    string? PaidAtDisplay,
    string StatusLabel,
    decimal TotalAmount,
    IReadOnlyList<AdminReportBillLineVm> Items);

[Authorize(Policy = "StaffAdmin")]
public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Preset { get; set; }

    [BindProperty(SupportsGet = true, Name = "from")]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public DateTime? To { get; set; }

    [BindProperty(SupportsGet = true, Name = "bill")]
    public Guid? Bill { get; set; }

    public AdminSalesReportVm Report { get; private set; } = null!;

    public AdminReportBillDisplayVm? SelectedBill { get; private set; }

    public string? BillWarning { get; private set; }

    public string ActivePreset { get; private set; } = "today";

    public const string BillNotInRangeMessage = "Không tìm thấy bill trong khoảng báo cáo này.";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ResolveDateRange();
        Report = await AdminSalesReportQuery.LoadAsync(db, ReportFromLocal, ReportToLocal, cancellationToken);

        var billId = ResolveBillId();
        Bill = billId;

        if (billId is { } selectedBillId)
        {
            var order = await db.Orders.AsNoTracking()
                .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == selectedBillId, cancellationToken);

            if (order is null
                || !AdminSalesReportQuery.IsOrderInReportRange(order, Report.UtcStart, Report.UtcEndExclusive)
                || !OrderBillHelper.CanExposeBillToGuest(order.Status))
            {
                BillWarning = BillNotInRangeMessage;
            }
            else
            {
                SelectedBill = ToBillDisplay(OrderBillHelper.Build(order));
            }
        }

        return Page();
    }

    private Guid? ResolveBillId()
    {
        if (Bill is { } bound && bound != Guid.Empty)
            return bound;

        if (Guid.TryParse(Request.Query["bill"], out var queryBill) && queryBill != Guid.Empty)
            return queryBill;

        return null;
    }

    public DateTime ReportFromLocal { get; private set; }

    public DateTime ReportToLocal { get; private set; }

    public string BuildFilterUrl(string preset, DateTime? from = null, DateTime? to = null) =>
        BuildPageUrl(BuildFilterQuery(preset, from, to));

    public string BuildBillUrl(Guid orderId)
    {
        var query = BuildActiveFilterQuery();
        query["bill"] = orderId.ToString("D");
        return BuildPageUrl(query) + "#bill-detail";
    }

    public string BuildCloseBillUrl() => BuildPageUrl(BuildActiveFilterQuery());

    private Dictionary<string, string?> BuildActiveFilterQuery() =>
        BuildFilterQuery(ActivePreset, ReportFromLocal, ReportToLocal);

    private Dictionary<string, string?> BuildFilterQuery(string preset, DateTime? from = null, DateTime? to = null)
    {
        var normalized = string.IsNullOrWhiteSpace(preset) ? "today" : preset.Trim().ToLowerInvariant();
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["preset"] = normalized
        };

        if (string.Equals(normalized, "custom", StringComparison.OrdinalIgnoreCase))
        {
            query["from"] = (from ?? ReportFromLocal).ToString("yyyy-MM-dd");
            query["to"] = (to ?? ReportToLocal).ToString("yyyy-MM-dd");
        }

        return query;
    }

    private string BuildPageUrl(IReadOnlyDictionary<string, string?> query) =>
        Url.Page("/Admin/Reports/Index", new Dictionary<string, string?>(query, StringComparer.OrdinalIgnoreCase))!;

    private void ResolveDateRange()
    {
        var today = AnnapBusinessTime.TodayLocal;
        ActivePreset = string.IsNullOrWhiteSpace(Preset) ? "today" : Preset.Trim().ToLowerInvariant();

        switch (ActivePreset)
        {
            case "yesterday":
                ReportFromLocal = today.AddDays(-1);
                ReportToLocal = today.AddDays(-1);
                break;
            case "this-month":
                ReportFromLocal = new DateTime(today.Year, today.Month, 1);
                ReportToLocal = today;
                break;
            case "last-month":
                var firstThisMonth = new DateTime(today.Year, today.Month, 1);
                ReportFromLocal = firstThisMonth.AddMonths(-1);
                ReportToLocal = firstThisMonth.AddDays(-1);
                break;
            case "custom":
                ReportFromLocal = ResolveLocalDate(From, "from") ?? today;
                ReportToLocal = ResolveLocalDate(To, "to") ?? today;
                break;
            default:
                ActivePreset = "today";
                ReportFromLocal = today;
                ReportToLocal = today;
                break;
        }

        if (ReportFromLocal > ReportToLocal)
            (ReportFromLocal, ReportToLocal) = (ReportToLocal, ReportFromLocal);
    }

    private DateTime? ResolveLocalDate(DateTime? bound, string queryKey)
    {
        if (bound.HasValue)
            return bound.Value.Date;

        if (DateTime.TryParse(Request.Query[queryKey], out var parsed))
            return parsed.Date;

        return null;
    }

    private static AdminReportBillDisplayVm ToBillDisplay(OrderBillDto bill) =>
        new(
            bill.ShopName,
            bill.BillNumber,
            bill.TableCode,
            bill.PaidAtUtc is { } p ? AnnapBusinessTime.FormatLocalDateTime(p) : null,
            bill.PaymentStatusLabelVi,
            bill.TotalAmount,
            bill.Items.Select(i => new AdminReportBillLineVm(i.Name, i.Quantity, i.LineTotal)).ToList());
}
