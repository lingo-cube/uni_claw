using System.Collections.Immutable;

namespace UniClaw.Kernel.Trace;

/// <summary>Recorder 自身故障 / 词表执法信息；不得伪装成 Runtime failure。</summary>
public sealed record TraceDiagnostic(string Reason);

/// <summary>
/// span 内有界事件：reference-first；ReasonCode 只允许所属
/// TraceEventDefinition.AllowedReasonCodes 的成员（owner 已发布词汇，
/// G4 三边界），禁自由文本考古。
/// </summary>
public sealed record TraceEvent(
    string EventId,
    ImmutableArray<TraceReference> References,
    string? ReasonCode);

/// <summary>
/// 技术 correlation identity（TraceId/SpanId 无 domain authority；RunId
/// 才是唯一语义根）。bullet 实现确定性派生（序数 / 内容哈希），无
/// random / ambient。
/// </summary>
public sealed record TraceContext(string TraceId, string SpanId);

/// <summary>一次 SpanDefinition 的运行时 occurrence（immutable）。</summary>
public sealed record TraceSpan(
    string SpanId,
    string? ParentSpanId,
    string SpanDefinitionId,
    StructuralOutcome StructuralOutcome,
    ImmutableArray<TraceReference> References,
    ImmutableArray<TraceEvent> Events,
    int CaptureSequence);

/// <summary>
/// finalize 后的 immutable structural projection（ADR-0013）：非权威、
/// reference-oriented、不参与任何 Runtime 决策。深冻结（评审 Standards
/// V2）：全部集合成员为 ImmutableArray，无 List 强转改写面。Timing 为
/// 可选观测元数据——bullet 版刻意省略（canonical clock 维持 Deferred ⑪）。
/// </summary>
public sealed record RunTraceArtifact(
    string SchemaVersion,
    string RunId,
    string TraceId,
    string? RootSpanId,
    ImmutableArray<TraceSpan> Spans,
    ImmutableArray<TraceDiagnostic> RecorderDiagnostics);
