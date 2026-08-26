using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Planning;

/// <summary>Stable fail-closed admission outcomes for the Strategy Contract.</summary>
public enum StrategyRejectionCode
{
    /// <summary>The typed fields are incomplete or mutually inconsistent.</summary>
    Malformed = 1,

    /// <summary>The Strategy Contract version is not implemented.</summary>
    UnsupportedContractVersion = 2,

    /// <summary>No composition-provided capability can interpret the objective.</summary>
    UnsupportedCapability = 3,

    /// <summary>The selected capability cannot interpret the typed criterion.</summary>
    UnsupportedCriterion = 4,

    /// <summary>The selected capability cannot produce the declared completion evidence.</summary>
    UnverifiableCompletion = 5,

    /// <summary>Two immutable strategy boundaries contradict each other.</summary>
    BoundaryConflict = 6,

    /// <summary>The selected device cannot admit the strategy Run.</summary>
    DeviceUnavailable = 7,

    /// <summary>The UniAgent strategy identity has already created a Run.</summary>
    DuplicateStrategy = 8,
}

/// <summary>
/// Composition-owned generic semantic capability. Implementations supply only the
/// observation/evidence rules needed by the existing open-world pipeline; the wire
/// contract never carries these delegates.
/// </summary>
public interface IStrategySemanticCapabilityBinding
{
    /// <summary>Stable capability identity.</summary>
    string CapabilityId { get; }

    /// <summary>Implemented capability contract version.</summary>
    int Version { get; }

    /// <summary>Exploration approach this binding can interpret.</summary>
    ExplorationIntent Exploration { get; }

    /// <summary>Whether this binding is the composition's unqualified exhaustive explorer.</summary>
    bool SupportsUnqualifiedObjective { get; }

    /// <summary>Whether a typed criterion id is supported.</summary>
    bool SupportsCriterion(string criterionId);

    /// <summary>Whether the binding can produce the declared completion evidence.</summary>
    bool SupportsCompletion(StrategyCompletionKind completion);

    /// <summary>Create generic Goal evaluators for the already-validated strategy.</summary>
    Goal CreateGoal(StrategyDirective strategy);

    /// <summary>Create an optional category handling policy bounded by the strategy constraints.</summary>
    TypeLevelDispatchPolicy? CreateDispatchPolicy(StrategyDirective strategy);
}

/// <summary>Admission and interpretation result; rejection contains no executable intent.</summary>
public abstract record StrategyCompilationResult
{
    private StrategyCompilationResult()
    {
    }

    /// <summary>Accepted immutable runtime-local execution intent.</summary>
    public sealed record Accepted(RuntimeExecutionIntent Intent) : StrategyCompilationResult;

    /// <summary>Fail-closed rejection with stable code and bounded reason.</summary>
    public sealed record Rejected(StrategyRejectionCode Code, string Reason) : StrategyCompilationResult;
}

/// <summary>Validated strategy plus its composition-owned capability binding.</summary>
internal sealed record ValidatedStrategy(
    StrategyDirective Strategy,
    IStrategySemanticCapabilityBinding Binding);

/// <summary>
/// Runtime-local, immutable semantic execution description. It contains no
/// DeviceAction, route, selector, lifecycle command, RunState, or completion fact.
/// The internal specification and Goal are consumed only by the existing Agent seam.
/// </summary>
public sealed record RuntimeExecutionIntent
{
    internal RuntimeExecutionIntent(
        StrategyDirective strategy,
        TypeLevelTraversalSpecification specification,
        Goal goal,
        ExplorationExecutionSemantics explorationSemantics)
    {
        ArgumentNullException.ThrowIfNull(explorationSemantics);
        Strategy = strategy;
        Specification = specification;
        Goal = goal;
        ExplorationSemantics = explorationSemantics;
    }

    /// <summary>Immutable UniAgent-authored boundary interpreted by this intent.</summary>
    public StrategyDirective Strategy { get; }

    /// <summary>Stable strategy identity; never a Run identity or action identity.</summary>
    public string StrategyId => Strategy.StrategyId;

    /// <summary>Runtime-local adaptation permissions.</summary>
    public StrategyAdaptationBoundary Adaptation => Strategy.Adaptation;

    internal TypeLevelTraversalSpecification Specification { get; }

    internal Goal Goal { get; }

    /// <summary>Admission-derived immutable exploration interpretation.</summary>
    internal ExplorationExecutionSemantics ExplorationSemantics { get; }
}

/// <summary>Bounded runtime-local reason that a hypothesis revision is not permitted.</summary>
public sealed record StrategyBoundaryViolation(
    StrategyAdaptationKind RequiredAdaptation,
    string DecisionReference,
    string Reason);

/// <summary>
/// Passive strategy reasoning receipt. It reports reconciliation/adaptation only and
/// deliberately contains no RunState, completion flag, action, or lifecycle command.
/// </summary>
public sealed record StrategyReasoningResult(
    RuntimeDecision? Decision,
    HypothesisAdaptation? Adaptation,
    StrategyBoundaryViolation? BoundaryViolation,
    StrategyExecutionReasoningReceipt? Receipt = null);

/// <summary>Read-only terminal receipt for one sealed Strategy reasoning session.</summary>
public sealed record StrategyExecutionReasoningReceipt(
    string StrategyExecutionId,
    string RunId,
    string RuntimeExecutionIntentReference,
    IReadOnlyList<AcceptedReasoningRevision> AcceptedRevisions,
    DateTimeOffset SealedAt,
    bool IsSealed);

/// <summary>Pure permission check for one requested runtime-local adaptation class.</summary>
internal static class StrategyBoundaryGuard
{
    internal static StrategyBoundaryViolation? Check(
        StrategyAdaptationBoundary boundary,
        StrategyAdaptationKind requested,
        string decisionReference)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (!Enum.IsDefined(requested))
            throw new ArgumentOutOfRangeException(nameof(requested));
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionReference);

        return boundary.Allows(requested)
            ? null
            : new StrategyBoundaryViolation(
                requested,
                decisionReference,
                $"Runtime-local adaptation '{requested}' is outside the accepted strategy boundary.");
    }
}

/// <summary>Pure strategy admission and interpretation into the existing open-world inputs.</summary>
public sealed class StrategyContractCompiler
{
    /// <summary>Current closed Strategy Contract version.</summary>
    public const int SupportedContractVersion = 1;

    /// <summary>Hard finite depth guard; larger requests fail closed as unbounded.</summary>
    public const int MaximumSupportedDepth = 64;

    private readonly IReadOnlyList<IStrategySemanticCapabilityBinding> _bindings;

    /// <summary>Create a compiler over composition-provided generic bindings.</summary>
    public StrategyContractCompiler(IEnumerable<IStrategySemanticCapabilityBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        _bindings = bindings.ToArray();
        if (_bindings.Any(binding => binding is null))
            throw new ArgumentException("Capability bindings must not contain null.", nameof(bindings));
    }

    /// <summary>Validate and interpret one immutable strategy, or reject without execution.</summary>
    public StrategyCompilationResult Compile(StrategyDirective strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        var validation = Validate(strategy);
        if (validation is not null)
            return validation;

        var bindingResult = ResolveBinding(strategy);
        if (bindingResult.Rejection is not null)
            return bindingResult.Rejection;

        var validated = new ValidatedStrategy(strategy, bindingResult.Binding!);
        return Interpret(validated);
    }

    private static StrategyCompilationResult.Rejected? Validate(StrategyDirective strategy)
    {
        if (strategy.ContractVersion != SupportedContractVersion)
        {
            return Reject(
                StrategyRejectionCode.UnsupportedContractVersion,
                $"Strategy Contract version {strategy.ContractVersion} is unsupported.");
        }

        if (strategy.Scope.MaximumDepth < 0 || strategy.Scope.MaximumDepth > MaximumSupportedDepth)
        {
            return Reject(
                StrategyRejectionCode.Malformed,
                $"Strategy scope depth {strategy.Scope.MaximumDepth} exceeds the finite maximum {MaximumSupportedDepth}.");
        }

        if (!strategy.Adaptation.Allows(StrategyAdaptationKind.ReconcileBelief))
        {
            return Reject(
                StrategyRejectionCode.BoundaryConflict,
                "The strategy must permit WorldBelief reconciliation for runtime interpretation.");
        }

        if (strategy.Constraints.ProhibitedEffects.Contains(StrategyProhibitedEffect.StateMutation)
            && strategy.Constraints.AllowedInteractionCategories.Contains(TypeLevelElementCategory.StateChangingControl))
        {
            return Reject(
                StrategyRejectionCode.BoundaryConflict,
                "State-changing controls are allowed while state mutation is explicitly prohibited.");
        }

        if (strategy.Objective.Kind == StrategyObjectiveKind.ExploreScope)
        {
            if (strategy.Objective.Criterion is not null
                || strategy.Exploration != ExplorationIntent.ExhaustiveWithinScope
                || strategy.Completion.Kind != StrategyCompletionKind.ExhaustiveCoverageWithinScope)
            {
                return Reject(
                    StrategyRejectionCode.BoundaryConflict,
                    "ExploreScope requires unqualified exhaustive exploration and exhaustive in-scope completion evidence.");
            }
        }
        else if (strategy.Objective.Kind == StrategyObjectiveKind.InspectMatchesWithinScope)
        {
            if (strategy.Objective.Criterion is null)
            {
                return Reject(
                    StrategyRejectionCode.Malformed,
                    "InspectMatchesWithinScope requires a typed semantic criterion reference; unresolved intent is not inferred.");
            }

            if (strategy.Exploration != ExplorationIntent.InspectMatchesWithinScope
                || strategy.Completion.Kind != StrategyCompletionKind.AllDiscoveredMatchesInspected)
            {
                return Reject(
                    StrategyRejectionCode.BoundaryConflict,
                    "InspectMatchesWithinScope requires matching exploration and completion semantics.");
            }
        }

        return null;
    }

    private (IStrategySemanticCapabilityBinding? Binding, StrategyCompilationResult.Rejected? Rejection) ResolveBinding(
        StrategyDirective strategy)
    {
        var criterion = strategy.Objective.Criterion;
        IStrategySemanticCapabilityBinding[] candidates;

        if (criterion is null)
        {
            candidates = _bindings
                .Where(binding => binding.Exploration == strategy.Exploration
                    && binding.SupportsUnqualifiedObjective)
                .ToArray();
        }
        else
        {
            candidates = _bindings
                .Where(binding => string.Equals(binding.CapabilityId, criterion.CapabilityId, StringComparison.Ordinal)
                    && binding.Version == criterion.Version
                    && binding.Exploration == strategy.Exploration)
                .ToArray();
        }

        if (candidates.Length != 1)
        {
            return (null, Reject(
                StrategyRejectionCode.UnsupportedCapability,
                candidates.Length == 0
                    ? "No compatible semantic capability binding is available."
                    : "Semantic capability binding is ambiguous for the declared strategy."));
        }

        var binding = candidates[0];
        if (criterion is not null && !binding.SupportsCriterion(criterion.CriterionId))
        {
            return (null, Reject(
                StrategyRejectionCode.UnsupportedCriterion,
                $"Semantic criterion '{criterion.CriterionId}' is not supported by capability '{criterion.CapabilityId}'."));
        }

        if (!binding.SupportsCompletion(strategy.Completion.Kind))
        {
            return (null, Reject(
                StrategyRejectionCode.UnverifiableCompletion,
                $"Capability '{binding.CapabilityId}' cannot produce '{strategy.Completion.Kind}' evidence."));
        }

        return (binding, null);
    }

    private static StrategyCompilationResult Interpret(ValidatedStrategy validated)
    {
        var strategy = validated.Strategy;
        var safety = new TypeLevelSafetyBoundary(strategy.Constraints.AllowedInteractionCategories);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(strategy.Scope.ApplicationIdentity, strategy.Scope.SemanticRoot),
            strategy.Constraints.AllowedInteractionCategories,
            strategy.Scope.MaximumDepth,
            safety,
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(strategy.Scope.ApplicationIdentity, strategy.Scope.SemanticRoot),
            validated.Binding.CreateDispatchPolicy(strategy));

        var goal = validated.Binding.CreateGoal(strategy)
            ?? throw new InvalidOperationException("A strategy capability binding returned no Goal.");

        var semanticsResult = DeriveExplorationSemantics(strategy);
        if (semanticsResult.Rejection is not null)
            return semanticsResult.Rejection;

        return new StrategyCompilationResult.Accepted(
            new RuntimeExecutionIntent(strategy, specification, goal, semanticsResult.Semantics!));
    }

    private static (ExplorationExecutionSemantics? Semantics, StrategyCompilationResult.Rejected? Rejection)
        DeriveExplorationSemantics(StrategyDirective strategy)
    {
        var depth = strategy.Scope.MaximumDepth;
        var exhaustive = strategy.Objective.Kind == StrategyObjectiveKind.ExploreScope
            && strategy.Objective.Criterion is null
            && strategy.Exploration == ExplorationIntent.ExhaustiveWithinScope
            && strategy.Completion.Kind == StrategyCompletionKind.ExhaustiveCoverageWithinScope;
        var matching = strategy.Objective.Kind == StrategyObjectiveKind.InspectMatchesWithinScope
            && strategy.Objective.Criterion is not null
            && strategy.Exploration == ExplorationIntent.InspectMatchesWithinScope
            && strategy.Completion.Kind == StrategyCompletionKind.AllDiscoveredMatchesInspected;

        var boundary = depth switch
        {
            0 or 1 when exhaustive || matching => ExplorationBoundaryDisposition.RecordOnly,
            >= 2 when exhaustive => ExplorationBoundaryDisposition.FailClosed,
            >= 2 when matching => ExplorationBoundaryDisposition.RecordOnly,
            _ => (ExplorationBoundaryDisposition?)null,
        };

        if (boundary is null)
        {
            return (null, Reject(
                StrategyRejectionCode.BoundaryConflict,
                "The accepted objective, exploration, completion, and depth tuple has no closed exploration interpretation."));
        }

        var depthSemantics = depth switch
        {
            0 => ExplorationDepthSemantics.RootRecordOnly,
            1 => ExplorationDepthSemantics.RootAndDirectChildren,
            >= 2 => ExplorationDepthSemantics.BoundedRecursive,
            _ => throw new InvalidOperationException("Negative strategy depth must be rejected before interpretation."),
        };
        return (new ExplorationExecutionSemantics(
            strategy.StrategyId,
            strategy.StrategyId,
            ExplorationRule.ExpandContainer,
            ExplorationRule.RecordOnly,
            depthSemantics,
            boundary.Value,
            depth), null);
    }

    private static StrategyCompilationResult.Rejected Reject(StrategyRejectionCode code, string reason)
        => new(code, reason);
}

/// <summary>Pure permission check around the existing hypothesis adapter.</summary>
internal static class StrategyHypothesisAdapter
{
    internal abstract record Result
    {
        private Result()
        {
        }

        internal sealed record Adapted(HypothesisAdaptation Adaptation) : Result;
        internal sealed record Blocked(StrategyBoundaryViolation Violation) : Result;
    }

    internal static Result Evaluate(
        StrategyAdaptationBoundary boundary,
        RuntimeDecision decision,
        ExecutionHypothesis current)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(current);

        if (decision.State == RuntimeDecisionState.Continue)
            return new Result.Adapted(HypothesisAdapter.Adapt(decision, current));

        if (decision.State == RuntimeDecisionState.Revise
            && StrategyBoundaryGuard.Check(
                boundary,
                StrategyAdaptationKind.ReviseExecutionHypothesis,
                decision.HypothesisReference) is null)
        {
            return new Result.Adapted(HypothesisAdapter.Adapt(decision, current));
        }

        var required = StrategyAdaptationKind.ReviseExecutionHypothesis;
        var reason = decision.State == RuntimeDecisionState.Escalate
            ? "Runtime decision exceeds the accepted strategy authority; revision or escalation is required."
            : "Execution-hypothesis revision is outside the accepted adaptation boundary.";
        return new Result.Blocked(new StrategyBoundaryViolation(
            required,
            decision.HypothesisReference,
            reason));
    }
}

/// <summary>
/// Authority-preserving execution adapter for an accepted runtime intent. It calls
/// the existing open-world Agent seam and performs passive pre-terminal reasoning.
/// </summary>
public static class StrategyExecution
{
    /// <summary>Execute one accepted strategy inside exactly one caller-owned Run identity.</summary>
    public static async Task<StrategyReasoningResult> RunAsync(
        RuntimeAgent agent,
        RuntimeExecutionIntent intent,
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var session = new StrategyExecutionReasoningSession(intent, runId);
        if (!agent.TryBindPreTerminalReasoningEvaluator(session))
            throw new InvalidOperationException("Strategy execution requires an idle Agent without an existing reasoning evaluator.");
        var envelope = IntentSemanticEnvelope.Project(
            "UniAgent-authored bounded strategy",
            intent.Goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(intent.Specification));

        StrategyExecutionReasoningReceipt receipt;
        try
        {
            _ = await IntentExecution.RunStrategyOpenWorldAsync(agent, envelope, runId, intent.ExplorationSemantics, cancellationToken);
        }
        finally
        {
            receipt = session.Seal();
        }
        var accepted = session.AcceptedHistory.Last();
        return new StrategyReasoningResult(accepted.Decision, accepted.Adaptation, accepted.BoundaryViolation, receipt);
    }
}
