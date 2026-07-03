using System.Collections.Immutable;

namespace UniClaw.Core.Trace;

/// <summary>
/// Trace 节点基类 — 4 类型层级: TraceNode (base), SessionNode, StepNode, SpanNode。
/// 所有子类型为 sealed record class。
/// </summary>
/// <param name="SpanId">跨度ID (ULID)</param>
/// <param name="ParentSpanId">父跨度ID</param>
/// <param name="Timestamp">时间戳</param>
/// <param name="Metadata">元数据</param>
public abstract record class TraceNode(
    string SpanId,
    string? ParentSpanId,
    DateTimeOffset Timestamp,
    ImmutableDictionary<string, string>? Metadata = null);

/// <summary>
/// 会话节点 — 记录遍历会话级信息。
/// </summary>
/// <param name="SpanId">跨度ID</param>
/// <param name="ParentSpanId">父跨度ID</param>
/// <param name="Timestamp">时间戳</param>
/// <param name="Metadata">元数据</param>
/// <param name="SessionId">会话ID</param>
/// <param name="DeviceInfo">设备信息</param>
/// <param name="AppInfo">应用信息</param>
/// <param name="Status">会话状态</param>
public sealed record class SessionNode(
    string SpanId,
    string? ParentSpanId,
    DateTimeOffset Timestamp,
    ImmutableDictionary<string, string>? Metadata = null,
    string? SessionId = null,
    string? DeviceInfo = null,
    string? AppInfo = null,
    string? Status = null) : TraceNode(SpanId, ParentSpanId, Timestamp, Metadata);

/// <summary>
/// 步骤节点 — 记录单步遍历信息。
/// </summary>
/// <param name="SpanId">跨度ID</param>
/// <param name="ParentSpanId">父跨度ID</param>
/// <param name="Timestamp">时间戳</param>
/// <param name="Metadata">元数据</param>
/// <param name="StepType">步骤类型</param>
/// <param name="NodeId">相关节点ID</param>
/// <param name="Action">执行的动作</param>
/// <param name="Result">步骤结果</param>
public sealed record class StepNode(
    string SpanId,
    string? ParentSpanId,
    DateTimeOffset Timestamp,
    ImmutableDictionary<string, string>? Metadata = null,
    string? StepType = null,
    string? NodeId = null,
    string? Action = null,
    string? Result = null) : TraceNode(SpanId, ParentSpanId, Timestamp, Metadata);

/// <summary>
/// 跨度节点 — 记录子跨度信息 (AI调用/执行/错误等)。
/// </summary>
/// <param name="SpanId">跨度ID</param>
/// <param name="ParentSpanId">父跨度ID</param>
/// <param name="Timestamp">时间戳</param>
/// <param name="Metadata">元数据</param>
/// <param name="SpanType">跨度类型</param>
/// <param name="DurationMs">持续时间(毫秒)</param>
/// <param name="Status">跨度状态</param>
public sealed record class SpanNode(
    string SpanId,
    string? ParentSpanId,
    DateTimeOffset Timestamp,
    ImmutableDictionary<string, string>? Metadata = null,
    string? SpanType = null,
    double? DurationMs = null,
    string? Status = null) : TraceNode(SpanId, ParentSpanId, Timestamp, Metadata);
