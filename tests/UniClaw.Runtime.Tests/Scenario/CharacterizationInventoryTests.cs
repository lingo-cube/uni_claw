using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// Temporary migration inventory for OpenSpec task 1.4. These tests record where
/// scenario-package evidence currently lives; they are not Runtime semantic contracts
/// and intentionally do not require a physical device or emulator.
/// </summary>
[Trait("Characterization", "ScenarioPackage")]
public sealed class CharacterizationInventoryTests
{
    [Fact]
    [Trait("Evidence", "SETTINGS-TREE-01")]
    public void SettingsTreeCharacterizationEntryPointExists()
        => AssertScenarioType("UniClaw.Runtime.Tests.Scenario.SettingsTreeCapstoneTests");

    [Fact]
    [Trait("Evidence", "ParentReturn")]
    public void ParentReturnCharacterizationEntryPointExists()
        => AssertScenarioType("UniClaw.Runtime.Tests.Scenario.ParentReturnControlResolutionTests");

    [Fact]
    [Trait("Evidence", "ScrollContinuity")]
    public void ScrollContinuityCharacterizationEntryPointsExist()
    {
        AssertScenarioType("UniClaw.Runtime.Tests.Perception.FastSemanticContainerIdentityTests");
        AssertScenarioType("UniClaw.Runtime.Tests.Unit.ContainerTests");
        AssertScenarioType("UniClaw.Runtime.Tests.Unit.FreshContainerEvidenceTests");
    }

    [Fact]
    [Trait("Evidence", "RuntimeAgentPhase1-4")]
    public void RuntimeAgentPhaseContractsRemainRepresentedByTypedModels()
    {
        Assert.NotNull(typeof(ExecutionHypothesis));
        Assert.NotNull(typeof(RuntimeDecision));
        Assert.NotNull(typeof(HypothesisAdaptation));
        Assert.NotNull(typeof(PreTerminalContinuationProposal));
    }

    [Fact]
    [Trait("Evidence", "StrategyContract")]
    public void StrategyContractCharacterizationEntryPointsExist()
    {
        AssertScenarioType("UniClaw.Runtime.Tests.Unit.StrategyContractTests");
        AssertScenarioType("UniClaw.Runtime.Tests.Strategy.StrategyExecutionLoopContractTests");
        Assert.NotNull(typeof(StrategyDirective));
        Assert.NotNull(typeof(RuntimeExecutionIntent));
    }

    [Fact]
    [Trait("Evidence", "NoPhysicalDevice")]
    public void InventoryDoesNotClaimPhysicalDeviceEvidence()
    {
        // Characterization inventory is metadata only. Physical-host tests remain
        // separately opt-in and a missing emulator is never interpreted as success.
        Assert.True(System.Environment.GetEnvironmentVariable("UNICLAW_CHARACTERIZATION_DEVICE") is null);
    }

    private static void AssertScenarioType(string fullName)
    {
        var type = typeof(CharacterizationInventoryTests).Assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.NotEmpty(type!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<FactAttribute>() is not null));
    }
}
