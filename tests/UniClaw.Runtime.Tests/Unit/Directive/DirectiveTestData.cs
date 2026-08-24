using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Shared deterministc construction data for the directive-decomposition unit
/// tests. All strategy rules are trivially deterministic (no world observation).
/// </summary>
internal static class DirectiveTestData
{
    internal const string App = "Settings";
    internal const string Root = "SettingsRoot";
    internal const string BranchA = "Safe section A";
    internal const string BranchB = "Safe section B";
    internal const string DangerousCandidate = "Factory reset";

    internal static TypeLevelTaskScope Scope => new(App, Root);

    internal static TypeLevelEntryBoundary Entry => new(App, Root);

    internal static TypeLevelSafetyBoundary NavigableSafety()
        => new(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer));

    internal static TypeLevelDispatchPolicy NavigableEnterDispatch()
        => new(
            ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>
                .Empty
                .Add(TypeLevelElementCategory.NavigableContainer, TypeLevelHandling.EnterAndTraverse));

    internal static GoalEvidence EvaluateEvidence(Observation observation)
        => new(true, "Directive completion criterion satisfied.", observation.SequenceNumber);

    internal static CandidateAuthorizationEvidence EvaluateAuthorization(
        Observation observation,
        ObservedElement candidate)
        => new(
            candidate.Text is BranchA or BranchB or Root,
            candidate.Text is BranchA or BranchB or Root
                ? "Explicitly authorized navigation-only candidate."
                : "Outside the authorized navigation-only boundary.");

    internal static BranchInventoryEvidence EvaluateInventory(
        ImmutableArray<Observation> observations,
        int semanticDepth)
        => new(ImmutableDictionary<string, long>.Empty, "Bounded leaf inventory; no required child.");

    internal static ViewportExplorationEvidence EvaluateViewport(
        ImmutableArray<Observation> observations)
        => new(false, "Deterministic viewport exhaustion.");

    internal static TypeLevelElementCategory? ClassifyNavigable(ObservedElement element)
        => element.Text is BranchA or BranchB or Root
            ? TypeLevelElementCategory.NavigableContainer
            : null;

    internal static DirectiveStrategyRules Rules(
        bool includeViewport = false,
        bool includeClassifier = false)
        => new(
            EvaluateEvidence,
            EvaluateAuthorization,
            BranchInventoryEvaluator: EvaluateInventory,
            ViewportExplorationEvaluator: includeViewport ? EvaluateViewport : null,
            CategoryClassifier: includeClassifier ? ClassifyNavigable : null);

    internal static Directive ValidDirective(int maximumDepth = 1)
        => new(
            Scope,
            Entry,
            maximumDepth,
            NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            Rules());
}
