using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Projections;
using Annap.CoffeeQrOrdering.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// InMemory tests for the bilingual menu-item persistence layer.
///
/// These cover the EF mapping and the form-projection round trip without
/// requiring the Postgres Docker fixture. End-to-end persistence (including
/// the actual migration column) is exercised by
/// <c>OrderIntegritySnapshotTests</c> which uses
/// <c>AnnapPostgresWebApplicationFactory</c>.
/// </summary>
public sealed class MenuItemNameEnPersistenceTests
{
    /// <summary>
    /// Per-test shared InMemory database. Two <see cref="AppDbContext"/>s
    /// created from the same <see cref="DbContextOptions"/> share state —
    /// which is what we need to verify a save survives a fresh DbContext
    /// (i.e., a subsequent HTTP request).
    /// </summary>
    private sealed class InMemoryScope : IDisposable
    {
        public DbContextOptions<AppDbContext> Options { get; }
        public InMemoryScope(string suffix)
        {
            Options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"menu-name-en-{suffix}")
                .Options;
        }
        public AppDbContext NewContext() => new(Options);
        public void Dispose() { /* EF InMemory cleans itself up via name */ }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EF mapping — column metadata
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Configuration_NameEn_is_optional_string_with_200_max_length()
    {
        using var scope = new InMemoryScope("config");
        using var db = scope.NewContext();
        var entity = db.Model.FindEntityType(typeof(MenuItem))!;
        var prop = entity.FindProperty(nameof(MenuItem.NameEn))
            ?? throw new InvalidOperationException("MenuItem.NameEn is not mapped.");
        Assert.True(prop.IsNullable);
        Assert.Equal(200, prop.GetMaxLength());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Create / edit / reload — both names
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_with_both_names_persists_both_via_vi_and_en()
    {
        using var scope = new InMemoryScope("create-both");
        await SeedCategoryAsync(scope);

        await using (var db = scope.NewContext())
        {
            db.MenuItems.Add(new MenuItem
            {
                CategoryId = db.MenuCategories.AsNoTracking().Single().Id,
                Name = "Cà phê sữa đá",
                NameEn = "Vietnamese Iced Coffee",
                Price = 45000m
            });
            await db.SaveChangesAsync();
        }

        await using var verify = scope.NewContext();
        var loaded = await verify.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("Cà phê sữa đá", loaded.Name);
        Assert.Equal("Vietnamese Iced Coffee", loaded.NameEn);
    }

    [Fact]
    public async Task Edit_nameen_updates_only_nameen_and_keeps_name_intact()
    {
        using var scope = new InMemoryScope("edit-nameen");
        await SeedCategoryAsync(scope);

        Guid id;
        await using (var db = scope.NewContext())
        {
            var item = new MenuItem
            {
                CategoryId = db.MenuCategories.AsNoTracking().Single().Id,
                Name = "Bạc xỉu",
                NameEn = "Bac Xiu (old)",
                Price = 49000m
            };
            db.MenuItems.Add(item);
            await db.SaveChangesAsync();
            id = item.Id;
        }

        await using (var s1 = scope.NewContext())
        {
            var existing = await s1.MenuItems.SingleAsync();
            existing.NameEn = "Bac Xiu (new)";
            await s1.SaveChangesAsync();
        }

        await using var s2 = scope.NewContext();
        var reloaded = await s2.MenuItems.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("Bạc xỉu", reloaded.Name);
        Assert.Equal("Bac Xiu (new)", reloaded.NameEn);
    }

    [Fact]
    public async Task Save_without_nameen_keeps_name_intact_and_nameen_null()
    {
        using var scope = new InMemoryScope("no-nameen");
        await SeedCategoryAsync(scope);

        await using (var db = scope.NewContext())
        {
            db.MenuItems.Add(new MenuItem
            {
                CategoryId = db.MenuCategories.AsNoTracking().Single().Id,
                Name = "Trà đào",
                // NameEn intentionally omitted — must be optional for backwards compat.
                Price = 49000m
            });
            await db.SaveChangesAsync();
        }

        await using var verify = scope.NewContext();
        var loaded = await verify.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("Trà đào", loaded.Name);
        Assert.Null(loaded.NameEn);
    }

    [Fact]
    public async Task Existing_legacy_items_without_nameen_remain_readable()
    {
        // Seed an item that mimics a pre-migration row (NameEn = null).
        using (var scope = new InMemoryScope("legacy"))
        {
            await SeedCategoryAsync(scope, "Legacy Cat");
            await using var db = scope.NewContext();
            db.MenuItems.Add(new MenuItem
            {
                CategoryId = db.MenuCategories.AsNoTracking().Single().Id,
                Name = "Phin Đen",
                NameEn = null, // legacy invariant
                Price = 39000m
            });
            await db.SaveChangesAsync();
        }

        using var verify = new InMemoryScope("legacy"); // shared name -> sees prior seed
        await using var ctx = verify.NewContext();
        var items = await ctx.MenuItems.AsNoTracking().ToListAsync();
        var legacy = items.Single(i => i.Name == "Phin Đen");
        Assert.Null(legacy.NameEn);
        Assert.Equal(39000m, legacy.Price);
    }

    [Fact]
    public async Task Clearing_nameen_back_to_null_persists()
    {
        using var scope = new InMemoryScope("clear-nameen");
        await SeedCategoryAsync(scope);

        Guid id;
        await using (var db = scope.NewContext())
        {
            var item = new MenuItem
            {
                CategoryId = db.MenuCategories.AsNoTracking().Single().Id,
                Name = "Cà phê muối",
                NameEn = "Salt Coffee",
                Price = 55000m
            };
            db.MenuItems.Add(item);
            await db.SaveChangesAsync();
            id = item.Id;
        }

        await using (var s1 = scope.NewContext())
        {
            var existing = await s1.MenuItems.SingleAsync();
            existing.NameEn = null;
            await s1.SaveChangesAsync();
        }

        await using var s2 = scope.NewContext();
        var reloaded = await s2.MenuItems.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("Cà phê muối", reloaded.Name);
        Assert.Null(reloaded.NameEn);
    }

    [Fact]
    public async Task Setting_nameen_to_whitespace_persists_as_whitespace()
    {
        // We do not collapse whitespace-only values at the entity layer — the
        // helper does that for display. The entity must faithfully store
        // whatever the form passed, including "   ".
        using var scope = new InMemoryScope("ws-nameen");
        await SeedCategoryAsync(scope);

        await using (var db = scope.NewContext())
        {
            db.MenuItems.Add(new MenuItem
            {
                CategoryId = db.MenuCategories.AsNoTracking().Single().Id,
                Name = "Espresso",
                NameEn = "   ",
                Price = 35000m
            });
            await db.SaveChangesAsync();
        }

        await using var verify = scope.NewContext();
        var reloaded = await verify.MenuItems.AsNoTracking().SingleAsync();
        Assert.Equal("Espresso", reloaded.Name);
        Assert.Equal("   ", reloaded.NameEn);
        // But the localizer falls back to Name when NameEn is whitespace.
        Assert.Equal("Espresso", Annap.CoffeeQrOrdering.Web.Internal.MenuItemNameLocalizer.For(reloaded, "en"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MenuCurationFormModel — Normalize() and round-trip via projections
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Form_normalize_trims_nameen_and_converts_whitespace_to_null()
    {
        var form = new MenuCurationFormModel
        {
            Name = "  Espresso  ",
            NameEn = "   Espresso   ",
            Price = 35000m
        };
        form.Normalize();
        Assert.Equal("Espresso", form.Name);
        Assert.Equal("Espresso", form.NameEn);
    }

    [Fact]
    public void Form_normalize_converts_blank_nameen_to_null()
    {
        var form = new MenuCurationFormModel
        {
            Name = "Espresso",
            NameEn = "   \t\n  ",
            Price = 35000m
        };
        form.Normalize();
        Assert.Null(form.NameEn);
    }

    [Fact]
    public void Form_normalize_leaves_null_nameen_null()
    {
        var form = new MenuCurationFormModel
        {
            Name = "Espresso",
            NameEn = null,
            Price = 35000m
        };
        form.Normalize();
        Assert.Null(form.NameEn);
    }

    [Fact]
    public void ApplyFormToEntity_writes_nameen_verbatim_when_trimmed()
    {
        var form = new MenuCurationFormModel
        {
            Name = "Espresso",
            NameEn = "Espresso",
            CategoryId = Guid.NewGuid(),
            Price = 35000m
        };
        var entity = new MenuItem();
        MenuItemProjections.ApplyFormToEntity(form, entity);
        Assert.Equal("Espresso", entity.Name);
        Assert.Equal("Espresso", entity.NameEn);
    }

    [Fact]
    public void ApplyFormToEntity_writes_null_nameen_when_blank()
    {
        var form = new MenuCurationFormModel
        {
            Name = "Espresso",
            NameEn = "  ",
            CategoryId = Guid.NewGuid(),
            Price = 35000m
        };
        var entity = new MenuItem { Name = "old", NameEn = "old-en" };
        MenuItemProjections.ApplyFormToEntity(form, entity);
        Assert.Equal("Espresso", entity.Name);
        Assert.Null(entity.NameEn);
    }

    [Fact]
    public void ToAdminForm_round_trips_nameen_from_entity()
    {
        var entity = new MenuItem
        {
            Name = "Espresso",
            NameEn = "Espresso",
            CategoryId = Guid.NewGuid(),
            Price = 35000m
        };
        var form = MenuItemProjections.ToAdminForm(entity);
        Assert.Equal("Espresso", form.Name);
        Assert.Equal("Espresso", form.NameEn);
    }

    [Fact]
    public void ToAdminForm_round_trips_null_nameen_from_legacy_entity()
    {
        var entity = new MenuItem
        {
            Name = "Espresso",
            NameEn = null, // legacy
            CategoryId = Guid.NewGuid(),
            Price = 35000m
        };
        var form = MenuItemProjections.ToAdminForm(entity);
        Assert.Equal("Espresso", form.Name);
        Assert.Null(form.NameEn);
    }

    [Fact]
    public void ToEditorialDetail_includes_nameen()
    {
        var cat = new MenuCategory { Name = "Coffee" };
        var item = new MenuItem
        {
            Name = "Cà phê sữa đá",
            NameEn = "Vietnamese Iced Coffee",
            Category = cat,
            CategoryId = cat.Id,
            Price = 45000m
        };
        var vm = MenuItemProjections.ToEditorialDetail(
            item, related: Array.Empty<RelatedDrinkVm>(), effectivePrice: 45000m, serviceNote: null);
        Assert.Equal("Cà phê sữa đá", vm.Name);
        Assert.Equal("Vietnamese Iced Coffee", vm.NameEn);
    }

    [Fact]
    public void ToEditorialDetail_includes_null_nameen_for_legacy_items()
    {
        var cat = new MenuCategory { Name = "Coffee" };
        var item = new MenuItem
        {
            Name = "Phin Đen",
            NameEn = null,
            Category = cat,
            CategoryId = cat.Id,
            Price = 39000m
        };
        var vm = MenuItemProjections.ToEditorialDetail(
            item, related: Array.Empty<RelatedDrinkVm>(), effectivePrice: 39000m, serviceNote: null);
        Assert.Null(vm.NameEn);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // End-to-end projection: persistence -> view-model -> localized name
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task End_to_end_bilingual_name_round_trip()
    {
        using var scope = new InMemoryScope("end-to-end");
        await SeedCategoryAsync(scope);

        await using (var db = scope.NewContext())
        {
            db.MenuItems.Add(new MenuItem
            {
                CategoryId = db.MenuCategories.AsNoTracking().Single().Id,
                Name = "Cà phê sữa đá",
                NameEn = "Vietnamese Iced Coffee",
                Price = 45000m
            });
            await db.SaveChangesAsync();
        }

        await using var verify = scope.NewContext();
        var loaded = await verify.MenuItems
            .Include(i => i.Category)
            .AsNoTracking()
            .SingleAsync();

        var vm = MenuItemProjections.ToEditorialDetail(
            loaded, related: Array.Empty<RelatedDrinkVm>(), effectivePrice: 45000m, serviceNote: null);

        Assert.Equal(
            "Cà phê sữa đá",
            Annap.CoffeeQrOrdering.Web.Internal.MenuItemNameLocalizer.For(loaded, "vi"));
        Assert.Equal(
            "Vietnamese Iced Coffee",
            Annap.CoffeeQrOrdering.Web.Internal.MenuItemNameLocalizer.For(loaded, "en"));
        Assert.Equal(
            "Cà phê sữa đá",
            Annap.CoffeeQrOrdering.Web.Internal.MenuItemNameLocalizer.For(loaded, "fr"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Form-model validation — NameEn is optional (no [Required])
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Form_model_nameen_is_optional()
    {
        var form = new MenuCurationFormModel
        {
            Name = "Espresso",
            CategoryId = Guid.NewGuid(),
            Price = 35000m
        };
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(form);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(form, ctx, results, validateAllProperties: true));
        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(MenuCurationFormModel.NameEn)));
    }

    [Fact]
    public void Form_model_nameen_max_length_is_200()
    {
        var form = new MenuCurationFormModel
        {
            Name = "Espresso",
            NameEn = new string('A', 201),
            CategoryId = Guid.NewGuid(),
            Price = 35000m
        };
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(form);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(form, ctx, results, validateAllProperties: true);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MenuCurationFormModel.NameEn)));
    }

    [Fact]
    public void Form_model_nameen_200_chars_passes_validation()
    {
        var form = new MenuCurationFormModel
        {
            Name = "Espresso",
            NameEn = new string('A', 200),
            CategoryId = Guid.NewGuid(),
            Price = 35000m
        };
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(form);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(form, ctx, results, validateAllProperties: true));
        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(MenuCurationFormModel.NameEn)));
    }

    private static async Task SeedCategoryAsync(InMemoryScope scope, string name = "Test Cat")
    {
        await using var db = scope.NewContext();
        db.MenuCategories.Add(new MenuCategory { Name = name, SortOrder = 1 });
        await db.SaveChangesAsync();
    }
}