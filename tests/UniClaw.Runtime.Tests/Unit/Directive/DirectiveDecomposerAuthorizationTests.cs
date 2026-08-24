using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Authorization-boundary preservation: the decomposer must not widen, relax, or
/// synthesize authorization. The decomposed Goal's candidate-authorization
/// evaluator IS the caller's rule, so a rejected candidate stays rejected and a
/// forbidden category stays forbidden.
/// </summary>
public sealed class DirectiveDecomposerAuthorizationTests
{
    private static readonly ObservedElement RejectedCandidate =
        new(DirectiveTestData.DangerousCandidate, null, 0);

    private static readonly ObservedElement AuthorizedCandidate =
        new(DirectiveTestData.BranchA, null, 1);

    [Fact]
    public void RejectedCandidate_StaysRejectedAfterDecomposition()
    {
        var directive = DirectiveTestData.ValidDirective();

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        // The decomposed Goal's authorization evaluator is the caller's rule —
        // the dangerous candidate must remain rejected exactly as the caller rejected it.
        var observation = new Observation(
            ImmutableArray.Create(RejectedCandidate, AuthorizedCandidate),
            DirectiveTestData.App,
            1);
        var evidence = resolved.Goal.CandidateAuthorizationEvaluator!(observation, RejectedCandidate);

        Assert.False(evidence.Authorized);
        Assert.Equal(
            DirectiveTestData.EvaluateAuthorization(observation, RejectedCandidate),
            evidence);
    }

    [Fact]
    public void AuthorizedCandidate_RemainsAuthorized_AndRuleIsTheOriginalDelegate()
    {
        var directive = DirectiveTestData.ValidDirective();

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        // The delegated rule instance is projected verbatim — no wrapper that
        // could alter its verdict.
        Assert.Same(directive.StrategyRules.CandidateAuthorizationEvaluator, resolved.Goal.CandidateAuthorizationEvaluator);

        var observation = new Observation(
            ImmutableArray.Create(AuthorizedCandidate),
            DirectiveTestData.App,
            1);
        var evidence = resolved.Goal.CandidateAuthorizationEvaluator!(observation, AuthorizedCandidate);

        Assert.True(evidence.Authorized);
    }

    [Fact]
    public void Decomposer_AddsNoAdditionalAuthorizationBeyondCallerRule()
    {
        var directive = DirectiveTestData.ValidDirective();

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        // The only authorization surface on the decomposed Goal is the exact
        // caller-injected CandidateAuthorizationEvaluator. The decomposer has no
        // second authorization path to add one.
        var candidateAuthorizationProperties = resolved.Goal.GetType().GetProperties();
        Assert.DoesNotContain(candidateAuthorizationProperties, property =>
            property.Name != "CandidateAuthorizationEvaluator"
            && typeof(CandidateAuthorizationEvidence).IsAssignableFrom(property.PropertyType));
        Assert.NotNull(resolved.Goal.CandidateAuthorizationEvaluator);
    }

    [Fact]
    public void ForbiddenCategory_StaysForbidden_WhenDispatchPolicyForbidsIt()
    {
        // A caller that classifies the dangerous candidate as StateChangingControl
        // and forbids that category must have its boundary preserved through
        // decomposition and dispatch resolution.
        TypeLevelElementCategory? Classifier(ObservedElement element)
            => element.Text == DirectiveTestData.DangerousCandidate
                ? TypeLevelElementCategory.StateChangingControl
                : null;
        var forbidden = new TypeLevelDispatchPolicy(
            ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>
                .Empty
                .Add(TypeLevelElementCategory.StateChangingControl, TypeLevelHandling.Forbidden));
        var rules = new DirectiveStrategyRules(
            DirectiveTestData.EvaluateEvidence,
            DirectiveTestData.EvaluateAuthorization,
            BranchInventoryEvaluator: DirectiveTestData.EvaluateInventory,
            ViewportExplorationEvaluator: null,
            CategoryClassifier: Classifier);

        var directive = new Directive(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            1,
            DirectiveTestData.NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            rules,
            forbidden);

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        // The forbid decision derives entirely from caller-injected inputs — the
        // decomposed classifier labels the candidate, and the preserved dispatch
        // policy forbids that category. The decomposer neither creates nor relaxes it.
        Assert.Same(directive.DispatchPolicy, resolved.Specification.DispatchPolicy);
        var candidate = new ObservedElement(DirectiveTestData.DangerousCandidate, null, 0);
        var category = resolved.Goal.CategoryClassifier!(candidate);
        Assert.NotNull(category);
        Assert.Equal(TypeLevelElementCategory.StateChangingControl, category.Value);
        Assert.Equal(TypeLevelHandling.Forbidden, resolved.Specification.DispatchPolicy!.Resolve(category.Value));
    }
}
