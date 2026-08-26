using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Hosting;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.ValidationHarness.Results;

/// <summary>
/// Tier-A ledger attestation seam (WI-EVH-003 4.3, design D3): the in-process
/// Agent public read model is read ONLY through this interface — the
/// <see cref="ResultCollector"/> never reaches into Runtime internals. Only the
/// Tier-A surface implements it; a wire-tier surface does not, which makes the
/// ledger truthfully unavailable there. Attestation is a read-only projection
/// (<c>Agent.CompileExplorationLedgerView</c>); it never mutates Runtime state.
/// </summary>
public interface ITierALedgerAttestation
{
    /// <summary>Whether the accepted run's Agent read model is held (captured
    /// at admission; null after run-record release).</summary>
    bool CanAttest { get; }

    /// <summary>
    /// Compile the read-only <see cref="ExplorationLedgerView"/> from the held
    /// Agent. Returns null when no run context is bound (or the Agent was
    /// released before capture). Post-terminal call — the view carries the five
    /// per-scope counts, depth semantics, and the stable <see cref="ExplorationLedgerView.LedgerDigest"/>.
    /// </summary>
    ExplorationLedgerView? CompileExplorationLedger(string runId);
}

/// <summary>
/// <see cref="IRuntimeReadSurface"/> for the Tier-A in-process composition
/// (WI-EVH-003 4.1): the same frozen read ops, but answered in-process from
/// <see cref="TierAHost.Observability"/> instead of over the transport. The
/// Agent reference is captured AT ADMISSION time so the post-terminal ledger
/// attestation still works after the coordinator releases the run record.
/// Emulator/payload facts are never read through this surface — only Runtime
/// read models.
/// </summary>
public sealed class TierAReadSurface : IRuntimeReadSurface, ITierALedgerAttestation
{
    private readonly TierAHost _host;
    private readonly string _runId;
    private readonly RuntimeAgent? _attestedAgent;

    /// <summary>
    /// Create the Tier-A surface and capture the accepted run's Agent
    /// reference at admission time (bounded retries mirror the round-trip
    /// test; null if the coordinator released the record before capture — a
    /// truthful unattested state, never a fabricated ledger).
    /// </summary>
    public TierAReadSurface(TierAHost host, string runId)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        _host = host;
        _runId = runId;
        _attestedAgent = CaptureAgent(host, runId);
    }

    /// <inheritdoc />
    public bool CanAttest => _attestedAgent is not null;

    /// <inheritdoc />
    public ExplorationLedgerView? CompileExplorationLedger(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (_attestedAgent is null
            || !string.Equals(runId, _runId, StringComparison.Ordinal))
        {
            return null;
        }

        return _attestedAgent.CompileExplorationLedgerView();
    }

    /// <inheritdoc />
    public Task<RunSnapshot> GetRunSnapshotAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return Task.FromResult(_host.Observability.GetRunSnapshot(runId));
    }

    /// <inheritdoc />
    public Task<SurfaceEventPage> GetRuntimeEventsAfterAsync(string runId, EventCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var page = _host.Observability.GetRuntimeEvents(runId, cursor);
        return Task.FromResult(ToEventPage(page));
    }

    /// <inheritdoc />
    public Task<SurfaceEventPage> DrainRuntimeEventsAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        using var subscription = _host.Observability.SubscribeRunEvents(runId);
        return Task.FromResult(ToEventPage(subscription.Drain()));
    }

    /// <inheritdoc />
    public Task<InspectTrapResult> GetRunTrapAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var control = new UniClawControlSurface(_host.Observability);
        return Task.FromResult(control.InspectTrap(runId));
    }

    /// <inheritdoc />
    public Task<EvidenceResolution> GetEvidenceAsync(EvidenceRef evidenceRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidenceRef);
        return Task.FromResult(_host.Observability.GetEvidence(evidenceRef));
    }

    private static SurfaceEventPage ToEventPage(RuntimeEventPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var events = ImmutableArray.CreateBuilder<SurfaceRuntimeEvent>();
        foreach (var envelope in page.Events)
        {
            events.Add(new SurfaceRuntimeEvent(
                EventId: envelope.EventId,
                Kind: envelope.Kind.ToString(),
                Sequence: envelope.Sequence,
                SourceClassification: RuntimeEventKindTable.For(envelope.Kind).Classification.ToString(),
                ObservationSequence: envelope.ObservationSequence,
                Reason: ToReason(envelope.Kind, envelope.Payload),
                EvidenceRefs: envelope.EvidenceRefs.IsDefault ? [] : envelope.EvidenceRefs));
        }

        return new SurfaceEventPage(events.ToImmutable(), page.Diagnostics);
    }

    private static string? ToReason(RuntimeEventKind kind, RuntimeEventPayload? payload)
        => payload switch
        {
            GoalEvidenceProducedPayload partial => partial.Reason,
            RunCompletedPayload completed => completed.Reason,
            RunFailedPayload failed => failed.Reason,
            _ => null,
        };

    private static RuntimeAgent? CaptureAgent(TierAHost host, string runId)
    {
        // Bounded retries mirror the round-trip test: the fixture run may be
        // released quickly; a null capture is a truthful unattested state.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var agent = host.AttestationAgent(runId);
            if (agent is not null)
            {
                return agent;
            }

            Thread.Sleep(2);
        }

        return null;
    }
}