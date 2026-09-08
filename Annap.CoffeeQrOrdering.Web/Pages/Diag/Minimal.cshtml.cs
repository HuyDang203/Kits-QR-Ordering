using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Diag;

[AllowAnonymous]
public sealed class MinimalModel(IWebHostEnvironment env) : PageModel
{
    public IActionResult OnGet()
    {
        if (!env.IsDevelopment())
            return NotFound();
        return Page();
    }
}
