namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Resolved QR / external-link base URL and its provenance.</summary>
public sealed record AppUrlResolution(
    AppUrlResolutionSource Source,
    string ResolvedBaseUrl,
    string? DatabaseOverride,
    string? ConfiguredPublicBaseUrl,
    string? RequestDerivedBaseUrl,
    IReadOnlyList<string> Warnings)
{
    public string SourceLabel => Source switch
    {
        AppUrlResolutionSource.DatabaseOverride => "Database override (PublicBaseUrlOverride)",
        AppUrlResolutionSource.AppUrlPublicBaseUrl => "AppUrl__PublicBaseUrl",
        AppUrlResolutionSource.RequestHost => "Request host fallback",
        _ => "Unresolved"
    };

    public string ResolvedHostname
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ResolvedBaseUrl))
                return "(none)";
            return Uri.TryCreate(ResolvedBaseUrl, UriKind.Absolute, out var u)
                ? u.Host
                : ResolvedBaseUrl;
        }
    }

    public string SampleTableQrUrl(string tableCode = "T01") =>
        string.IsNullOrWhiteSpace(ResolvedBaseUrl)
            ? $"/table/{tableCode}"
            : $"{ResolvedBaseUrl.TrimEnd('/')}/table/{tableCode}";
}
