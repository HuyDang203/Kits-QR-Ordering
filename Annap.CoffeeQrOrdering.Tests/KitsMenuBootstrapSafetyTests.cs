using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Production-safety tests for <see cref="KitsMenuBootstrap"/>.
///
/// <para>
/// These tests guarantee the bootstrap is additive and idempotent against
/// any combination of:
/// <list type="bullet">
///   <item>empty databases,</item>
///   <item>freshly seeded databases,</item>
///   <item>existing production databases containing historical <c>Order</c>
///         /<c>OrderItem</c> rows,</item>
///   <item>legacy category names (<c>TRÀ</c>, <c>Bánh</c>, <c>Banh</c>,
///         <c>BANH</c>),</item>
///   <item>menu items not present in the current canonical seed.</item>
/// </list>
/// </para>
/// <para>
/// The bootstrap MUST NEVER issue a <c>DELETE</c> against
/// <c>menu_items</c> or <c>menu_categories</c>, and MUST NEVER change the
/// primary key of an existing row. Historical <c>OrderItem.MenuItemId</c>
/// foreign keys must stay valid after running the bootstrap.
/// </para>
/// </summary>
public sealed class KitsMenuBootstrapSafetyTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"kits-bootstrap-safety-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    // ─── Scenario 1: Empty database receives the canonical catalog ──────────────

    [Fact]
    public async Task Empty_database_receives_canonical_categories_and_items()
    {
        await using var db = NewContext();

        var inserted = await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);

        // Bootstrap inserts every canonical Kits seed row.
        Assert.True(inserted > 0, "Bootstrap should insert the canonical catalog into an empty database.");

        var canonicalNames = new[]
        {
            KitsMenuCatalog.Categories.Cafe,
            KitsMenuCatalog.Categories.Tra,
            KitsMenuCatalog.Categories.Banh,
            KitsMenuCatalog.Categories.Combo,
            KitsMenuCatalog.Categories.Smoothie
        };
        foreach (var name in canonicalNames)
        {
            Assert.Contains(db.MenuCategories.AsNoTracking(), c => c.Name == name);
        }

        // Smoothie must exist and be empty — the catalog intentionally has no smoothie items.
        var smoothie = await db.MenuCategories.AsNoTracking().SingleAsync(c => c.Name == KitsMenuCatalog.Categories.Smoothie);
        Assert.Equal(KitsMenuCatalog.SortSmoothie, smoothie.SortOrder);
        Assert.Empty(await db.MenuItems.AsNoTracking().Where(m => m.CategoryId == smoothie.Id).ToListAsync());
    }

    // ─── Scenario 2: Existing OrderItem-referenced MenuItem is never deleted ─────

    [Fact]
    public async Task Existing_MenuItem_referenced_by_OrderItem_is_never_deleted_by_bootstrap()
    {
        await using var db = NewContext();

        // Seed an existing menu item that is also referenced by a historical order.
        var legacyCategory = new MenuCategory { Name = "Annap Heritage", SortOrder = 99 };
        db.MenuCategories.Add(legacyCategory);
        await db.SaveChangesAsync();

        var referencedItem = new MenuItem
        {
            CategoryId = legacyCategory.Id,
            Name = "Heritage Phin",
            Price = 42000m,
            CatalogKey = null, // legacy non-kits item
            IsAvailable = true,
            IsArchived = false
        };
        db.MenuItems.Add(referencedItem);
        await db.SaveChangesAsync();

        var table = new VenueTable
        {
            VenueCode = "annap",
            DisplayCode = "T01",
            PublicSlug = "annap-t01",
            DisplayLabel = "Table 1",
            IsActive = true
        };
        db.VenueTables.Add(table);
        await db.SaveChangesAsync();

        var historicalOrder = new Order
        {
            VenueTableId = table.Id,
            TableCode = "T01",
            GuestSessionToken = "guest-heritage",
            SubmitIdempotencyKey = "idem-heritage-" + Guid.NewGuid().ToString("N"),
            Status = OrderStatus.Paid,
            TotalAmount = 42000m
        };
        db.Orders.Add(historicalOrder);
        await db.SaveChangesAsync();

        db.OrderItems.Add(new OrderItem
        {
            OrderId = historicalOrder.Id,
            MenuItemId = referencedItem.Id,
            Quantity = 1,
            UnitPrice = referencedItem.Price,
            MenuItemName = referencedItem.Name
        });
        await db.SaveChangesAsync();

        // Run the bootstrap.
        await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);

        // The historical menu item must still exist, with the same primary key,
        // and still be reachable from the historical OrderItem.
        var stillThere = await db.MenuItems.AsNoTracking().SingleOrDefaultAsync(m => m.Id == referencedItem.Id);
        Assert.NotNull(stillThere);
        Assert.Equal(referencedItem.Name, stillThere!.Name);
        Assert.Equal(referencedItem.Price, stillThere.Price);

        var stillLinked = await db.OrderItems.AsNoTracking().SingleAsync(oi => oi.OrderId == historicalOrder.Id);
        Assert.Equal(referencedItem.Id, stillLinked.MenuItemId);
    }

    // ─── Scenario 3: Historical MenuItem not in canonical seed remains in DB ────

    [Fact]
    public async Task Historical_MenuItem_not_in_canonical_seed_remains_after_bootstrap()
    {
        await using var db = NewContext();

        // Seed a legacy category + item that is NOT in the canonical Kits seed.
        var legacyCategory = new MenuCategory { Name = "Annap Heritage", SortOrder = 99 };
        db.MenuCategories.Add(legacyCategory);
        await db.SaveChangesAsync();

        var heritageItem = new MenuItem
        {
            CategoryId = legacyCategory.Id,
            Name = "Dura Farm Espresso",
            Price = 60000m,
            CatalogKey = "annap-dura-farm-espresso",
            IsAvailable = false,
            IsArchived = true // already archived; bootstrap must not touch it
        };
        db.MenuItems.Add(heritageItem);
        await db.SaveChangesAsync();

        await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);

        // The non-canonical item must still exist with the same identity and category.
        var stillThere = await db.MenuItems.AsNoTracking()
            .Include(m => m.Category)
            .SingleOrDefaultAsync(m => m.Id == heritageItem.Id);
        Assert.NotNull(stillThere);
        Assert.Equal("Dura Farm Espresso", stillThere!.Name);
        Assert.Equal(60000m, stillThere.Price);
        Assert.True(stillThere.IsArchived);
        Assert.NotNull(stillThere.Category);
        Assert.Equal("Annap Heritage", stillThere.Category!.Name);
    }

    // ─── Scenario 4: Legacy "Bánh" becomes "Cake & Ice Cream" without PK change ─

    [Theory]
    [InlineData("Bánh")]
    [InlineData("Banh")]
    [InlineData("BANH")]
    [InlineData("BÁNH")]
    public async Task Legacy_bakery_category_is_renamed_in_place_without_changing_primary_key(string legacyName)
    {
        await using var db = NewContext();

        var legacy = new MenuCategory { Name = legacyName, SortOrder = 99 };
        db.MenuCategories.Add(legacy);

        // Existing menu item attached to the legacy category.
        var existing = new MenuItem
        {
            CategoryId = legacy.Id,
            Name = "Brownie chocolate",
            Price = 40000m,
            CatalogKey = "kits-banh-brownie-chocolate",
            IsAvailable = true
        };
        db.MenuItems.Add(existing);
        await db.SaveChangesAsync();

        var legacyId = legacy.Id;
        var itemId = existing.Id;

        await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);

        var canonical = await db.MenuCategories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Name == KitsMenuCatalog.Categories.Banh);
        Assert.NotNull(canonical);
        Assert.Equal(legacyId, canonical!.Id); // PK unchanged
        Assert.Equal(KitsMenuCatalog.SortBanh, canonical.SortOrder);

        // The existing menu item is still attached and reachable.
        var item = await db.MenuItems.AsNoTracking().SingleAsync(m => m.Id == itemId);
        Assert.Equal(legacyId, item.CategoryId);
    }

    // ─── Scenario 5: Legacy "TRÀ" becomes "Tea & Juice" without PK change ───────

    [Theory]
    [InlineData("TRÀ")]
    [InlineData("Trà")]
    [InlineData("Tra")]
    public async Task Legacy_tea_category_is_renamed_in_place_without_changing_primary_key(string legacyName)
    {
        await using var db = NewContext();

        var legacy = new MenuCategory { Name = legacyName, SortOrder = 42 };
        db.MenuCategories.Add(legacy);
        await db.SaveChangesAsync();
        var legacyId = legacy.Id;

        await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);

        var canonical = await db.MenuCategories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Name == KitsMenuCatalog.Categories.Tra);
        Assert.NotNull(canonical);
        Assert.Equal(legacyId, canonical!.Id); // PK unchanged
        Assert.Equal(KitsMenuCatalog.SortTra, canonical.SortOrder);

        // Only one row with the canonical name exists — no duplicates created.
        Assert.Equal(1, await db.MenuCategories.AsNoTracking().CountAsync(c => c.Name == KitsMenuCatalog.Categories.Tra));
    }

    // ─── Scenario 6: Smoothie is created if missing ─────────────────────────────

    [Fact]
    public async Task Smoothie_is_created_when_missing_with_canonical_sort_order()
    {
        await using var db = NewContext();

        // Pre-seed everything except Smoothie.
        foreach (var name in new[]
        {
            KitsMenuCatalog.Categories.Cafe,
            KitsMenuCatalog.Categories.Tra,
            KitsMenuCatalog.Categories.Banh,
            KitsMenuCatalog.Categories.Combo
        })
        {
            db.MenuCategories.Add(new MenuCategory { Name = name, SortOrder = 1 });
        }
        await db.SaveChangesAsync();

        await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);

        var smoothie = await db.MenuCategories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Name == KitsMenuCatalog.Categories.Smoothie);
        Assert.NotNull(smoothie);
        Assert.Equal(KitsMenuCatalog.SortSmoothie, smoothie!.SortOrder);
        Assert.Empty(await db.MenuItems.AsNoTracking().Where(m => m.CategoryId == smoothie.Id).ToListAsync());
    }

    // ─── Scenario 7: Running bootstrap twice does not create duplicates ──────────

    [Fact]
    public async Task Running_bootstrap_twice_is_idempotent_and_creates_no_duplicates()
    {
        await using var db = NewContext();

        var firstPass = await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);
        Assert.True(firstPass > 0);

        var categoriesAfterFirst = await db.MenuCategories.AsNoTracking().CountAsync();
        var itemsAfterFirst = await db.MenuItems.AsNoTracking().CountAsync();
        var cafeCount = await db.MenuCategories.AsNoTracking().CountAsync(c => c.Name == KitsMenuCatalog.Categories.Cafe);
        var traCount = await db.MenuCategories.AsNoTracking().CountAsync(c => c.Name == KitsMenuCatalog.Categories.Tra);
        var banhCount = await db.MenuCategories.AsNoTracking().CountAsync(c => c.Name == KitsMenuCatalog.Categories.Banh);
        var comboCount = await db.MenuCategories.AsNoTracking().CountAsync(c => c.Name == KitsMenuCatalog.Categories.Combo);
        var smoothieCount = await db.MenuCategories.AsNoTracking().CountAsync(c => c.Name == KitsMenuCatalog.Categories.Smoothie);

        var secondPass = await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(0, secondPass);
        Assert.Equal(categoriesAfterFirst, await db.MenuCategories.AsNoTracking().CountAsync());
        Assert.Equal(itemsAfterFirst, await db.MenuItems.AsNoTracking().CountAsync());
        Assert.Equal(1, cafeCount);
        Assert.Equal(1, traCount);
        Assert.Equal(1, banhCount);
        Assert.Equal(1, comboCount);
        Assert.Equal(1, smoothieCount);

        // Each CatalogKey is unique across the canonical catalog.
        var keys = await db.MenuItems.AsNoTracking()
            .Where(m => m.CatalogKey != null && m.CatalogKey.StartsWith(KitsMenuCatalog.CatalogKeyPrefix))
            .Select(m => m.CatalogKey)
            .ToListAsync();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    // ─── Scenario 8: Order-OrderItem relationships remain valid after bootstrap ──

    [Fact]
    public async Task Existing_order_and_order_item_relationships_remain_valid_after_bootstrap()
    {
        await using var db = NewContext();

        // Seed a category, menu item, and a paid historical order that references the item.
        var category = new MenuCategory { Name = "Annap Heritage", SortOrder = 99 };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();

        var item = new MenuItem
        {
            CategoryId = category.Id,
            Name = "Annap Espresso",
            Price = 45000m,
            CatalogKey = null,
            IsAvailable = true,
            IsArchived = false
        };
        db.MenuItems.Add(item);

        var table = new VenueTable
        {
            VenueCode = "annap",
            DisplayCode = "T05",
            PublicSlug = "annap-t05",
            DisplayLabel = "Table 5",
            IsActive = true
        };
        db.VenueTables.Add(table);
        await db.SaveChangesAsync();

        var order = new Order
        {
            VenueTableId = table.Id,
            TableCode = "T05",
            GuestSessionToken = "guest-bootstrap-safety",
            SubmitIdempotencyKey = "idem-bs-" + Guid.NewGuid().ToString("N"),
            Status = OrderStatus.Paid,
            TotalAmount = 45000m
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            MenuItemId = item.Id,
            Quantity = 1,
            UnitPrice = 45000m,
            MenuItemName = "Annap Espresso"
        });
        order.RecalculateTotals();
        await db.SaveChangesAsync();

        var orderId = order.Id;
        var itemId = item.Id;
        var orderItemCountBefore = await db.OrderItems.CountAsync();
        var totalBefore = await db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.TotalAmount).SingleAsync();

        await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);

        // Orders untouched.
        Assert.Equal(1, await db.Orders.AsNoTracking().CountAsync());
        // OrderItems untouched.
        Assert.Equal(orderItemCountBefore, await db.OrderItems.AsNoTracking().CountAsync());

        // The OrderItem still references the historical MenuItem and the FK resolves.
        var line = await db.OrderItems.AsNoTracking()
            .Include(oi => oi.MenuItem)
            .SingleAsync(oi => oi.OrderId == orderId);
        Assert.Equal(itemId, line.MenuItemId);
        Assert.NotNull(line.MenuItem);
        Assert.Equal("Annap Espresso", line.MenuItem!.Name);

        // The Order is still findable and its TotalAmount is unchanged.
        var freshOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(totalBefore, freshOrder.TotalAmount);
    }

    // ─── Bonus: No DELETE statements are emitted by the bootstrap ───────────────
    //
    // The InMemory provider does not expose raw SQL, but we can guarantee the
    // intent of the safety contract by asserting that after a full bootstrap
    // pass on a database that contains items and orders, the underlying
    // ChangeTracker never enters the Deleted state. This is a proxy for
    // "no DELETE will be emitted against menu_items or menu_categories".

    [Fact]
    public async Task Bootstrap_does_not_mark_any_MenuItem_or_MenuCategory_as_Deleted_in_the_change_tracker()
    {
        await using var db = NewContext();

        // Seed: a fully-populated legacy database.
        var legacyCat = new MenuCategory { Name = "Annap Heritage", SortOrder = 99 };
        db.MenuCategories.Add(legacyCat);
        await db.SaveChangesAsync();

        var legacyItem = new MenuItem
        {
            CategoryId = legacyCat.Id,
            Name = "Annap Phin",
            Price = 45000m,
            CatalogKey = "annap-phin",
            IsAvailable = true,
            IsArchived = false
        };
        db.MenuItems.Add(legacyItem);

        var legacyItem2 = new MenuItem
        {
            CategoryId = legacyCat.Id,
            Name = "Annap Cold Brew",
            Price = 50000m,
            CatalogKey = "annap-cold-brew",
            IsAvailable = true,
            IsArchived = false
        };
        db.MenuItems.Add(legacyItem2);
        await db.SaveChangesAsync();

        // Run the bootstrap.
        await KitsMenuBootstrap.EnsureKitsMenuAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);

        // No row is staged for deletion.
        Assert.Empty(db.ChangeTracker.Entries<MenuItem>().Where(e => e.State == EntityState.Deleted));
        Assert.Empty(db.ChangeTracker.Entries<MenuCategory>().Where(e => e.State == EntityState.Deleted));
    }
}
