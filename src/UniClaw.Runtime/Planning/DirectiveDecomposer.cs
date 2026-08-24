using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// The deterministic result of decomposing a bounded exploration
/// <see cref="Directive"/> into as many open-world execution inputs as the
/// caller's injected rules make resolvable.
/// </summary>
public abstract record DirectiveDecompositionResult
{
    private DirectiveDecompositionResult()
    {
    }

    /// <summary>Complete decomposition into exactly one spec and one type-directed Goal.</summary>
    public sealed record Resolved : DirectiveDecompositionResult
    {
        /// <summary>Creates a resolved decomposition.</summary>
        public Resolved(TypeLevelTraversalSpecification specification, Goal goal)
        {
            ArgumentNullException.ThrowIfNull(specification);
            ArgumentNullException.ThrowIfNull(goal);
            Specification = specification;
            Goal = goal;
        }

        /// <summary>Open-world traversal specification projected from the directive boundaries.</summary>
        public TypeLevelTraversalSpecification Specification { get; }
        /// <summary>Type-directed Goal whose evaluators are the caller-injected rules.</summary>
        public Goal Goal { get; }
    }

    /// <summary>An explicit non-executable receipt. It contains no spec and no goal.</summary>
    public sealed record Insufficient : DirectiveDecompositionResult
    {
        /// <summary>Creates a non-executable insufficiency receipt.</summary>
        public Insufficient(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            Reason = reason;
        }

        /// <summary>Explicit reason no execution inputs were produced.</summary>
        public string Reason { get; }
    }
}

/// <summary>
/// Bounded, stateless decomposition from a bounded exploration directive to the
/// existing open-world execution inputs. It mirrors the <see cref="IntentCompiler"/>
/// discipline: it never observes the world, never selects a UI target, never
/// constructs a route, and never invents a strategy rule — it projects only
/// already-authoritative caller input 1:1. It holds no mutable state and
/// participates in no decision; the RuntimeAgent keeps sole run-level authority.
/// </summary>
public static class DirectiveDecomposer
{
    /// <summary>
    /// Projects a <see cref="Directive"/> into exactly one
    /// <see cref="TypeLevelTraversalSpecification"/> and one type-directed
    /// <see cref="Goal"/>, or returns <see cref="DirectiveDecompositionResult.Insufficient"/>
    /// with no execution inputs when a rule required by the declared completion
    /// requirement is missing. Never throws for malformed-but-constructible input.
    /// </summary>
    public static DirectiveDecompositionResult Decompose(Directive directive)
    {
        ArgumentNullException.ThrowIfNull(directive);

        var rules = directive.StrategyRules;
        // Required for ExhaustiveWithinScope decomposition: a Goal cannot be
        // projected without a completion-criterion rule, and the bounded DFS
        // path requires authorization + inventory rules (never synthesized).
        if (rules.EvidenceEvaluator is null
            || rules.CandidateAuthorizationEvaluator is null
            || rules.BranchInventoryEvaluator is null)
        {
            return new DirectiveDecompositionResult.Insufficient(
                "Directive missing a required strategy rule for ExhaustiveWithinScope decomposition: "
                + "completion (EvidenceEvaluator), candidate-authorization, and branch-inventory rules are required.");
        }

        var specification = new TypeLevelTraversalSpecification(
            directive.Scope,
            directive.Safety.AllowedInteractionCategories,
            directive.MaximumDepth,
            directive.Safety,
            directive.Completion,
            directive.Entry,
            directive.DispatchPolicy);

        var goal = new Goal(
            rules.EvidenceEvaluator,
            rules.CandidateAuthorizationEvaluator,
            rules.ViewportExplorationEvaluator,
            rules.BranchInventoryEvaluator,
            DiscoveredBranchEffectCriterion: null,
            rules.CategoryClassifier);

        return new DirectiveDecompositionResult.Resolved(specification, goal);
    }
}
