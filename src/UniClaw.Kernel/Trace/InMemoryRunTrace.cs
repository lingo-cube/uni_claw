using System.Security.Cryptography;
using System.Text;

namespace UniClaw.Kernel.Trace;

/// <summary>
/// 确定性内存 recorder（TRC-001 bullet 唯一生产 adapter；read model /
/// persistence / OTel export 属未来扩张，需真实 buyer 证据）。特点：
/// 全路径 no-throw（一切内部故障 → TraceDiagnostic，acceptance 2）；
/// 词表执法 fail-closed（非法 ref / event 丢弃 + diagnostic，acceptance
/// 4）；technical ids 确定性派生（TraceId = RunId 内容哈希；SpanId =
/// 捕获序数；无 random / ambient，acceptance 6/7）；finalize 幂等
/// （acceptance 8）；未关闭 span 如实标 Incomplete（acceptance 9）。
/// </summary>
internal sealed class InMemoryRunTrace : IRunTraceSink
{
    private const string SchemaVersion = "trc/0.1";

    private readonly string _runId;
    private readonly string _traceId;
    private readonly List<SpanBuffer> _spans = new();
    private readonly List<TraceDiagnostic> _diagnostics = new();
    private RunTraceArtifact? _finalized;
    private int _sequence;

    public InMemoryRunTrace(RunCorrelation correlation)
    {
        _runId = correlation.RunId;
        _traceId = "trc-" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(correlation.RunId))).ToLowerInvariant()[..12];
    }

    public ITraceOperationScope StartOperation(
        SpanDefinition definition, TraceContext? parent, IReadOnlyList<TraceReference> references)
    {
        try
        {
            if (_finalized is not null)
            {
                Diagnose("start-after-finalize");
                return NoOpOperationScope.Instance;
            }
            if (definition is null)
            {
                Diagnose("definition-null");
                return NoOpOperationScope.Instance;
            }

            var buffer = new SpanBuffer($"sp-{_sequence++:D4}", parent?.SpanId, definition, _sequence);
            buffer.References.AddRange(FilterReferences(definition, references));
            _spans.Add(buffer);
            return new OperationScope(this, buffer, _traceId);
        }
        catch (Exception e)
        {
            Diagnose($"recorder-fault:{e.GetType().Name}");
            return NoOpOperationScope.Instance;
        }
    }

    public RunTraceArtifact Finalize()
    {
        if (_finalized is not null)
            return _finalized;

        var spans = _spans.Select(b => new TraceSpan(
            b.SpanId,
            b.ParentSpanId,
            b.Definition.OperationId,
            b.Completed ? b.Outcome : StructuralOutcome.Incomplete,
            b.References,
            b.Events,
            b.CaptureSequence)).ToList();
        _finalized = new RunTraceArtifact(
            SchemaVersion, _runId, _traceId,
            RootSpanId: spans.FirstOrDefault()?.SpanId,
            spans,
            _diagnostics.ToList());
        return _finalized;
    }

    private List<TraceReference> FilterReferences(SpanDefinition definition, IReadOnlyList<TraceReference> references)
    {
        var kept = new List<TraceReference>();
        foreach (var reference in references ?? Array.Empty<TraceReference>())
        {
            if (definition.AllowedReferenceKinds.Contains(reference.Kind))
                kept.Add(reference);
            else
                Diagnose($"reference-kind-not-allowed:{reference.Kind}:{definition.OperationId}");
        }
        return kept;
    }

    private void Record(SpanBuffer span, string eventId, IReadOnlyList<TraceReference> references, string? reasonCode)
    {
        if (_finalized is not null)
        {
            Diagnose("record-after-finalize");
            return;
        }
        if (span.Completed)
        {
            Diagnose("record-after-complete");
            return;
        }
        if (!span.Definition.AllowedEvents.Contains(eventId))
        {
            Diagnose($"event-not-allowed:{eventId}:{span.Definition.OperationId}");
            return;
        }
        span.Events.Add(new TraceEvent(eventId, FilterReferences(span.Definition, references), reasonCode));
    }

    private void Complete(SpanBuffer span, StructuralOutcome outcome)
    {
        if (_finalized is not null)
        {
            Diagnose("complete-after-finalize");
            return;
        }
        if (span.Completed)
        {
            Diagnose("double-complete");
            return;
        }
        span.Completed = true;
        span.Outcome = outcome;
    }

    private void Diagnose(string reason) => _diagnostics.Add(new TraceDiagnostic(reason));

    private sealed class SpanBuffer
    {
        public SpanBuffer(string spanId, string? parentSpanId, SpanDefinition definition, int captureSequence)
        {
            SpanId = spanId;
            ParentSpanId = parentSpanId;
            Definition = definition;
            CaptureSequence = captureSequence;
        }

        public string SpanId { get; }
        public string? ParentSpanId { get; }
        public SpanDefinition Definition { get; }
        public int CaptureSequence { get; }
        public List<TraceReference> References { get; } = new();
        public List<TraceEvent> Events { get; } = new();
        public bool Completed { get; set; }
        public StructuralOutcome Outcome { get; set; } = StructuralOutcome.Incomplete;
    }

    private sealed class OperationScope : ITraceOperationScope
    {
        private readonly InMemoryRunTrace _owner;
        private readonly SpanBuffer _buffer;

        public OperationScope(InMemoryRunTrace owner, SpanBuffer buffer, string traceId)
        {
            _owner = owner;
            _buffer = buffer;
            Context = new TraceContext(traceId, buffer.SpanId);
        }

        public TraceContext Context { get; }

        public void Record(string eventId, IReadOnlyList<TraceReference> references, string? reasonCode = null) =>
            _owner.Record(_buffer, eventId, references, reasonCode);

        public void Complete(StructuralOutcome outcome) => _owner.Complete(_buffer, outcome);

        public void Dispose()
        {
            // 未显式 Complete 的 span 在 Finalize 时如实标 Incomplete
            // （acceptance 9），此处无额外动作。
        }
    }
}

/// <summary>无操作 scope：Recorder 自身故障 / finalize 后的 fail-safe 返回值。</summary>
internal sealed class NoOpOperationScope : ITraceOperationScope
{
    public static readonly NoOpOperationScope Instance = new();

    private NoOpOperationScope() => Context = new TraceContext("trc-disabled", "sp-disabled");

    public TraceContext Context { get; }

    public void Record(string eventId, IReadOnlyList<TraceReference> references, string? reasonCode = null)
    {
    }

    public void Complete(StructuralOutcome outcome)
    {
    }

    public void Dispose()
    {
    }
}
