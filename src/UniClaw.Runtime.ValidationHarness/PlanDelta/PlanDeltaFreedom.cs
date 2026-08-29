namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// Closed freedom set a PlanDelta may legally revise (spec requirement "PlanDelta
/// contract"; design D5): exactly the eight directive levers upper-agent plan
/// adaptation is allowed to touch. Everything else in the directive contract —
/// exploration intent, adaptation boundary, strategy identity, contract version —
/// is NOT a delta freedom: any such difference between rounds is undeclared
/// directive drift and is rejected by <see cref="PlanDeltaValidator"/>.
/// DispatchPolicy is not carried by <c>StrategyDirective</c> (the binding
/// creates it per strategy) and is modeled through the round's dispatch-policy
/// summaries instead. PlanDelta lives in validation tooling only; no field ever
/// enters the wire.
/// </summary>
public enum PlanDeltaFreedom
{
    /// <summary>Revision of <c>scope.maximumDepth</c> (the finite semantic traversal depth).</summary>
    Depth = 1,

    /// <summary>Revision of the scope application identity / semantic root.</summary>
    Scope = 2,

    /// <summary>Revision of the allowed interaction category set (<c>constraints.allowedInteractionCategories</c>).</summary>
    Constraints = 3,

    /// <summary>Revision of the prohibited effects set (<c>constraints.prohibitedEffects</c>).</summary>
    ProhibitedEffects = 4,

    /// <summary>Revision of the binding-created dispatch policy (category → handling), declared via the round's dispatch-policy summaries — never carried by the directive itself.</summary>
    DispatchPolicy = 5,

    /// <summary>Revision of the typed objective kind (<c>objective.kind</c>).</summary>
    Objective = 6,

    /// <summary>Revision of the typed semantic criterion reference (<c>objective.criterion</c> id/version/capability).</summary>
    TypedCriterion = 7,

    /// <summary>Revision of the completion evidence kind (<c>completion.kind</c>).</summary>
    Completion = 8,
}