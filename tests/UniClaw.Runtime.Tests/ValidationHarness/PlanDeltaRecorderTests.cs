using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Strategy;
using UniClaw.Runtime.ValidationHarness.PlanDelta;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-P26-E capability tests: the PlanDelta recorder contract (spec requirement
/// "PlanDelta contract" — citations resolve, deltas land strictly inside the
/// eight directive freedoms, NO-OP rounds are honest; design D5):
///  1. accepted deltas: depth, prohibited effects, scope, objective + typed
///     criterion, completion — each a REAL lever difference with resolvable
///     knowledge + evidence citations (including NewKnowledge citations);
///  2. rejected deltas: unresolvable knowledge/evidence citations, undeclared
///     directive drift, vacuous deltas, duplicate changes, dishonest NO-OPs
///     (changed NextStrategy, empty reason);
///  3. dispatch policy: the round's summaries are its evidence surface — a
///     declared summary difference is legal, an undeclared summary difference
///     is drift, a declared-but-equal summary set is vacuous, and a declared
///     change without BOTH summaries is rejected;
///  4. renderer determinism: the same round renders byte-identically twice
///     (JSON and Markdown).
/// Directives mirror <see cref="StrategyTestSupport"/>'s construction style;
/// assertions check contract legality capabilities — never fixed click counts,
/// coordinates, page paths, selectors, or action histories.
/// </summary>
public sealed class PlanDeltaRecorderTests
{
    private const string RunId = "run-1";
    private const string EvidenceRef1 = "evidence:run-1:obs-1";
    private const string EvidenceRef2 = "evidence:run-1:obs-2";
    private const string KnowledgeRef1 = "k-1";
    private const string KnowledgeRef2 = "k-2";

    // ── builders (mirror StrategyTestSupport construction style) ────────────

    // StrategyDirective / StrategyScope are records with custom constructors
    // (no primary constructor → no synthesized copy constructor), so directives
    // are rebuilt field-by-field rather than via `with` expressions.

    private static StrategyDirective WithStrategyId(StrategyDirective directive, string strategyId)
        => new(strategyId, directive.ContractVersion, directive.Objective, directive.Scope,
            directive.Exploration, directive.Constraints, directive.Completion, directive.Adaptation);

    private static StrategyDirective WithScope(StrategyDirective directive, StrategyScope scope)
        => new(directive.StrategyId, directive.ContractVersion, directive.Objective, scope,
            directive.Exploration, directive.Constraints, directive.Completion, directive.Adaptation);

    private static StrategyDirective WithObjective(StrategyDirective directive, StrategyObjective objective)
        => new(directive.StrategyId, directive.ContractVersion, objective, directive.Scope,
            directive.Exploration, directive.Constraints, directive.Completion, directive.Adaptation);

    private static StrategyDirective WithConstraints(StrategyDirective directive, StrategyConstraintSet constraints)
        => new(directive.StrategyId, directive.ContractVersion, directive.Objective, directive.Scope,
            directive.Exploration, constraints, directive.Completion, directive.Adaptation);

    private static StrategyDirective WithCompletion(StrategyDirective directive, StrategyCompletionCriteria completion)
        => new(directive.StrategyId, directive.ContractVersion, directive.Objective, directive.Scope,
            directive.Exploration, directive.Constraints, completion, directive.Adaptation);

    private static StrategyScope AtDepth(StrategyDirective directive, int maximumDepth)
        => new(directive.Scope.ApplicationIdentity, directive.Scope.SemanticRoot, maximumDepth);

    private static RoundEvidenceSummary Observed(params string[] evidenceRefs)
        => new(
            RunId,
            "strategy-explore-1",
            "GoalEvidenceSatisfied:ExhaustiveCoverageWithinScope",
            new[] { "run.strategy.start", "goal.evidenced", "terminal.reached" },
            evidenceRefs.Length > 0 ? evidenceRefs : new[] { EvidenceRef1 });

    private static PlanDeltaChange Change(
        PlanDeltaFreedom freedom,
        string description = "fresh observed-result evidence revised the plan",
        string[]? knowledgeRefs = null,
        string[]? evidenceRefs = null)
        => new(
            freedom,
            description,
            knowledgeRefs ?? new[] { KnowledgeRef1 },
            evidenceRefs ?? new[] { EvidenceRef1 });

    private static PlanningRound Round(
        StrategyDirective previous,
        StrategyDirective next,
        PlanDelta delta,
        RoundEvidenceSummary? observed = null,
        string[]? loaded = null,
        string[]? newKnowledge = null,
        DispatchPolicySummary? previousDispatch = null,
        DispatchPolicySummary? nextDispatch = null)
        => new(
            0,
            previous,
            observed ?? Observed(),
            loaded ?? new[] { KnowledgeRef1, KnowledgeRef2 },
            newKnowledge ?? Array.Empty<string>(),
            new[] { "anchor:settings.root:unresolved" },
            delta,
            next,
            previousDispatch,
            nextDispatch);

    private static DispatchPolicySummary Dispatch(
        TypeLevelHandling containerHandling,
        TypeLevelHandling? controlHandling = null)
    {
        var map = ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>.Empty
            .Add(TypeLevelElementCategory.NavigableContainer, containerHandling);
        if (controlHandling is { } control)
            map = map.Add(TypeLevelElementCategory.StateChangingControl, control);
        return new DispatchPolicySummary(map);
    }

    private static void AssertAccepted(PlanDeltaValidation result)
        => Assert.IsType<PlanDeltaValidation.Accepted>(result);

    private static void AssertRejected(PlanDeltaValidation result, params string[] expectedReasonParts)
    {
        var rejected = Assert.IsType<PlanDeltaValidation.Rejected>(result);
        foreach (var part in expectedReasonParts)
            Assert.Contains(part, rejected.Reason, StringComparison.Ordinal);
    }

    // ── 1. Accepted evidenced deltas ─────────────────────────────────────────

    [Fact]
    public void Accepted_DepthChange_CitingResolvableKnowledgeAndEvidence()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithScope(previous, AtDepth(previous, 2));
        var round = Round(previous, next, new PlanDelta([Change(PlanDeltaFreedom.Depth, "deeper branches discovered by the observed run")]));

        AssertAccepted(PlanDeltaValidator.Validate(round));
    }

    [Fact]
    public void Accepted_ProhibitedEffectsChange_CitingNewKnowledge()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithConstraints(
            previous,
            new StrategyConstraintSet(
                previous.Constraints.AllowedInteractionCategories,
                ImmutableHashSet.Create(StrategyProhibitedEffect.StateMutation)));
        var round = Round(
            previous,
            next,
            new PlanDelta([Change(
                PlanDeltaFreedom.ProhibitedEffects,
                "external boundary disproven by fresh evidence; class no longer prohibited",
                knowledgeRefs: new[] { "k-3" })]),
            newKnowledge: new[] { "k-3" });

        AssertAccepted(PlanDeltaValidator.Validate(round));
    }

    [Fact]
    public void Accepted_ScopeChange_CitingTwoEvidenceRefs()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithScope(previous, new StrategyScope(StrategyTestSupport.Application, "NetworkRoot", 1));
        var round = Round(
            previous,
            next,
            new PlanDelta([Change(PlanDeltaFreedom.Scope, "observed root re-grounds to the network subtree",
                evidenceRefs: new[] { EvidenceRef1, EvidenceRef2 })]),
            observed: Observed(EvidenceRef1, EvidenceRef2));

        AssertAccepted(PlanDeltaValidator.Validate(round));
    }

    [Fact]
    public void Accepted_ObjectiveKindPlusTypedCriterionChange_TwoDeclaredChanges()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithObjective(
            previous,
            new StrategyObjective(
                StrategyObjectiveKind.InspectMatchesWithinScope,
                new SemanticCriterionRef(StrategyTestSupport.Capability, StrategyTestSupport.SupportedCriterion, version: 1)));
        var round = Round(
            previous,
            next,
            new PlanDelta(
            [
                Change(PlanDeltaFreedom.Objective, "objective narrows to typed-match inspection"),
                Change(PlanDeltaFreedom.TypedCriterion, "typed criterion introduced for the narrowed objective"),
            ]));

        AssertAccepted(PlanDeltaValidator.Validate(round));
    }

    [Fact]
    public void Accepted_TypedCriterionVersionBump_OnlyCriterionFreedom()
    {
        var previous = StrategyTestSupport.Inspect(strategyId: "strategy-inspect-1");
        var next = WithObjective(
            previous,
            new StrategyObjective(
                previous.Objective.Kind,
                new SemanticCriterionRef(StrategyTestSupport.Capability, StrategyTestSupport.SupportedCriterion, version: 2)));
        var round = Round(previous, next, new PlanDelta([Change(PlanDeltaFreedom.TypedCriterion, "capability contract version bumped by fresh evidence")]));

        AssertAccepted(PlanDeltaValidator.Validate(round));
    }

    [Fact]
    public void Accepted_CompletionChange_DeclaredCompletionFreedom()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithCompletion(previous, new StrategyCompletionCriteria(StrategyCompletionKind.AllDiscoveredMatchesInspected));
        var round = Round(previous, next, new PlanDelta([Change(PlanDeltaFreedom.Completion, "evidence requires inspected matches as completion")]));

        AssertAccepted(PlanDeltaValidator.Validate(round));
    }

    [Fact]
    public void Accepted_NoOp_IdenticalDirectiveOnComparedLevers_WithReason()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithStrategyId(previous, "strategy-explore-2"); // new idempotency identity is excluded from comparison
        var round = Round(previous, next, PlanDelta.NoOp("no new evidence; frontier exhausted"));

        AssertAccepted(PlanDeltaValidator.Validate(round));
    }

    // ── 2. Rejected illegal deltas ───────────────────────────────────────────

    [Fact]
    public void Rejected_CitationToUnknownKnowledgeId()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithScope(previous, AtDepth(previous, 2));
        var round = Round(
            previous,
            next,
            new PlanDelta([Change(PlanDeltaFreedom.Depth, knowledgeRefs: new[] { "k-no-such" })]));

        AssertRejected(PlanDeltaValidator.Validate(round), "k-no-such", "unresolvable knowledge ref");
    }

    [Fact]
    public void Rejected_CitationToUnknownEvidenceRef()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithScope(previous, AtDepth(previous, 2));
        var round = Round(
            previous,
            next,
            new PlanDelta([Change(PlanDeltaFreedom.Depth, evidenceRefs: new[] { "evidence:run-1:obs-9" })]));

        AssertRejected(PlanDeltaValidator.Validate(round), "evidence:run-1:obs-9", "unresolvable evidence ref");
    }

    [Fact]
    public void Rejected_UndeclaredDrift_CompletionKindChangedWithoutDeclaredChange()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithCompletion(
            WithScope(previous, AtDepth(previous, 2)),
            new StrategyCompletionCriteria(StrategyCompletionKind.AllDiscoveredMatchesInspected));
        var round = Round(previous, next, new PlanDelta([Change(PlanDeltaFreedom.Depth)]));

        AssertRejected(PlanDeltaValidator.Validate(round), "undeclared directive drift", "completion.kind");
    }

    [Fact]
    public void Rejected_VacuousDelta_DeclaredDepthButSameDepth()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithStrategyId(previous, "strategy-explore-2"); // no lever changed
        var round = Round(previous, next, new PlanDelta([Change(PlanDeltaFreedom.Depth)]));

        AssertRejected(PlanDeltaValidator.Validate(round), "vacuous delta: Depth");
    }

    [Fact]
    public void Rejected_DuplicateDelta_TwoDeclaredChangesOnSameFreedom()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithScope(previous, AtDepth(previous, 2));
        var round = Round(
            previous,
            next,
            new PlanDelta(
            [
                Change(PlanDeltaFreedom.Depth, "first declared depth change"),
                Change(PlanDeltaFreedom.Depth, "second declared depth change"),
            ]));

        AssertRejected(PlanDeltaValidator.Validate(round), "duplicate delta: Depth");
    }

    [Fact]
    public void Rejected_NoOp_WithChangedDirective()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithScope(previous, AtDepth(previous, 2));
        var round = Round(previous, next, PlanDelta.NoOp("frontier exhausted"));

        AssertRejected(PlanDeltaValidator.Validate(round), "NO-OP delta records no change but NextStrategy differs");
    }

    [Fact]
    public void Rejected_NoOp_WithoutReason()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithStrategyId(previous, "strategy-explore-2");
        var round = Round(previous, next, PlanDelta.NoOp(" "));

        AssertRejected(PlanDeltaValidator.Validate(round), "NO_OP_WITH_REASON requires a non-empty reason");
    }

    // ── 3. Dispatch policy freedom (round summaries are its evidence surface) ─

    [Fact]
    public void DispatchPolicyDelta_Accepted_WhenSummariesDifferAndDeclared()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithStrategyId(previous, "strategy-explore-2");
        var round = Round(
            previous,
            next,
            new PlanDelta([Change(PlanDeltaFreedom.DispatchPolicy, "state-changing controls downgraded from forbidden to inspect")]),
            previousDispatch: Dispatch(TypeLevelHandling.EnterAndTraverse, TypeLevelHandling.Forbidden),
            nextDispatch: Dispatch(TypeLevelHandling.EnterAndTraverse, TypeLevelHandling.Inspect));

        AssertAccepted(PlanDeltaValidator.Validate(round));
    }

    [Fact]
    public void DispatchPolicyDelta_Rejected_WhenSummariesDifferUndeclared()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithScope(previous, AtDepth(previous, 2));
        var round = Round(
            previous,
            next,
            new PlanDelta([Change(PlanDeltaFreedom.Depth)]),
            previousDispatch: Dispatch(TypeLevelHandling.EnterAndTraverse, TypeLevelHandling.Forbidden),
            nextDispatch: Dispatch(TypeLevelHandling.EnterAndTraverse, TypeLevelHandling.Inspect));

        AssertRejected(PlanDeltaValidator.Validate(round), "undeclared directive drift", "dispatch policy");
    }

    [Fact]
    public void DispatchPolicyDelta_Rejected_DeclaredButSummariesEqual()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithStrategyId(previous, "strategy-explore-2");
        var round = Round(
            previous,
            next,
            new PlanDelta([Change(PlanDeltaFreedom.DispatchPolicy)]),
            previousDispatch: Dispatch(TypeLevelHandling.EnterAndTraverse, TypeLevelHandling.Forbidden),
            nextDispatch: Dispatch(TypeLevelHandling.EnterAndTraverse, TypeLevelHandling.Forbidden));

        AssertRejected(PlanDeltaValidator.Validate(round), "vacuous delta: DispatchPolicy");
    }

    [Fact]
    public void DispatchPolicyDelta_Rejected_DeclaredWithoutBothSummaries()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithStrategyId(previous, "strategy-explore-2");
        var round = Round(
            previous,
            next,
            new PlanDelta([Change(PlanDeltaFreedom.DispatchPolicy)]),
            previousDispatch: Dispatch(TypeLevelHandling.EnterAndTraverse, TypeLevelHandling.Forbidden));

        AssertRejected(PlanDeltaValidator.Validate(round), "requires both previous and next dispatch policy summaries");
    }

    // ── 4. Renderer determinism ──────────────────────────────────────────────

    [Fact]
    public void Renderer_DeterministicJsonAndMarkdown_SameRoundTwice()
    {
        var previous = StrategyTestSupport.Explore(maximumDepth: 1);
        var next = WithObjective(
            WithScope(previous, AtDepth(previous, 2)),
            new StrategyObjective(
                StrategyObjectiveKind.InspectMatchesWithinScope,
                new SemanticCriterionRef(StrategyTestSupport.Capability, StrategyTestSupport.SupportedCriterion, version: 1)));
        var round = Round(
            previous,
            next,
            new PlanDelta(
            [
                Change(PlanDeltaFreedom.Depth, "deeper branches discovered by the observed run"),
                Change(PlanDeltaFreedom.Objective, "objective narrowed by fresh evidence"),
            ]),
            observed: Observed(EvidenceRef1, EvidenceRef2),
            newKnowledge: new[] { "k-3" },
            previousDispatch: Dispatch(TypeLevelHandling.EnterAndTraverse, TypeLevelHandling.Forbidden),
            nextDispatch: Dispatch(TypeLevelHandling.EnterAndTraverse, TypeLevelHandling.Inspect));

        var json1 = PlanningRoundRecord.ToJson(round);
        var json2 = PlanningRoundRecord.ToJson(round);
        Assert.Equal(json1, json2);

        var markdown1 = PlanningRoundRecord.ToMarkdown(round);
        var markdown2 = PlanningRoundRecord.ToMarkdown(round);
        Assert.Equal(markdown1, markdown2);

        Assert.Contains("roundIndex", json1, StringComparison.Ordinal);
        Assert.Contains("\"freedom\": \"Depth\"", json1, StringComparison.Ordinal);
        Assert.Contains("## Plan Delta", markdown1, StringComparison.Ordinal);
        Assert.Contains("- Depth:", markdown1, StringComparison.Ordinal);
    }
}