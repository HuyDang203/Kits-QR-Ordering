using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Demo;

[Authorize(Policy = "Staff")]
public sealed class QrModel(
    IAppUrlService appUrlService,
    IWebHostEnvironment environment,
    ILanIpDetector lanIpDetector,
    IHttpContextAccessor httpContextAccessor,
    IApplicationDbContext db) : PageModel
{
    public string PublicBaseUrl { get; private set; } = "";

    public AppUrlResolution QrResolution { get; private set; } =
        new(AppUrlResolutionSource.Unresolved, "", null, null, null, []);

    public bool ShowLocalhostLanBanner { get; private set; }

    public string PhoneDemoWiFiBaseUrl { get; private set; } = "";

    public bool CanGenerateQr { get; private set; }

    public string? BlockReason { get; private set; }

    public int TotalActiveTables { get; private set; }

    public int Take { get; private set; }

    public string CodesQuery { get; private set; } = "";

    public IReadOnlyList<DemoTableQrRow> Rows { get; private set; } = [];

    /// <summary>
    /// Query: take = how many cards (1–500). codes = optional comma list (T01,T02).
    /// Default: all active venue tables (capped at 500 per print job).
    /// </summary>
    public async Task OnGetAsync(int? take = null, string? codes = null, CancellationToken cancellationToken = default)
    {
        CodesQuery = (codes ?? "").Trim();
        var http = httpContextAccessor.HttpContext;
        QrResolution = appUrlService.DescribeResolution(http);
        var resolved = QrResolution.ResolvedBaseUrl.TrimEnd('/');
        PublicBaseUrl = resolved;

        var port = 8080;
        if (Uri.TryCreate(resolved, UriKind.Absolute, out var pubUri) && pubUri.Port > 0)
            port = pubUri.Port;

        var detectedIp = lanIpDetector.TryGetPreferredLanIPv4();
        PhoneDemoWiFiBaseUrl = !string.IsNullOrEmpty(detectedIp) && port > 0
            ? $"http://{detectedIp}:{port}"
            : resolved;

        var host = http?.Request.Host.Host ?? "";
        ShowLocalhostLanBanner = environment.IsDevelopment()
            && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.Equals("127.0.0.1", StringComparison.Ordinal));

        string baseUrl;
        if (PublicBaseUrlRules.TryNormalizeAbsoluteHttpUrl(resolved, out var safeBase, out var baseError))
        {
            baseUrl = safeBase;
            PublicBaseUrl = safeBase;
            CanGenerateQr = true;
        }
        else if (environment.IsDevelopment()
                 && Uri.TryCreate(resolved, UriKind.Absolute, out var devUri)
                 && !PublicBaseUrlRules.IsLoopbackHost(devUri.Host))
        {
            // Development LAN / Render preview hosts that are absolute but failed other checks.
            baseUrl = resolved;
            CanGenerateQr = true;
        }
        else
        {
            CanGenerateQr = false;
            BlockReason = environment.IsProduction()
                ? "Không thể tạo QR production: đặt AppUrl__PublicBaseUrl thành URL https công khai (không localhost). "
                  + (baseError ?? "")
                : "Địa chỉ QR chưa hợp lệ. Mở trang qua LAN IP hoặc đặt AppUrl__PublicBaseUrl. "
                  + (baseError ?? "");
            Rows = [];
            return;
        }

        if (!environment.IsDevelopment()
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out var prodUri)
            && PublicBaseUrlRules.IsLoopbackHost(prodUri.Host))
        {
            CanGenerateQr = false;
            BlockReason = "Production QR không được chứa localhost.";
            Rows = [];
            return;
        }

        if (!environment.IsDevelopment()
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out var prodHttpsUri)
            && !string.Equals(prodHttpsUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            CanGenerateQr = false;
            BlockReason = "Production QR phải dùng https (ví dụ https://your-kits-domain.example).";
            Rows = [];
            return;
        }

        var tablesQuery = db.VenueTables.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayCode);

        var codeFilter = (codes ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        TotalActiveTables = await tablesQuery.CountAsync(cancellationToken);

        var selected = codeFilter.Count > 0
            ? await tablesQuery.Where(t => codeFilter.Contains(t.DisplayCode)).ToListAsync(cancellationToken)
            : await tablesQuery.ToListAsync(cancellationToken);

        Take = take is > 0
            ? Math.Clamp(take.Value, 1, 500)
            : Math.Clamp(Math.Max(selected.Count, 1), 1, 500);
        selected = selected.Take(Take).ToList();

        if (selected.Count == 0)
        {
            CanGenerateQr = false;
            BlockReason = "Không có bàn active nào để in QR.";
            Rows = [];
            return;
        }

        Rows = selected.Select(t =>
        {
            var tableName = string.IsNullOrWhiteSpace(t.DisplayLabel) ? t.DisplayCode : t.DisplayLabel.Trim();
            var url = $"{baseUrl}/table/{t.DisplayCode}";
            return new DemoTableQrRow(
                t.DisplayCode,
                tableName,
                url,
                QrCodeDataUriBuilder.FromText(url, pixelsPerModule: 28));
        }).ToList();
    }
}

public sealed record DemoTableQrRow(
    string TableCode,
    string TableName,
    string GuestUrl,
    string QrDataUri);
