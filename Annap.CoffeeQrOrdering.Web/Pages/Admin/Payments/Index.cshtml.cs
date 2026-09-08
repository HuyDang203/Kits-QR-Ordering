using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Payments;

[Authorize(Policy = "StaffAdmin")]
public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    public const string ManualReviewNote = "Kiểm tra thủ công tại quầy";

    [BindProperty(SupportsGet = true)]
    public string? Preset { get; set; }

    [BindProperty(SupportsGet = true, Name = "from")]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public DateTime? To { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Provider { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageIndex { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    public AdminPaymentConfirmationPageVm PageData { get; private set; } = null!;

    public AdminPaymentConfirmationDetailVm? Selected { get; private set; }

    public string ActivePreset { get; private set; } = "last-7-days";

    public DateTime FromLocal { get; private set; }

    public DateTime ToLocal { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ResolveDateRange();
        var pageIndex = PageIndex;
        if (int.TryParse(Request.Query["page"], out var queryPage) && queryPage > 0)
            pageIndex = queryPage;

        PageData = await AdminPaymentConfirmationQuery.LoadPageAsync(
            db,
            FromLocal,
            ToLocal,
            string.IsNullOrWhiteSpace(Status) ? "all" : Status.Trim(),
            Provider,
            Search,
            pageIndex,
            cancellationToken);

        PageIndex = PageData.Page;

        if (Id is { } detailId && detailId != Guid.Empty)
            Selected = await AdminPaymentConfirmationQuery.LoadDetailAsync(db, detailId, cancellationToken);

        return Page();
    }

    public string BuildFilterUrl(
        string preset,
        DateTime? from = null,
        DateTime? to = null,
        string? status = null,
        string? provider = null,
        string? search = null,
        int? page = null,
        Guid? id = null) =>
        BuildPageUrl(BuildFilterQuery(preset, from, to, status, provider, search, page, id));

    public string BuildBillUrl(Guid orderId)
    {
        var query = BuildActiveFilterQuery();
        query.Remove("id");
        query["page"] = PageData.Page.ToString();
        var reportsFrom = query.GetValueOrDefault("from") ?? FromLocal.ToString("yyyy-MM-dd");
        var reportsTo = query.GetValueOrDefault("to") ?? ToLocal.ToString("yyyy-MM-dd");
        return Url.Page("/Admin/Reports/Index", new Dictionary<string, string?>
        {
            ["preset"] = "custom",
            ["from"] = reportsFrom,
            ["to"] = reportsTo,
            ["bill"] = orderId.ToString("D")
        })! + "#bill-detail";
    }

    public string BuildDetailUrl(Guid confirmationId)
    {
        var query = BuildActiveFilterQuery();
        query["id"] = confirmationId.ToString("D");
        return BuildPageUrl(query) + "#confirmation-detail";
    }

    public string BuildCloseDetailUrl() => BuildPageUrl(BuildActiveFilterQuery());

    private Dictionary<string, string?> BuildActiveFilterQuery() =>
        BuildFilterQuery(ActivePreset, FromLocal, ToLocal, Status, Provider, Search, PageData.Page, Id);

    private Dictionary<string, string?> BuildFilterQuery(
        string preset,
        DateTime? from = null,
        DateTime? to = null,
        string? status = null,
        string? provider = null,
        string? search = null,
        int? page = null,
        Guid? id = null)
    {
        var normalized = string.IsNullOrWhiteSpace(preset) ? "last-7-days" : preset.Trim().ToLowerInvariant();
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["preset"] = normalized,
            ["status"] = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim(),
            ["provider"] = string.IsNullOrWhiteSpace(provider) ? null : provider.Trim(),
            ["q"] = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            ["page"] = (page ?? 1).ToString()
        };

        if (id is { } detailId && detailId != Guid.Empty)
            query["id"] = detailId.ToString("D");

        if (string.Equals(normalized, "custom", StringComparison.OrdinalIgnoreCase))
        {
            query["from"] = (from ?? FromLocal).ToString("yyyy-MM-dd");
            query["to"] = (to ?? ToLocal).ToString("yyyy-MM-dd");
        }

        return query;
    }

    private string BuildPageUrl(IReadOnlyDictionary<string, string?> query)
    {
        var filtered = query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        return Url.Page("/Admin/Payments/Index", filtered)!;
    }

    private void ResolveDateRange()
    {
        var today = AnnapBusinessTime.TodayLocal;
        ActivePreset = string.IsNullOrWhiteSpace(Preset) ? "last-7-days" : Preset.Trim().ToLowerInvariant();

        switch (ActivePreset)
        {
            case "today":
                FromLocal = today;
                ToLocal = today;
                break;
            case "custom":
                FromLocal = ResolveLocalDate(From, "from") ?? today.AddDays(-6);
                ToLocal = ResolveLocalDate(To, "to") ?? today;
                break;
            default:
                ActivePreset = "last-7-days";
                FromLocal = today.AddDays(-6);
                ToLocal = today;
                break;
        }

        if (FromLocal > ToLocal)
            (FromLocal, ToLocal) = (ToLocal, FromLocal);
    }

    private DateTime? ResolveLocalDate(DateTime? bound, string queryKey)
    {
        if (bound.HasValue)
            return bound.Value.Date;

        if (DateTime.TryParse(Request.Query[queryKey], out var parsed))
            return parsed.Date;

        return null;
    }
}
