using System.Collections.Immutable;

namespace UniClaw.Runtime.Harness;

/// <summary>
/// Scenario observability conformance assertions.
/// Validates stable structural properties of a TraceRun:
/// required spans, events, parent closure, layer/component validity.
///
/// Does NOT assert exact timing, private call order, CLR type names,
/// or diagnostic text.
/// </summary>
public static class TraceAssertions
{
    /// <summary>Result of a conformance check.</summary>
    public sealed record TraceConformanceResult
    {
        public ImmutableArray<string> Errors { get; init; } = [];
        public bool Passed => Errors.IsEmpty;
        public static TraceConformanceResult Ok => new();
        public static TraceConformanceResult Fail(params string[] errors) => new() { Errors = [.. errors] };
    }

    /// <summary>Assert that a span with the given layer and component exists.</summary>
    public static TraceConformanceResult HasSpan(
        TraceRun trace, string layer, string component, string? name = null)
    {
        var match = trace.Spans.FirstOrDefault(s =>
            s.Layer == layer && s.Component == component
            && (name is null || s.Name == name));
        if (match is null)
            return TraceConformanceResult.Fail(
                $"Required span not found: layer={layer}, component={component}{(name is null ? "" : $", name={name}")}.");
        return TraceConformanceResult.Ok;
    }

    /// <summary>Assert that every span's layer is in the approved taxonomy.</summary>
    public static TraceConformanceResult AllLayersValid(TraceRun trace)
    {
        var approved = new HashSet<string>(StringComparer.Ordinal)
        {
            "ORCHESTRATION", "AGENT", "STARTUP", "WORLD", "CONTAINER",
            "TRAVERSAL", "RECOVERY", "ENVIRONMENT", "CAPABILITY", "HARNESS",
        };
        var invalid = trace.Spans.Where(s => !approved.Contains(s.Layer)).ToList();
        if (invalid.Count > 0)
        {
            var bad = string.Join(", ", invalid.Select(s => $"{s.SpanId}:{s.Layer}"));
            return TraceConformanceResult.Fail($"Invalid layers found: {bad}.");
        }
        return TraceConformanceResult.Ok;
    }

    /// <summary>Assert that every span's component is non-empty.</summary>
    public static TraceConformanceResult AllComponentsValid(TraceRun trace)
    {
        var empty = trace.Spans.Where(s => string.IsNullOrWhiteSpace(s.Component)).ToList();
        if (empty.Count > 0)
            return TraceConformanceResult.Fail(
                $"{empty.Count} span(s) have empty component ID.");
        return TraceConformanceResult.Ok;
    }

    /// <summary>Assert that all parent references exist (no orphans).</summary>
    public static TraceConformanceResult AllParentsExist(TraceRun trace)
    {
        var spanIds = trace.Spans.Select(s => s.SpanId).ToHashSet(StringComparer.Ordinal);
        var orphans = trace.Spans
            .Where(s => s.ParentSpanId is not null && !spanIds.Contains(s.ParentSpanId))
            .ToList();
        if (orphans.Count > 0)
        {
            var ids = string.Join(", ", orphans.Select(s => $"{s.SpanId}→{s.ParentSpanId}"));
            return TraceConformanceResult.Fail($"Orphan spans found (parent not in trace): {ids}.");
        }
        return TraceConformanceResult.Ok;
    }

    /// <summary>Assert that no span ID is duplicated.</summary>
    public static TraceConformanceResult NoDuplicateSpanIds(TraceRun trace)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var dups = new List<string>();
        foreach (var s in trace.Spans)
            if (!seen.Add(s.SpanId)) dups.Add(s.SpanId);
        if (dups.Count > 0)
            return TraceConformanceResult.Fail($"Duplicate span IDs: {string.Join(", ", dups)}.");
        return TraceConformanceResult.Ok;
    }

    /// <summary>Assert that a specific event exists on a matching span.</summary>
    public static TraceConformanceResult HasEvent(
        TraceRun trace, string layer, string component, string eventId)
    {
        var span = trace.Spans.FirstOrDefault(s =>
            s.Layer == layer && s.Component == component);
        if (span is null)
            return TraceConformanceResult.Fail(
                $"Span not found for event check: layer={layer}, component={component}.");
        if (!span.Events.Any(e => e.EventId == eventId))
            return TraceConformanceResult.Fail(
                $"Event '{eventId}' not found on span {span.SpanId}.");
        return TraceConformanceResult.Ok;
    }

    /// <summary>Assert that span outcomes are in the approved vocabulary.</summary>
    public static TraceConformanceResult AllOutcomesValid(TraceRun trace)
    {
        var approved = new HashSet<string>(StringComparer.Ordinal)
            { "SUCCEEDED", "FAILED", "CANCELLED", "UNKNOWN" };
        var invalid = trace.Spans.Where(s => !approved.Contains(s.Outcome)).ToList();
        if (invalid.Count > 0)
        {
            var bad = string.Join(", ", invalid.Select(s => $"{s.SpanId}:{s.Outcome}"));
            return TraceConformanceResult.Fail($"Invalid outcomes found: {bad}.");
        }
        return TraceConformanceResult.Ok;
    }

    /// <summary>Combine multiple conformance checks — all must pass.</summary>
    public static TraceConformanceResult All(
        TraceRun trace, params Func<TraceRun, TraceConformanceResult>[] checks)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        foreach (var check in checks)
        {
            var result = check(trace);
            if (!result.Passed) errors.AddRange(result.Errors);
        }
        return errors.Count == 0 ? TraceConformanceResult.Ok : TraceConformanceResult.Fail([.. errors]);
    }
}
