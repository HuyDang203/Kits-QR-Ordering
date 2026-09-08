using System.Globalization;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 7 — Kits category normalization.
///
/// Normalizes any legacy bakery category variants (<c>Bánh</c>, <c>Banh</c>,
/// <c>BÁNH</c>, <c>BANH</c>) into exactly one canonical row named <c>BÁNH</c>
/// with <c>SortOrder = 3</c>.
///
/// Guarantees:
/// <list type="bullet">
///   <item>No <c>MenuItem</c> is deleted. All items attached to legacy bakery
///         categories are reassigned to the canonical row.</item>
///   <item>No <c>Ingredient</c> or <c>MenuItemIngredient</c> is touched.</item>
///   <item>No order data is touched.</item>
///   <item>No unrelated category is touched.</item>
///   <item>The migration is idempotent in effect — re-running produces no further
///         change once the canonical row exists.</item>
/// </list>
///
/// Implementation notes:
/// <list type="bullet">
///   <item>PostgreSQL default collation is binary (<c>C</c>), which is
///         case-sensitive AND accent-sensitive. Each variant must be matched by
///         its exact UTF-8 bytes — they cannot be folded in SQL.</item>
///   <item>If multiple variant rows exist, the lowest <c>Id</c> is preserved as
///         the canonical row (deterministic). All other variant rows are deleted
///         after their MenuItems have been reassigned.</item>
///   <item>If no variant row exists, a canonical row is inserted.</item>
/// </list>
/// </summary>
/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260825120000_MergeBakeryCategoryVariants")]
public partial class MergeBakeryCategoryVariants : Migration
{
    /// <summary>The canonical Kits bakery category name.</summary>
    private const string CanonicalBakeryName = "BÁNH";

    /// <summary>The canonical Kits bakery category sort order.</summary>
    private const int CanonicalBakerySortOrder = 3;

    /// <summary>Legacy variant names — each must match byte-exact against PostgreSQL.</summary>
    private static readonly string[] LegacyBakeryVariants =
    [
        "Bánh",
        "Banh",
        "BÁNH",
        "BANH"
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Comma-separated quoted list for IN(...) clause. Each variant is byte-exact.
        var variantsLiteral = string.Join(",", LegacyBakeryVariants.Select(v => "'" + v.Replace("'", "''") + "'"));
        var sortOrderLiteral = CanonicalBakerySortOrder.ToString(CultureInfo.InvariantCulture);
        var canonicalLiteral = CanonicalBakeryName.Replace("'", "''");

        // PostgreSQL identifier quoting: the table name is lowercase
        // ("menu_categories") but the column identifiers are PascalCase
        // ("Id", "Name", "SortOrder", "CreatedAtUtc", "UpdatedAtUtc"). EF Core
        // generated the schema with quoted identifiers, so PostgreSQL requires
        // every PascalCase column reference to be double-quoted. Unquoted
        // references fold to lowercase and resolve as 42703 (column not found).
        //
        // NOTE: MIN("Id") is used inside a DO $$ PL/pgSQL block. PostgreSQL's
        // aggregate resolution in procedural-language contexts does not reliably
        // find MIN for the uuid type (error 42883 "function min(uuid) does not
        // exist"). The standard workaround is to use ORDER BY + LIMIT 1 instead
        // of the aggregate function.
        var sql =
            "DO $$\n" +
            "DECLARE\n" +
            "    canonical_id uuid;\n" +
            "BEGIN\n" +
            "    -- 1. Pick the lowest-Id variant row as canonical (deterministic).\n" +
            "    --    If none exist, insert one and use it.\n" +
            "    BEGIN\n" +
            "        SELECT \"Id\" INTO canonical_id\n" +
            "        FROM menu_categories\n" +
            "        WHERE \"Name\" IN (" + variantsLiteral + ")\n" +
            "        ORDER BY \"Id\"\n" +
            "        LIMIT 1;\n" +
            "    EXCEPTION WHEN OTHERS THEN\n" +
            "        canonical_id := NULL;\n" +
            "    END;\n" +
            "\n" +
            "    IF canonical_id IS NULL THEN\n" +
            "        INSERT INTO menu_categories (\"Id\", \"Name\", \"SortOrder\", \"CreatedAtUtc\")\n" +
            "        VALUES (gen_random_uuid(), '" + canonicalLiteral + "', " + sortOrderLiteral + ", NOW());\n" +
            "        SELECT \"Id\" INTO canonical_id\n" +
            "        FROM menu_categories\n" +
            "        WHERE \"Name\" = '" + canonicalLiteral + "'\n" +
            "        ORDER BY \"Id\"\n" +
            "        LIMIT 1;\n" +
            "    ELSE\n" +
            "        -- 2a. Reassign all MenuItems from other variant rows to the canonical row.\n" +
            "        UPDATE menu_items mi\n" +
            "        SET \"CategoryId\" = canonical_id,\n" +
            "            \"UpdatedAtUtc\" = NOW()\n" +
            "        FROM menu_categories mc\n" +
            "        WHERE mi.\"CategoryId\" = mc.\"Id\"\n" +
            "          AND mc.\"Id\" <> canonical_id\n" +
            "          AND mc.\"Name\" IN (" + variantsLiteral + ");\n" +
            "\n" +
            "        -- 2b. Delete the duplicate legacy variant rows (FK reassignment already happened above).\n" +
            "        DELETE FROM menu_categories\n" +
            "        WHERE \"Id\" <> canonical_id\n" +
            "          AND \"Name\" IN (" + variantsLiteral + ");\n" +
            "\n" +
            "        -- 2c. Normalize the canonical row's Name and SortOrder.\n" +
            "        UPDATE menu_categories\n" +
            "        SET \"Name\" = '" + canonicalLiteral + "',\n" +
            "            \"SortOrder\" = " + sortOrderLiteral + ",\n" +
            "            \"UpdatedAtUtc\" = NOW()\n" +
            "        WHERE \"Id\" = canonical_id;\n" +
            "    END IF;\n" +
            "END\n" +
            "$$;";

        migrationBuilder.Sql(sql);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Phase 7 downgrade is intentionally a no-op:
        //   - We do not know the original spelling(s) of legacy bakery rows
        //     that were merged, so we cannot recreate them faithfully.
        //   - The canonical BÁNH row is intentionally retained so the
        //     application remains operational.
        // No SQL is issued.
    }
}