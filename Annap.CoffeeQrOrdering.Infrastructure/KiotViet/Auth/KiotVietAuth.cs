using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Auth;

internal static class KiotVietNamedHttpClients
{
    public const string Token = "kv-token";
    public const string Api = "kv-api";
}

public sealed class KiotVietTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<KiotVietOptions> options,
    ILogger<KiotVietTokenProvider> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _expiresAtUtc = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken ct)
    {
        var opts = options.Value;
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!forceRefresh
                && _cachedToken != null
                && DateTimeOffset.UtcNow.AddSeconds(90) < _expiresAtUtc)
            {
                return _cachedToken;
            }

            using var req = new HttpRequestMessage(HttpMethod.Post, opts.AuthUrl);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = opts.ClientId ?? "",
                ["client_secret"] = opts.ClientSecret ?? "",
                ["scope"] = opts.Scope ?? "PublicApi.Access"
            });

            var client = httpClientFactory.CreateClient(KiotVietNamedHttpClients.Token);
            using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            var raw = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "KiotViet token endpoint returned {Http}. Snippet={Snippet}",
                    (int)res.StatusCode,
                    raw.Length <= 240 ? raw : raw[..237] + "…");
                throw new InvalidOperationException($"KiotViet token denied: {(int)res.StatusCode}");
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            var root = doc.RootElement;
            var token = root.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("KiotViet token response missing access_token.");

            var expiresInSec = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
            _cachedToken = token;
            _expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresInSec);

            logger.LogInformation("KiotViet OAuth token renewed (expires in {Secs}s)", expiresInSec);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>Attaches Bearer; on first 401 rotates token and retries send once.</summary>
public sealed class KiotVietAuthDelegatingHandler(
    KiotVietTokenProvider tokenProvider,
    ILogger<KiotVietAuthDelegatingHandler> logger)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var tok = await tokenProvider.GetAccessTokenAsync(forceRefresh: attempt > 0, cancellationToken)
                .ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tok);
            var res = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized && attempt == 0)
            {
                logger.LogWarning("KiotViet API returned 401 — forcing token rotation and replaying send once.");
                res.Dispose();
                continue;
            }

            return res;
        }

        throw new InvalidOperationException("KiotViet auth delegation exhausted retries.");
    }
}

/// <summary>Injects Retailer header per request.</summary>
public sealed class KiotVietRetailerHeaderHandler(IOptions<KiotVietOptions> options)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var r = options.Value.Retailer?.Trim();
        if (!string.IsNullOrEmpty(r))
            request.Headers.TryAddWithoutValidation("Retailer", r);
        return base.SendAsync(request, cancellationToken);
    }
}
