using System.Reflection;
using Annap.CoffeeQrOrdering.Application;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Static unit tests for the canonical category names declared in
/// <see cref="KitsMenuCatalog.Categories"/>.
/// </summary>
public sealed class KitsMenuCatalogTests
{
    [Fact]
    public void Cafe_constant_is_CAFE()
    {
        Assert.Equal("CAFE", KitsMenuCatalog.Categories.Cafe);
    }

    [Fact]
    public void Tra_constant_is_Tea_and_Juice()
    {
        Assert.Equal("Tea & Juice", KitsMenuCatalog.Categories.Tra);
    }

    [Fact]
    public void Banh_constant_is_Cake_and_Ice_Cream()
    {
        Assert.Equal("Cake & Ice Cream", KitsMenuCatalog.Categories.Banh);
    }

    [Fact]
    public void Combo_constant_is_COMBO()
    {
        Assert.Equal("COMBO", KitsMenuCatalog.Categories.Combo);
    }

    [Fact]
    public void Smoothie_constant_exists_with_value_Smoothie()
    {
        Assert.Equal("Smoothie", KitsMenuCatalog.Categories.Smoothie);
    }

    [Fact]
    public void Smoothie_sort_order_is_5_after_existing_four()
    {
        // SortOrder must be strictly greater than SortCombo (4) to keep
        // "Smoothie" last in the guest menu.
        Assert.Equal(5, KitsMenuCatalog.SortSmoothie);
        Assert.True(KitsMenuCatalog.SortSmoothie > KitsMenuCatalog.SortCombo);
        Assert.True(KitsMenuCatalog.SortCombo > KitsMenuCatalog.SortBanh);
        Assert.True(KitsMenuCatalog.SortBanh > KitsMenuCatalog.SortTra);
        Assert.True(KitsMenuCatalog.SortTra > KitsMenuCatalog.SortCafe);
    }

    [Fact]
    public void All_category_constants_are_unique()
    {
        var values = new[]
        {
            KitsMenuCatalog.Categories.Cafe,
            KitsMenuCatalog.Categories.Tra,
            KitsMenuCatalog.Categories.Banh,
            KitsMenuCatalog.Categories.Combo,
            KitsMenuCatalog.Categories.Smoothie
        };
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
    }
}

/// <summary>
/// Static checks on the bootstrap's legacy-healing table so renamed categories converge
/// from legacy rows (TRÀ, BÁNH, Bánh, Banh, BANH) to the canonical display values.
/// </summary>
public sealed class KitsMenuBootstrapLegacyHealingTests
{
    [Fact]
    public void Legacy_aliases_table_contains_required_legacy_names_for_Tea_and_Juice()
    {
        var t = GetLegacyCategoryDisplayAliases();
        Assert.True(t.TryGetValue("Tea & Juice", out var traAliases));
        Assert.Contains("TRÀ", traAliases!);
        Assert.Contains("Trà", traAliases!);
        Assert.Contains("Tra", traAliases!);
    }

    [Fact]
    public void Legacy_aliases_table_contains_required_legacy_names_for_Cake_and_Ice_Cream()
    {
        var t = GetLegacyCategoryDisplayAliases();
        Assert.True(t.TryGetValue("Cake & Ice Cream", out var banhAliases));
        Assert.Contains("BÁNH", banhAliases!);
        Assert.Contains("Bánh", banhAliases!);
        Assert.Contains("BANH", banhAliases!);
        Assert.Contains("Banh", banhAliases!);
    }

    private static IReadOnlyDictionary<string, string[]> GetLegacyCategoryDisplayAliases()
    {
        var asm = typeof(Annap.CoffeeQrOrdering.Web.Services.KitsMenuBootstrap).Assembly;
        var type = asm.GetType("Annap.CoffeeQrOrdering.Web.Services.KitsMenuBootstrap", throwOnError: true)!;
        var field = type.GetField(
            "LegacyCategoryDisplayAliases",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var value = (IReadOnlyDictionary<string, string[]>)field.GetValue(null)!;
        return value;
    }
}
