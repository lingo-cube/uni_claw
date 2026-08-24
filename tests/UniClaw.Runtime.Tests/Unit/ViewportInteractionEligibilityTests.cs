using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// SETTINGS_SCROLL_ARTIFACT_ELIGIBILITY — ART-1..ART-10, ART-13.
///
/// Viewport interaction eligibility: a structured interaction occurrence is
/// eligible to enter the current viewport's interaction-semantic evidence only
/// when Bounds != null AND Bounds.IsValid AND positive area AND intersects the
/// current viewport. A clickable/checkable node failing eligibility is a
/// NON_ACTIONABLE_STRUCTURAL_ARTIFACT (e.g. a RecyclerView recycled container
/// captured mid-recycle with negative-height bounds, persistently present in
/// real uiautomator dumps): it is not admitted as viewport interaction
/// evidence — it is not a NAVIGATION_CANDIDATE, not a LOCAL_CONTROL, not an
/// UNKNOWN interaction obligation, not a NavigationSourceOccurrence, not a
/// dispatch target. Eligibility is BOUNDS-ONLY (never text, never title,
/// never package): a valid-bounds textless clickable node REMAINS admitted and
/// is a genuine UNKNOWN that fails closed.
/// ART-11 / ART-12 (settle convergence / fail-closed through the full agent
/// run) are covered in Scenario.ScrollArtifactEligibilityScenarioTests.
/// ART-14 (no regression) is the full deterministic suite.
/// </summary>
public sealed class ViewportInteractionEligibilityTests
{
    private const int Width = 1080;
    private const int Height = 1920;

    // ── raw XML builders ─────────────────────────────────────────────────────

    private static string Esc(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal);

    private static string Node(
        string cls,
        string text,
        bool clickable,
        string bounds,
        string resourceId = "",
        bool checkable = false,
        bool focusable = true)
        => $"<node index=\"0\" text=\"{text}\" resource-id=\"{resourceId}\" class=\"{cls}\" "
           + $"package=\"com.android.settings\" content-desc=\"\" checkable=\"{checkable.ToString().ToLowerInvariant()}\" "
           + $"checked=\"false\" clickable=\"{clickable.ToString().ToLowerInvariant()}\" enabled=\"true\" "
           + $"focusable=\"{focusable.ToString().ToLowerInvariant()}\" focused=\"false\" scrollable=\"false\" "
           + $"long-clickable=\"false\" password=\"false\" selected=\"false\" bounds=\"{bounds}\"/>";

    /// <summary>A Settings-style clickable row with a title descendant (android:id/title).</summary>
    private static string TitledRow(string title, string bounds)
        => $"<node index=\"0\" text=\"\" resource-id=\"\" class=\"android.widget.LinearLayout\" "
           + $"package=\"com.android.settings\" content-desc=\"\" checkable=\"false\" checked=\"false\" "
           + $"clickable=\"true\" enabled=\"true\" focusable=\"true\" focused=\"false\" scrollable=\"false\" "
           + $"long-clickable=\"false\" password=\"false\" selected=\"false\" bounds=\"{bounds}\">"
           + Node("android.widget.TextView", Esc(title), clickable: false, bounds: bounds, resourceId: "android:id/title")
           + "</node>";

    private static ImmutableArray<StructuredElementEvidence> Parse(params string[] nodes)
    {
        var xml = "<?xml version='1.0' encoding='UTF-8' standalone='yes' ?><hierarchy rotation=\"0\">"
                  + string.Join("", nodes)
                  + "</hierarchy>";
        return AdbUiHierarchySource.Parse(xml, Width, Height);
    }

    private static Observation Obs(params StructuredElementEvidence[] elements)
        => new(ImmutableArray<ObservedElement>.Empty, "com.android.settings", 1)
        {
            StructuredElements = elements.ToImmutableArray(),
        };

    private static Observation Obs(ImmutableArray<StructuredElementEvidence> elements)
        => new(ImmutableArray<ObservedElement>.Empty, "com.android.settings", 1)
        {
            StructuredElements = elements,
        };

    private static ImmutableArray<string> NavSignatures(Observation observation)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
            builder.Add(occurrence.StructuredSignature);
        return builder.ToImmutable();
    }

    // ── ART-1: negative-height clickable node -> NOT admitted ────────────────

    [Fact]
    public void ART1_NegativeHeightClickable_NotAdmitted()
    {
        // The persistent recycled-container artifact: clickable, negative height.
        var parsed = Parse(Node("android.widget.LinearLayout", "", clickable: true, bounds: "[0,284][1080,203]"));

        Assert.Empty(parsed);
    }

    // ── ART-2: zero-area clickable node -> NOT admitted ──────────────────────

    [Fact]
    public void ART2_ZeroAreaClickable_NotAdmitted()
    {
        // Y2 == Y1: valid per IsValid, but zero height -> no actionable target.
        var parsed = Parse(Node("android.widget.LinearLayout", "", clickable: true, bounds: "[0,100][1080,100]"));

        Assert.Empty(parsed);
    }

    // ── ART-3: fully outside viewport clickable node -> NOT admitted ─────────

    [Fact]
    public void ART3_FullyOutsideViewportClickable_NotAdmitted()
    {
        // y > screen height (1920): cannot intersect the viewport frame.
        var parsed = Parse(Node("android.widget.LinearLayout", "", clickable: true, bounds: "[0,2000][1080,2100]"));

        Assert.Empty(parsed);
    }

    // ── ART-4: partially visible positive-area valid row -> ADMITTED ─────────

    [Fact]
    public void ART4_PartiallyVisiblePositiveAreaRow_Admitted()
    {
        var parsed = Parse(TitledRow("Security & privacy", "[0,1700][1080,1920]"));

        var row = Assert.Single(parsed);
        Assert.Equal("Security & privacy", row.RawText);
        Assert.True(row.Bounds is { IsValid: true });
    }

    // ── ART-5: valid-bounds textless clickable -> ADMITTED + UNKNOWN ─────────

    [Fact]
    public void ART5_ValidBoundsTextlessClickable_AdmittedAndUnknown()
    {
        var parsed = Parse(Node("android.widget.LinearLayout", "", clickable: true, bounds: "[0,300][1080,600]"));

        var row = Assert.Single(parsed);
        Assert.Null(row.RawText);
        Assert.True(row.Bounds is { IsValid: true });
        var affordances = InteractionAffordanceAnalyzer.Analyze(Obs(parsed.ToArray()));
        Assert.Contains(affordances, a => a.Classification == InteractionAffordanceKind.Unknown);
    }

    // ── ART-6: valid navigation row -> NAVIGATION_CANDIDATE unchanged ────────

    [Fact]
    public void ART6_ValidNavigationRow_NavigationCandidate()
    {
        var parsed = Parse(TitledRow("Network & internet", "[0,400][1080,631]"));

        var affordances = InteractionAffordanceAnalyzer.Analyze(Obs(parsed.ToArray()));
        Assert.Contains(affordances, a => a.Classification == InteractionAffordanceKind.NavigationCandidate);
    }

    // ── ART-7: Search control -> LOCAL_CONTROL unchanged ─────────────────────

    [Fact]
    public void ART7_SearchControl_LocalControl()
    {
        var parsed = Parse(Node(
            "android.view.ViewGroup", "Search settings", clickable: true,
            bounds: "[42,105][1038,242]", resourceId: "com.android.settings:id/search_action_bar"));

        var affordances = InteractionAffordanceAnalyzer.Analyze(Obs(parsed.ToArray()));
        Assert.Contains(affordances, a => a.Classification == InteractionAffordanceKind.LocalControl);
    }

    // ── ART-8: excluded artifact produces no NavigationSourceOccurrence ──────

    [Fact]
    public void ART8_ExcludedArtifact_NoNavigationSourceOccurrence()
    {
        var parsed = Parse(
            Node("android.widget.LinearLayout", "", clickable: true, bounds: "[0,284][1080,203]"),
            TitledRow("Apps", "[0,284][1080,434]"));

        // Only the eligible row is admitted.
        Assert.Single(parsed);
        var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(Obs(parsed.ToArray()));
        Assert.Single(occurrences);
        Assert.StartsWith("Apps|", occurrences[0].StructuredSignature, StringComparison.Ordinal);
    }

    // ── ART-9: excluded artifact does not enter normalization ────────────────

    [Fact]
    public void ART9_ExcludedArtifact_DoesNotEnterNormalization()
    {
        // obs1: two valid rows. obs2: same two rows + a stale recycled container
        // (negative-height, carrying a title that would claim a THIRD source).
        var obs1 = Obs(Parse(TitledRow("Network & internet", "[0,400][1080,631]"),
            TitledRow("Apps", "[0,631][1080,862]")));
        var obs2 = Obs(Parse(TitledRow("Network & internet", "[0,400][1080,631]"),
            TitledRow("Apps", "[0,631][1080,862]"),
            Node("android.widget.LinearLayout", "", clickable: true, bounds: "[0,284][1080,203]")
                + Node("android.widget.TextView", "Storage", clickable: false, bounds: "[0,284][1080,203]", resourceId: "android:id/title")));

        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(obs1, obs2));

        Assert.True(normalization.IsResolved);
        // "Storage" (the stale artifact title) must NOT appear as a source.
        Assert.DoesNotContain(normalization.UniqueSourceSignatures, s => s.StartsWith("Storage|", StringComparison.Ordinal));
        Assert.Equal(2, normalization.UniqueSourceSignatures.Length);
    }

    // ── ART-10: excluded artifact does not affect positive exhaustion ────────

    [Fact]
    public void ART10_ExcludedArtifact_DoesNotAffectPositiveExhaustion()
    {
        // The exploration evaluator decides "new source appeared" from the
        // navigation-occurrence signatures ONLY. An artifact-only observation
        // carries ZERO occurrences, so it can neither fabricate a "new source"
        // signal nor delay exhaustion.
        var artifactOnly = Obs(Parse(
            Node("android.widget.LinearLayout", "", clickable: true, bounds: "[0,284][1080,203]"),
            Node("android.widget.LinearLayout", "", clickable: true, bounds: "[0,1820][1080,1794]")));

        Assert.Empty(artifactOnly.StructuredElements);
        Assert.Empty(NavSignatures(artifactOnly));

        // Exhaustion evaluation (test-side ExploreWhileNew pattern): latest vs
        // prior — an artifact-only latest introduces NO new signature.
        var prior = ImmutableArray.Create("Network & internet|android.widget.LinearLayout||");
        var latestSigs = NavSignatures(artifactOnly);
        Assert.False(latestSigs.Any(s => !prior.Contains(s, StringComparer.Ordinal)));
    }

    // ── ART-13: ScrollBackward uses identical eligibility ────────────────────

    [Fact]
    public void ART13_ScrollBackward_IdenticalEligibility()
    {
        // A backward-revisit capture (top rows visible again) carrying the same
        // persistent artifacts: eligibility lives in the shared admission
        // boundary (Parse) — direction-agnostic. Artifacts excluded, valid rows
        // admitted, exactly as in the forward direction.
        var parsed = Parse(
            Node("android.widget.LinearLayout", "", clickable: true, bounds: "[0,284][1080,203]"),
            TitledRow("Network & internet", "[0,284][1080,515]"),
            TitledRow("Connected devices", "[0,515][1080,746]"));

        Assert.Equal(2, parsed.Length);
        Assert.All(parsed, e => Assert.True(e.Bounds is { IsValid: true }));
        Assert.DoesNotContain(parsed, e => e.Bounds is null);
    }

}
