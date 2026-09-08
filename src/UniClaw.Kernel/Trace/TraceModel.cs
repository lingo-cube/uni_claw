namespace UniClaw.Kernel.Trace;

/// <summary>Recorder 自身故障 / 词表执法信息；不得伪装成 Runtime failure。</summary>
public sealed record TraceDiagnostic(string Reason);

/// <summary>
/// span 内有界事件：reference-first；ReasonCode 只允许 owner 已发布词汇
/// 透传（G4 三边界：封闭 union / owner 词表 / 有 durable record 时引用
/// 优先），禁自由文本考古。
/// </summary>
public sealed record TraceEvent(
    string EventId,
    IReadOnlyList<TraceReference> References,
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
    IReadOnlyList<TraceReference> References,
    IReadOnlyList<TraceEvent> Events,
    int CaptureSequence);

/// <summary>
/// finalize 后的 immutable structural projection：非权威、reference-
/// oriented、不参与任何 Runtime 决策（ADR-0013）。Timing 为可选观测
/// 元数据——bullet 版刻意省略（canonical clock 维持 Deferred ⑪）。
/// </summary>
public sealed record RunTraceArtifact(
    string SchemaVersion,
    string RunId,
    string TraceId,
    string? RootSpanId,
    IReadOnlyList<TraceSpan> Spans,
    IReadOnlyList<TraceDiagnostic> RecorderDiagnostics);
