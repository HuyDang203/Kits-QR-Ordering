using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Regression checks for the "grouped A+B but A and B are still rendered as
/// separate cards" rendering bug in <c>staff-orders-board.js</c>.
///
/// The grouping layer is a presentation-layer concept: the backend
/// (<c>/api/staff/order-groups</c>) returns both the aggregate group and the
/// underlying Orders in their normal columns. The client MUST render the
/// group exactly once and hide every member OrderId from the standalone
/// card path.
///
/// We exercise the actual rendering with a Node.js virtual-machine boot of
/// <c>staff-orders-board.js</c> against a tiny DOM stub, then assert the
/// per-column card counts. We also do static-analysis checks against the
/// source to lock the structure so a future refactor cannot silently
/// regress the fix.
/// </summary>
public sealed class StaffOrderGroupingRenderTests
{
    private static string TestsDir => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

    private static string HarnessPath => Path.Combine(TestsDir, "staff-board-render-harness.cjs");

    private static string StaffBoardJsPath => Path.Combine(
        TestsDir, "..", "Annap.CoffeeQrOrdering.Web", "wwwroot", "js", "staff-orders-board.js");

    /// <summary>
    /// Extracts the body of the function named <paramref name="functionName"/>
    /// by counting balanced braces from the first '{' after the signature.
    /// </summary>
    private static string ExtractFunctionBody(string js, string functionName)
    {
        var sigIdx = js.IndexOf("function " + functionName, StringComparison.Ordinal);
        Assert.True(sigIdx > 0, $"Function {functionName} not found");
        var openBrace = js.IndexOf('{', sigIdx);
        Assert.True(openBrace > 0);
        var depth = 0;
        for (var i = openBrace; i < js.Length; i++)
        {
            if (js[i] == '{') depth++;
            else if (js[i] == '}')
            {
                depth--;
                if (depth == 0) return js[openBrace..(i + 1)];
            }
        }
        Assert.Fail("Unbalanced function body for " + functionName);
        return "";
    }

    /// <summary>
    /// Runs the Node.js render harness and returns its stdout. Asserts the
    /// process exited cleanly (exit 0). Harness prints PASS/FAIL lines and
    /// the assertion below relies on the harness's PASS lines to confirm
    /// behavior, then we assert the overall exit code is 0.
    /// </summary>
    private static string RunRenderHarness()
    {
        Assert.True(File.Exists(HarnessPath),
            $"Render harness not found at {HarnessPath}. The CJS harness is the regression check for the grouped-vs-individual card bug.");
        var node = ResolveNodeBinary();
        Assert.True(File.Exists(node),
            $"node binary not found at {node}. Install Node.js or set NODE_EXE.");

        var psi = new ProcessStartInfo
        {
            FileName = node,
            Arguments = $"\"{HarnessPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = TestsDir
        };
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30_000);
        Assert.True(proc.ExitCode == 0,
            $"Render harness exited {proc.ExitCode}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        return stdout;
    }

    private static string ResolveNodeBinary()
    {
        // Prefer the explicit override; fall back to "node" on PATH; finally
        // try common Windows install locations.
        var env = Environment.GetEnvironmentVariable("NODE_EXE");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
        if (OperatingSystem.IsWindows())
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe")
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
        }
        return OperatingSystem.IsWindows() ? "node.exe" : "node";
    }

    [Fact]
    public void Static_syncColumn_builds_groupedOrderIds_from_all_member_orderIds()
    {
        // Lock the structure: every member OrderId (including primary) must
        // end up in the same Set, and the Set must be derived from
        // g.orderIds (NOT from g.primaryOrderId alone). A future refactor
        // that splits primary/secondary back into two Sets reintroduces the
        // rendering bug where OrderA and OrderB both still show as standalone
        // cards under the grouped card.
        var js = File.ReadAllText(StaffBoardJsPath);
        var body = ExtractFunctionBody(js, "syncColumn");
        Assert.NotEmpty(body);

        // The fix uses a single Set whose name reflects its role: "every
        // OrderId that already appears inside a group anchored in this
        // column".
        Assert.Contains("groupedOrderIds", body, StringComparison.Ordinal);

        // The set must be populated from g.orderIds — never from
        // g.primaryOrderId alone, which is the original bug.
        Assert.Matches(
            new Regex(@"groupedOrderIds\.add\(oid\)", RegexOptions.Singleline),
            body);

        // The single-pass exclusion is required: every member (including
        // primary) is filtered in the same `continue` branch.
        var skipIdx = body.IndexOf("if (groupedOrderIds.has(o.id)) continue", StringComparison.Ordinal);
        Assert.True(skipIdx > 0, "syncColumn must skip standalone card mounts for any order whose OrderId is in groupedOrderIds");
        // The old, broken primary-only path must be gone.
        Assert.DoesNotContain("primaryIdsInThisColumn", body, StringComparison.Ordinal);
        Assert.DoesNotContain("secondaryIds", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Static_syncColumn_renders_each_group_exactly_once()
    {
        // The grouped-card mount happens once per group in the column loop. The
        // single render is enforced by the same loop, not by accumulating
        // per-member renders, so even an extended [A,B,C] group renders one
        // grouped card.
        var js = File.ReadAllText(StaffBoardJsPath);
        var body = ExtractFunctionBody(js, "syncColumn");
        Assert.NotEmpty(body);

        // One grouped-card mount loop keyed off lastBoardGroups.
        Assert.Contains("mountGroupedCard(board, body, g, status)", body, StringComparison.Ordinal);
        // The standalone-card mount must be guarded by groupedOrderIds.
        Assert.Contains("mountCard(board, body, o, status, isServed)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_harness_group_AB_renders_one_grouped_card()
    {
        // 1. Basic group: A + B → expect exactly one grouped card, zero
        //    standalone cards. Pre-fix this asserted 3 (A + B + Group[A,B]).
        var output = RunRenderHarness();
        Assert.Contains("PASS Group[A,B]:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_harness_group_ABC_renders_one_grouped_card()
    {
        // 2. Three-member group: A + B + C → exactly one grouped card.
        var output = RunRenderHarness();
        Assert.Contains("PASS Group[A,B,C]:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_harness_two_groups_render_two_grouped_cards()
    {
        // 3. Two independent groups: Group[A,B] + Group[C,D] → exactly two
        //    grouped cards.
        var output = RunRenderHarness();
        Assert.Contains("PASS Group[A,B]+Group[C,D]:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_harness_group_plus_lone_order_renders_mixed_cards()
    {
        // 4. Group [A,B] + lone C → 1 grouped + 1 standalone = 2 total. This
        //    guards against over-aggressive Set usage (which would drop C).
        var output = RunRenderHarness();
        Assert.Contains("PASS Group[A,B]+C:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_harness_same_table_code_renders_one_grouped_card()
    {
        // 5. Same TableCode: A and B share T36. The group card must collapse
        //    them to one card, not two. (TableCode is presentation only.)
        var output = RunRenderHarness();
        Assert.Contains("PASS Group[A,B] sameTableCode:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_harness_refresh_renders_one_grouped_card()
    {
        // 6. Refresh: same payload as case 1, re-applied via mergeBoard.
        //    The fix must not rely on any client-side flag — it is derived
        //    purely from the server `groups` response on every refresh.
        var output = RunRenderHarness();
        Assert.Contains("PASS Refresh Group[A,B]:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_harness_ungrouped_renders_two_standalone_cards()
    {
        // 7. Ungrouped: A + B with empty groups → both render as standalone.
        //    Guards against accidentally hiding ungrouped orders.
        var output = RunRenderHarness();
        Assert.Contains("PASS Ungrouped A,B:", output, StringComparison.Ordinal);
    }

    // ── Multi-step regression tests ─────────────────────────────────────────
    // The "old standalone A and B still visible after group A+B" bug ONLY
    // manifests across multiple mergeBoard() calls. Each scenario below
    // issues several payloads in sequence through the SAME FakeDocument and
    // asserts the DOM shape after every step. A failing snapshot here is the
    // canonical regression signal.

    [Fact]
    public void Multistep_A_drag_B_onto_A_no_stale_cards()
    {
        // Initial Column 3 has 3 standalone orders. After dragging B onto A:
        //   exactly 1 grouped card [GROUP[A,B]] and 1 standalone card [C].
        //   No leftover standalone A or B.
        var output = RunRenderHarness();
        Assert.Contains("PASS Multistep A step=initial:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep A step=after-group:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep A (all steps)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Multistep_B_group_then_refresh_no_stale_cards()
    {
        // Group A+B, then refresh twice. Every refresh must produce the
        // exact same 2 cards (1 grouped + 1 standalone C). Pre-fix the second
        // refresh would leave the OLD grouped card AND a duplicate of it.
        var output = RunRenderHarness();
        Assert.Contains("PASS Multistep B step=after-group:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep B step=refresh-1:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep B step=refresh-2:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Multistep_C_group_then_ungroup_no_stale_grouped_card()
    {
        // Group A+B then ungroup. Expected final DOM: 3 standalone cards A, B, C.
        // Pre-fix the OLD grouped card persists alongside the 3 new standalones.
        var output = RunRenderHarness();
        Assert.Contains("PASS Multistep C step=after-group:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep C step=after-ungroup:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Multistep_D_two_independent_groups_no_stragglers()
    {
        // Group A+B then group C+D. Expected after both groupings: 2 grouped
        // cards and 0 standalone cards. Pre-fix the OLD Group[A,B] card
        // survives AND the Group[C,D] card is also added, with the
        // intermediate "3 cards" state momentarily showing stale C and D
        // standalones from the first grouping.
        var output = RunRenderHarness();
        Assert.Contains("PASS Multistep D step=group-AB:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep D step=group-CD:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Multistep_E_regroup_with_different_partner()
    {
        // Group A+B, ungroup, group A+C. The final state must be exactly
        // 1 grouped [A,C] + 1 standalone B. Pre-fix the OLD Group[A,B] card
        // would still be in the DOM after the regroup.
        var output = RunRenderHarness();
        Assert.Contains("PASS Multistep E step=ungroup-AB:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep E step=group-AC:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Multistep_F_multiple_refreshes_one_grouped_card()
    {
        // Group A+B, refresh 3 times. Each refresh must produce exactly
        // 1 grouped card. Pre-fix duplicate Group[A,B] cards accumulate.
        var output = RunRenderHarness();
        Assert.Contains("PASS Multistep F step=group-AB:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep F step=refresh-1:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep F step=refresh-2:", output, StringComparison.Ordinal);
        Assert.Contains("PASS Multistep F step=refresh-3:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Multistep_no_duplicate_data_order_id()
    {
        // Direct check that no scenario leaves a duplicate (column, orderId,
        // groupId) triple in the DOM. Pre-fix, multiple scenarios print a
        // "duplicates: [...]" annotation in their FAIL output. Asserting
        // that no FAIL line mentions "duplicates" across the multi-step
        // scenarios is the explicit assertion against the original bug.
        var output = RunRenderHarness();
        Assert.DoesNotContain("duplicates:", output, StringComparison.Ordinal);
    }
}