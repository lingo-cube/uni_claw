using System.Collections.Immutable;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Cursor into one run's projected event stream (design.md §5).
/// Cursor semantics: LastSequence is the sequence of the last event the
/// consumer has seen; GetAfter returns strictly newer events. Re-delivery is
/// recognizable by stable EventId (duplicate-safe by construction).
/// </summary>
public sealed record EventCursor(string RunId, long LastSequence);

/// <summary>One page of projected events (design.md §5).</summary>
public sealed record RuntimeEventPage
{
    /// <summary>Run identity for this page.</summary>
    public string RunId { get; init; } = "";

    /// <summary>Projected events in this page.</summary>
    public ImmutableArray<RuntimeEventEnvelope> Events { get; init; } = [];

    /// <summary>Resume cursor — pass to the next GetRuntimeEvents/Subscribe drain.</summary>
    public EventCursor NextCursor { get; init; } = new("", 0);

    /// <summary>True when more events may exist beyond this page (store is complete here).</summary>
    public bool HasMore { get; init; }

    /// <summary>Truthful diagnostics (e.g. unknown run) — never runtime authority.</summary>
    public ImmutableArray<string> Diagnostics { get; init; } = [];
}

/// <summary>
/// Append-only, in-process, transport-neutral event store (design.md §5).
/// One writer per run: a run is projected once (idempotent); events are never
/// rewritten, reordered, or deleted. EventId and Sequence are assigned here so
/// the projector stays pure.
/// </summary>
public sealed class RuntimeEventStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<RuntimeEventEnvelope>> _runs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _projectedRuns = new(StringComparer.Ordinal);

    /// <summary>Append a projected run's events (idempotent for an already-projected run).</summary>
    public RuntimeEventPage Append(string runId, IEnumerable<RuntimeEventEnvelope> events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            if (!_runs.TryGetValue(runId, out var list))
            {
                list = [];
                _runs[runId] = list;
            }
            else if (_projectedRuns.Contains(runId))
            {
                // Idempotent re-registration: never duplicate events.
                return BuildPage(runId, list, startAfter: 0, slice: [.. list]);
            }

            var sequence = list.Count;
            foreach (var envelope in events)
            {
                sequence++;
                var stamped = envelope with
                {
                    RunId = runId,
                    Sequence = sequence,
                    EventId = $"evt-{runId}-{sequence}",
                };
                list.Add(stamped);
            }

            _projectedRuns.Add(runId);
            return BuildPage(runId, list, startAfter: 0, slice: [.. list]);
        }
    }

    /// <summary>Events after the given cursor (or all events when cursor is null).</summary>
    public RuntimeEventPage GetAfter(string runId, EventCursor? cursor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        lock (_gate)
        {
            if (!_runs.TryGetValue(runId, out var list))
            {
                return new RuntimeEventPage
                {
                    RunId = runId,
                    Events = [],
                    NextCursor = new EventCursor(runId, 0),
                    HasMore = false,
                    Diagnostics = [$"No projected events for run '{runId}'."],
                };
            }

            var startAfter = cursor?.RunId == runId ? cursor.LastSequence : 0L;
            var slice = list.Where(e => e.Sequence > startAfter).ToImmutableArray();
            return BuildPage(runId, list, startAfter, slice);
        }
    }

    /// <summary>
    /// Replace a run's ENTIRE projected event stream with the given full
    /// projection, stamped from sequence 1 (dsh-runtime-agent-subagent-run-entry).
    /// Intended for the single accept→terminal transition of a live run: the
    /// accept-time registration projects an EMPTY stream, and the terminal
    /// re-registration replaces it with the full final projection. Append-only
    /// semantics are preserved (nothing is rewritten once stamped; the replace is
    /// the caller-declared transition). Frozen <see cref="Append"/> idempotency
    /// semantics are untouched for all existing callers.
    /// </summary>
    public RuntimeEventPage ReplaceRunEvents(string runId, IEnumerable<RuntimeEventEnvelope> events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            var list = new List<RuntimeEventEnvelope>();
            var sequence = 0;
            foreach (var envelope in events)
            {
                sequence++;
                var stamped = envelope with
                {
                    RunId = runId,
                    Sequence = sequence,
                    EventId = $"evt-{runId}-{sequence}",
                };
                list.Add(stamped);
            }

            _runs[runId] = list;
            _projectedRuns.Add(runId);
            return BuildPage(runId, list, startAfter: 0, slice: [.. list]);
        }
    }

    /// <summary>True when the run has been projected into this store.</summary>
    public bool HasRun(string runId)
    {
        lock (_gate)
        {
            return _projectedRuns.Contains(runId);
        }
    }

    private static RuntimeEventPage BuildPage(
        string runId,
        List<RuntimeEventEnvelope> fullList,
        long startAfter,
        ImmutableArray<RuntimeEventEnvelope> slice)
    {
        var lastSequence = fullList.Count > 0 ? fullList[^1].Sequence : 0L;
        return new RuntimeEventPage
        {
            RunId = runId,
            Events = slice,
            NextCursor = new EventCursor(runId, lastSequence),
            HasMore = false, // in-process store returns the complete remainder per call
            Diagnostics = [],
        };
    }
}

/// <summary>
/// Live drain subscription over a run's projected stream (design.md §5 —
/// SubscribeRunEvents). Transport-neutral; each drain returns only events
/// newer than the subscription's own cursor.
/// </summary>
public sealed class StoreSubscription : IObservabilitySubscription
{
    private readonly RuntimeEventStore _store;
    private long _lastSequence;

    /// <summary>Creates a subscription for one run.</summary>
    public StoreSubscription(RuntimeEventStore store, string runId)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        RunId = runId;
    }

    /// <summary>Run identity.</summary>
    public string RunId { get; }

    /// <summary>Drains events newer than the subscription cursor.</summary>
    public RuntimeEventPage Drain()
    {
        var page = _store.GetAfter(RunId, new EventCursor(RunId, _lastSequence));
        if (page.Events.Length > 0)
        {
            _lastSequence = page.NextCursor.LastSequence;
        }

        return page;
    }

    /// <summary>Releases the in-process subscription.</summary>
    public void Dispose()
    {
        // In-process subscription holds no resources.
    }
}
