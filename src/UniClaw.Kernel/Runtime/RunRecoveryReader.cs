using System.Text.Json;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Runtime;

/// <summary>Trusted original histories for one archived Run. The caller owns source lifetimes.
/// Implementations normalize unavailable/corrupt storage to IOException or InvalidDataException;
/// access never initializes/appends, replays execution, or creates current authority.</summary>
public interface IRunRecoverySources
{
    /// <summary>Original Run history; source identity is checked against the archived recording.</summary>
    IRunStateJournal RunStateHistory { get; }
    /// <summary>Original Evidence admissions and lineage, without new admission.</summary>
    IEvidenceJournal EvidenceHistory { get; }
    /// <summary>Original World outputs, without reconciliation or current World authority.</summary>
    IWorldJournal WorldHistory { get; }
    /// <summary>Original Control traversal operations, without selecting or replaying work.</summary>
    IControlTraversalJournal ControlTraversalHistory { get; }
    /// <summary>Original Runtime Plan commits. Older sources may explicitly lack this domain.</summary>
    IRuntimePlanJournal RuntimePlanHistory => throw new InvalidDataException("runtime-plan-source-unavailable");

    /// <summary>Read original resources through RunResourceAccounting with the supplied binding.
    /// The adapter must verify the original consultation/execution source relationship using its
    /// medium's original commit identifiers. A strict read additionally verifies the exact committed
    /// resource endpoint. Unverified data cannot claim AccountingComplete or CommittedEndpointVerified.
    /// A strict read requires a non-null binding. These are trusted storage attestations, not user claims.</summary>
    RunResourceRead ReadResources(RunResourceBinding? binding, bool requireCommittedEndpoint);
}

/// <summary>Media-independent original-history queries. Does not own/dispose injected sources,
/// construct an executable Kernel, initialize storage, or authorize continuation.</summary>
public sealed class RunRecoveryReader
{
    private readonly IRunRecoveryArchive _archive;
    private readonly IRunRecoverySources _sources;

    /// <summary>Use caller-owned original identity and history sources; construction performs no reads or writes.</summary>
    public RunRecoveryReader(IRunRecoveryArchive archive, IRunRecoverySources sources)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    /// <summary>Only historical identity/confirmed resource recovery. Does not
    /// construct a Kernel, call admission/activation, append journals or deliver.</summary>
    public RunResourceRead Inspect(string expectedRunId)
    {
        try
        {
            var binding = RunResourceAccounting.RestoreBinding(_archive, expectedRunId);
            return ReadResourceHistory(binding, requireCommittedEndpoint: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException
            or ArgumentException or NullReferenceException or UnauthorizedAccessException)
        {
            var history = ReadResourceHistory(null, requireCommittedEndpoint: false);
            return history with { AccountingComplete = false, Reason = "run-identity-unavailable:" + ex.GetType().Name };
        }
    }

    /// <summary>Original canonical Run history, restored as an immutable/read-only
    /// Owner. Missing or unlinked history is an error, never an active default.</summary>
    public RunModel RestoreRunState(string expectedRunId)
    {
        try
        {
            var original = _archive.Read();
            _ = RunResourceAccounting.RestoreBinding(_archive, expectedRunId);
            if (original.RunStateRecordingId is not { Length: > 0 } recordingId)
                throw new InvalidDataException("run-state-source-unavailable");
            var source = _sources.RunStateHistory;
            return RunModel.RestoreReadOnly(source, original.Run, recordingId);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException or NullReferenceException)
        { throw new InvalidDataException("run-state-recovery-unavailable", ex); }
    }

    /// <summary>Independent original Control traversal history. Does not certify a common execution point.</summary>
    public ControlTraversalHistoryRead ReadControlTraversalHistory(string expectedRunId)
    {
        var original = _archive.Read();
        var run = RestoreRunState(expectedRunId);
        if (original.Schema is not ("uniclaw.runtime.original-identity.v4" or "uniclaw.runtime.original-identity.v5")
            || original.ControlTraversalRecordingId is not { } sourceId)
            throw new InvalidDataException("control-traversal-source-unavailable");
        var source = _sources.ControlTraversalHistory;
        return ControlTraversalHistoryRead.ReadOriginal(source, sourceId, run);
    }

    /// <summary>Original Plan-adoption commits only; no execution cursor or continuation authority.</summary>
    public RuntimePlanHistoryRead ReadRuntimePlanHistory(string expectedRunId)
    {
        var original = _archive.Read();
        var run = RestoreRunState(expectedRunId);
        if (original.Schema != "uniclaw.runtime.original-identity.v5" || original.RuntimePlanRecordingId is not { } sourceId)
            throw new InvalidDataException("runtime-plan-source-unavailable");
        return RuntimePlanHistoryRead.ReadOriginal(_sources.RuntimePlanHistory, sourceId, run);
    }

    /// <summary>Read original Run and resource histories at their exact committed endpoints.
    /// A stopped writer is required; success never authorizes execution.</summary>
    public RunRecoveryRead InspectRecovery(string expectedRunId)
    {
        RunModel? run = null;
        try
        {
            run = RestoreRunState(expectedRunId);
            var binding = RunResourceAccounting.RestoreBinding(_archive, expectedRunId);
            var resources = ReadResourceHistory(binding, requireCommittedEndpoint: true);
            return RunResourceAccounting.InspectRecovery(run, resources);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException
            or NullReferenceException or UnauthorizedAccessException)
        { return new(run, Inspect(expectedRunId), null, false, "run-recovery-unavailable:" + ex.GetType().Name); }
    }

    /// <summary>Original historical admissions and canonical lineage only; no World/current authority or execution.</summary>
    public EvidenceLedger RestoreEvidence(string expectedRunId)
    {
        try
        {
            var original = _archive.Read();
            _ = RunResourceAccounting.RestoreBinding(_archive, expectedRunId);
            if (original.EvidenceRecordingId is not { Length: > 0 } id)
                throw new InvalidDataException("evidence-source-unavailable");
            var source = _sources.EvidenceHistory;
            return EvidenceLedger.RestoreReadOnly(source, id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException
            or NullReferenceException or UnauthorizedAccessException)
        { throw new InvalidDataException("evidence-recovery-unavailable", ex); }
    }

    /// <summary>Compare all three original committed histories after the writer has stopped.
    /// Independent readable facts remain available on mismatch; no current or continuation authority is granted.</summary>
    public RunEvidenceRecoveryRead InspectEvidenceRecovery(string expectedRunId)
    {
        var pair = InspectRecovery(expectedRunId);
        try
        {
            var evidence = RestoreEvidence(expectedRunId);
            return pair.Run is { } run ? RunResourceAccounting.InspectEvidenceRecovery(run, pair.Resources, evidence)
                : new(pair, evidence, null, false, pair.Reason ?? "run-history-unavailable");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException
            or NullReferenceException or UnauthorizedAccessException)
        {
            return new(pair, null, null, false, pair.StateAndResourcesConsistent
                ? "evidence-recovery-unavailable:" + ex.GetType().Name : pair.Reason);
        }
    }

    /// <summary>Certify four exact original historical endpoints after the writer stopped. Never grants current or continuation authority.</summary>
    public RunWorldRecoveryRead InspectWorldRecovery(string expectedRunId)
    {
        var three = InspectEvidenceRecovery(expectedRunId);
        try
        {
            var original = _archive.Read();
            _ = RunResourceAccounting.RestoreBinding(_archive, expectedRunId);
            if (original.WorldRecordingId is not { Length: > 0 } worldId
                || original.RunStateRecordingId is not { Length: > 0 } runId)
                return new(three, null, null, false, three.Reason ?? "world-original-link-missing");
            if (three.Evidence is not { } evidence)
                return new(three, null, null, false, three.Reason ?? "world-original-evidence-unavailable");
            var source = _sources.WorldHistory;
            var world = WorldHistoryRead.ReadOriginal(source, worldId, runId, evidence);
            return three.RunAndResources.Run is { } run
                ? RunResourceAccounting.InspectWorldRecovery(run, three.RunAndResources.Resources, evidence, world)
                : new(three, world, null, false, three.Reason ?? "run-history-unavailable");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException
            or NullReferenceException or UnauthorizedAccessException)
        { return new(three, null, null, false, three.Reason ?? "world-recovery-unavailable:" + ex.GetType().Name); }
    }

    private RunResourceRead ReadResourceHistory(RunResourceBinding? binding, bool requireCommittedEndpoint)
    {
        try { return _sources.ReadResources(binding, requireCommittedEndpoint); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException
            or ArgumentException or NullReferenceException or UnauthorizedAccessException)
        { return new(null, null, null, false, "resource-history-unreadable:" + ex.GetType().Name); }
    }
}
