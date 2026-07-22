using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// StepContext — sealed record class, 封装单步执行的所有依赖。
/// 构造后不可变 (record immutability)。
/// 包含 19 个依赖字段: context, state_machine, brain (IUniBrain),
/// screen_state (IScreenStateProvider, 滚动感知独立接口), action, child_mgr,
/// node_registry, trace, snapshot_mgr, stack, error_handler,
/// popup_handler, container_handler, handler_trace, effective_max_depth,
/// last_known_path, last_recorded_path, last_recorded_action, scroll_swipe。
/// </summary>
public sealed record class StepContext(
    TraversalRuntimeContext Context,
    TraversalFSM StateMachine,
    IUniBrain Brain,
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
