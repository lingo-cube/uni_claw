using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// IVisionProvider — 视觉分析接口 (过渡期保留，后续迁移到 IPageAnalyzer)。
/// 2 方法: 页面分析。滚动感知方法已分离到 IScreenStateProvider (Traversal namespace)。
/// </summary>
public interface IVisionProvider
{
    /// <summary>分析当前页面截图 → PageAnalysis（元素列表、菜单、弹窗等）</summary>
    Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default);

    /// <summary>在启动器中查找目标 app 的图标坐标</summary>
    Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default);
}

/// <summary>
/// App 入口坐标（归一化 0-1）。FindAppEntryAsync 的返回值。
/// </summary>
public sealed record class AppEntryPoint(double X, double Y);

/// <summary>
/// StepContext — sealed record class, 封装单步执行的所有依赖。
/// 构造后不可变 (record immutability)。
/// 包含 19 个依赖字段: context, state_machine, vision (过渡期保留),
/// screen_state (新增, 滚动感知独立接口), action, child_mgr,
/// node_registry, trace, snapshot_mgr, stack, error_handler,
/// popup_handler, container_handler, handler_trace, effective_max_depth,
/// last_known_path, last_recorded_path, last_recorded_action, scroll_swipe。
/// </summary>
public sealed record class StepContext(
    TraversalRuntimeContext Context,
    TraversalFSM StateMachine,
    IVisionProvider Vision,
    IScreenStateProvider ScreenState,
    IActionExecutor Action,
    IDynamicChildManager ChildMgr,
    INodeRegistry NodeRegistry,
    ITraceCoordinator Trace,
    IPageSnapshotManager SnapshotMgr,
    INodeStackAdapter Stack,
    ErrorHandler? ErrorHandler = null,
    PopupHandler? PopupHandler = null,
    ContainerHandler? ContainerHandler = null,
    IHandlerTraceWriter? HandlerTrace = null,
    int EffectiveMaxDepth = 100,
    string? LastKnownPath = null,
    string? LastRecordedPath = null,
    string? LastRecordedAction = null,
    ScrollSwipeConfig ScrollSwipe = null!);

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
