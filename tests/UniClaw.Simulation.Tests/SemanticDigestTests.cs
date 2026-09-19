using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// Semantic digest 确定性（间接验收）：canonical builder 的确定性以
/// 「同 bundle 两次全链路运行 digest 相等」证明；不同场景 digest 不同。
/// </summary>
public sealed class SemanticDigestTests
{
    [Fact]
    public void SameBundle_TwoRuns_ProduceIdenticalSemanticDigest()
    {
        var first = ScenarioRunner.Run(GoldenScenarioBundles.WifiToggleOffToOn());
        var second = ScenarioRunner.Run(GoldenScenarioBundles.WifiToggleOffToOn());
        Assert.NotEmpty(first.Report.SemanticDigest);
        Assert.Equal(first.Report.SemanticDigest, second.Report.SemanticDigest);
    }

    [Fact]
    public void DifferentScenarios_ProduceDifferentSemanticDigests()
    {
        var happyPath = ScenarioRunner.Run(GoldenScenarioBundles.WifiToggleOffToOn());
        var alreadyOn = ScenarioRunner.Run(GoldenScenarioBundles.AlreadyOnZeroEffect());
        Assert.NotEqual(happyPath.Report.SemanticDigest, alreadyOn.Report.SemanticDigest);
    }
}
