using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.ValidationHarness.Fixtures;

/// <summary>
/// Recorded goal → directive pair driving deterministic-mode transports
/// (design D2; task 3.3). Only the directive SOURCE differs from live mode:
/// the validation / transport / call-log path is identical.
/// </summary>
public sealed record DirectiveFixtureRecord(
    string Goal,
    StrategyDirective? Directive,
    string Device);

/// <summary>
/// Recorded directive fixtures for the Tier-A settings world. The directive
/// shape mirrors the accepted round-trip directive (depth 1, exhaustive within
/// scope, record-only leaves) so the fixture device deterministically Accepts
/// strategy admission. The fixture module authors directives; the driver never
/// does.
/// </summary>
public static class DirectiveFixtureCatalog
{
    /// <summary>Recorded legal directive for the fixture device (Accept expected).</summary>
    public static DirectiveFixtureRecord SettingsExplore(string? strategyId = null)
        => new(
            "Explore the settings scope and record everything reachable (deterministic fixture goal).",
            BuildLegalDirective(strategyId ?? "evh-fixture-settings-1"),
            FixtureComposition.FixtureDeviceText);

    /// <summary>Recorded depth-2 directive for the fixture device (S1 settings
    /// exploration: container-expand at depth 0–1, record-only leaves at depth 2;
    /// exhaustive within scope, zero state mutation, zero boundary crossing).</summary>
    public static DirectiveFixtureRecord SettingsExploreDepth2(string? strategyId = null)
        => new(
            "Explore the settings scope two levels deep and record everything reachable (deterministic fixture goal).",
            BuildLegalDirective(strategyId ?? "evh-fixture-settings-depth2", maximumDepth: 2),
            FixtureComposition.FixtureDeviceText);

    /// <summary>Goal-only record: no directive — the driver must answer
    /// DIRECTIVE_REQUIRED and never synthesize a strategy.</summary>
    public static DirectiveFixtureRecord GoalOnly()
        => new(
            "Explore the settings scope and record everything reachable (deterministic fixture goal, no directive).",
            null,
            FixtureComposition.FixtureDeviceText);

    /// <summary>
    /// Minimal legal directive for the fixture scope — the same closed shape
    /// the round-trip test transports (deterministic Accept on the fixture
    /// device): declared depth (default 1), exhaustive within scope, record-only
    /// leaf children, navigation-only interaction, ReconcileBelief permitted.
    /// </summary>
    public static StrategyDirective BuildLegalDirective(string strategyId, int maximumDepth = 1,
        string? application = null, string? semanticRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyId);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDepth, 1);
        return new StrategyDirective(
            strategyId,
            contractVersion: StrategyContractCompiler.SupportedContractVersion,
            objective: new StrategyObjective(StrategyObjectiveKind.ExploreScope),
            scope: new StrategyScope(
                application ?? FixtureStrategyBinding.Application,
                semanticRoot ?? FixtureStrategyBinding.Root,
                maximumDepth),
            exploration: ExplorationIntent.ExhaustiveWithinScope,
            constraints: new StrategyConstraintSet(
                ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
                ImmutableHashSet.Create(
                    StrategyProhibitedEffect.StateMutation,
                    StrategyProhibitedEffect.ExternalBoundaryCrossing)),
            completion: new StrategyCompletionCriteria(StrategyCompletionKind.ExhaustiveCoverageWithinScope),
            adaptation: new StrategyAdaptationBoundary(
                ImmutableHashSet.Create(
                    StrategyAdaptationKind.ReconcileBelief,
                    StrategyAdaptationKind.ReviseExecutionHypothesis)));
    }
}