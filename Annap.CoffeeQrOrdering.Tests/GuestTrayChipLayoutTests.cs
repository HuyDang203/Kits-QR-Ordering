namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Bottom tray chip layout contract: flex lead | price | chevron.
/// checkout-sheet.css is the sole layout owner.
/// </summary>
public sealed class GuestTrayChipLayoutTests
{
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web"));

    private static string ReadWebFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { WebRoot }.Concat(parts).ToArray()));

    [Fact]
    public void Chip_markup_uses_flex_three_zone_architecture()
    {
        var cshtml = ReadWebFile("Pages", "Shared", "_OrderTrayDock.cshtml");
        Assert.Contains("id=\"order-tray-chip\"", cshtml, StringComparison.Ordinal);
        Assert.Contains("order-tray-chip__lead", cshtml, StringComparison.Ordinal);
        Assert.Contains("order-tray-chip__price", cshtml, StringComparison.Ordinal);
        Assert.Contains("order-tray-chip__impact", cshtml, StringComparison.Ordinal);
        Assert.DoesNotContain("order-tray-chip__side", cshtml, StringComparison.Ordinal);
        Assert.DoesNotContain("grid-cols-[", cshtml, StringComparison.Ordinal);

        var chipStart = cshtml.IndexOf("id=\"order-tray-chip\"", StringComparison.Ordinal);
        var chipEnd = cshtml.IndexOf("</button>", chipStart, StringComparison.Ordinal);
        var chipBlock = cshtml[chipStart..chipEnd];
        Assert.Contains("class=\"order-tray-chip__impact", chipBlock, StringComparison.Ordinal);
        Assert.Contains("order-tray-chip__lead", chipBlock, StringComparison.Ordinal);
        Assert.Contains("id=\"order-tray-chip-total\"", chipBlock, StringComparison.Ordinal);
        Assert.Contains("order-tray-chevron", chipBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Checkout_sheet_is_sole_chip_layout_owner()
    {
        var css = ReadWebFile("wwwroot", "css", "checkout-sheet.css");
        Assert.Contains("sole layout owner", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("flex-wrap: nowrap", css, StringComparison.Ordinal);
        Assert.Contains(".order-tray-chip__lead", css, StringComparison.Ordinal);
        Assert.Contains("flex: 0 1 auto", css, StringComparison.Ordinal);
        Assert.Contains(".order-tray-chip__price", css, StringComparison.Ordinal);
        Assert.Contains("flex: 1 1 auto", css, StringComparison.Ordinal);
        Assert.Contains("justify-content: flex-end", css, StringComparison.Ordinal);
        Assert.Contains(".order-tray-chip__total", css, StringComparison.Ordinal);
        Assert.Contains("flex-shrink: 0", css, StringComparison.Ordinal);
        Assert.Contains("font-variant-numeric: tabular-nums", css, StringComparison.Ordinal);
        Assert.Contains("text-overflow: ellipsis", css, StringComparison.Ordinal);
        var totalRuleStart = css.IndexOf(".order-tray-chip__total {", StringComparison.Ordinal);
        Assert.True(totalRuleStart >= 0);
        var totalRuleEnd = css.IndexOf('}', totalRuleStart);
        var totalRule = css[totalRuleStart..totalRuleEnd];
        Assert.DoesNotContain("max-width:", totalRule, StringComparison.Ordinal);
        Assert.Contains("flex-shrink: 0", totalRule, StringComparison.Ordinal);
    }

    [Fact]
    public void Correspondence_css_has_no_chip_grid_or_layout_competition()
    {
        var css = ReadWebFile("wwwroot", "css", "correspondence.css");
        Assert.DoesNotContain("order-tray-chip__side", css, StringComparison.Ordinal);
        Assert.DoesNotContain("order-tray-chip__price", css, StringComparison.Ordinal);
        Assert.DoesNotContain("grid-template-columns: minmax(0, 1fr) auto", css, StringComparison.Ordinal);
        Assert.DoesNotContain("grid-template-columns: 2.15rem", css, StringComparison.Ordinal);
        Assert.DoesNotContain("max-width: 5.3rem", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_experience_css_does_not_own_chip_flex_layout()
    {
        var css = ReadWebFile("wwwroot", "css", "guest-experience.css");
        Assert.DoesNotContain("order-tray-chip__price", css, StringComparison.Ordinal);
        Assert.DoesNotContain("order-tray-chip__side", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("checkout-sheet.css")]
    [InlineData("correspondence.css")]
    [InlineData("guest-experience.css")]
    [InlineData("guest-tray-submitted.css")]
    public void Chip_total_has_no_max_width_cap(string cssFile)
    {
        var css = ReadWebFile("wwwroot", "css", cssFile);
        var totalIdx = 0;
        while ((totalIdx = css.IndexOf("order-tray-chip__total", totalIdx, StringComparison.Ordinal)) >= 0)
        {
            var blockEnd = css.IndexOf('}', totalIdx);
            if (blockEnd < 0) break;
            var block = css[totalIdx..blockEnd];
            Assert.DoesNotContain("max-width:", block, StringComparison.Ordinal);
            totalIdx = blockEnd + 1;
        }
    }

    [Fact]
    public void No_css_file_applies_grid_to_order_tray_chip()
    {
        var cssDir = Path.Combine(WebRoot, "wwwroot", "css");
        foreach (var file in Directory.EnumerateFiles(cssDir, "*.css"))
        {
            var css = File.ReadAllText(file);
            if (!css.Contains("#order-tray-chip", StringComparison.Ordinal)
                && !css.Contains(".order-tray-chip", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = css.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("grid-template-columns", StringComparison.Ordinal))
                    continue;

                var contextStart = Math.Max(0, i - 6);
                var context = string.Join('\n', lines[contextStart..Math.Min(lines.Length, i + 1)]);
                if (context.Contains("order-tray-chip", StringComparison.Ordinal)
                    || context.Contains("#order-tray-chip", StringComparison.Ordinal))
                {
                    Assert.Fail($"grid-template-columns on chip in {Path.GetFileName(file)}:{i + 1}");
                }
            }
        }
    }
}
