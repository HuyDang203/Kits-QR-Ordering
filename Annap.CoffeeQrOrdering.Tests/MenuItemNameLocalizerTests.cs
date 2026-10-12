using System.Globalization;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Unit tests for <see cref="MenuItemNameLocalizer"/> — the single source of
/// truth that selects the active-locale name for a menu item.
///
/// The rule (mirrors the helper's XML doc):
///   - VI            -> <c>MenuItem.Name</c>
///   - EN, NameEn    -> <c>MenuItem.NameEn</c> (trimmed)
///   - EN, no NameEn -> <c>MenuItem.Name</c>  (fallback; never null/empty)
///   - other / null  -> <c>MenuItem.Name</c>  (safe fallback)
/// </summary>
public sealed class MenuItemNameLocalizerTests
{
    private static MenuItem NewItem(string vi, string? en = null) => new()
    {
        Name = vi,
        NameEn = en
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Vietnamese path
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("vi")]
    [InlineData("VI")]
    [InlineData("vi-VN")]
    [InlineData("vi-vn")]
    public void For_vi_returns_vietnamese_name(string langCode)
    {
        var item = NewItem("Cà phê sữa đá", "Vietnamese Iced Coffee");
        Assert.Equal("Cà phê sữa đá", MenuItemNameLocalizer.For(item, langCode));
    }

    [Fact]
    public void For_vi_returns_vietnamese_name_even_when_nameen_blank()
    {
        var item = NewItem("Cà phê sữa đá", null);
        Assert.Equal("Cà phê sữa đá", MenuItemNameLocalizer.For(item, "vi"));
    }

    [Fact]
    public void For_vi_returns_vietnamese_name_even_when_nameen_whitespace()
    {
        var item = NewItem("Cà phê sữa đá", "   \t\n  ");
        Assert.Equal("Cà phê sữa đá", MenuItemNameLocalizer.For(item, "vi"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // English path — NameEn present
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("en")]
    [InlineData("EN")]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    public void For_en_returns_english_name(string langCode)
    {
        var item = NewItem("Cà phê sữa đá", "Vietnamese Iced Coffee");
        Assert.Equal("Vietnamese Iced Coffee", MenuItemNameLocalizer.For(item, langCode));
    }

    [Fact]
    public void For_en_trims_surrounding_whitespace_from_nameen()
    {
        var item = NewItem("Cà phê sữa đá", "   Vietnamese Iced Coffee  ");
        Assert.Equal("Vietnamese Iced Coffee", MenuItemNameLocalizer.For(item, "en"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // English path — fallback when NameEn missing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void For_en_falls_back_to_vietnamese_when_nameen_null()
    {
        var item = NewItem("Cà phê sữa đá", null);
        Assert.Equal("Cà phê sữa đá", MenuItemNameLocalizer.For(item, "en"));
    }

    [Fact]
    public void For_en_falls_back_to_vietnamese_when_nameen_empty()
    {
        var item = NewItem("Cà phê sữa đá", "");
        Assert.Equal("Cà phê sữa đá", MenuItemNameLocalizer.For(item, "en"));
    }

    [Fact]
    public void For_en_falls_back_to_vietnamese_when_nameen_whitespace()
    {
        var item = NewItem("Cà phê sữa đá", "   \t  \n  ");
        Assert.Equal("Cà phê sữa đá", MenuItemNameLocalizer.For(item, "en"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unknown / null locales — safe fallback
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fr")]
    [InlineData("ja")]
    [InlineData("de-DE")]
    public void For_unknown_locale_falls_back_to_vietnamese(string? langCode)
    {
        var item = NewItem("Cà phê sữa đá", "Vietnamese Iced Coffee");
        Assert.Equal("Cà phê sữa đá", MenuItemNameLocalizer.For(item, langCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void For_unknown_locale_falls_back_even_when_nameen_present(string? langCode)
    {
        var item = NewItem("Cà phê sữa đá", "Vietnamese Iced Coffee");
        Assert.Equal("Cà phê sữa đá", MenuItemNameLocalizer.For(item, langCode));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CultureInfo overload
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void For_with_culture_en_US_returns_english_name()
    {
        var item = NewItem("Cà phê sữa đá", "Vietnamese Iced Coffee");
        Assert.Equal(
            "Vietnamese Iced Coffee",
            MenuItemNameLocalizer.For(item, new CultureInfo("en-US")));
    }

    [Fact]
    public void For_with_culture_vi_returns_vietnamese_name()
    {
        var item = NewItem("Cà phê sữa đá", "Vietnamese Iced Coffee");
        Assert.Equal(
            "Cà phê sữa đá",
            MenuItemNameLocalizer.For(item, new CultureInfo("vi-VN")));
    }

    [Fact]
    public void For_with_null_culture_returns_vietnamese_name()
    {
        var item = NewItem("Cà phê sữa đá", "Vietnamese Iced Coffee");
        Assert.Equal("Cà phê sữa đá", MenuItemNameLocalizer.For(item, (CultureInfo?)null));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Never returns empty / null
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void For_never_returns_null()
    {
        var item = NewItem("Cà phê sữa đá", "");
        Assert.NotNull(MenuItemNameLocalizer.For(item, "en"));
        Assert.NotNull(MenuItemNameLocalizer.For(item, "vi"));
        Assert.NotNull(MenuItemNameLocalizer.For(item, (string?)null));
    }

    [Fact]
    public void For_never_returns_empty_string()
    {
        var item = NewItem("Cà phê sữa đá", "");
        foreach (var lang in new[] { "vi", "en", null, "", "fr" })
        {
            var result = MenuItemNameLocalizer.For(item, lang);
            Assert.False(string.IsNullOrEmpty(result), $"Locale '{lang ?? "<null>"}' produced empty result.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Argument validation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void For_with_null_item_throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => MenuItemNameLocalizer.For(null!, "vi"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IsEnglish_recognises_en_variants()
    {
        Assert.True(MenuItemNameLocalizer.IsEnglish("en"));
        Assert.True(MenuItemNameLocalizer.IsEnglish("EN"));
        Assert.True(MenuItemNameLocalizer.IsEnglish("en-US"));
        Assert.True(MenuItemNameLocalizer.IsEnglish("  en  "));
        Assert.False(MenuItemNameLocalizer.IsEnglish("vi"));
        Assert.False(MenuItemNameLocalizer.IsEnglish(null));
        Assert.False(MenuItemNameLocalizer.IsEnglish(""));
        Assert.False(MenuItemNameLocalizer.IsEnglish("   "));
        Assert.False(MenuItemNameLocalizer.IsEnglish("fr"));
    }
}