using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.LocalVision;

/// <summary>
/// ScrollSwipeConfig 单测 — MaxEmptyScrollRetries 空差分重试次数 (R-12)。
/// 重试逻辑 (N 次连续空差分确认到底) 已由 ScrollLoopTerminationTests 覆盖 (14.3)。
/// </summary>
public class ScrollSwipeConfigTests
{
    // ── 14.1: 默认 MaxEmptyScrollRetries == 1 ──

    [Fact(DisplayName = "14.1: 默认 MaxEmptyScrollRetries == 1")]
    public void Default_MaxEmptyScrollRetries_IsOne()
    {
        var cfg = new ScrollSwipeConfig();

        Assert.Equal(1, cfg.MaxEmptyScrollRetries);
    }

    // ── 14.2: MaxEmptyScrollRetries=0 → 立即到底 (无重试) ──

    [Fact(DisplayName = "14.2: MaxEmptyScrollRetries=0 → 字段为 0 (空差分立即到底)")]
    public void ZeroRetries_FieldIsZero()
    {
        var cfg = new ScrollSwipeConfig(MaxEmptyScrollRetries: 0);

        Assert.Equal(0, cfg.MaxEmptyScrollRetries);
    }
}
