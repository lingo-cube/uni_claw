using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// B4 World/Reconcile 纯函数测试（run-lifecycle SHALL §10）：语义解析 / SourceObservationSequence /
/// Unknown 不假装确定（§10）/ 确定性 / 无场景特定字段（裁决 2）。
/// </summary>
public class ReconcileTests
{
    [Fact]
    public void FromObservation_ResolvesSemanticPage_WithEvidenceAndSourceSequence()
    {
        var observation = new Observation(
            [new ObservedElement("Network & Internet", null, 0)], "Settings", 7);

        var belief = Reconcile.FromObservation(observation, o => o.ForegroundApplication == "Settings" ? "SettingsMain" : null);

        Assert.Equal("SettingsMain", belief.SemanticPage);
        Assert.Equal(7, belief.SourceObservationSequence);
        Assert.InRange(belief.Confidence, 0f, 1f);
        Assert.False(string.IsNullOrWhiteSpace(belief.Evidence));
    }

    [Fact]
    public void FromObservation_UnknownWhenNoRuleMatches_NoFakedCertainty()
    {
        var observation = new Observation(
            [new ObservedElement("Bluetooth", null, 0)], "Launcher", 3);

        var belief = Reconcile.FromObservation(observation, _ => null);

        Assert.Null(belief.SemanticPage);
        Assert.Equal(3, belief.SourceObservationSequence);
        Assert.Equal(0f, belief.Confidence);
        Assert.False(string.IsNullOrWhiteSpace(belief.Evidence));
    }

    [Fact]
    public void FromObservation_IsDeterministic()
    {
        var observation = new Observation(
            [new ObservedElement("WiFi", null, 0)], "Settings", 5);
        Func<Observation, string?> rule = o => o.ForegroundApplication == "Settings" ? "SettingsMain" : null;

        Assert.Equal(Reconcile.FromObservation(observation, rule), Reconcile.FromObservation(observation, rule));
    }

    [Fact]
    public void WorldBelief_ExposesOnlyContractFields_NoScenarioSpecificState()
    {
        var propertyNames = typeof(WorldBelief)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "Confidence", "Evidence", "SemanticPage", "SourceObservationSequence" }, propertyNames);
    }
}
