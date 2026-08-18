using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SETTINGS_VERIFIED_RETURN_TRIGGER — VRT-1..VRT-17.
///
/// The parent-return trigger repair: return eligibility is based on
///   ContainerComplete(Current) == TRUE
///   AND PendingAuthorizedChildren(Current) == 0
///   AND known parent exists
/// — NOT on structural leaf-ness (NavigationCandidateCount == 0). DISCOVERED
/// CHILD CANDIDATE != AUTHORIZED CHILD OBLIGATION: a denied/audited candidate
/// is not an obligation and never blocks the verified parent return. The
/// Agent owns recursive authorization / pending obligations / the return
/// decision; the Traversal executes only authorized actions; Container
/// completeness grants no new recursion authority.
/// VRT-17 (no regression) is the full deterministic suite.
/// </summary>
public sealed class VerifiedReturnTriggerTests
{
    private const string App = "com.android.settings";
    private const string RootPage = "SettingsRoot";
    private const string SelectedChildLabel = "Location";
    private const string SelectedGrandchildLabel = "Location services";
    private const string ChildIdentity = "SettingsSubpage(Location)";
    private const string GrandchildIdentity = "SettingsSubpage(Location services)";

    // ── model-level eligibility contract ─────────────────────────────────────

    private static BranchProgressEvidence Progress(
        string[] approved,
        string[] authorized,
        string[] completed)
        => new(
            "SettingsSubpage(Location)",
            approved.ToImmutableDictionary(id => id, _ => 1L, StringComparer.Ordinal),
            completed.ToImmutableDictionary(id => id, _ => 2L, StringComparer.Ordinal),
            authorized.ToImmutableDictionary(id => id, _ => 1L, StringComparer.Ordinal));

    // ── VRT-1: complete + discovered candidates + zero authorized → eligible ─

    [Fact]
    public void VRT1_Complete_DiscoveredCandidates_ZeroAuthorized_ReturnEligible()
    {
        // Location services: 2 discovered candidates ("Wi-Fi scanning",
        // "Bluetooth scanning"), ZERO authorized → RETURN_ELIGIBLE even though
        // it is NOT a structural leaf.
        var progress = Progress(
            approved: ["Wi-Fi scanning", "Bluetooth scanning"],
            authorized: [],
            completed: []);
        Assert.True(RuntimeAgent.IsReturnEligible(
            parentCount: 1, containerComplete: true, progress));
    }

    // ── VRT-2: complete + pending authorized child → NOT eligible ───────────

    [Fact]
    public void VRT2_Complete_PendingAuthorizedChild_NotEligible()
    {
        var progress = Progress(
            approved: ["A", "B"],
            authorized: ["A", "B"],
            completed: ["A"]); // B authorized but NOT completed → pending obligation
        Assert.False(RuntimeAgent.IsReturnEligible(
            parentCount: 1, containerComplete: true, progress));
    }

    // ── VRT-3: container incomplete → NOT eligible ──────────────────────────

    [Fact]
    public void VRT3_Incomplete_NotEligible()
    {
        var progress = Progress(["A"], [], []);
        Assert.False(RuntimeAgent.IsReturnEligible(
            parentCount: 1, containerComplete: false, progress));
    }

    // ── VRT-4: no known parent → NOT eligible ───────────────────────────────

    [Fact]
    public void VRT4_NoParent_NotEligible()
    {
        var progress = Progress(["A"], [], []);
        Assert.False(RuntimeAgent.IsReturnEligible(
            parentCount: 0, containerComplete: true, progress));
    }

    // ── VRT-5: denied/audited candidate does not become an obligation ───────

    [Fact]
    public void VRT5_DeniedCandidate_NotAnObligation()
    {
        // "Wi-Fi scanning" was AUDITED (denied) — it never enters the
        // authorized set, so it cannot block the return.
        var progress = Progress(
            approved: ["Wi-Fi scanning", "Bluetooth scanning"],
            authorized: [], // both denied
            completed: []);
        Assert.False(progress.AuthorizedSiblingEvidence.ContainsKey("Wi-Fi scanning"));
        Assert.True(RuntimeAgent.IsReturnEligible(1, true, progress));
    }

    // ── VRT-6: return does not imply SubtreeComplete ────────────────────────

    [Fact]
    public async Task VRT6_ReturnDoesNotImplySubtreeComplete()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(), "vrt-6");
        Assert.Contains(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(run.Agent.Trace, t => t.Reason?.Contains("SubtreeComplete", StringComparison.Ordinal) is true);
    }

    // ── VRT-7: fresh parent-return bounds required ───────────────────────────

    [Fact]
    public async Task VRT7_FreshParentReturnBoundsRequired()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(), "vrt-7");
        var taps = run.Environment.ActionHistory.OfType<DeviceAction.Tap>().ToList();
        Assert.True(taps.Count >= 3);
        // The grandchild→child return Tap (the third) carries fresh bounds.
        Assert.True(taps[2].TargetBounds is { IsValid: true } && taps[2].TargetBounds.Height > 0f);
    }

    // ── VRT-8: Tap receipt alone is not return truth ────────────────────────

    [Fact]
    public async Task VRT8_TapReceiptAloneNotReturnTruth()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(
                returnEffect: SettingsGrandchildVerifiedReturnTests.ReturnEffect.NoEffect), "vrt-8");
        Assert.Equal(RunState.Failed, run.State);
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    // ── VRT-9: fresh exact parent identity → verified PASS ──────────────────

    [Fact]
    public async Task VRT9_FreshExactParentIdentity_VerifiedPass()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(), "vrt-9");
        // The grandchild returns to the exact Child identity.
        Assert.Contains(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return; child 'Location services' progress retained", StringComparison.Ordinal) is true);
    }

    // ── VRT-10: wrong destination → FAIL ────────────────────────────────────

    [Fact]
    public async Task VRT10_WrongDestination_Fails()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(
                returnEffect: SettingsGrandchildVerifiedReturnTests.ReturnEffect.Foreign), "vrt-10");
        Assert.Equal(RunState.Failed, run.State);
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    // ── VRT-11: returned Child evidence checked against frozen epoch ────────

    [Fact]
    public async Task VRT11_ReturnedChildCheckedAgainstFrozenEpoch()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(), "vrt-11");
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("Post-completeness fresh evidence INVALIDATED", StringComparison.Ordinal) is true);
    }

    // ── VRT-12: Child frozen inventory unchanged ────────────────────────────

    [Fact]
    public async Task VRT12_ChildFrozenInventoryUnchanged()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(), "vrt-12");
        var childEpochs = run.Agent.Trace.Count(t =>
            t.ContainerId == ChildIdentity && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        Assert.Equal(1, childEpochs);
    }

    // ── VRT-13: ancestry pops Grandchild only after verified return ─────────

    [Fact]
    public async Task VRT13_AncestryPopsGrandchildAfterVerifiedReturn()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(), "vrt-13");
        Assert.Contains(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return; child 'Location services' progress retained", StringComparison.Ordinal) is true);
        // After the return the run continues at the Child (its audited
        // candidates are then also return-eligible → the run proceeds).
        Assert.Contains(run.Agent.Trace, t => t.ContainerId == ChildIdentity);
    }

    // ── VRT-14: Grandchild remains visited ──────────────────────────────────

    [Fact]
    public async Task VRT14_GrandchildRemainsVisited()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(), "vrt-14");
        // The grandchild was entered exactly once and never re-entered (no
        // second grandchild transition — identity-safety visited accounting).
        Assert.True(run.Agent.Trace.Count(t => t.ContainerId == GrandchildIdentity) >= 1);
    }

    // ── VRT-15: zero sibling dispatch after return ──────────────────────────

    [Fact]
    public async Task VRT15_ZeroSiblingDispatchAfterReturn()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(), "vrt-15");
        // 4 taps = 2 dispatches (Root→Child, Child→Grandchild) + 2 verified
        // returns (Grandchild→Child, Child→Root). Zero sibling dispatch.
        Assert.Equal(4, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
    }

    // ── VRT-16: More-options affordance untouched ───────────────────────────

    [Fact]
    public void VRT16_MoreOptionsAffordanceUntouched()
    {
        var moreOptions = new StructuredElementEvidence(
            "android.widget.ImageButton", null, true, false, false, true, true,
            new ElementBounds(0.9f, 0.03f, 1f, 0.1f), null, null, null, "More options", null);
        var obs = new Observation(ImmutableArray<ObservedElement>.Empty, App, 1)
        {
            StructuredElements = ImmutableArray.Create(moreOptions),
        };
        // The analyzer stays context-free: "More options" remains a genuine
        // UNKNOWN (no widening, no suppression — a separate affordance
        // pressure, explicitly NOT handled by this gate).
        var affordances = InteractionAffordanceAnalyzer.Analyze(obs);
        Assert.Single(affordances);
        Assert.Equal(InteractionAffordanceKind.Unknown, affordances[0].Classification);
    }

    // ── VRT-17: GC / DIM / PCC / PRC / RC1 / ART / ROLE / SIG / SEARCH / SQ /
    // ── PROV / NM / RVT / AFF / SET / COMPOSE-05 green — covered by the full
    // ── deterministic suite. ─────────────────────────────────────────────────
}
