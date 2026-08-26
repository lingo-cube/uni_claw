using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.ValidationHarness.Fixtures;

/// <summary>
/// Harness-local generic semantic capability binding for the Tier-A settings-like
/// fixture world (modeled on the Scenario test bindings; written against production
/// Runtime interfaces only). It supplies only the observation/evidence rules the
/// existing open-world pipeline needs for a deterministic depth-2 exploration:
///
/// SettingsRoot (depth 0) → two expandable child containers (connectivity,
/// display) → each child is a record-only leaf container (no required branch
/// expansion; leaves are never authorized or dispatched).
///
/// The binding never authors a strategy, never generates coordinates/paths/actions,
/// and never fabricates completion: it only interprets observations it truthfully
/// receives from the fixture world.
/// </summary>
public sealed class FixtureStrategyBinding : IStrategySemanticCapabilityBinding
{
    /// <summary>Fixture semantic capability identity.</summary>
    public const string SemanticCapabilityId = "fixture.semantic.settings";

    /// <summary>Fixture application identity (Startup target + foreground ownership).</summary>
    public const string Application = "UniClawValidationApp";

    /// <summary>Fixture semantic root container identity.</summary>
    public const string Root = "SettingsRoot";

    /// <summary>First expandable child branch label and its semantic page identity.</summary>
    public const string ChildOne = "connectivity";
    public const string ChildOnePage = "ConnectivitySettings";

    /// <summary>Second expandable child branch label and its semantic page identity.</summary>
    public const string ChildTwo = "display";
    public const string ChildTwoPage = "DisplaySettings";

    /// <summary>Labelled parent-return control on every child container.</summary>
    public const string ParentReturnLabel = "Back";

    /// <summary>Record-only leaf labels on the child containers.</summary>
    public const string LeafOneA = "wifi-item";
    public const string LeafOneB = "airplane-item";
    public const string LeafTwoA = "brightness-item";
    public const string LeafTwoB = "font-item";

    /// <inheritdoc />
    public string CapabilityId => SemanticCapabilityId;

    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public ExplorationIntent Exploration => ExplorationIntent.ExhaustiveWithinScope;

    /// <inheritdoc />
    public bool SupportsUnqualifiedObjective => true;

    /// <inheritdoc />
    public bool SupportsCriterion(string criterionId) => false;

    /// <inheritdoc />
    public bool SupportsCompletion(StrategyCompletionKind completion)
        => completion == StrategyCompletionKind.ExhaustiveCoverageWithinScope;

    /// <inheritdoc />
    public Goal CreateGoal(StrategyDirective strategy) => new(
        EvidenceEvaluator: observation => new GoalEvidence(
            Satisfied: string.Equals(ResolvePage(observation), Root, StringComparison.Ordinal),
            Reason: "Generic bounded strategy evidence satisfied at the fixture scope root.",
            SourceObservationSequence: observation.SequenceNumber),
        CandidateAuthorizationEvaluator: EvaluateAuthorization,
        ViewportExplorationEvaluator: null,
        BranchInventoryEvaluator: EvaluateInventory,
        DiscoveredBranchEffectCriterion: null,
        CategoryClassifier: element => element.Text is ChildOne or ChildTwo or ParentReturnLabel
            ? TypeLevelElementCategory.NavigableContainer
            : null);

    /// <inheritdoc />
    public TypeLevelDispatchPolicy? CreateDispatchPolicy(StrategyDirective strategy) => new(
        ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>.Empty
            .Add(TypeLevelElementCategory.NavigableContainer, TypeLevelHandling.EnterAndTraverse));

    /// <summary>Fixture semantic page resolver (Observation → page identity).</summary>
    public static string? ResolvePage(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!string.Equals(observation.ForegroundApplication, Application, StringComparison.Ordinal))
            return null;
        if (observation.Elements.Any(element => element.Text is LeafOneA or LeafOneB))
            return ChildOnePage;
        if (observation.Elements.Any(element => element.Text is LeafTwoA or LeafTwoB))
            return ChildTwoPage;
        if (observation.Elements.Any(element => element.Text is ChildOne or ChildTwo))
            return Root;
        return null;
    }

    private static CandidateAuthorizationEvidence EvaluateAuthorization(
        Observation observation,
        ObservedElement candidate)
        => new(
            candidate.Text is ChildOne or ChildTwo or ParentReturnLabel,
            candidate.Text is ChildOne or ChildTwo or ParentReturnLabel
                ? "Generic candidate is inside the fixture world strategy boundary."
                : "Generic candidate is outside the fixture world strategy boundary.");

    private static BranchInventoryEvidence EvaluateInventory(
        ImmutableArray<Observation> observations,
        int semanticDepth)
    {
        if (observations.IsDefaultOrEmpty)
            return new BranchInventoryEvidence(null, "No accepted fixture world evidence is available.");

        var current = observations[^1];
        if (semanticDepth == 0)
        {
            var ids = current.Elements
                .Where(element => element.Text is ChildOne or ChildTwo)
                .ToImmutableDictionary(element => element.Text, _ => current.SequenceNumber);
            if (ids.Count == 0)
                return new BranchInventoryEvidence(null, "Root fixture inventory is unresolved: no bounded child container observed.");
            var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(current)
                .Where(item => ids.ContainsKey(current.Elements[item.CanonicalOccurrence.Reference.ElementIndex].Text))
                .ToImmutableDictionary(
                    item => current.Elements[item.CanonicalOccurrence.Reference.ElementIndex].Text,
                    item => new NavigationSourceOccurrenceReference(current.SequenceNumber, item.OccurrenceIdentity));
            return new BranchInventoryEvidence(
                ids,
                "Root fixture inventory contains two bounded expandable child containers.",
                occurrences);
        }

        // semanticDepth >= 1: fixture children are record-only leaves — no required
        // branch expansion, so the leaf elements are never dispatched.
        return new BranchInventoryEvidence(
            ImmutableDictionary<string, long>.Empty,
            "Fixture child is a record-only leaf; no required branch expansion.");
    }
}