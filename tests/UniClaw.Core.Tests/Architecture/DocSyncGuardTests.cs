using System.Text.RegularExpressions;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.Architecture;

/// <summary>
/// Doc-code sync guard tests — verify that Tier 1 (Constitution) documentation
/// matches actual code reality. If someone changes an enum but forgets to update
/// locked-enums.md or charter tables, these tests fail (CI-blocking).
///
/// Enforces charter §5.6: "Constitution docs must match code reality after Apply."
/// See docs/system/charter-specification.md §6.4 for DocSync test design.
/// </summary>
public class DocSyncGuardTests
{
    private static string FindSourceRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "src", "UniClaw.Core.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    // ────────────────────────────────────────────────────────────────
    // P1a: locked-enums.md value counts ↔ Enum.GetValues<T>().Length
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verify ALL locked enums in locked-enums.md have value counts matching
    /// their actual C# enum definitions. Parses the markdown table to extract
    /// declared counts, then compares each to Enum.GetValues&lt;T&gt;().Length.
    /// </summary>
    [Fact]
    public void LockedEnums_ValueCounts_MatchCodeReality()
    {
        var root = FindSourceRoot();
        var lockedEnumsPath = Path.Combine(root, "docs", "system", "constitution", "locked-enums.md");
        Assert.True(File.Exists(lockedEnumsPath), $"locked-enums.md not found at {lockedEnumsPath}");

        var content = File.ReadAllText(lockedEnumsPath);

        // Parse table rows: | `EnumName` | Namespace | **N** | ...
        // Regex captures enum name (backtick-wrapped) and value count (bold-wrapped number)
        var rowPattern = @"`\s*(\w+)\s*`\s*\|\s*[^|]+\|\s*\*\*(\d+)\*\*";
        var matches = Regex.Matches(content, rowPattern);

        Assert.True(matches.Count >= 12,
            $"Expected at least 12 locked enums in locked-enums.md, found {matches.Count}. " +
            "Table format may have changed — update this test's regex.");

        // Map enum names to their actual Type for comparison
        var enumTypes = new Dictionary<string, Type>
        {
            ["TraversalState"] = typeof(TraversalState),
            ["GlobalState"] = typeof(GlobalState),
            ["NodeType"] = typeof(NodeType),
            ["ErrorType"] = typeof(ErrorType),
            ["ErrorStrategy"] = typeof(ErrorStrategy),
            ["PopupType"] = typeof(PopupType),
            ["DismissStrategy"] = typeof(DismissStrategy),
            ["UrgencyLevel"] = typeof(UrgencyLevel),
            ["BlockingType"] = typeof(BlockingType),
            ["FallbackAction"] = typeof(FallbackAction),
            ["TypeHint"] = typeof(TypeHint),
            ["SelectionState"] = typeof(SelectionState),
        };

        var mismatches = new List<string>();

        foreach (Match match in matches)
        {
            var enumName = match.Groups[1].Value;
            var declaredCount = int.Parse(match.Groups[2].Value);

            if (!enumTypes.TryGetValue(enumName, out var enumType))
            {
                // Enum not tracked in this test — skip (may be a non-locked enum)
                continue;
            }

            var actualCount = Enum.GetValues(enumType).Length;

            if (declaredCount != actualCount)
            {
                mismatches.Add(
                    $"{enumName}: locked-enums.md declares {declaredCount}, " +
                    $"code has {actualCount}. " +
                    $"Update locked-enums.md or the enum definition.");
            }
        }

        Assert.Empty(mismatches);
    }

    // ────────────────────────────────────────────────────────────────
    // P1b: charter §6.1 enum count table ↔ Enum.GetValues<T>().Length
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verify charter §6.1 EnumValueGuardTests table has value counts matching
    /// actual enum definitions. This catches the scenario where someone updates
    /// an enum AND the Guard test (making the Guard pass) but forgets to update
    /// the charter documentation.
    /// </summary>
    [Fact]
    public void Charter_GuardTable_ValueCounts_MatchCodeReality()
    {
        var root = FindSourceRoot();
        var charterPath = Path.Combine(root, "docs", "system", "charter-specification.md");
        Assert.True(File.Exists(charterPath), $"charter-specification.md not found at {charterPath}");

        var content = File.ReadAllText(charterPath);

        // Extract the §6.1 section (between "### 6.1" and "### 6.2")
        var sectionMatch = Regex.Match(content, @"### 6\.1.*?\n(.*?)(?=### 6\.2|## 7)", RegexOptions.Singleline);
        Assert.True(sectionMatch.Success, "Could not find §6.1 section in charter. Format may have changed.");

        var section = sectionMatch.Value;

        // Parse lines like: "- TraversalState=8, GlobalState=8, ..."
        // and individual entries with "=N" pattern
        var enumCountPattern = @"(\w+)=(\d+)";
        var matches = Regex.Matches(section, enumCountPattern);

        var enumTypes = new Dictionary<string, Type>
        {
            ["TraversalState"] = typeof(TraversalState),
            ["GlobalState"] = typeof(GlobalState),
            ["NodeType"] = typeof(NodeType),
            ["ErrorType"] = typeof(ErrorType),
            ["ErrorStrategy"] = typeof(ErrorStrategy),
            ["PopupType"] = typeof(PopupType),
            ["DismissStrategy"] = typeof(DismissStrategy),
            ["UrgencyLevel"] = typeof(UrgencyLevel),
            ["BlockingType"] = typeof(BlockingType),
            ["FallbackAction"] = typeof(FallbackAction),
        };

        var mismatches = new List<string>();

        foreach (Match match in matches)
        {
            var enumName = match.Groups[1].Value;
            var declaredCount = int.Parse(match.Groups[2].Value);

            if (!enumTypes.TryGetValue(enumName, out var enumType))
                continue;

            var actualCount = Enum.GetValues(enumType).Length;

            if (declaredCount != actualCount)
            {
                mismatches.Add(
                    $"{enumName}: charter §6.1 declares {declaredCount}, " +
                    $"code has {actualCount}. " +
                    $"Update charter §6.1 table or the enum definition.");
            }
        }

        Assert.Empty(mismatches);
    }

    // ────────────────────────────────────────────────────────────────
    // P1c: Guard test names ↔ locked-enums.md Guard Test column
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verify that the Guard test names listed in locked-enums.md actually exist
    /// in ArchitectureGuardTests.cs. This catches the scenario where someone
    /// updates the Guard test name but forgets to update the documentation reference.
    /// </summary>
    [Fact]
    public void LockedEnums_GuardTestNames_ExistInCode()
    {
        var root = FindSourceRoot();
        var lockedEnumsPath = Path.Combine(root, "docs", "system", "constitution", "locked-enums.md");
        var guardPath = Path.Combine(root, "tests", "UniClaw.Core.Tests", "Architecture", "ArchitectureGuardTests.cs");

        Assert.True(File.Exists(lockedEnumsPath));
        Assert.True(File.Exists(guardPath));

        var lockedContent = File.ReadAllText(lockedEnumsPath);
        var guardContent = File.ReadAllText(guardPath);

        // Parse Guard Test column: values like `TraversalState_Has8Values`
        var guardNamePattern = @"`(\w+_Has\d+Values)`";
        var matches = Regex.Matches(lockedContent, guardNamePattern);

        var missingTests = new List<string>();

        foreach (Match match in matches)
        {
            var testName = match.Groups[1].Value;
            if (!guardContent.Contains(testName))
            {
                missingTests.Add(
                    $"{testName}: listed in locked-enums.md but not found in ArchitectureGuardTests.cs. " +
                    $"Either add the test or update the doc.");
            }
        }

        Assert.Empty(missingTests);
    }

    // ────────────────────────────────────────────────────────────────
    // P1d: EnumValueGuardTests value count ↔ locked-enums.md value count
    // Cross-check that the Assert.Equal(N, ...) values in Guard tests
    // match the declared counts in locked-enums.md.
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verify that each Guard test's Assert.Equal expected value matches
    /// the declared count in locked-enums.md. This catches the scenario where
    /// someone updates the Guard test to a new number but forgets to update
    /// the constitution document — the Guard test passes but the doc is stale.
    /// </summary>
    [Fact]
    public void GuardTest_AssertValues_MatchLockedEnums()
    {
        var root = FindSourceRoot();
        var lockedEnumsPath = Path.Combine(root, "docs", "system", "constitution", "locked-enums.md");
        var guardPath = Path.Combine(root, "tests", "UniClaw.Core.Tests", "Architecture", "ArchitectureGuardTests.cs");

        var lockedContent = File.ReadAllText(lockedEnumsPath);
        var guardContent = File.ReadAllText(guardPath);

        // From locked-enums.md: parse (EnumName, DeclaredCount, GuardTestName)
        var rowPattern = @"`\s*(\w+)\s*`\s*\|\s*[^|]+\|\s*\*\*(\d+)\*\*\s*\|\s*[^|]+\|\s*[^|]+\|\s*`(\w+_Has\d+Values)`";
        var lockedMatches = Regex.Matches(lockedContent, rowPattern);

        var mismatches = new List<string>();

        foreach (Match match in lockedMatches)
        {
            var enumName = match.Groups[1].Value;
            var declaredCount = int.Parse(match.Groups[2].Value);
            var guardTestName = match.Groups[3].Value;

            // From guard code: find Assert.Equal(N, ...) in the specific test method
            var assertPattern = $@"{guardTestName}\s*\(\)\s*=>\s*Assert\.Equal\((\d+),";
            var assertMatch = Regex.Match(guardContent, assertPattern);

            if (!assertMatch.Success)
            {
                mismatches.Add($"{enumName}: Guard test {guardTestName} not found or has unexpected format.");
                continue;
            }

            var guardAssertedCount = int.Parse(assertMatch.Groups[1].Value);

            if (declaredCount != guardAssertedCount)
            {
                mismatches.Add(
                    $"{enumName}: locked-enums.md declares {declaredCount}, " +
                    $"Guard test asserts {guardAssertedCount}. " +
                    $"They must match — update both together.");
            }

            // Also verify the Guard test name implies the correct count
            // (e.g., Has8Values should assert 8)
            var impliedCountPattern = @"Has(\d+)Values";
            var impliedMatch = Regex.Match(guardTestName, impliedCountPattern);
            if (impliedMatch.Success)
            {
                var impliedCount = int.Parse(impliedMatch.Groups[1].Value);
                if (impliedCount != guardAssertedCount)
                {
                    mismatches.Add(
                        $"{enumName}: Guard test name says Has{impliedCount}Values " +
                        $"but Assert.Equal asserts {guardAssertedCount}. Name and value must match.");
                }
            }
        }

        Assert.Empty(mismatches);
    }
}
