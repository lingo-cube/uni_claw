using UniClaw.Host;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// PER-009 S6b-2：D8 探测状态机（纯逻辑）——服务未启用 → 60s 窗口 ×
/// 每 Run ≤3 次探测；瞬时失败不占次数；超限后持续 degraded 标记。
/// </summary>
public sealed class XmlProbeStateMachineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Initial_ShouldProbe()
    {
        var m = new UiAutomatorDump.ProbeStateMachine();
        Assert.True(m.ShouldProbe(T0));
        Assert.False(m.Exhausted);
    }

    [Fact]
    public void StructuralFailure_OpensWindow_BlocksUntilExpiry()
    {
        var m = new UiAutomatorDump.ProbeStateMachine();
        m.MarkUnavailable(T0);
        // 窗口内：不探测
        Assert.False(m.ShouldProbe(T0.AddSeconds(30)));
        // 窗口过期：恢复探测
        Assert.True(m.ShouldProbe(T0.AddSeconds(61)));
    }

    [Fact]
    public void ThreeStructuralFailures_ExhaustsForRun()
    {
        var m = new UiAutomatorDump.ProbeStateMachine();
        m.MarkUnavailable(T0);                          // 探测 1
        m.MarkUnavailable(T0.AddSeconds(61));           // 探测 2（窗口过期后再试）
        m.MarkUnavailable(T0.AddSeconds(122));          // 探测 3 → 超限
        Assert.True(m.Exhausted);
        Assert.False(m.ShouldProbe(T0.AddSeconds(183))); // 第 4 次窗口过期也不许
    }

    [Fact]
    public void TransientFailure_DoesNotConsumeProbe()
    {
        var m = new UiAutomatorDump.ProbeStateMachine();
        for (var i = 0; i < 10; i++)
            m.MarkTransient(); // 10 次瞬时失败
        Assert.False(m.Exhausted);
        Assert.True(m.ShouldProbe(T0)); // 仍然可以探测
    }

    [Fact]
    public void Mixed_StructuralPlusTransient_OnlyStructuralCounts()
    {
        var m = new UiAutomatorDump.ProbeStateMachine();
        m.MarkTransient();
        m.MarkUnavailable(T0);                  // 结构 1
        m.MarkTransient();
        m.MarkUnavailable(T0.AddSeconds(61));   // 结构 2
        m.MarkTransient();
        m.MarkUnavailable(T0.AddSeconds(122));  // 结构 3 → 超限
        Assert.True(m.Exhausted);
    }
}
