using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// PHASE 4 — Settings as an external evidence fixture.
///
/// The Settings-shaped world (SettingsRoot → Network/System → Wi‑Fi toggle)
/// is expressed through the SAME generic <see cref="EvidenceFixture"/> model as
/// the scenario-neutral tree/diamond worlds, and validated by the SAME generic
/// <see cref="EvidenceRuntimeHost"/> and <see cref="EvidenceEvaluator"/>.
/// Settings vocabulary exists only as fixture data; the evaluation semantics are
/// identical to any other scenario.
/// </summary>
public sealed class SettingsAsEvidenceFixtureTests
{
    [Fact]
    public async Task SettingsFixture_GenericRuntime_CompletesWithGoalEvidence()
    {
        var host = EvidenceRuntimeHost.Create(SettingsEvidenceFixture.Create(), SettingsEvidenceFixture.Specification());
        var result = await host.RunAndEvaluateAsync(runId: "settings-fixture-1");

        Assert.True(result.Passed, result.Summary);
        Assert.Equal(RunState.Completed, result.TerminalState);

        // Coverage of the declared Settings scope, proven through evidence.
        Assert.Contains(SettingsEvidenceFixture.Root, result.CoveredContainers);
        Assert.Contains(SettingsEvidenceFixture.Network, result.CoveredContainers);
        Assert.Contains(SettingsEvidenceFixture.System, result.CoveredContainers);

        // Goal evidence: the Wi‑Fi toggle reached ON (switch-state observation evidence).
        Assert.True(result.GoalEvidenceSatisfied);
        Assert.Contains(host.EvidenceReceipts, e => e.Satisfied);

        // Belief consistency: ended on the root container.
        Assert.Equal(SettingsEvidenceFixture.Root, host.Agent?.Belief?.SemanticPage);
    }

    [Fact]
    public async Task SettingsFixture_EquivalentEvaluationSemantics_ToGenericWorlds()
    {
        var settingsHost = EvidenceRuntimeHost.Create(SettingsEvidenceFixture.Create(), SettingsEvidenceFixture.Specification());
        var settingsResult = await settingsHost.RunAndEvaluateAsync(runId: "settings-fixture-2");

        var genericHost = EvidenceRuntimeHost.Create(GenericTreeWorld.Create(), GenericTreeWorld.Specification());
        var genericResult = await genericHost.RunAndEvaluateAsync(runId: "generic-fixture-2");

        // Same evaluation semantics: completed + satisfied goal evidence,
        // coverage equals the declared scope (never a scripted count).
        Assert.True(settingsResult.Passed, settingsResult.Summary);
        Assert.True(genericResult.Passed, genericResult.Summary);
        Assert.Equal(RunState.Completed, settingsResult.TerminalState);
        Assert.Equal(RunState.Completed, genericResult.TerminalState);
        Assert.True(settingsResult.GoalEvidenceSatisfied);
        Assert.True(genericResult.GoalEvidenceSatisfied);
        Assert.Equal(SettingsEvidenceFixture.Specification().RequiredCoverage.Count, settingsResult.CoveredContainers.Count);
        Assert.Equal(GenericTreeWorld.Specification().RequiredCoverage.Count, genericResult.CoveredContainers.Count);
    }

    [Fact]
    public async Task SettingsFixture_NoScenarioKnowledgeInEvaluator()
    {
        // The generic evaluator must not depend on Settings vocabulary: the same
        // evaluator instance is used for the tree world where no Settings string exists.
        var treeHost = EvidenceRuntimeHost.Create(GenericTreeWorld.Create(), GenericTreeWorld.Specification());
        var result = await treeHost.RunAndEvaluateAsync(runId: "settings-neutral-1");

        // The tree world has zero Settings vocabulary; the run must still pass.
        Assert.True(result.Passed, result.Summary);
        Assert.DoesNotContain(result.Summary, "Settings", StringComparison.Ordinal);
    }
}
