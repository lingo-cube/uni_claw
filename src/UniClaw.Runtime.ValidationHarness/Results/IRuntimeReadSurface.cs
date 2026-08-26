using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.ValidationHarness.Results;

/// <summary>
/// Harness-local typing of the frozen read-only wire surface (design D3/D4;
/// WI-EVH-003 4.1). This interface types exactly the read ops the DriverHost
/// wire exposes — snapshot-get, events-after/drain, trap-get, evidence-get —
/// so the <see cref="ResultCollector"/> consumes typed read facts and never
/// touches the transport. It is PURE harness-local typing: no new wire method,
/// no Runtime edit; the two implementations are the loopback wire client
/// (<see cref="WireReadSurface"/>) and the in-process Tier-A read surface
/// (<see cref="TierAReadSurface"/>). Return types are the frozen DriverHost
/// read models; harness-local event facts carry the audited A/B source
/// classification.
/// </summary>
public interface IRuntimeReadSurface
{
    /// <summary>Frozen <c>run.snapshot.get</c> — one classified RunSnapshot.</summary>
    Task<RunSnapshot> GetRunSnapshotAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>Frozen <c>run.events.after</c> — cursor-bounded projected event page.</summary>
    Task<SurfaceEventPage> GetRuntimeEventsAfterAsync(string runId, EventCursor? cursor = null, CancellationToken cancellationToken = default);

    /// <summary>Frozen <c>run.events.drain</c> — live-drain page (fresh cursor).</summary>
    Task<SurfaceEventPage> DrainRuntimeEventsAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>Frozen <c>run.trap.get</c> — one classified trap read.</summary>
    Task<InspectTrapResult> GetRunTrapAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>Frozen <c>evidence.get</c> — logical evidence resolution.</summary>
    Task<EvidenceResolution> GetEvidenceAsync(EvidenceRef evidenceRef, CancellationToken cancellationToken = default);
}

/// <summary>One projected lifecycle event kept for Result aggregation: kind
/// name, projected sequence, the audited A/B source classification from the
/// frozen <c>RuntimeEventKindTable</c>, the observation anchor when
/// attributable, and the payload reason when the kind truthfully carries one
/// (GoalEvidenceProduced / RunCompleted / RunFailed).</summary>
public sealed record SurfaceRuntimeEvent(
    string EventId,
    string Kind,
    long Sequence,
    string SourceClassification,
    long? ObservationSequence,
    string? Reason,
    ImmutableArray<EvidenceRef> EvidenceRefs);

/// <summary>Harness-local projected event page (mirror of
/// <c>RuntimeEventPage</c>): the in-process store returns the complete
/// remainder per call, so has-more is always false on both implementations.</summary>
public sealed record SurfaceEventPage(
    ImmutableArray<SurfaceRuntimeEvent> Events,
    ImmutableArray<string> Diagnostics);