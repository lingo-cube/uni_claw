using System.Collections.Immutable;
using UniClaw.Runtime.Harness;

namespace UniClaw.Runtime.DriverHost;

/// <summary>Closed status vocabulary for in-process trace reads.</summary>
public enum TraceQueryStatus
{
    /// <summary>The explicitly requested finalized trace data was found.</summary>
    Found,
    /// <summary>The run or its finalized trace is not currently available.</summary>
    Unavailable,
    /// <summary>The query uses an unsupported or malformed bounded value.</summary>
    InvalidRequest,
    /// <summary>The cursor is not valid for the requested run, trace, or filter.</summary>
    CursorMismatch,
}

/// <summary>Immutable identity and diagnostic summary of a finalized trace.</summary>
public sealed record TraceRunSummary(int SchemaVersion, string TraceRunId, string? TraceId, string? RunId, int SpanCount, ImmutableArray<string> Diagnostics);
/// <summary>Discriminated summary query result.</summary>
public sealed record TraceRunSummaryResult(TraceQueryStatus Status, TraceRunSummary? Summary, ImmutableArray<string> Diagnostics)
{
    /// <summary>Creates a typed unavailable result.</summary>
    public static TraceRunSummaryResult Unavailable(string message) => new(TraceQueryStatus.Unavailable, null, [message]);
}

/// <summary>Bounded exact-match span filter.</summary>
public sealed record TraceSpanFilter(string? Name = null, string? Layer = null, string? Component = null, string? Outcome = null, string? ParentSpanId = null);
/// <summary>Continuation cursor bound to one finalized trace and filter.</summary>
public sealed record TraceSpanCursor(string RunId, string TraceRunId, long LastSequence, string FilterFingerprint);
/// <summary>One immutable trace span with a read-model sequence.</summary>
public sealed record TraceSpanEnvelope(long Sequence, TraceSpan Span);
/// <summary>One page of a trace span query.</summary>
public sealed record TraceSpanPage(TraceQueryStatus Status, string RunId, ImmutableArray<TraceSpanEnvelope> Spans, TraceSpanCursor? NextCursor, bool HasMore, ImmutableArray<string> Diagnostics);

internal static class TraceSpanReadModelVocabulary
{
    public static readonly ImmutableHashSet<string> Names = ["RunSemanticGoal", "RefreshSnapshot", "LoweredAction", "ObserveAsync", "ExecuteAsync", "PlanStep", "StartupBootstrap", "PerceptionCapture", "PerceptionVision", "PerceptionFusion", "PerceptionCanonicalize", "PerceptionAdmission"];
    public static readonly ImmutableHashSet<string> Layers = ["ORCHESTRATION", "AGENT", "STARTUP", "WORLD", "CONTAINER", "TRAVERSAL", "RECOVERY", "ENVIRONMENT", "CAPABILITY", "HARNESS"];
    public static readonly ImmutableHashSet<string> Outcomes = ["SUCCEEDED", "FAILED", "CANCELLED", "UNKNOWN"];
    public static readonly ImmutableHashSet<string> Components = ["runtime.invocation", "agent.execution", "intent.execution", "container.refresh", "traversal.execution", "traversal.plan-step", "environment.observe", "environment.execute", "recovery.attempt", "capability.invocation", "startup.bootstrap", "perception.capture", "perception.vision", "perception.fusion", "perception.canonicalize", "perception.admission"];

    public static bool Validate(TraceSpanFilter filter, out string? error)
    {
        error = null;
        if (new[] { filter.Name, filter.Layer, filter.Component, filter.Outcome, filter.ParentSpanId }.Any(v => v is not null && string.IsNullOrWhiteSpace(v))) { error = "Filter values must not be blank."; return false; }
        if (filter.Name is not null && !Names.Contains(filter.Name)) { error = $"Unsupported span name '{filter.Name}'."; return false; }
        if (filter.Layer is not null && !Layers.Contains(filter.Layer)) { error = $"Unsupported span layer '{filter.Layer}'."; return false; }
        if (filter.Component is not null && !Components.Contains(filter.Component)) { error = $"Unsupported span component '{filter.Component}'."; return false; }
        if (filter.Outcome is not null && !Outcomes.Contains(filter.Outcome)) { error = $"Unsupported span outcome '{filter.Outcome}'."; return false; }
        return true;
    }

    public static string Fingerprint(TraceSpanFilter filter) => string.Join("\u001f", filter.Name ?? "", filter.Layer ?? "", filter.Component ?? "", filter.Outcome ?? "", filter.ParentSpanId ?? "");
}
