using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// Deterministic comparable facts of a <see cref="StrategyDirective"/> over the
/// compared levers (spec "PlanDelta contract"; design D5 eight-freedom surface).
/// Comparisons are explicit per-lever CONTENT comparisons (sets via
/// SetEquals — never reference equality), so equal directives always compare
/// equal regardless of construction. StrategyId and ContractVersion are
/// EXCLUDED: a new StrategyId per round is the UniAgent idempotency identity,
/// never a delta freedom. Exploration intent and the adaptation boundary are
/// NOT delta freedoms either: any difference there is undeclared directive
/// drift. Validation artifact; no field ever enters the wire.
/// </summary>
public static class DirectiveFacts
{
    /// <summary>Project a directive onto its compared levers.</summary>
    public static Computed Compute(StrategyDirective directive)
    {
        ArgumentNullException.ThrowIfNull(directive);
        return new Computed(
            MaximumDepth: directive.Scope.MaximumDepth,
            ApplicationIdentity: directive.Scope.ApplicationIdentity,
            SemanticRoot: directive.Scope.SemanticRoot,
            AllowedCategories: directive.Constraints.AllowedInteractionCategories,
            ProhibitedEffects: directive.Constraints.ProhibitedEffects,
            ObjectiveKind: directive.Objective.Kind,
            Criterion: directive.Objective.Criterion,
            CompletionKind: directive.Completion.Kind,
            Exploration: directive.Exploration,
            Adaptations: directive.Adaptation.AllowedAdaptations);
    }

    /// <summary>Whether two directives equal on ALL compared levers (NO-OP check).</summary>
    public static bool SameDirectiveLevers(Computed a, Computed b)
        => a.MaximumDepth == b.MaximumDepth
            && a.ApplicationIdentity == b.ApplicationIdentity
            && a.SemanticRoot == b.SemanticRoot
            && a.AllowedCategories.SetEquals(b.AllowedCategories)
            && a.ProhibitedEffects.SetEquals(b.ProhibitedEffects)
            && a.ObjectiveKind == b.ObjectiveKind
            && Equals(a.Criterion, b.Criterion)
            && a.CompletionKind == b.CompletionKind
            && a.Exploration == b.Exploration
            && a.Adaptations.SetEquals(b.Adaptations);

    /// <summary>Whether the lever a freedom revises actually differs between the two facts.</summary>
    public static bool LeverDiffers(Computed a, Computed b, PlanDeltaFreedom freedom) => freedom switch
    {
        PlanDeltaFreedom.Depth => a.MaximumDepth != b.MaximumDepth,
        PlanDeltaFreedom.Scope => a.ApplicationIdentity != b.ApplicationIdentity || a.SemanticRoot != b.SemanticRoot,
        PlanDeltaFreedom.Constraints => !a.AllowedCategories.SetEquals(b.AllowedCategories),
        PlanDeltaFreedom.ProhibitedEffects => !a.ProhibitedEffects.SetEquals(b.ProhibitedEffects),
        PlanDeltaFreedom.Objective => a.ObjectiveKind != b.ObjectiveKind,
        PlanDeltaFreedom.TypedCriterion => !Equals(a.Criterion, b.Criterion),
        PlanDeltaFreedom.Completion => a.CompletionKind != b.CompletionKind,
        _ => throw new ArgumentOutOfRangeException(
            nameof(freedom),
            freedom,
            "DispatchPolicy is compared via the round's dispatch summaries, not the directive facts."),
    };

    /// <summary>Whether the round's dispatch summaries are equal (both null, or both present with identical category→handling content).</summary>
    public static bool SameDispatchSummaries(DispatchPolicySummary? previous, DispatchPolicySummary? next)
        => previous is null ? next is null : previous.Equals(next);

    /// <summary>Human-readable lever name of a freedom, for drift/vacuous messages.</summary>
    public static string FieldName(PlanDeltaFreedom freedom) => freedom switch
    {
        PlanDeltaFreedom.Depth => "scope.maximumDepth",
        PlanDeltaFreedom.Scope => "scope.applicationIdentity/semanticRoot",
        PlanDeltaFreedom.Constraints => "constraints.allowedInteractionCategories",
        PlanDeltaFreedom.ProhibitedEffects => "constraints.prohibitedEffects",
        PlanDeltaFreedom.DispatchPolicy => "dispatch policy",
        PlanDeltaFreedom.Objective => "objective.kind",
        PlanDeltaFreedom.TypedCriterion => "objective.criterion",
        PlanDeltaFreedom.Completion => "completion.kind",
        _ => throw new ArgumentOutOfRangeException(nameof(freedom)),
    };

    /// <summary>Compared directive fields that are NOT delta freedoms; any difference is undeclared drift.</summary>
    public static IReadOnlyList<string> DriftOnlyDiffs(Computed a, Computed b)
    {
        var diffs = new List<string>();
        if (a.Exploration != b.Exploration)
            diffs.Add("exploration");
        if (!a.Adaptations.SetEquals(b.Adaptations))
            diffs.Add("adaptation.allowedAdaptations");
        return diffs;
    }

    /// <summary>Immutable summary of a directive's compared levers (directional facts for drift checks).</summary>
    public sealed record Computed(
        int MaximumDepth,
        string ApplicationIdentity,
        string SemanticRoot,
        ImmutableHashSet<TypeLevelElementCategory> AllowedCategories,
        ImmutableHashSet<StrategyProhibitedEffect> ProhibitedEffects,
        StrategyObjectiveKind ObjectiveKind,
        SemanticCriterionRef? Criterion,
        StrategyCompletionKind CompletionKind,
        ExplorationIntent Exploration,
        ImmutableHashSet<StrategyAdaptationKind> Adaptations);
}