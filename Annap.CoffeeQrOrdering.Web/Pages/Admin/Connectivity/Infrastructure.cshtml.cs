using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Connectivity;

[Authorize(Policy = "Staff")]
public sealed class InfrastructureModel(InfrastructureDiagnosticsService diagnostics) : PageModel
{
    public InfrastructureDiagnosticsService.InfrastructureReport Report { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var requestBaseUrl = $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
        Report = await diagnostics.BuildAsync(requestBaseUrl, cancellationToken);
    }
}
