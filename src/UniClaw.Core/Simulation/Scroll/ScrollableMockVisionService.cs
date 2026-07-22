using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 支持滚动的 Mock Vision Service — 薄适配器 (见设计 §5): 仅委托共享 <see cref="SimulatedScreen"/>。
/// <see cref="ScrollableMockActionExecutor"/> 与本类在构造时注入同一个 <see cref="SimulatedScreen"/> 实例,
/// 使 swipe (变异) 与随后的页面分析 (观察) 作用在一致状态上。本类不再持有滚动可变状态。
/// 过渡期: 实现 IVisionProvider (页面分析) + IScreenStateProvider (滚动状态)。
/// </summary>
public sealed class ScrollableMockVisionService : IVisionProvider, IScreenStateProvider
{
    private readonly SimulatedScreen _screen;

    /// <summary>共享模拟屏幕</summary>
    public SimulatedScreen Screen => _screen;

    /// <summary>当前页面 ID (委托屏幕)</summary>
    public string CurrentPageId => _screen.CurrentPageId;

    /// <summary>创建 ScrollableMockVisionService</summary>
    /// <param name="screen">与动作执行器共享的 <see cref="SimulatedScreen"/></param>
    public ScrollableMockVisionService(SimulatedScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    // ── IVisionProvider 实现 (页面分析) ──────────────────────────

    /// <inheritdoc />
    public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
        => Task.FromResult(_screen.GetPageAnalysis());

    /// <inheritdoc />
    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
        => Task.FromResult<AppEntryPoint?>(new AppEntryPoint(0.5, 0.5));

    // ── IScreenStateProvider 实现 (滚动状态) ──────────────────────────

    /// <inheritdoc />
    public bool HasScroll() => _screen.HasScroll;

    /// <inheritdoc />
    public double GetScrollProgress() => _screen.GetScrollProgress();

    /// <inheritdoc />
    public bool IsEndOfList() => _screen.IsEndOfList();

    /// <inheritdoc />
    public ScrollSwipeConfig? GetScrollSwipeConfig()
        => _screen.GetScrollSwipeConfig(_screen.CurrentPageId);
}
