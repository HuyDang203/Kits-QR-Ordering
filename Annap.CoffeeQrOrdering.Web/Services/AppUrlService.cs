using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed class AppUrlService(
    IHttpContextAccessor httpContextAccessor,
    IOptionsSnapshot<AppUrlOptions> appUrlOptions,
    IApplicationDbContext db) : IAppUrlService
{
    private const string RequestCacheKey = "__annapResolvedPublicBaseUrl";

    public string GetBaseUrl(HttpContext? httpContext = null)
    {
        var ctx = httpContext ?? httpContextAccessor.HttpContext;
        if (ctx?.Items.TryGetValue(RequestCacheKey, out var cached) == true && cached is string hit && !string.IsNullOrEmpty(hit))
            return hit;

        var resolved = ResolveCore(ctx).BaseUrl;
        if (ctx is not null)
            ctx.Items[RequestCacheKey] = resolved;
        return resolved;
    }

    public string BuildAbsoluteUrl(string relativePath, HttpContext? httpContext = null)
    {
        var path = (relativePath ?? "").Trim();
        if (path.Length == 0)
            path = "/";
        if (path[0] != '/')
            path = "/" + path;
        var baseUrl = GetBaseUrl(httpContext);
        if (string.IsNullOrEmpty(baseUrl))
            return path;
        return baseUrl + path;
    }

    public AppUrlResolution DescribeResolution(HttpContext? httpContext = null)
    {
        var ctx = httpContext ?? httpContextAccessor.HttpContext;
        var core = ResolveCore(ctx);
        var requestDerived = ctx?.Request is { } req && req.Host.HasValue
            ? $"{req.Scheme}://{req.Host.Value}".TrimEnd('/')
            : null;

        var resolved = core.BaseUrl;
        if (string.IsNullOrEmpty(resolved) && core.Source == AppUrlResolutionSource.Unresolved)
        {
            var renderUrl = NormalizeBase(Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL"));
            if (!string.IsNullOrEmpty(renderUrl))
            {
                resolved = renderUrl;
                core = core with { Source = AppUrlResolutionSource.RequestHost, BaseUrl = renderUrl };
            }
        }

        var warnings = BuildWarnings(core, requestDerived);
        return new AppUrlResolution(
            core.Source,
            resolved,
            core.DatabaseOverride,
            core.ConfiguredPublicBaseUrl,
            requestDerived,
            warnings);
    }

    private sealed record ResolveCoreResult(
        AppUrlResolutionSource Source,
        string BaseUrl,
        string? DatabaseOverride,
        string? ConfiguredPublicBaseUrl);

    private ResolveCoreResult ResolveCore(HttpContext? ctx)
    {
        var dbOverrideRaw = db.AppNetworkSettings.AsNoTracking()
            .Where(x => x.Id == AppNetworkSettings.SingletonId)
            .Select(x => x.PublicBaseUrlOverride)
            .FirstOrDefault();
        var fromDb = NormalizeBase(dbOverrideRaw);
        if (!string.IsNullOrEmpty(fromDb))
        {
            return new ResolveCoreResult(
                AppUrlResolutionSource.DatabaseOverride,
                fromDb,
                fromDb,
                null);
        }

        var fromCfg = NormalizeBase(appUrlOptions.Value.PublicBaseUrl);
        if (!string.IsNullOrEmpty(fromCfg))
        {
            return new ResolveCoreResult(
                AppUrlResolutionSource.AppUrlPublicBaseUrl,
                fromCfg,
                null,
                fromCfg);
        }

        if (ctx?.Request is { } req && req.Host.HasValue)
        {
            var requestBase = $"{req.Scheme}://{req.Host.Value}".TrimEnd('/');
            return new ResolveCoreResult(AppUrlResolutionSource.RequestHost, requestBase, null, null);
        }

        return new ResolveCoreResult(AppUrlResolutionSource.Unresolved, "", null, null);
    }

    private static IReadOnlyList<string> BuildWarnings(ResolveCoreResult core, string? requestDerivedBaseUrl)
    {
        var warnings = new List<string>();
        if (string.IsNullOrEmpty(core.BaseUrl))
            return warnings;

        if (!Uri.TryCreate(core.BaseUrl, UriKind.Absolute, out var resolvedUri))
        {
            warnings.Add($"QR public base URL is not a valid absolute URL: {core.BaseUrl}");
            return warnings;
        }

        var resolvedHost = resolvedUri.Host;
        if (IsLocalHost(resolvedHost) && !IsDevelopmentEnvironment())
        {
            warnings.Add(
                $"QR hostname is {resolvedHost}. Production QR codes should not use localhost; clear override/config or set AppUrl__PublicBaseUrl to the public Render URL.");
        }

        var renderUrl = NormalizeBase(Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL"));
        if (!string.IsNullOrEmpty(renderUrl)
            && Uri.TryCreate(renderUrl, UriKind.Absolute, out var renderUri)
            && !string.Equals(resolvedHost, renderUri.Host, StringComparison.OrdinalIgnoreCase)
            && core.Source is AppUrlResolutionSource.DatabaseOverride or AppUrlResolutionSource.AppUrlPublicBaseUrl)
        {
            warnings.Add(
                $"QR hostname ({resolvedHost}) does not match this Render service ({renderUri.Host}). "
                + "Printed QR codes may point at a stale domain. Update /admin/system/network or AppUrl__PublicBaseUrl.");
        }

        if (!string.IsNullOrEmpty(requestDerivedBaseUrl)
            && Uri.TryCreate(requestDerivedBaseUrl, UriKind.Absolute, out var requestUri)
            && !string.Equals(resolvedHost, requestUri.Host, StringComparison.OrdinalIgnoreCase)
            && core.Source is AppUrlResolutionSource.DatabaseOverride or AppUrlResolutionSource.AppUrlPublicBaseUrl)
        {
            warnings.Add(
                $"QR hostname ({resolvedHost}) differs from the host you are browsing ({requestUri.Host}). "
                + "New QR codes will not match this deployment until override/config is corrected.");
        }

        return warnings;
    }

    private static bool IsDevelopmentEnvironment()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeBase(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        return raw.Trim().TrimEnd('/');
    }
}
