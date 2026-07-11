using System.Collections.Immutable;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// 单步 trace 记录 — 独立于 ITraceRecorder (内存 trace vs 外部持久化)。
/// sealed record class (P-5)。
/// </summary>
/// <param name="StepNumber">步序号</param>
/// <param name="FromState">转换前状态</param>
/// <param name="ToState">转换后状态</param>
/// <param name="CurrentNodeId">当前节点 ID</param>
/// <param name="CurrentPageId">当前页面 ID</param>
/// <param name="ActionExecuted">执行的操作</param>
/// <param name="ActionSuccess">操作是否成功</param>
/// <param name="ChildPushed">是否推入子节点</param>
/// <param name="FrameCompleted">是否完成当前帧</param>
/// <param name="SpanTypes">Per-step semantic event classification (replaces single SpanType?)</param>
/// <param name="PageFrom">源页面 (page navigation source)</param>
/// <param name="PageTo">目标页面 (page navigation destination)</param>
/// <param name="PageTransitionType">页面转换类型</param>
/// <param name="StepDurationMs">步骤执行时间 (milliseconds)</param>
public sealed record class TraceRecord(
    int StepNumber,
    TraversalState FromState,
    TraversalState ToState,
    string? CurrentNodeId,
    string? CurrentPageId,
    string? ActionExecuted,
    bool ActionSuccess,
    bool ChildPushed,
    bool FrameCompleted,
    ImmutableArray<SpanType> SpanTypes = default,
    string? PageFrom = null,
    string? PageTo = null,
    string? PageTransitionType = null,
    double? StepDurationMs = null);
