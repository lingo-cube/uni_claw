using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// PER-009 S4：信任等级表——CSS 级联查找 + A/B/C 门槛（ADR-0028）。
/// </summary>
public sealed class ProducerTrustTests
{
    [Fact]
    public void Lookup_ExactCategoryBeatsProducerDefault()
    {
        var table = ProducerTrust.Default();
        // 快视：state=C（类别命中），即便未来给它加 producer 默认也以类别为准
        Assert.Equal(ProducerTrust.Grade.C, table.Lookup(ProducerTrust.VisionProducer, "state"));
        Assert.Equal(ProducerTrust.Grade.B, table.Lookup(ProducerTrust.VisionProducer, "text"));
    }

    [Fact]
    public void Lookup_XmlStateIsA()
    {
        var table = ProducerTrust.Default();
        Assert.Equal(ProducerTrust.Grade.A, table.Lookup(ProducerTrust.XmlProducer, "state"));
    }

    [Fact]
    public void Lookup_UnknownFallsBackToC_FailSafe()
    {
        var table = ProducerTrust.Default();
        Assert.Equal(ProducerTrust.Grade.C, table.Lookup("whoever.unknown", "whatever"));
    }

    [Fact]
    public void Lookup_PackageOverrideBeatsEverything()
    {
        // 对抗域防线（D5）：已知不可信 app 以包名级覆盖降级
        var table = ProducerTrust.Default().WithOverride(
            new ProducerTrust.Override("com.evil.app", ProducerTrust.XmlProducer, "state", ProducerTrust.Grade.C));
        Assert.Equal(ProducerTrust.Grade.C, table.Lookup(ProducerTrust.XmlProducer, "state", "com.evil.app"));
        Assert.Equal(ProducerTrust.Grade.A, table.Lookup(ProducerTrust.XmlProducer, "state", "com.good.app"));
    }

    [Fact]
    public void Gates_AMayAuthorizeRoutine_BAndBelowNeedCorroboration()
    {
        Assert.True(ProducerTrust.CanAuthorizeRoutine(ProducerTrust.Grade.A));
        Assert.False(ProducerTrust.CanAuthorizeRoutine(ProducerTrust.Grade.B));
        Assert.True(ProducerTrust.RequiresCorroborationBeforeIrreversible(ProducerTrust.Grade.B));
        Assert.True(ProducerTrust.RequiresCorroborationBeforeIrreversible(ProducerTrust.Grade.C));
        Assert.False(ProducerTrust.RequiresCorroborationBeforeIrreversible(ProducerTrust.Grade.A));
    }

    [Fact]
    public void FromJson_RoundTrips()
    {
        const string json = """
            {
              "grades": [
                { "producer": "platform.uiautomator", "category": "state", "grade": "A" },
                { "producer": "perception.live.vision", "grade": "B" }
              ],
              "overrides": [
                { "package": "com.evil.app", "producer": "platform.uiautomator", "category": "state", "grade": "C" }
              ]
            }
            """;
        var table = ProducerTrust.FromJson(json);
        Assert.Equal(ProducerTrust.Grade.A, table.Lookup("platform.uiautomator", "state"));
        Assert.Equal(ProducerTrust.Grade.B, table.Lookup("perception.live.vision", "anything")); // producer 默认
        Assert.Equal(ProducerTrust.Grade.C,
            table.Lookup("platform.uiautomator", "state", "com.evil.app"));
    }
}

file static class TrustTableExtensions
{
    public static ProducerTrust.TrustTable WithOverride(
        this ProducerTrust.TrustTable table, ProducerTrust.Override addition) =>
        new(table.Grades, table.PackageOverrides.Append(addition).ToArray());
}
