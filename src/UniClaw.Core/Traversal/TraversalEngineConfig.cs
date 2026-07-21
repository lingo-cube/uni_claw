using System.Collections.Immutable;

namespace UniClaw.Core.Traversal;

/// <summary>
/// 引擎配置 — 合并 SimulationConfig (DelayPerStepMs 通用化命名)。
/// sealed record class, init-only 属性 (P-5)。
/// </summary>
public sealed record class TraversalEngineConfig
{
    /// <summary>最大步数（安全上限，防止死循环）</summary>
    public int MaxSteps { get; init; } = 1000;

    /// <summary>栈最大深度</summary>
    public int MaxDepth { get; init; } = 10;

    /// <summary>true = handler 异常立即中断; false = 记录后继续 (Log-and-Continue)</summary>
    public bool ThrowOnError { get; init; } = false;

    /// <summary>是否记录每步 trace (TraceRecord[])</summary>
    public bool TraceEnabled { get; init; } = true;

    /// <summary>每步延迟（毫秒）。仿真: 模拟延迟; 生产: 等待 UI 稳定。0 = 无延迟</summary>
    public int DelayPerStepMs { get; init; } = 0;

    /// <summary>引擎级默认滑动坐标配置。页面级可通过 IVisionProvider.GetScrollSwipeConfig() 覆盖。</summary>
    public ScrollSwipeConfig ScrollSwipe { get; init; } = new();

    /// <summary>遍历生命周期钩子 — init-only, 不可运行时修改。空数组 = 零开销跳过。</summary>
    public ImmutableArray<ITraversalHook> Hooks { get; init; } = ImmutableArray<ITraversalHook>.Empty;
}
