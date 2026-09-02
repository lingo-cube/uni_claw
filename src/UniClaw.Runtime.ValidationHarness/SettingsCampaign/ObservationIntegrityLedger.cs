using System.Text.Json;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign;

/// <summary>
/// PERCEPTION_OBSERVATION_INTEGRITY ledger (Project-Leader observability gate).
/// For every perception observation this derives stage-granularity counters from
/// the ALREADY-EMITTED pipeline evidence (stageViews + fusion trace) so an empty
/// or sparse Runtime observation can be attributed to exactly one first
/// divergent stage. Validation-side ONLY: derives, records, never mutates the
/// pipeline, never feeds any Runtime decision. No production behavior change.
///
/// Stage axis: Capture(inputFingerprint) → rawModelDetections →
/// normalizedDetections → fusionStages[composition-input..row-stabilization] →
/// fusedEvidence → structuredEvidence → affordances.
/// </summary>
public static class ObservationIntegrityLedger
{
    public static object Build(
        Observation observation,
        StagedViewModels stageViews,
        CameraFusionTrace? fusionTrace)
    {
        var stages = new Dictionary<string, int>(StringComparer.Ordinal);
        if (stageViews.FusionStages is { } fusionStages)
            foreach (var s in fusionStages)
                stages[s.Stage] = s.Candidates?.Count ?? 0;

        return new
        {
            sequenceNumber = observation.SequenceNumber,
            timestampUtc = DateTimeOffset.UtcNow,
            capture = new
            {
                inputFingerprint = fusionTrace?.Fingerprint,
                rawDetectionCount = stageViews.RawModelDetections?.Count ?? 0,
                normalizedDetectionCount = stageViews.NormalizedDetections?.Count ?? 0,
            },
            fusionStages = stages,
            fusedEvidenceCount = stageViews.FusedEvidence?.Count ?? 0,
            structuredEvidenceCount = observation.StructuredElements.Length,
            primaryElementCount = observation.Elements.Length,
            affordanceCount = observation.AdmittedSemanticEvidence.Evidence.Length,
            emptyRuntimeObservation = observation.Elements.Length == 0 && observation.StructuredElements.Length == 0,
        };
    }
}

// Lightweight view models matching the stage views JSON (harness-local, read-only).
public sealed record StagedViewModels(
    IReadOnlyList<JsonElement>? RawModelDetections = null,
    IReadOnlyList<JsonElement>? NormalizedDetections = null,
    IReadOnlyList<FusionStageEntry>? FusionStages = null,
    IReadOnlyList<JsonElement>? FusedEvidence = null);

public sealed record FusionStageEntry(string Stage, IReadOnlyList<JsonElement>? Candidates);

public sealed record CameraFusionTrace(string? Fingerprint);