using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Strategy;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class StrategyContractTests
{
    [Fact]
    public void BoundedExploreStrategy_IsAcceptedAndCreatesRuntimeExecutionIntent()
    {
        var strategy = StrategyTestSupport.Explore();
        var result = StrategyTestSupport.ExploreCompiler().Compile(strategy);

        var accepted = Assert.IsType<StrategyCompilationResult.Accepted>(result);
        Assert.Same(strategy, accepted.Intent.Strategy);
        Assert.Equal(strategy.StrategyId, accepted.Intent.StrategyId);
        Assert.Equal(strategy.Scope.MaximumDepth, accepted.Intent.Specification.MaximumDepth);
        Assert.Equal(strategy.Scope.SemanticRoot, accepted.Intent.Specification.Scope.SemanticRoot);
        Assert.NotNull(accepted.Intent.Goal.EvidenceEvaluator);
    }

    [Fact]
    public void TypedMatchStrategy_IsAcceptedByMatchingGenericCapability()
    {
        var result = StrategyTestSupport.InspectCompiler().Compile(StrategyTestSupport.Inspect());

        var accepted = Assert.IsType<StrategyCompilationResult.Accepted>(result);
        Assert.Equal(ExplorationIntent.InspectMatchesWithinScope, accepted.Intent.Strategy.Exploration);
        Assert.Equal(
            StrategyTestSupport.SupportedCriterion,
            accepted.Intent.Strategy.Objective.Criterion?.CriterionId);
    }

    [Fact]
    public void UnsupportedSemanticCriterion_FailsClosedWithoutIntent()
    {
        var result = StrategyTestSupport.InspectCompiler().Compile(
            StrategyTestSupport.Inspect(criterionId: "unsupported-criterion"));

        var rejected = Assert.IsType<StrategyCompilationResult.Rejected>(result);
        Assert.Equal(StrategyRejectionCode.UnsupportedCriterion, rejected.Code);
        Assert.DoesNotContain("guess", rejected.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingTypedCriterion_IsRejectedRatherThanInferred()
    {
        var strategy = new StrategyDirective(
            "strategy-unresolved",
            contractVersion: 1,
            new StrategyObjective(StrategyObjectiveKind.InspectMatchesWithinScope),
            new StrategyScope(StrategyTestSupport.Application, StrategyTestSupport.Root, maximumDepth: 1),
            ExplorationIntent.InspectMatchesWithinScope,
            new StrategyConstraintSet(
                ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
                ImmutableHashSet.Create(StrategyProhibitedEffect.StateMutation)),
            new StrategyCompletionCriteria(StrategyCompletionKind.AllDiscoveredMatchesInspected),
            new StrategyAdaptationBoundary(
                ImmutableHashSet.Create(StrategyAdaptationKind.ReconcileBelief)));

        var rejected = Assert.IsType<StrategyCompilationResult.Rejected>(
            StrategyTestSupport.InspectCompiler().Compile(strategy));
        Assert.Equal(StrategyRejectionCode.Malformed, rejected.Code);
    }

    [Fact]
    public void UnboundedDepth_IsRejected()
    {
        var rejected = Assert.IsType<StrategyCompilationResult.Rejected>(
            StrategyTestSupport.ExploreCompiler().Compile(
                StrategyTestSupport.Explore(maximumDepth: StrategyContractCompiler.MaximumSupportedDepth + 1)));

        Assert.Equal(StrategyRejectionCode.Malformed, rejected.Code);
    }

    [Fact]
    public void ContradictorySafetyBoundary_IsRejected()
    {
        var baseline = StrategyTestSupport.Explore();
        var strategy = new StrategyDirective(
            "strategy-conflict",
            baseline.ContractVersion,
            baseline.Objective,
            baseline.Scope,
            baseline.Exploration,
            new StrategyConstraintSet(
                ImmutableHashSet.Create(TypeLevelElementCategory.StateChangingControl),
                ImmutableHashSet.Create(StrategyProhibitedEffect.StateMutation)),
            baseline.Completion,
            baseline.Adaptation);

        var rejected = Assert.IsType<StrategyCompilationResult.Rejected>(
            StrategyTestSupport.ExploreCompiler().Compile(strategy));
        Assert.Equal(StrategyRejectionCode.BoundaryConflict, rejected.Code);
    }

    [Fact]
    public void UnverifiableCompletion_IsRejected()
    {
        var rejected = Assert.IsType<StrategyCompilationResult.Rejected>(
            StrategyTestSupport.InspectCompiler(supportsCompletion: false)
                .Compile(StrategyTestSupport.Inspect()));

        Assert.Equal(StrategyRejectionCode.UnverifiableCompletion, rejected.Code);
    }

    [Fact]
    public void HypothesisRevision_RequiresDeclaredAdaptationPermission()
    {
        var hypothesis = Hypothesis();
        var decision = new RuntimeDecision(
            hypothesis.RunId,
            RuntimeDecisionState.Revise,
            hypothesis.RunId,
            "fresh-world-evidence",
            "World evidence contradicts the hypothesis.");
        var boundary = new StrategyAdaptationBoundary(
            ImmutableHashSet.Create(StrategyAdaptationKind.ReconcileBelief));

        var blocked = StrategyHypothesisAdapter.Evaluate(boundary, decision, hypothesis);

        var receipt = Assert.IsType<StrategyHypothesisAdapter.Result.Blocked>(blocked);
        Assert.Equal(StrategyAdaptationKind.ReviseExecutionHypothesis, receipt.Violation.RequiredAdaptation);
        Assert.Equal(hypothesis.RunId, receipt.Violation.DecisionReference);
    }

    [Fact]
    public void DeclaredHypothesisRevision_ReusesExistingHypothesisAdaptation()
    {
        var hypothesis = Hypothesis();
        var decision = new RuntimeDecision(
            hypothesis.RunId,
            RuntimeDecisionState.Revise,
            hypothesis.RunId,
            "fresh-world-evidence",
            "World evidence contradicts the hypothesis.");
        var boundary = new StrategyAdaptationBoundary(
            ImmutableHashSet.Create(
                StrategyAdaptationKind.ReconcileBelief,
                StrategyAdaptationKind.ReviseExecutionHypothesis));

        var adapted = StrategyHypothesisAdapter.Evaluate(boundary, decision, hypothesis);

        var receipt = Assert.IsType<StrategyHypothesisAdapter.Result.Adapted>(adapted);
        Assert.Equal(HypothesisAdaptationType.Replace, receipt.Adaptation.AdaptationType);
    }

    [Fact]
    public void DeclaredRegroundAndReorder_AreAllowedWithoutMutatingStrategy()
    {
        var boundary = new StrategyAdaptationBoundary(
            ImmutableHashSet.Create(
                StrategyAdaptationKind.ReconcileBelief,
                StrategyAdaptationKind.RegroundSemanticTarget,
                StrategyAdaptationKind.ReorderPendingWork));

        Assert.Null(StrategyBoundaryGuard.Check(
            boundary,
            StrategyAdaptationKind.RegroundSemanticTarget,
            "decision-1"));
        Assert.Null(StrategyBoundaryGuard.Check(
            boundary,
            StrategyAdaptationKind.ReorderPendingWork,
            "decision-1"));
        Assert.NotNull(StrategyBoundaryGuard.Check(
            boundary,
            StrategyAdaptationKind.ReviseExecutionHypothesis,
            "decision-1"));
    }

    [Fact]
    public void AuthorityBearingStrategyFields_AreImmutable()
    {
        var immutableTypes = new[]
        {
            typeof(StrategyDirective),
            typeof(StrategyObjective),
            typeof(StrategyScope),
            typeof(StrategyConstraintSet),
            typeof(StrategyCompletionCriteria),
            typeof(StrategyAdaptationBoundary),
        };

        Assert.All(immutableTypes, type =>
            Assert.All(type.GetProperties(), property => Assert.False(property.CanWrite)));
    }

    private static ExecutionHypothesis Hypothesis()
        => new(
            "strategy-run",
            "strategy-ref",
            "Explore bounded scope",
            "Discover -> Authorize -> Expand",
            "Exhaustive coverage within scope",
            confidence: 1f,
            revisionReason: null,
            createdAtObservation: 1,
            ExecutionHypothesisStatus.Active);
}
