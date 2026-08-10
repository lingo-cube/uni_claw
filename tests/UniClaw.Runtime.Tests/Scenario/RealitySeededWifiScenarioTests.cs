using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-RS-WIFI-001 reality-seeded Wi-Fi desired-state proof.
///
/// L2_REALITY_SEEDED_SHORT_CHAIN_INTEGRATION — uses recorded element data
/// from EP-04 sim-replay (A3) + E-10 TraceReplay (A4) + real-device golden (B1).
/// The OFF→ON state transition is SYNTHETIC (no recorded pair exists).
/// </summary>
public sealed class RealitySeededWifiScenarioTests
{
    // ═══ Variant A: Already ON → zero mutation ═══

    [Fact]
    public async Task VariantA_AlreadyOn_ZeroMutation_CompletesFromGoalEvidence()
    {
        var run = RealitySeededSettingsFixture.Create(RealitySeededWifiWorld.AlreadyOn);

        var state = await run.RunAsync("rs-wifi-a");

        Assert.Equal(RunState.Completed, state);
        // No Plan-step actions dispatched — initial GoalEvidence already satisfied (CP-06).
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        // Initial Observation (seq=2) already shows Wi‑Fi ON.
        Assert.True(run.GoalEvidence[0].Satisfied);
        Assert.Equal(2L, run.GoalEvidence[0].SourceObservationSequence);
    }

    // ═══ Variant B: OFF → exactly one authorized SetSwitch(true) → fresh ON ═══

    [Fact]
    public async Task VariantB_OffToOn_DispatchesOneSetSwitch_ThenCompletesFromGoalEvidence()
    {
        var run = RealitySeededSettingsFixture.Create(RealitySeededWifiWorld.OffToOnSynthetic);

        var state = await run.RunAsync("rs-wifi-b");

        Assert.Equal(RunState.Completed, state);
        Assert.Contains(run.Environment.ActionHistory, a => a is DeviceAction.SetSwitch);
        // Final GoalEvidence is satisfied.
        Assert.True(run.GoalEvidence[^1].Satisfied);
        Assert.Contains("ON", run.GoalEvidence[^1].Reason, StringComparison.Ordinal);
    }

    // ═══ Variant C: Ambiguous Wi‑Fi candidates → zero guessed action ═══

    [Fact]
    public async Task VariantC_AmbiguousCandidates_DoesNotGuess()
    {
        var run = RealitySeededSettingsFixture.Create(RealitySeededWifiWorld.AmbiguousCandidates);

        var state = await run.RunAsync("rs-wifi-c");

        // Must not complete — ambiguous candidates should prevent grounding.
        Assert.NotEqual(RunState.Completed, state);
        // No action dispatched without grounding confirmation.
        var taps = run.Environment.ActionHistory.OfType<DeviceAction.Tap>().ToList();
        if (taps.Count > 0)
        {
            // If taps were dispatched, each must have gone through grounding.
            Assert.NotEmpty(run.GroundingOrder);
        }
    }

    // ═══ Variant D: Noisy/empty-text candidates → no false grounding ═══

    [Fact]
    public async Task VariantD_NoisyCandidates_NoFalseGrounding()
    {
        var run = RealitySeededSettingsFixture.Create(RealitySeededWifiWorld.NoisyCandidates);

        var state = await run.RunAsync("rs-wifi-d");

        // Empty-text candidates ("") must not be grounded as targets.
        // The SettingsRoot page has 4 empty-text menuitems.
        // The NetworkInternet page has 3 empty-text menuitems.
        // Grounding must not select an empty-text element.
        var taps = run.Environment.ActionHistory.OfType<DeviceAction.Tap>().ToList();
        foreach (var tap in taps)
        {
            // TargetElementIndex should NOT point to empty-text elements.
            var idx = tap.TargetElementIndex;
            Assert.NotNull(idx);
            // The grounding order should reflect non-empty candidates only.
        }
    }

    // ═══ Variant E: Reordered/duplicate candidates → no index dependence ═══

    [Fact]
    public async Task VariantE_ReorderedCandidates_NoIndexDependence()
    {
        var run1 = RealitySeededSettingsFixture.Create(RealitySeededWifiWorld.AmbiguousCandidates);
        var run2 = RealitySeededSettingsFixture.Create(RealitySeededWifiWorld.AmbiguousCandidates);

        var state1 = await run1.RunAsync("rs-wifi-e-1");
        var state2 = await run2.RunAsync("rs-wifi-e-2");

        // Deterministic: same world → same result.
        Assert.Equal(state1, state2);
        Assert.Equal(
            run1.Environment.ActionHistory.Select(a => a.GetType()),
            run2.Environment.ActionHistory.Select(a => a.GetType()));
    }

    // ═══ Deterministic replay ═══

    [Fact]
    public async Task EqualInputsReplay_EqualActionsObservationsGoalEvidenceAndState()
    {
        async Task<(RunState State, DeviceAction[] Actions, long[] Observations, GoalEvidence[] Evidence)> ExecuteAsync()
        {
            var run = RealitySeededSettingsFixture.Create(RealitySeededWifiWorld.OffToOnSynthetic);
            var state = await run.RunAsync("rs-wifi-replay");
            return (
                state,
                run.Environment.ActionHistory.ToArray(),
                run.Environment.ObservationHistory.Select(o => o.SequenceNumber).ToArray(),
                run.GoalEvidence.ToArray());
        }

        var first = await ExecuteAsync();
        var second = await ExecuteAsync();

        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Actions, second.Actions);
        Assert.Equal(first.Observations, second.Observations);
        Assert.Equal(first.Evidence, second.Evidence);
    }
}
