using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Stateless directive decomposition: a valid directive decomposes into a spec
/// whose boundaries match the caller's, a Goal whose evaluators ARE the
/// caller-injected rules, deterministically and world-free.
/// </summary>
public sealed class DirectiveDecomposerTests
{
    [Fact]
    public void Decompose_ProjectsBoundaryFieldsIntoTheTypeLevelSpecification()
    {
        var dispatch = DirectiveTestData.NavigableEnterDispatch();
        var directive = new Directive(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            maximumDepth: 3,
            DirectiveTestData.NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            DirectiveTestData.Rules(),
            dispatch);

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        var specification = resolved.Specification;
        Assert.Equal(directive.Scope, specification.Scope);
        Assert.Equal(directive.Entry, specification.Entry);
        Assert.Equal(directive.MaximumDepth, specification.MaximumDepth);
        Assert.Equal(directive.Safety, specification.Safety);
        Assert.Equal(directive.Completion, specification.Completion);
        Assert.Same(directive.DispatchPolicy, specification.DispatchPolicy);
        // Target categories are projected 1:1 from the caller's safety boundary —
        // nothing beyond the caller's declared boundary is introduced.
        Assert.Equal(directive.Safety.AllowedInteractionCategories, specification.TargetCategories);
    }

    [Fact]
    public void Decompose_GoalEvaluatorsAreTheCallerInjectedRules()
    {
        var directive = DirectiveTestData.ValidDirective();

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        var goal = resolved.Goal;
        Assert.Same(directive.StrategyRules.EvidenceEvaluator, goal.EvidenceEvaluator);
        Assert.Same(directive.StrategyRules.CandidateAuthorizationEvaluator, goal.CandidateAuthorizationEvaluator);
        Assert.Same(directive.StrategyRules.BranchInventoryEvaluator, goal.BranchInventoryEvaluator);
        Assert.Same(directive.StrategyRules.ViewportExplorationEvaluator, goal.ViewportExplorationEvaluator);
        Assert.Same(directive.StrategyRules.CategoryClassifier, goal.CategoryClassifier);
    }

    [Fact]
    public void Decompose_OptionalRulesProjectWhenPresent()
    {
        var directive = new Directive(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            1,
            DirectiveTestData.NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            DirectiveTestData.Rules(includeViewport: true, includeClassifier: true));

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        Assert.Same(directive.StrategyRules.ViewportExplorationEvaluator, resolved.Goal.ViewportExplorationEvaluator);
        Assert.Same(directive.StrategyRules.CategoryClassifier, resolved.Goal.CategoryClassifier);
    }

    [Fact]
    public void Decompose_IsDeterministicAndStructurallyIdenticalAcrossInvocations()
    {
        var directive = DirectiveTestData.ValidDirective();

        var first = DirectiveDecomposer.Decompose(directive);
        var second = DirectiveDecomposer.Decompose(directive);

        var firstResolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(first);
        var secondResolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(second);
        Assert.Equal(firstResolved.Specification, secondResolved.Specification);
        Assert.Same(firstResolved.Goal.EvidenceEvaluator, secondResolved.Goal.EvidenceEvaluator);
        Assert.Same(firstResolved.Goal.CandidateAuthorizationEvaluator, secondResolved.Goal.CandidateAuthorizationEvaluator);
        Assert.Same(firstResolved.Goal.BranchInventoryEvaluator, secondResolved.Goal.BranchInventoryEvaluator);
    }

    [Fact]
    public void Decompose_IsWorldFree_AndDoesNotMutateItsInput()
    {
        var directive = DirectiveTestData.ValidDirective();
        var original = new Directive(
            directive.Scope,
            directive.Entry,
            directive.MaximumDepth,
            directive.Safety,
            directive.Completion,
            directive.StrategyRules,
            directive.DispatchPolicy);

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        // A pure, side-effect-free projection: the caller's Directive remains
        // byte-for-byte unchanged, and the output carries the caller's spec and
        // goal with no world observation and no mutable decomposer state.
        Assert.Equal(original, directive);
        Assert.NotNull(resolved.Specification);
        Assert.NotNull(resolved.Goal);
    }

    [Fact]
    public void Decomposer_IsAStatelessStaticProjection()
    {
        // A stateless static transform: no instance state, and its only public
        // surface is the single deterministic Decompose entry point.
        Assert.True(typeof(DirectiveDecomposer).IsAbstract);
        Assert.True(typeof(DirectiveDecomposer).IsSealed);
        Assert.Single(typeof(DirectiveDecomposer).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "Decompose"));
    }
}
