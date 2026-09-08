using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class GuestSessionTokensTests
{
    private const string ValidToken = "a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef123456";

    [Fact]
    public void Matches_empty_stored_token_returns_false()
    {
        Assert.False(GuestSessionTokens.Matches("", ValidToken));
    }

    [Fact]
    public void Matches_null_stored_token_returns_false()
    {
        Assert.False(GuestSessionTokens.Matches(null, ValidToken));
    }

    [Fact]
    public void Matches_empty_supplied_token_returns_false()
    {
        Assert.False(GuestSessionTokens.Matches(ValidToken, ""));
    }

    [Fact]
    public void Matches_null_supplied_token_returns_false()
    {
        Assert.False(GuestSessionTokens.Matches(ValidToken, null));
    }

    [Fact]
    public void Matches_whitespace_supplied_token_returns_false()
    {
        Assert.False(GuestSessionTokens.Matches(ValidToken, "   "));
    }

    [Fact]
    public void Matches_correct_token_returns_true()
    {
        Assert.True(GuestSessionTokens.Matches(ValidToken, ValidToken));
    }

    [Fact]
    public void Matches_correct_token_with_surrounding_whitespace_returns_true()
    {
        Assert.True(GuestSessionTokens.Matches(ValidToken, $"  {ValidToken}  "));
    }

    [Fact]
    public void Matches_wrong_token_returns_false()
    {
        var wrong = new string(ValidToken.Reverse().ToArray());
        Assert.False(GuestSessionTokens.Matches(ValidToken, wrong));
    }

    [Fact]
    public void Matches_both_empty_returns_false()
    {
        Assert.False(GuestSessionTokens.Matches("", ""));
    }
}
