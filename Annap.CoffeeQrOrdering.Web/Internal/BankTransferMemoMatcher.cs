using System.Text.RegularExpressions;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal static partial class BankTransferMemoMatcher
{
    private const string LegacyBrandPrefix = "ANNAP";
    private const string NewBrandPrefix    = "KITS";

    public static string NormalizeMemo(string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo))
            return "";

        var collapsed = WhitespaceCollapse().Replace(memo.Trim(), " ");
        return collapsed.ToUpperInvariant();
    }

    public static bool MemoMatches(string? incomingMemo, string expectedMemo)
    {
        var incoming = NormalizeMemo(incomingMemo);
        var expected = NormalizeMemo(expectedMemo);
        if (string.IsNullOrEmpty(expected))
            return false;

        // Tier 1 — exact equality
        if (incoming == expected)
            return true;

        // Tier 2 — incoming contains expected (e.g. "KITS ABC123 EXTRA")
        if (incoming.Contains(expected, StringComparison.Ordinal))
            return true;

        // Tier 3 — legacy ANNAP ↔ KITS compatibility
        // Handles the case where a customer pays with an old ANNAP memo
        // against an order that now generates KITS memos (or vice versa).
        if (LegacyCompatibilityMatch(incoming, expected))
            return true;

        return false;
    }

    private static bool LegacyCompatibilityMatch(string incoming, string expected)
    {
        // Only attempt legacy swap when one side has ANNAP and the other has KITS
        bool incomingHasLegacy = incoming.StartsWith(LegacyBrandPrefix, StringComparison.Ordinal);
        bool expectedHasLegacy = expected.StartsWith(LegacyBrandPrefix, StringComparison.Ordinal);
        bool incomingHasNew = incoming.StartsWith(NewBrandPrefix, StringComparison.Ordinal);
        bool expectedHasNew = expected.StartsWith(NewBrandPrefix, StringComparison.Ordinal);

        bool oneLegacyOneNew =
            (incomingHasLegacy && expectedHasNew) ||
            (incomingHasNew && expectedHasLegacy);

        if (!oneLegacyOneNew)
            return false;

        // Swap the brand prefix on both sides so they align
        var swappedIncoming = SwapLegacyBrandPrefix(incoming);
        var swappedExpected = SwapLegacyBrandPrefix(expected);

        // Re-check exact and substring on the swapped pair
        if (swappedIncoming == swappedExpected)
            return true;

        return swappedIncoming.Contains(swappedExpected, StringComparison.Ordinal);
    }

    private static string SwapLegacyBrandPrefix(string memo)
    {
        if (memo.StartsWith(LegacyBrandPrefix, StringComparison.Ordinal))
            return NewBrandPrefix + memo[LegacyBrandPrefix.Length..];
        if (memo.StartsWith(NewBrandPrefix, StringComparison.Ordinal))
            return LegacyBrandPrefix + memo[NewBrandPrefix.Length..];
        return memo;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceCollapse();
}
