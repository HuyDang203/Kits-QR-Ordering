using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Connectivity;

[Authorize(Policy = "Staff")]
public sealed class NetworkModel(AppDbContext db, IWebHostEnvironment environment) : PageModel
{
    [BindProperty]
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Always derived from this request (scheme + host), ignoring DB override.</summary>
    public string RequestDerivedBaseUrl { get; private set; } = "";

    public bool TrailingSlashWarning { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        RequestDerivedBaseUrl = $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
        var row = await db.AppNetworkSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == AppNetworkSettings.SingletonId, cancellationToken);
        PublicBaseUrl = (row?.PublicBaseUrlOverride ?? "").Trim();
        TrailingSlashWarning = PublicBaseUrl.EndsWith('/');
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var raw = (PublicBaseUrl ?? "").Trim();
        TrailingSlashWarning = raw.EndsWith('/');
        if (string.IsNullOrEmpty(raw))
        {
            await SetOverrideAsync(null, cancellationToken);
            return RedirectToPage();
        }

        if (!PublicBaseUrlRules.TryNormalizeAbsoluteHttpUrl(raw, out var normalized, out var error))
        {
            ModelState.AddModelError(string.Empty, error ?? "Enter a valid absolute http or https URL, or leave empty for auto-detect.");
            RequestDerivedBaseUrl = $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
            TrailingSlashWarning = raw.EndsWith('/');
            return Page();
        }

        if (!environment.IsDevelopment() && PublicBaseUrlRules.IsLoopbackHost(new Uri(normalized).Host))
        {
            ModelState.AddModelError(string.Empty, "Production public base URL must not use localhost or 127.0.0.1.");
            RequestDerivedBaseUrl = $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
            return Page();
        }

        if (!environment.IsDevelopment()
            && !string.Equals(new Uri(normalized).Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Production public base URL must use https.");
            RequestDerivedBaseUrl = $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
            return Page();
        }

        await SetOverrideAsync(normalized, cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetAsync(CancellationToken cancellationToken)
    {
        await SetOverrideAsync(null, cancellationToken);
        return RedirectToPage();
    }

    private async Task SetOverrideAsync(string? value, CancellationToken cancellationToken)
    {
        var row = await db.AppNetworkSettings.FirstOrDefaultAsync(x => x.Id == AppNetworkSettings.SingletonId, cancellationToken);
        if (row is null)
        {
            row = new AppNetworkSettings
            {
                Id = AppNetworkSettings.SingletonId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                PublicBaseUrlOverride = value
            };
            db.AppNetworkSettings.Add(row);
        }
        else
        {
            row.PublicBaseUrlOverride = value;
            row.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
