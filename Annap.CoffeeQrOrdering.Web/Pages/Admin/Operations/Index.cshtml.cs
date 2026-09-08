using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Operations;

[Authorize(Policy = "Staff")]
public sealed class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
