using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Incomplete-directive rejection: when a rule required by the declared
/// completion requirement is missing, decomposition returns an explicit
/// insufficiency result with NO execution inputs and NO fabricated rule.
/// </summary>
public sealed class DirectiveDecomposerInsufficientTests
{
    private static Directive DirectiveWithRules(DirectiveStrategyRules rules)
        => new(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            1,
            DirectiveTestData.NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            rules);

    [Fact]
    public void MissingCompletionRule_ReturnsInsufficientWithNoExecutionInputs()
    {
        var rules = new DirectiveStrategyRules(
            EvidenceEvaluator: null!,
            DirectiveTestData.EvaluateAuthorization,
            DirectiveTestData.EvaluateInventory);

        var result = DirectiveDecomposer.Decompose(DirectiveWithRules(rules));

        var insufficient = Assert.IsType<DirectiveDecompositionResult.Insufficient>(result);
        Assert.False(string.IsNullOrWhiteSpace(insufficient.Reason));
    }

    [Fact]
    public void MissingAuthorizationRule_ReturnsInsufficientWithNoExecutionInputs()
    {
        var rules = new DirectiveStrategyRules(
            DirectiveTestData.EvaluateEvidence,
            CandidateAuthorizationEvaluator: null!,
            DirectiveTestData.EvaluateInventory);

        var result = DirectiveDecomposer.Decompose(DirectiveWithRules(rules));

        Assert.IsType<DirectiveDecompositionResult.Insufficient>(result);
    }

    [Fact]
    public void MissingInventoryRule_ReturnsInsufficientWithNoExecutionInputs()
    {
        var rules = new DirectiveStrategyRules(
            DirectiveTestData.EvaluateEvidence,
            DirectiveTestData.EvaluateAuthorization,
            BranchInventoryEvaluator: null!);

        var result = DirectiveDecomposer.Decompose(DirectiveWithRules(rules));

        Assert.IsType<DirectiveDecompositionResult.Insufficient>(result);
    }

    [Theory]
    [InlineData("completion")]
    [InlineData("authorization")]
    [InlineData("inventory")]
    public void Insufficient_ContainsNoSpecAndNoGoal_AndNoFabricatedRule(string missing)
    {
        DirectiveStrategyRules rules = missing switch
        {
            "completion" => new DirectiveStrategyRules(
                EvidenceEvaluator: null!,
                DirectiveTestData.EvaluateAuthorization,
                DirectiveTestData.EvaluateInventory),
            "authorization" => new DirectiveStrategyRules(
                DirectiveTestData.EvaluateEvidence,
                CandidateAuthorizationEvaluator: null!,
                DirectiveTestData.EvaluateInventory),
            _ => new DirectiveStrategyRules(
                DirectiveTestData.EvaluateEvidence,
                DirectiveTestData.EvaluateAuthorization,
                BranchInventoryEvaluator: null!),
        };

        var result = DirectiveDecomposer.Decompose(DirectiveWithRules(rules));

        var insufficient = Assert.IsType<DirectiveDecompositionResult.Insufficient>(result);
        Assert.Equal(new[] { "Reason" }, typeof(DirectiveDecompositionResult.Insufficient)
            .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(property => property.Name).ToArray());
    }

    [Fact]
    public void Insufficient_ProjectionProjectsNoFabricatedRule()
    {
        // The insufficiency receipt carries no Goal and no execution representation —
        // the caller cannot accidentally execute a guessed directive.
        var result = DirectiveDecomposer.Decompose(DirectiveWithRules(
            new DirectiveStrategyRules(
                EvidenceEvaluator: null!,
                DirectiveTestData.EvaluateAuthorization,
                DirectiveTestData.EvaluateInventory)));

        var insufficient = Assert.IsType<DirectiveDecompositionResult.Insufficient>(result);
        Assert.False(string.IsNullOrWhiteSpace(insufficient.Reason));

        var envelope = IntentSemanticEnvelope.Project(
            "insufficient directive",
            insufficient.Reason);
        Assert.IsType<IntentSemanticEnvelope.Insufficient>(envelope);
    }
}
