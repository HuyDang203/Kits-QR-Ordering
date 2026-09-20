namespace Annap.CoffeeQrOrdering.Application;

/// <summary>
/// Stable canonical catalog of KitsQR menu items.
/// Catalog keys use the prefix <c>kits-</c> and the Vietnamese-safe slug of the item name.
/// Idempotent seed: a row is inserted only when its <see cref="Annap.CoffeeQrOrdering.Domain.Entities.MenuItem.CatalogKey"/>
/// is not already present.
/// </summary>
public static class KitsMenuCatalog
{
    public static class Categories
    {
        public const string Cafe = "CAFE";
        public const string Tra = "Tea & Juice";
        public const string Banh = "Cake & Ice Cream";
        public const string Combo = "COMBO";
        public const string Smoothie = "Smoothie";
    }

    public static class ItemTypes
    {
        public const string Hot = "hot";
        public const string Iced = "iced";
        public const string Drink = "drink";
        public const string Food = "food";
        public const string Combo = "combo";
    }

    /// <summary>Category sort order (lower sorts first in the guest menu).</summary>
    public const int SortCafe = 1;
    public const int SortTra = 2;
    public const int SortBanh = 3;
    public const int SortCombo = 4;
    public const int SortSmoothie = 5;

    public const string CatalogKeyPrefix = "kits-";
}
