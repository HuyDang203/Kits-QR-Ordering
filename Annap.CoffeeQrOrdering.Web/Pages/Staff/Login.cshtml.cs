using System.Security.Claims;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Security;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Annap.CoffeeQrOrdering.Web.Pages.Staff;

[AllowAnonymous]
[EnableRateLimiting("staff-login")]
public sealed class LoginModel(
    IOptions<StaffAuthOptions> options,
    IStaffAccountService staffAccounts) : PageModel
{
    [BindProperty]
    public string UserName { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectAfterLogin();

        if (Request.Query.ContainsKey("rateLimited"))
        {
            ErrorMessage = "Bạn thử đăng nhập quá nhiều lần. Vui lòng chờ một chút rồi thử lại.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var o = options.Value;
        var userTrim = UserName?.Trim() ?? "";
        var password = Password ?? "";
        var isSharedLogin = string.IsNullOrEmpty(userTrim) || SlowEquals(userTrim, o.UserName);

        if (!isSharedLogin)
        {
            var account = await staffAccounts.AuthenticateAsync(userTrim, password);
            if (account is not null)
            {
                await SignInStaffAccountAsync(account);
                await staffAccounts.RecordLoginAsync(account.Id);
                return RedirectAfterLogin();
            }

            ErrorMessage = "Vui lòng kiểm tra tên đăng nhập và mật khẩu.";
            return Page();
        }

        var roles = ResolveRoles(o, password);
        if (roles.Count > 0 && (string.IsNullOrEmpty(userTrim) || SlowEquals(userTrim, o.UserName)))
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, o.UserName) };
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(id),
                new AuthenticationProperties { IsPersistent = true });

            return RedirectAfterLogin();
        }

        ErrorMessage = "Vui lòng kiểm tra tên đăng nhập và mật khẩu.";
        return Page();
    }

    private IActionResult RedirectAfterLogin()
    {
        var returnUrl = Request.Query["returnUrl"].FirstOrDefault();
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToPage("/Staff/Orders");
    }

    private async Task SignInStaffAccountAsync(StaffAccount account)
    {
        var isBarista = StaffAccountRoles.IsBarista(account.Role);
        var floorRole = isBarista ? StaffRoleNames.Barista : StaffRoleNames.Checkout;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Role, floorRole),
            new(StaffClaimTypes.AccountId, account.Id.ToString("D")),
            new(StaffClaimTypes.DisplayName, account.DisplayName),
            new(StaffClaimTypes.Username, account.Username),
            new(StaffClaimTypes.AccountRole, account.Role)
        };

        if (!isBarista)
            claims.Add(new(StaffClaimTypes.CanCloseShift, "true"));

        var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(id),
            new AuthenticationProperties { IsPersistent = true });
    }

    internal static IReadOnlyList<string> ResolveRoles(StaffAuthOptions o, string password)
    {
        if (SlowEquals(password, o.Password))
            return [StaffRoleNames.Admin, StaffRoleNames.Checkout, StaffRoleNames.Barista];

        if (!string.IsNullOrEmpty(o.CheckoutPassword) && SlowEquals(password, o.CheckoutPassword))
            return [StaffRoleNames.Checkout];

        if (!string.IsNullOrEmpty(o.BaristaPassword) && SlowEquals(password, o.BaristaPassword))
            return [StaffRoleNames.Barista];

        return [];
    }

    private static bool SlowEquals(string? a, string? b)
    {
        var ab = Encoding.UTF8.GetBytes(a ?? "");
        var bb = Encoding.UTF8.GetBytes(b ?? "");
        if (ab.Length != bb.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(ab, bb);
    }
}
