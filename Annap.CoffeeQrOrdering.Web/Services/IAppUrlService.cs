namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Resolves the public base URL for QR payloads and absolute external links (not in-app API routing).</summary>
public interface IAppUrlService
{
    /// <summary>Origin without trailing slash: DB override → appsettings AppUrl:PublicBaseUrl → current request.</summary>
    string GetBaseUrl(HttpContext? httpContext = null);

    /// <summary>Combines <see cref="GetBaseUrl"/> with a path beginning with <c>/</c>.</summary>
    string BuildAbsoluteUrl(string relativePath, HttpContext? httpContext = null);

    /// <summary>Describes how <see cref="GetBaseUrl"/> would resolve for QR generation (includes stale-hostname warnings).</summary>
    AppUrlResolution DescribeResolution(HttpContext? httpContext = null);
}
