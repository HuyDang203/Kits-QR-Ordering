using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Static unit tests for the FulfillmentModes normalizer/labels helper.
/// </summary>
public sealed class FulfillmentModesHelperTests
{
    [Theory]
    [InlineData("DineIn", "DineIn")]
    [InlineData("dinein", "DineIn")]
    [InlineData("DINEIN", "DineIn")]
    [InlineData("Dine In", null)]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void Normalize_returns_canonical_DineIn_or_null(string? raw, string? expected)
    {
        Assert.Equal(expected, FulfillmentModes.Normalize(raw));
    }

    [Theory]
    [InlineData("Takeaway", "Takeaway")]
    [InlineData("takeaway", "Takeaway")]
    [InlineData("TAKEAWAY", "Takeaway")]
    [InlineData("Take Away", null)]
    [InlineData("DriveThru", null)]
    public void Normalize_returns_canonical_Takeaway_or_null(string? raw, string? expected)
    {
        Assert.Equal(expected, FulfillmentModes.Normalize(raw));
    }

    [Fact]
    public void Labels_for_DineIn_returns_Vietnamese_and_English()
    {
        var (vi, en) = FulfillmentModes.Labels("DineIn");
        Assert.Equal("Uống tại bàn", vi);
        Assert.Equal("Dine in", en);
    }

    [Fact]
    public void Labels_for_Takeaway_returns_Vietnamese_and_English()
    {
        var (vi, en) = FulfillmentModes.Labels("Takeaway");
        Assert.Equal("Take away", vi);
        Assert.Equal("Take away", en);
    }

    [Fact]
    public void Labels_for_null_returns_unknown_neutral()
    {
        var (vi, en) = FulfillmentModes.Labels(null);
        Assert.Equal("Không xác định", vi);
        Assert.Equal("Unknown", en);
    }

    [Fact]
    public void SubmittedBadge_for_DineIn_returns_Vietnamese_label()
    {
        Assert.Equal("Uống tại bàn", FulfillmentModes.SubmittedBadgeVi("DineIn"));
    }

    [Fact]
    public void SubmittedBadge_for_Takeaway_returns_Vietnamese_label()
    {
        Assert.Equal("Take away", FulfillmentModes.SubmittedBadgeVi("Takeaway"));
    }

    [Fact]
    public void SubmittedBadge_for_null_returns_unknown_neutral()
    {
        Assert.Equal("Không xác định", FulfillmentModes.SubmittedBadgeVi(null));
    }
}
