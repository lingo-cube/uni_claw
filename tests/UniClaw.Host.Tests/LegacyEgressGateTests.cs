using UniClaw.Host;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// PER-014 R5 / PER-012 M-08：legacy role-state 发射回滚旗——默认关。
/// 关 = 正常 typed run 零 XML role-state claims（ConflictResolver legacy
/// 路径休眠）；开 = run 由 HostRunner 落 facts.legacyEgress 标记 legacy/degraded。
/// Next() 发射行为需真感知会话（全真档由 HostLiveFullTests env 门控承载），
/// 本测试锁旗的默认值与 run 级标记初值（fail-closed 默认面）。
/// </summary>
public sealed class LegacyEgressGateTests
{
    [Fact]
    public void LegacyStateEgress_DefaultsOff()
    {
        var options = new HostRunner.HostOptions();
        Assert.False(options.LegacyStateEgress);
    }

    [Fact]
    public void LegacyEgressObserved_DefaultsFalse_BeforeAnyEmission()
    {
        var clock = new HostUtilities.VirtualClock();
        using var feed = new LivePerception.LiveFrameFeed(
            clock,
            new LivePerception.LiveAssets("dev", "screen", "/nonexistent-provider", "python3"),
            () => "on");
        Assert.False(feed.LegacyEgressObserved);
    }
}
