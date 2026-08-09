using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-S0-CAPSTONE-001 Task 1.1 deterministic fixture tests: the S0 world exposes an approved
/// semantic navigation tree with safe reachable Settings pages across at least four levels, fully
/// traversable within the depth bound 4; the dangerous mutation candidate is visible but not an
/// approved executable action; the Popup and the external Launcher drift each occur exactly once at
/// deterministic schedule points; equal inputs replay an equal world; and the fixture encodes no
/// production conclusions (no Container identity, Recovery authority, progress completion, Goal
/// success, or pre-encoded route). Fixture-side expression only; no Agent behavior is exercised.
/// </summary>
public sealed class CapstoneSettingsWorldFixtureTests
{
    [Fact]
    public async Task World_ExposesApprovedFourLevelTreeFullyTraversableWithinDepthBound()
    {
        var fixture = CapstoneSettingsWorldFixture.Create();
        var evidence = await fixture.RunAsync();

        Assert.Equal(CapstoneSettingsWorldFixture.DefaultRunId, evidence.RunId);

        // Approved tree metadata: 14 pages across depths 0..4 (five 1-indexed levels, i.e. at least
        // four levels), every approved page within the depth bound input.
        Assert.Equal(14, evidence.ApprovedTree.Length);
        Assert.Equal(4, evidence.ApprovedTree.Max(page => page.Depth));
        Assert.All(evidence.ApprovedTree, page => Assert.InRange(page.Depth, 0, evidence.DepthBound));

        // The verification walk reaches every approved page and nothing else; all safe Settings pages.
        var resolvedPages = evidence.Observations
            .Select(CapstoneSettingsWorldFixture.ResolveSemanticPage)
            .Where(page => page is not null)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            evidence.ApprovedTree.Select(page => page.Name).Order(StringComparer.Ordinal),
            resolvedPages.Order(StringComparer.Ordinal));
        Assert.Equal(30, evidence.Observations.Length);
        for (var index = 1; index < evidence.Observations.Length; index++)
        {
            Assert.True(
                evidence.Observations[index].SequenceNumber > evidence.Observations[index - 1].SequenceNumber,
                "Observation sequences must be strictly increasing (deterministic world).");
        }
        Assert.All(
            evidence.Observations.Where(observation =>
                observation.SequenceNumber != CapstoneSettingsWorldFixture.DefaultDriftObservationSequence),
            observation => Assert.Equal("Settings", observation.ForegroundApplication));

        // Depth-bounded inventory evidence (CAND-008 surface): each approved page at its own semantic
        // depth yields its complete required-branch inventory, or the positive leaf marker.
        foreach (var page in evidence.ApprovedTree)
        {
            var pageObservation = evidence.Observations.First(observation =>
                CapstoneSettingsWorldFixture.ResolveSemanticPage(observation) == page.Name);
            var inventory = fixture.EvaluateInventory(ImmutableArray.Create(pageObservation), page.Depth);

            Assert.NotNull(inventory.RequiredBranchEvidence);
            if (page.IsLeaf)
            {
                Assert.Empty(inventory.RequiredBranchEvidence);
            }
            else
            {
                Assert.Equal(
                    page.RequiredBranches.Order(StringComparer.Ordinal),
                    inventory.RequiredBranchEvidence.Keys.Order(StringComparer.Ordinal));
                Assert.All(
                    page.RequiredBranches,
                    branch => Assert.Equal(pageObservation.SequenceNumber, inventory.RequiredBranchEvidence[branch]));
            }
        }

        // A depth beyond the bound proves no deeper approved inventory (honest depth boundary).
        var beyond = fixture.EvaluateInventory(
            ImmutableArray.Create(evidence.Observations[0]),
            CapstoneSettingsWorldFixture.DefaultDepthBound + 1);
        Assert.Null(beyond.RequiredBranchEvidence);
    }

    [Fact]
    public async Task DangerousCandidate_IsVisibleDeniedAndHasNoExecutableWorldEffect()
    {
        var fixture = CapstoneSettingsWorldFixture.Create();
        var probe = await fixture.ProbeDangerousCandidateAsync();

        // Visible: the destructive element is present on the approved Reset options page.
        Assert.Equal(
            CapstoneSettingsWorldFixture.ResetOptionsScreen,
            CapstoneSettingsWorldFixture.ResolveSemanticPage(probe.DangerousObservation));
        Assert.Equal(
            CapstoneSettingsWorldFixture.DangerousCandidateText,
            probe.DangerousElement.Text);
        Assert.Contains(
            probe.DangerousObservation.Elements,
            element => element.Text == CapstoneSettingsWorldFixture.DangerousCandidateText);

        // Denied: CAND-006 authorization evidence positively rejects the candidate.
        Assert.False(probe.Authorization.Authorized);
        Assert.False(string.IsNullOrWhiteSpace(probe.Authorization.Reason));

        // Not executable at the world level: the probe dispatch has no approved world transition —
        // the world stays on the same screen with the same elements (visible candidate != approved
        // executable action is a fixture property, not a production conclusion).
        Assert.Equal(ActionResultOutcome.Dispatched, probe.DangerousDispatch.Outcome);
        Assert.Equal(probe.DangerousObservation.Elements, probe.PostProbeObservation.Elements);
        Assert.Equal(
            CapstoneSettingsWorldFixture.ResetOptionsScreen,
            CapstoneSettingsWorldFixture.ResolveSemanticPage(probe.PostProbeObservation));

        // Zero dangerous dispatch in the verification walk: the only action dispatched while the
        // dangerous page is visible is the safe return tap (index 1).
        var evidence = await CapstoneSettingsWorldFixture.Create().RunAsync();
        Assert.Single(evidence.Observations, observation =>
            CapstoneSettingsWorldFixture.ResolveSemanticPage(observation)
                == CapstoneSettingsWorldFixture.ResetOptionsScreen);
        for (var index = 0; index < evidence.ActionHistory.Length; index++)
        {
            if (CapstoneSettingsWorldFixture.ResolveSemanticPage(evidence.Observations[index])
                != CapstoneSettingsWorldFixture.ResetOptionsScreen)
            {
                continue;
            }

            var tap = Assert.IsType<DeviceAction.Tap>(evidence.ActionHistory[index]);
            Assert.Equal(1, tap.TargetElementIndex);
        }
    }

    [Fact]
    public async Task PopupAndDrift_EachOccurExactlyOnceAtDeterministicSchedulePoints()
    {
        var fixture = CapstoneSettingsWorldFixture.Create();
        var evidence = await fixture.RunAsync();

        // Exactly one local Popup overlay observation at the deterministic schedule point.
        var popup = evidence.Observations.Single(observation =>
            observation.Elements.Any(element =>
                element.Text == CapstoneSettingsWorldFixture.PopupOverlayText));
        Assert.Equal(CapstoneSettingsWorldFixture.DefaultPopupObservationSequence, popup.SequenceNumber);
        Assert.Equal(fixture.Schedule.PopupObservationSequence, popup.SequenceNumber);
        Assert.Contains(popup.Elements, element => element.Text == CapstoneSettingsWorldFixture.DismissText);
        Assert.All(
            evidence.Observations.Where(observation => observation.SequenceNumber != popup.SequenceNumber),
            observation => Assert.DoesNotContain(
                observation.Elements,
                element => element.Text == CapstoneSettingsWorldFixture.PopupOverlayText));

        // World-side continuity: the observation after dismiss shows the same underlying page as
        // before the Popup (fresh observation, no fabricated page).
        var beforePopup = evidence.Observations.Single(observation =>
            observation.SequenceNumber == fixture.Schedule.PopupObservationSequence - 2);
        var afterDismiss = evidence.Observations.Single(observation =>
            observation.SequenceNumber == fixture.Schedule.PopupObservationSequence + 1);
        Assert.Equal(
            CapstoneSettingsWorldFixture.WifiPrefsScreen,
            CapstoneSettingsWorldFixture.ResolveSemanticPage(beforePopup));
        Assert.Equal(
            CapstoneSettingsWorldFixture.WifiPrefsScreen,
            CapstoneSettingsWorldFixture.ResolveSemanticPage(afterDismiss));
        Assert.True(afterDismiss.SequenceNumber > popup.SequenceNumber);

        // Exactly one external Launcher drift at the deterministic schedule point.
        var drift = evidence.Observations.Single(observation =>
            observation.ForegroundApplication == "Launcher");
        Assert.Equal(CapstoneSettingsWorldFixture.DefaultDriftObservationSequence, drift.SequenceNumber);
        Assert.Equal(fixture.Schedule.DriftObservationSequence, drift.SequenceNumber);
        Assert.Empty(drift.Elements);
        Assert.All(
            evidence.Observations.Where(observation => observation.SequenceNumber != drift.SequenceNumber),
            observation => Assert.NotEqual("Launcher", observation.ForegroundApplication));

        // World-side re-entry: the observation after LaunchApp is the re-entered trusted root with
        // fresh recovered-world evidence (drift occurs after the completed Network branch).
        var reentered = evidence.Observations.Single(observation =>
            observation.SequenceNumber == fixture.Schedule.DriftObservationSequence + 1);
        Assert.Equal(
            CapstoneSettingsWorldFixture.SettingsRootScreen,
            CapstoneSettingsWorldFixture.ResolveSemanticPage(reentered));
        Assert.True(reentered.SequenceNumber > drift.SequenceNumber);
        Assert.Contains(
            reentered.Elements,
            element => element.Text == CapstoneSettingsWorldFixture.RecoveredEvidenceText);
    }

    [Fact]
    public async Task EqualInputs_ReplayEqualWorld()
    {
        var first = await CapstoneSettingsWorldFixture.Create().RunAsync();
        var second = await CapstoneSettingsWorldFixture.Create().RunAsync();

        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.TraversalIntent, second.TraversalIntent);
        Assert.Equal(first.AllowedScope, second.AllowedScope);
        Assert.Equal(first.DepthBound, second.DepthBound);
        Assert.Equal(first.SafetyConstraints, second.SafetyConstraints);
        Assert.Equal(first.Schedule, second.Schedule);
        Assert.Equal(first.ApprovedTree, second.ApprovedTree);
        Assert.Equal(first.InitialPlan, second.InitialPlan);
        Assert.Equal(first.Observations.Length, second.Observations.Length);
        for (var index = 0; index < first.Observations.Length; index++)
        {
            Assert.Equal(
                first.Observations[index].SequenceNumber,
                second.Observations[index].SequenceNumber);
            Assert.Equal(
                first.Observations[index].ForegroundApplication,
                second.Observations[index].ForegroundApplication);
            Assert.Equal(first.Observations[index].Elements, second.Observations[index].Elements);
        }
        Assert.Equal(first.Dispatches.ToArray(), second.Dispatches.ToArray());
        Assert.Equal(first.ActionHistory.ToArray(), second.ActionHistory.ToArray());
        Assert.Equal(first.DangerousCandidateObservation.SequenceNumber, second.DangerousCandidateObservation.SequenceNumber);
        Assert.Equal(first.PopupObservation.SequenceNumber, second.PopupObservation.SequenceNumber);
        Assert.Equal(first.DriftObservation.SequenceNumber, second.DriftObservation.SequenceNumber);
    }

    [Fact]
    public void Fixture_EncodesNoProductionConclusionsAndNoPreEncodedRoute()
    {
        var fixture = CapstoneSettingsWorldFixture.Create();

        // No pre-enumerated route: the world's Plan is empty.
        Assert.Empty(fixture.InitialPlan.Steps);

        // The dangerous candidate is visible world data, never an approved branch of the tree.
        Assert.DoesNotContain(
            CapstoneSettingsWorldFixture.DangerousCandidateText,
            fixture.ApprovedTree.SelectMany(page => page.RequiredBranches));

        // No production conclusions in the fixture's or evidence records' exposed surface: no
        // Container identity, Recovery authority, progress, RunState, or completion-bearing types.
        string[] forbiddenTypeNames = ["Container", "Agent", "Recovery", "Traversal", "RunState", "BranchProgressEvidence"];
        string[] forbiddenPropertyNames = ["Completed", "Progress", "Route", "Succeeded"];
        foreach (var type in new[]
                 {
                     typeof(CapstoneSettingsWorldFixture),
                     typeof(CapstoneSettingsWorldEvidence),
                     typeof(CapstoneDangerousProbeEvidence),
                 })
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.False(
                    forbiddenTypeNames.Any(name => property.PropertyType.Name.Contains(name, StringComparison.Ordinal)),
                    $"{type.Name}.{property.Name} exposes production-conclusion type {property.PropertyType.Name}.");
                Assert.False(
                    forbiddenPropertyNames.Any(name => property.Name.Contains(name, StringComparison.Ordinal)),
                    $"{type.Name}.{property.Name} exposes a production-conclusion-shaped member.");
            }
        }
    }

    [Fact]
    public void World_AcceptsTraversalIntentScopeDepthBoundSafetyConstraintsAndScheduleInputs()
    {
        var schedule = new S0DisturbanceSchedule(9, CapstoneSettingsWorldFixture.WifiPrefsScreen, 21);
        var constraints = ImmutableArray.Create(
            CapstoneSettingsWorldFixture.DefaultSafetyConstraint,
            "Read-only traversal");
        var fixture = CapstoneSettingsWorldFixture.Create(
            runId: "custom-s0-run",
            traversalIntent: "Custom traversal intent",
            allowedScope: "Settings",
            depthBound: 4,
            safetyConstraints: constraints,
            schedule: schedule);

        Assert.Equal("custom-s0-run", fixture.RunId);
        Assert.Equal("Custom traversal intent", fixture.TraversalIntent);
        Assert.Equal("Settings", fixture.AllowedScope);
        Assert.Equal(4, fixture.DepthBound);
        Assert.Equal(constraints, fixture.SafetyConstraints);
        Assert.Equal(schedule, fixture.Schedule);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    [InlineData(-1)]
    public void World_RejectsDepthBoundBelowApprovedTreeDepth(int depthBound)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CapstoneSettingsWorldFixture.Create(depthBound: depthBound));
    }

    [Fact]
    public void World_RejectsBlankRunId()
    {
        Assert.Throws<ArgumentException>(() => CapstoneSettingsWorldFixture.Create(runId: "  "));
    }

    [Fact]
    public void World_RejectsScheduleWithCoincidentDisturbancePoints()
    {
        Assert.Throws<ArgumentException>(() => CapstoneSettingsWorldFixture.Create(
            schedule: new S0DisturbanceSchedule(9, CapstoneSettingsWorldFixture.WifiPrefsScreen, 9)));
    }

    [Fact]
    public void World_RejectsScheduleWithUnknownPopupDismissScreen()
    {
        Assert.Throws<ArgumentException>(() => CapstoneSettingsWorldFixture.Create(
            schedule: new S0DisturbanceSchedule(9, "UnknownScreen", 21)));
    }

    [Fact]
    public void World_RejectsEmptySafetyConstraints()
    {
        Assert.Throws<ArgumentException>(() => CapstoneSettingsWorldFixture.Create(
            safetyConstraints: ImmutableArray<string>.Empty));
    }
}
