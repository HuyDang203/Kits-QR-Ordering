using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Connectivity;

[Authorize(Policy = "Staff")]
public sealed class DataAuditModel(
    ProductionDataAuditService auditService,
    IConfiguration configuration) : PageModel
{
    public ProductionDataAuditReport Report { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Report = await auditService.BuildAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            var dbTarget = DatabaseStartupHelper.ResolveConnectionTarget(configuration);
            Report = ProductionDataAuditReport.Failed(
                $"Data audit failed unexpectedly: {ex.Message}",
                dbTarget.Host,
                dbTarget.Database);
        }
    }
}
