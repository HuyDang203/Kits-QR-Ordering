using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Staff;

[AllowAnonymous]
public sealed class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync(bool returnToLogin = false, string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (returnToLogin)
        {
            var login = "/staff/login";
            var next = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/staff/orders";
            login += "?returnUrl=" + Uri.EscapeDataString(next);
            return Redirect(login);
        }

        return Redirect("/staff/login");
    }
}
