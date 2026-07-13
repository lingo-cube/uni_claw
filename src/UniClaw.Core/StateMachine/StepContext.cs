using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// IVisionProvider — 视觉分析接口。
/// 5 方法: 页面分析 (2) + 滚动感知 (3，默认实现用于向后兼容)。
/// </summary>
public interface IVisionProvider
{
    /// <summary>分析当前页面截图 → PageAnalysis（元素列表、菜单、弹窗等）</summary>
    Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default);

    /// <summary>在启动器中查找目标 app 的图标坐标</summary>
    Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default);

    // ── 滚动感知接口（默认实现用于向后兼容） ─────────────────────────────

    /// <summary>检查当前页面是否有滚动数据</summary>
    /// <returns>如果有滚动支持返回 true，否则返回 false</returns>
    virtual bool HasScroll() => false;

    /// <summary>获取当前滚动进度（0.0 = 顶部，1.0 = 底部）</summary>
    /// <returns>当前滚动进度，无滚动数据时返回 0.0</returns>
    virtual double GetScrollProgress() => 0.0;

    /// <summary>检查是否到达滚动内容的末尾</summary>
    /// <returns>已到达末尾返回 true，否则返回 false（无滚动数据视为已到底）</returns>
    virtual bool IsEndOfList() => true;
}

/// <summary>
/// App 入口坐标（归一化 0-1）。FindAppEntryAsync 的返回值。
/// </summary>
public sealed record class AppEntryPoint(double X, double Y);

/// <summary>
/// StepContext — sealed record class, 封装单步执行的所有依赖。
/// 构造后不可变 (record immutability)。
/// 包含 13 个依赖字段: context, state_machine, vision, action, child_mgr,
/// node_registry, trace, snapshot_mgr, stack, last_known_path,
/// last_recorded_path, last_recorded_action。
/// </summary>
public sealed record class StepContext(
    TraversalRuntimeContext Context,
    TraversalFSM StateMachine,
    IVisionProvider Vision,
    IActionExecutor Action,
    IDynamicChildManager ChildMgr,
    INodeRegistry NodeRegistry,
    ITraceCoordinator Trace,
    IPageSnapshotManager SnapshotMgr,
    INodeStackAdapter Stack,
    ErrorHandler? ErrorHandler = null,
    PopupHandler? PopupHandler = null,
    string? LastKnownPath = null,
    string? LastRecordedPath = null,
    string? LastRecordedAction = null);

/// <summary>
/// StepResult — sealed record class, 捕获 orchestrator 单步结果。
/// 包含 6 个结果字段: next_state, path_changed, child_pushed,
/// frame_completed, anti_loop_triggered, frame_override_triggered。
/// </summary>
public sealed record class StepResult(
    TraversalState NextState,
    bool PathChanged,
    bool ChildPushed,
    bool FrameCompleted,
    bool AntiLoopTriggered,
    bool FrameOverrideTriggered);
