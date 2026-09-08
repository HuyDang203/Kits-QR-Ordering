using Annap.CoffeeQrOrdering.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Intelligence;

[Authorize(Policy = "Staff")]
public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    public IntelligencePageVm Observatory { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Observatory = await IntelligenceDataLoader.LoadAsync(db, cancellationToken);
}
