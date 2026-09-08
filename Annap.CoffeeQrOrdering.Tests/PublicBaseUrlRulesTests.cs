using Annap.CoffeeQrOrdering.Web;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class PublicBaseUrlRulesTests
{
    [Theory]
    [InlineData("https://coffee.example.com", true)]
    [InlineData("https://coffee.example.com/", true)]
    [InlineData("http://localhost:8080", false)]
    [InlineData("http://127.0.0.1", false)]
    [InlineData("/table/T01", false)]
    [InlineData("", false)]
    public void TryNormalizeAbsoluteHttpUrl_validates_hosts(string input, bool expected)
    {
        var ok = PublicBaseUrlRules.TryNormalizeAbsoluteHttpUrl(input, out var normalized, out _);
        Assert.Equal(expected, ok);
        if (expected)
            Assert.Equal(input.TrimEnd('/'), normalized);
    }

    [Fact]
    public void ConnectionStringUsesLoopbackHost_detects_localhost()
    {
        Assert.True(PublicBaseUrlRules.ConnectionStringUsesLoopbackHost(
            "Host=localhost;Database=x;Username=u;Password=p"));
        Assert.False(PublicBaseUrlRules.ConnectionStringUsesLoopbackHost(
            "Host=postgres;Database=x;Username=u;Password=p"));
    }
}
