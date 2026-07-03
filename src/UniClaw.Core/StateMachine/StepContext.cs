using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// IVisionProvider — 最小视觉接口定义 (Phase 2 placeholder)。
/// Phase 3 实现真实 ADB/Vision 交互。
/// </summary>
public interface IVisionProvider
{
    /// <summary>获取当前页面的分析结果</summary>
    Task<PageAnalysis?> GetCurrentPageAnalysisAsync(CancellationToken ct = default);
}

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
    DynamicChildManager ChildMgr,
    INodeRegistry NodeRegistry,
    TraceCoordinator Trace,
    PageSnapshotManager SnapshotMgr,
    NodeStackAdapter Stack,
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
