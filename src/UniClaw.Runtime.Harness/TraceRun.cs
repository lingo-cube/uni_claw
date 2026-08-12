using System.Collections.Immutable;

namespace UniClaw.Runtime.Harness;

/// <summary>Immutable, versioned hierarchical observability run.
/// Distinct from TraceCaptureSession — this is finalized diagnostic data,
/// not capture/lifecycle ownership.</summary>
public sealed record TraceRun
{
    public int SchemaVersion { get; init; } = 1;
    public string TraceRunId { get; init; } = "";
    public string? TraceId { get; init; }
    public string? RunId { get; init; }
    public ImmutableArray<TraceSpan> Spans { get; init; } = [];
    public ImmutableArray<string> Diagnostics { get; init; } = [];
}

/// <summary>Immutable hierarchical observability span — one bounded operation.</summary>
public sealed record TraceSpan
{
    public int SchemaVersion { get; init; } = 1;
    public string SpanId { get; init; } = "";
    public string? ParentSpanId { get; init; }
    public string Name { get; init; } = "";
    public string Layer { get; init; } = "";
    public string Component { get; init; } = "";
    public long StartOffsetNs { get; init; }
    public long DurationNs { get; init; }
    public string Outcome { get; init; } = "UNKNOWN";
    public ImmutableArray<TraceSpanAttribute> Attributes { get; init; } = [];
    public ImmutableArray<ObservabilityEvent> Events { get; init; } = [];
}

/// <summary>Immutable structured attribute on a span.</summary>
public sealed record TraceSpanAttribute
{
    public string Key { get; init; } = "";
    public string? Value { get; init; }
}

/// <summary>Immutable point-in-time observability event within a span.</summary>
public sealed record ObservabilityEvent
{
    public int SchemaVersion { get; init; } = 1;
    public string EventId { get; init; } = "";
    public string SpanId { get; init; } = "";
    public long TimestampOffsetNs { get; init; }
    public ImmutableArray<TraceSpanAttribute> Attributes { get; init; } = [];
}
