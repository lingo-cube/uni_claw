using System.Collections.Immutable;

namespace UniClaw.Kernel.Trace;

/// <summary>
/// 显式禁用 tracing 的 no-op adapter（TRC-001：禁 nullable / 全局可变 /
/// 隐式 fallback——调用方必须显式传入）。Instance 供 UniKernel 组合缝
/// 注入；For(correlation) 供 RunTraceFactory.BeginDisabled 产出显式空
/// artifact（零 span + tracing-disabled diagnostic）。禁用面无 emission
/// 门（零捕获即零可封存内容）。
/// </summary>
public sealed class DisabledRunTrace : IRunTraceSink
{
    private const string SchemaVersion = "trc/0.1";

    private readonly string? _runId;

    private DisabledRunTrace(string? runId) => _runId = runId;

    /// <summary>无状态单例（等价 string.Empty，非 ambient authority）。</summary>
    public static DisabledRunTrace Instance { get; } = new(runId: null);

    internal static DisabledRunTrace For(RunCorrelation correlation) => new(correlation.RunId);

    public ITraceOperationScope StartOperation(
        SpanDefinition definition, TraceContext? parent, IReadOnlyList<TraceReference> references)
        => NoOpOperationScope.Instance;

    public void MarkOutcomeEmitted()
    {
    }

    public RunTraceArtifact Finalize() => new(
        SchemaVersion,
        _runId ?? "tracing-disabled",
        "trc-disabled",
        RootSpanId: null,
        Spans: ImmutableArray<TraceSpan>.Empty,
        RecorderDiagnostics: new[] { new TraceDiagnostic("tracing-disabled") }.ToImmutableArray());
}
