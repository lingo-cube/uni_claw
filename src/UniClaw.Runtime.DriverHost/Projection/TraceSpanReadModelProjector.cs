using System.Collections.Immutable;
using UniClaw.Runtime.Harness;

namespace UniClaw.Runtime.DriverHost;

internal static class TraceSpanReadModelProjector
{
    public static TraceRunSummaryResult Summary(string runId, TraceRun? trace)
    {
        if (string.IsNullOrWhiteSpace(runId)) return new(TraceQueryStatus.InvalidRequest, null, ["Run id must not be blank."]);
        if (trace is not null && (trace.SchemaVersion != 1 || string.IsNullOrWhiteSpace(trace.TraceRunId))) return new(TraceQueryStatus.InvalidRequest, null, ["Trace schema or TraceRunId is invalid."]);
        if (trace is null) return TraceRunSummaryResult.Unavailable($"No finalized trace for run '{runId}'.");
        return new(TraceQueryStatus.Found,
            new TraceRunSummary(trace.SchemaVersion, trace.TraceRunId, trace.TraceId, trace.RunId, trace.Spans.Length, trace.Diagnostics), []);
    }

    public static TraceSpanPage Page(string runId, TraceRun? trace, int pageSize, TraceSpanCursor? cursor, TraceSpanFilter? requestedFilter)
    {
        var filter = requestedFilter ?? new TraceSpanFilter();
        if (string.IsNullOrWhiteSpace(runId)) return new(TraceQueryStatus.InvalidRequest, runId, [], null, false, ["Run id must not be blank."]);
        if (pageSize is < 1 or > 256) return new(TraceQueryStatus.InvalidRequest, runId, [], null, false, ["Page size must be between 1 and 256."]);
        if (!TraceSpanReadModelVocabulary.Validate(filter, out var error)) return new(TraceQueryStatus.InvalidRequest, runId, [], null, false, [error!]);
        if (trace is not null && (trace.SchemaVersion != 1 || string.IsNullOrWhiteSpace(trace.TraceRunId))) return new(TraceQueryStatus.InvalidRequest, runId, [], null, false, ["Trace schema or TraceRunId is invalid."]);
        if (trace is null) return new(TraceQueryStatus.Unavailable, runId, [], null, false, [$"No finalized trace for run '{runId}'."]);
        var fingerprint = TraceSpanReadModelVocabulary.Fingerprint(filter);
        if (cursor is not null && (cursor.LastSequence < 0 || cursor.LastSequence > trace.Spans.Length || cursor.RunId != runId || cursor.TraceRunId != trace.TraceRunId || cursor.FilterFingerprint != fingerprint))
            return new(TraceQueryStatus.CursorMismatch, runId, [], null, false, ["Cursor does not match run, trace, or filter."]);
        var ordered = trace.Spans.OrderBy(s => s.StartOffsetNs).ThenBy(s => s.SpanId, StringComparer.Ordinal).Select((span, i) => new TraceSpanEnvelope(i + 1L, span));
        var filtered = ordered.Where(e => Matches(e.Span, filter));
        var after = cursor?.LastSequence ?? 0;
        var page = filtered.Where(e => e.Sequence > after).Take(pageSize + 1).ToArray();
        var hasMore = page.Length > pageSize;
        var items = page.Take(pageSize).ToImmutableArray();
        var next = hasMore && items.Length > 0 ? new TraceSpanCursor(runId, trace.TraceRunId, items[^1].Sequence, fingerprint) : null;
        return new(TraceQueryStatus.Found, runId, items, next, hasMore, []);
    }

    private static bool Matches(TraceSpan s, TraceSpanFilter f) =>
        (f.Name is null || s.Name == f.Name) && (f.Layer is null || s.Layer == f.Layer) &&
        (f.Component is null || s.Component == f.Component) && (f.Outcome is null || s.Outcome == f.Outcome) &&
        (f.ParentSpanId is null || s.ParentSpanId == f.ParentSpanId);
}
