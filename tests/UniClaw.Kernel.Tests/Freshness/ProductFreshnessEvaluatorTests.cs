using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Freshness;

/// <summary>
/// FRS-008 — 产品 freshness evaluator（HOST-001 D7）：三态不折叠、窗口
/// 边界含等号、同输入×同时钟同结果（缝确定性）、clock/window 配置错误
/// ctor fail-fast（composition error ≠ runtime Unknown）。
/// </summary>
public sealed class ProductFreshnessEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static FreshnessEvaluationInput Input(DateTimeOffset asOf) => new(
        new FreshnessBasis(asOf),
        RevisionId: "rev-test",
        Requirement: new ConsumptionRequirement(TargetSubject: "switch", EffectClass: "tap"));

    [Fact]
    public void MissingAsOf_Returns_Unknown_NotInsufficient()
    {
        var evaluator = new ProductFreshnessEvaluator(() => Now, TimeSpan.FromSeconds(5));

        var judgment = evaluator.Evaluate(Input(default));

        Assert.Equal(FreshnessSufficiency.Unknown, judgment.Sufficiency);
        Assert.Contains("missing-asof", judgment.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AgeExactlyAtWindow_IsSufficient_BoundaryInclusive()
    {
        var evaluator = new ProductFreshnessEvaluator(() => Now, TimeSpan.FromSeconds(5));

        var judgment = evaluator.Evaluate(Input(Now - TimeSpan.FromSeconds(5)));

        Assert.Equal(FreshnessSufficiency.Sufficient, judgment.Sufficiency);
    }

    [Fact]
    public void AgeBeyondWindow_IsInsufficient_FailClosed()
    {
        var evaluator = new ProductFreshnessEvaluator(() => Now, TimeSpan.FromSeconds(5));

        var judgment = evaluator.Evaluate(Input(Now - TimeSpan.FromSeconds(5.001)));

        Assert.Equal(FreshnessSufficiency.Insufficient, judgment.Sufficiency);
    }

    [Fact]
    public void FutureAsOf_CountsAsSufficient_DocumentedPermissiveDirection()
    {
        var evaluator = new ProductFreshnessEvaluator(() => Now, TimeSpan.FromSeconds(5));

        var judgment = evaluator.Evaluate(Input(Now + TimeSpan.FromSeconds(1)));

        Assert.Equal(FreshnessSufficiency.Sufficient, judgment.Sufficiency);
    }

    [Fact]
    public void SameInputAndClock_ProducesSameResult_DeterminismContract()
    {
        var evaluator = new ProductFreshnessEvaluator(() => Now, TimeSpan.FromSeconds(5));
        var input = Input(Now - TimeSpan.FromSeconds(2));

        var first = evaluator.Evaluate(input);
        var second = evaluator.Evaluate(input);

        Assert.Equal(first, second);
    }

    [Fact]
    public void NullClock_ThrowsAtConstruction_CompositionErrorNotRuntimeUnknown()
        => Assert.Throws<ArgumentNullException>(() => new ProductFreshnessEvaluator(null!, TimeSpan.FromSeconds(5)));

    [Fact]
    public void NegativeWindow_ThrowsAtConstruction()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProductFreshnessEvaluator(() => Now, TimeSpan.FromSeconds(-1)));
}
