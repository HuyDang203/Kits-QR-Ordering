using System.Net;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffLoginRateLimitTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public async Task Staff_login_get_is_not_rate_limited()
    {
        var partition = Guid.NewGuid().ToString("N");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", partition);

        for (var i = 0; i < 20; i++)
        {
            var ok = await client.GetAsync("/Staff/Login");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
    }

    [Fact]
    public async Task Staff_login_post_rate_limits_after_repeated_attempts()
    {
        var partition = Guid.NewGuid().ToString("N");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", partition);

        for (var i = 0; i < 8; i++)
        {
            var token = await GetAntiforgeryTokenAsync(client);
            Assert.False(string.IsNullOrEmpty(token));
            var attempt = await PostLoginAsync(client, token!);
            Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
        }

        var limitedToken = await GetAntiforgeryTokenAsync(client);
        Assert.False(string.IsNullOrEmpty(limitedToken));
        var limited = await PostLoginAsync(client, limitedToken!);
        Assert.Equal(HttpStatusCode.Redirect, limited.StatusCode);
        var location = limited.Headers.Location?.ToString() ?? "";
        Assert.Contains("rateLimited=1", location, StringComparison.Ordinal);
    }

    private static async Task<string?> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/Staff/Login");
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(
                html,
                @"value=""([^""]+)""[^>]*name=""__RequestVerificationToken""",
                RegexOptions.IgnoreCase);
        }

        return match.Success ? match.Groups[1].Value : null;
    }

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string antiforgeryToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserName"] = "wrong-user",
            ["Password"] = "wrong-password",
            ["__RequestVerificationToken"] = antiforgeryToken
        });
        return client.PostAsync("/Staff/Login", content);
    }
}
