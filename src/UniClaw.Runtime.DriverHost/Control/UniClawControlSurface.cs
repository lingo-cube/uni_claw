using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Result of an InspectTrap read: whether the run currently has an active trap,
/// the classified ActiveTrap snapshot field, and a truthful diagnostic when the
/// trap cannot be read. Read-only — never mutates Kernel state.
/// </summary>
public sealed record InspectTrapResult(
    string RunId,
    bool Found,
    SnapshotField<Trap?> Trap,
    string? Diagnostic);

/// <summary>
/// Bounded READ-ONLY control facade over the DriverHost observability surface
/// (protocol baseline §7 / §13 — deterministic human control seam).
///
/// Every method is a deterministic, zero-model read. There is deliberately NO
/// method that mutates Kernel state: execution/state authority belongs to the
/// UniClaw Kernel, and control operations without a truthful Kernel buyer are
/// answered from the frozen audit table (ControlSupportAudit) instead.
/// </summary>
public interface IUniClawControlSurface
{
    /// <summary>Identity handshake: the service name of this DriverHost surface.</summary>
    string Ping();

    /// <summary>Registered run ids (read-only diagnostic view).</summary>
    ImmutableArray<string> ListRuns();

    /// <summary>Classified read-only snapshot of one run (Unknown when unregistered).</summary>
    RunSnapshot InspectRun(string runId);

    /// <summary>Classified active-trap read for one run.</summary>
    InspectTrapResult InspectTrap(string runId);

    /// <summary>Logical evidence resolution (locator-only, metadata-only).</summary>
    EvidenceResolution OpenEvidence(EvidenceRef evidenceRef);

    /// <summary>Cursor-based runtime event page read (GetAfter semantics).</summary>
    RuntimeEventPage GetRuntimeEvents(string runId, EventCursor? cursor = null);

    /// <summary>Frozen audit lookup for one candidate control operation.</summary>
    ControlSupportResult ControlSupport(string operation);
}

/// <summary>
/// Default facade implementation: adapts the concrete
/// <see cref="DriverHostObservability"/> (the DriverHost-internal read model)
/// without adding any authority of its own. The concrete type is required only
/// for the registered-run diagnostic view; the surface exposes no other
/// observability detail.
/// </summary>
public sealed class UniClawControlSurface : IUniClawControlSurface
{
    private readonly DriverHostObservability _observability;

    /// <summary>Create the surface over the DriverHost observability read model.</summary>
    public UniClawControlSurface(DriverHostObservability observability)
    {
        ArgumentNullException.ThrowIfNull(observability);
        _observability = observability;
    }

    /// <summary>Identity handshake: the service name of this DriverHost surface.</summary>
    public string Ping() => "dsh-uniclaw-driverhost";

    /// <summary>Registered run ids (read-only diagnostic view).</summary>
    public ImmutableArray<string> ListRuns() => _observability.RegisteredRunIds;

    /// <summary>Classified read-only snapshot of one run (Unknown when unregistered).</summary>
    public RunSnapshot InspectRun(string runId)
    {
        ArgumentNullException.ThrowIfNull(runId);
        return _observability.GetRunSnapshot(runId);
    }

    /// <summary>Classified active-trap read for one run.</summary>
    public InspectTrapResult InspectTrap(string runId)
    {
        ArgumentNullException.ThrowIfNull(runId);

        var snapshot = _observability.GetRunSnapshot(runId);
        var trapField = snapshot.ActiveTrap;

        if (!IsRegisteredRun(snapshot))
        {
            return new InspectTrapResult(
                RunId: runId,
                Found: false,
                Trap: trapField,
                Diagnostic: $"Run '{runId}' is not registered with the DriverHost observability surface.");
        }

        if (trapField.Classification == SnapshotFieldClassification.NotCurrentlyAvailable)
        {
            return new InspectTrapResult(
                RunId: runId,
                Found: false,
                Trap: trapField,
                Diagnostic: trapField.TruthSource);
        }

        return new InspectTrapResult(
            RunId: runId,
            Found: trapField.Value is not null,
            Trap: trapField,
            Diagnostic: null);
    }

    /// <summary>Logical evidence resolution (locator-only, metadata-only).</summary>
    public EvidenceResolution OpenEvidence(EvidenceRef evidenceRef)
    {
        ArgumentNullException.ThrowIfNull(evidenceRef);
        return _observability.GetEvidence(evidenceRef);
    }

    /// <summary>Cursor-based runtime event page read (GetAfter semantics).</summary>
    public RuntimeEventPage GetRuntimeEvents(string runId, EventCursor? cursor = null)
    {
        ArgumentNullException.ThrowIfNull(runId);
        return _observability.GetRuntimeEvents(runId, cursor);
    }

    /// <summary>Frozen audit lookup for one candidate control operation.</summary>
    public ControlSupportResult ControlSupport(string operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ControlSupportAudit.Audit(operation);
    }

    /// <summary>
    /// A run is registered iff its snapshot carries a Direct RunState (projected
    /// from Agent.State); an unregistered run returns RunSnapshot.Unknown whose
    /// fields are all NotCurrentlyAvailable.
    /// </summary>
    private static bool IsRegisteredRun(RunSnapshot snapshot)
        => snapshot.RunState.Classification == SnapshotFieldClassification.DirectPublicProjection;
}
