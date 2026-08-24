using System.Collections.Immutable;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Logical evidence catalog built from an existing Harness capture bundle
/// (design.md §6). Reuses the bundle's append-only records/artifacts as the
/// metadata source; refs carry LOGICAL locator keys only — never paths.
/// The catalog is a pure, immutable lookup; it never loads, copies, or
/// transfers evidence content.
/// </summary>
public sealed class EvidenceCatalog
{
    private readonly ImmutableDictionary<string, EvidenceRef> _byLocator;

    private EvidenceCatalog(
        string? captureSessionId,
        ImmutableArray<CaptureRecord> records,
        ImmutableArray<CaptureArtifact> artifacts,
        ImmutableDictionary<long, EvidenceRef> byObservationSequence,
        ImmutableDictionary<string, EvidenceRef> byActionId,
        ImmutableDictionary<string, EvidenceRef> byLocator)
    {
        CaptureSessionId = captureSessionId;
        Records = records;
        Artifacts = artifacts;
        ByObservationSequence = byObservationSequence;
        ByActionId = byActionId;
        _byLocator = byLocator;
    }

    /// <summary>Capture session identity, when present.</summary>
    public string? CaptureSessionId { get; }

    /// <summary>Capture records used to build the catalog.</summary>
    public ImmutableArray<CaptureRecord> Records { get; }

    /// <summary>Capture artifacts used to build the catalog.</summary>
    public ImmutableArray<CaptureArtifact> Artifacts { get; }

    /// <summary>Observation-sequence → evidence ref (PerceptionOutput records).</summary>
    public ImmutableDictionary<long, EvidenceRef> ByObservationSequence { get; }

    /// <summary>ActionId → evidence ref (ActionJournal records).</summary>
    public ImmutableDictionary<string, EvidenceRef> ByActionId { get; }

    /// <summary>Build a catalog for one run from a finalized capture bundle.</summary>
    public static EvidenceCatalog FromBundle(TraceCaptureBundle bundle, string runId)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var byObservationSequence = ImmutableDictionary.CreateBuilder<long, EvidenceRef>();
        var byActionId = ImmutableDictionary.CreateBuilder<string, EvidenceRef>(StringComparer.Ordinal);
        var byLocator = ImmutableDictionary.CreateBuilder<string, EvidenceRef>(StringComparer.Ordinal);
        var maturity = MapMaturity(bundle.Provenance);

        foreach (var record in bundle.Records)
        {
            var locator = $"capture:{bundle.CaptureSessionId}:record:{record.Order}";
            var evidence = new EvidenceRef
            {
                EvidenceId = locator,
                Kind = record.Kind switch
                {
                    CaptureRecordKind.Observation => EvidenceKind.PerceptionOutput,
                    CaptureRecordKind.ActionDispatch => EvidenceKind.ActionJournal,
                    _ => EvidenceKind.TraceFragment,
                },
                RunId = runId,
                ObservationSequence = record.Kind == CaptureRecordKind.Observation ? record.SequenceNumber : null,
                ContentIdentity = $"record:{bundle.CaptureSessionId}:{record.Order}",
                Maturity = maturity,
                SizeBytes = null,
                Locator = locator,
            };
            byLocator[locator] = evidence;
            if (record.Kind == CaptureRecordKind.Observation)
            {
                byObservationSequence[record.SequenceNumber] = evidence;
            }

            if (record.Kind == CaptureRecordKind.ActionDispatch && !string.IsNullOrEmpty(record.ActionId))
            {
                byActionId[record.ActionId] = evidence;
            }
        }

        foreach (var artifact in bundle.Artifacts)
        {
            var locator = $"capture:{bundle.CaptureSessionId}:artifact:{artifact.ArtifactId}";
            byLocator[locator] = new EvidenceRef
            {
                EvidenceId = locator,
                Kind = EvidenceKind.Screenshot,
                RunId = runId,
                ObservationSequence = null,
                ContentIdentity = artifact.ContentHash,
                Maturity = maturity,
                SizeBytes = artifact.ByteCount,
                Locator = locator,
            };
        }

        return new EvidenceCatalog(
            bundle.CaptureSessionId,
            bundle.Records,
            bundle.Artifacts,
            byObservationSequence.ToImmutable(),
            byActionId.ToImmutable(),
            byLocator.ToImmutable());
    }

    /// <summary>Evidence ref for the observation with the given Kernel sequence, if catalogued.</summary>
    public bool TryGetObservationRef(long sequenceNumber, out EvidenceRef evidenceRef)
        => ByObservationSequence.TryGetValue(sequenceNumber, out evidenceRef!);

    /// <summary>Evidence ref for the dispatched action with the given ActionId, if catalogued.</summary>
    public bool TryGetActionRef(string actionId, out EvidenceRef evidenceRef)
        => ByActionId.TryGetValue(actionId, out evidenceRef!);

    /// <summary>
    /// Resolve a logical evidence ref. Resolution is by LOGICAL locator only —
    /// a ref carrying a filesystem-looking locator is simply not found.
    /// Returns metadata, never embedded content.
    /// </summary>
    public EvidenceResolution Resolve(EvidenceRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (string.IsNullOrEmpty(reference.Locator)
            || !_byLocator.TryGetValue(reference.Locator, out var canonical))
        {
            return new EvidenceResolution
            {
                Found = false,
                Ref = reference,
                Diagnostic = $"Evidence '{reference.Locator}' not found in catalog '{CaptureSessionId}' " +
                             "(logical locator only — no path resolution).",
            };
        }

        var record = Records.FirstOrDefault(
            r => $"capture:{CaptureSessionId}:record:{r.Order}" == canonical.Locator);
        var artifact = Artifacts.FirstOrDefault(
            a => $"capture:{CaptureSessionId}:artifact:{a.ArtifactId}" == canonical.Locator);

        return new EvidenceResolution
        {
            Found = true,
            Ref = canonical,
            CaptureSessionId = CaptureSessionId,
            Record = record,
            Artifact = artifact,
        };
    }

    private static AssetMaturity MapMaturity(string provenance) => provenance switch
    {
        "Synthetic" => AssetMaturity.Synthetic,
        "RealitySeeded" => AssetMaturity.RealitySeeded,
        "RecordedReality" => AssetMaturity.RecordedReality,
        _ => AssetMaturity.LiveCapture,
    };
}
