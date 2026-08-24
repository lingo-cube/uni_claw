using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable CS1591

namespace UniClaw.Runtime.Model;

/// <summary>Closed, scenario-neutral categories of accepted structural progress.</summary>
public enum StrategyStructuralProgressKind
{
    BoundedScopeEntered = 1,
    ChildObligationDiscovered = 2,
    CoverageObligationRecorded = 3,
    CoverageObligationResolved = 4,
    ContinuityVerified = 5,
    ContradictionObserved = 6,
}

/// <summary>One immutable structural fact with an opaque evidence reference.</summary>
public sealed record StrategyStructuralProgressFact
{
    public StrategyStructuralProgressFact(StrategyStructuralProgressKind kind, long revision, string evidenceReference)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        Kind = kind;
        Revision = revision;
        EvidenceReference = evidenceReference;
    }

    public StrategyStructuralProgressKind Kind { get; }
    public long Revision { get; }
    public string EvidenceReference { get; }
}

/// <summary>
/// Immutable evidence projection consumed by Strategy reasoning. It contains no
/// world/DFS object, scenario text, action, route, selector, target, ordering, or
/// completion authority.
/// </summary>
public sealed record StrategyExecutionEvidenceView
{
    public const string CurrentContractVersion = "strategy-execution-evidence.v1";

    public StrategyExecutionEvidenceView(
        string runId,
        string runtimeExecutionIntentReference,
        long acceptedObservationSequence,
        long beliefRevision,
        string beliefDigest,
        long structuralProgressRevision,
        IReadOnlyList<StrategyStructuralProgressFact>? structuralProgressFacts,
        IReadOnlyList<string>? coverageEvidenceReferences,
        IReadOnlyList<string>? contradictionEvidenceReferences,
        IReadOnlyList<string>? traceReferences,
        string traceDigest,
        string contractVersion = CurrentContractVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeExecutionIntentReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(beliefDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(traceDigest);
        if (acceptedObservationSequence < 0) throw new ArgumentOutOfRangeException(nameof(acceptedObservationSequence));
        if (beliefRevision < 0) throw new ArgumentOutOfRangeException(nameof(beliefRevision));
        if (structuralProgressRevision < 0) throw new ArgumentOutOfRangeException(nameof(structuralProgressRevision));
        ContractVersion = contractVersion;
        RunId = runId;
        RuntimeExecutionIntentReference = runtimeExecutionIntentReference;
        AcceptedObservationSequence = acceptedObservationSequence;
        BeliefRevision = beliefRevision;
        BeliefDigest = beliefDigest;
        StructuralProgressRevision = structuralProgressRevision;
        var facts = (structuralProgressFacts ?? Array.Empty<StrategyStructuralProgressFact>()).ToArray();
        if (facts.Any(f => f is null || f.Revision > structuralProgressRevision)) throw new ArgumentException("Structural facts must be non-null and correlated.", nameof(structuralProgressFacts));
        var coverage = (coverageEvidenceReferences ?? Array.Empty<string>()).ToArray();
        var contradictions = (contradictionEvidenceReferences ?? Array.Empty<string>()).ToArray();
        var traces = (traceReferences ?? Array.Empty<string>()).ToArray();
        if (coverage.Any(string.IsNullOrWhiteSpace) || contradictions.Any(string.IsNullOrWhiteSpace) || traces.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Evidence references cannot be blank.");
        StructuralProgressFacts = new ReadOnlyCollection<StrategyStructuralProgressFact>(facts);
        CoverageEvidenceReferences = new ReadOnlyCollection<string>(coverage);
        ContradictionEvidenceReferences = new ReadOnlyCollection<string>(contradictions);
        TraceReferences = new ReadOnlyCollection<string>(traces);
        TraceDigest = traceDigest;
        EvidenceViewDigest = ComputeDigest(contractVersion, runId, runtimeExecutionIntentReference, acceptedObservationSequence, beliefRevision, beliefDigest, structuralProgressRevision, facts, coverage, contradictions, traces, traceDigest);
    }

    public string ContractVersion { get; }
    public string RunId { get; }
    public string RuntimeExecutionIntentReference { get; }
    public long AcceptedObservationSequence { get; }
    public long BeliefRevision { get; }
    public string BeliefDigest { get; }
    public long StructuralProgressRevision { get; }
    public IReadOnlyList<StrategyStructuralProgressFact> StructuralProgressFacts { get; }
    public IReadOnlyList<string> CoverageEvidenceReferences { get; }
    public IReadOnlyList<string> ContradictionEvidenceReferences { get; }
    public IReadOnlyList<string> TraceReferences { get; }
    public string TraceDigest { get; }
    public string EvidenceViewDigest { get; }

    private static string ComputeDigest(string contract, string runId, string intent, long observation, long belief, string beliefDigest, long progress, IEnumerable<StrategyStructuralProgressFact> facts, IEnumerable<string> coverage, IEnumerable<string> contradictions, IEnumerable<string> traces, string traceDigest)
    {
        static string Field(string value) => $"{value.Length}:{value}";
        var material = string.Concat(Field(contract), Field(runId), Field(intent), observation, belief, Field(beliefDigest), progress,
            Field(string.Join(";", facts.Select(f => $"{(int)f.Kind}:{f.Revision}:{Field(f.EvidenceReference)}"))),
            Field(string.Join(";", coverage.Select(Field))), Field(string.Join(";", contradictions.Select(Field))),
            Field(string.Join(";", traces.Select(Field))), Field(traceDigest));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
