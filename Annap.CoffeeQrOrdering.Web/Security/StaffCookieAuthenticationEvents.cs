using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Annap.CoffeeQrOrdering.Web.Security;

/// <summary>
/// Cookie auth defaults redirect browsers to /Staff/Login. Staff JSON APIs must return 401/403 instead.
/// </summary>
internal static class StaffCookieAuthenticationEvents
{
    public static Task OnRedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteApiStatusOrRedirect(context, StatusCodes.Status401Unauthorized, "Unauthorized");

    public static Task OnRedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteApiStatusOrRedirect(context, StatusCodes.Status403Forbidden, "Forbidden");

    private static Task WriteApiStatusOrRedirect(
        RedirectContext<CookieAuthenticationOptions> context,
        int statusCode,
        string error)
    {
        if (IsStaffApiRequest(context.Request))
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(new { error });
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    private static bool IsStaffApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api/staff", StringComparison.OrdinalIgnoreCase);
}
