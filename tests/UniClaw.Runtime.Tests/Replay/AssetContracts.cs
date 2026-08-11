using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Replay;

internal static class HarnessAssetSchema
{
    internal const int CurrentVersion = 1;
}

/// <summary>Asset provenance — immutable historical evidence classification.</summary>
public enum AssetMaturity
{
    Synthetic = 0,
    RealitySeeded = 1,
    RecordedReality = 2,
    LiveCapture = 3,
}

/// <summary>Simulation mode for a test/scenario.</summary>
public enum SimulationMode
{
    S0_Component = 0,
    S1_Runtime = 1,
    S2_ObservationReplay = 2,
    S3_PerceptionReplay = 3,
    S4_LiveCalibration = 4,
}

/// <summary>Replay consumption boundary. This is not a simulation-mode alias.</summary>
public enum ReplayMode
{
    Observation = 1,
    Perception = 2,
    Trace = 3,
}

public enum DevicePlatform { Unknown, Android, iOS, Windows, macOS, Browser, Synthetic }
public enum DeviceKind { Unknown, Synthetic, Emulator, Physical }

/// <summary>Persistent device context. Unknown metadata remains absent.</summary>
public sealed record DeviceProfile
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string DeviceProfileId { get; init; } = "";
    public DevicePlatform Platform { get; init; } = DevicePlatform.Unknown;
    public DeviceKind Kind { get; init; } = DeviceKind.Unknown;
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public int? DisplayWidth { get; init; }
    public int? DisplayHeight { get; init; }
    public float? DisplayDensity { get; init; }
    public string? DisplayOrientation { get; init; }
    public string? OsFamily { get; init; }
    public string? OsVersion { get; init; }
    public string? NavigationMode { get; init; }
    public bool? AccessibilityAvailable { get; init; }
    public string? ScreenshotFormat { get; init; }
    public int? ScreenshotWidth { get; init; }
    public int? ScreenshotHeight { get; init; }

    public static DeviceProfile SyntheticDefault { get; } = new()
    {
        DeviceProfileId = "synthetic-default",
        Platform = DevicePlatform.Synthetic,
        Kind = DeviceKind.Synthetic,
    };

    public static DeviceProfile Pkj110 { get; } = new()
    {
        DeviceProfileId = "pkj110",
        Platform = DevicePlatform.Android,
        Kind = DeviceKind.Physical,
        Manufacturer = "OPPO",
        Model = "PKJ110",
        DisplayWidth = 1440,
        DisplayHeight = 3168,
    };
}

/// <summary>
/// A captured observation frame. Screenshot is an Artifact reference, not the Frame.
/// A Frame without a screenshot remains valid for Observation Replay.
/// </summary>
public sealed record FrameAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string FrameId { get; init; } = "";
    public string? CaptureSessionId { get; init; }
    public int SequenceIndex { get; init; }
    public string? Timestamp { get; init; }
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public string? ScreenshotArtifactId { get; init; }
    public string? NormalizedScreenshotArtifactId { get; init; }
    public ImmutableArray<string> ArtifactIds { get; init; } = [];
    public Observation? Observation { get; init; }
    public ImmutableArray<FrameRelation> Relations { get; init; } = [];
}

/// <summary>Explicit association between two frames. Filenames never define relations.</summary>
public sealed record FrameRelation
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string FrameRelationId { get; init; } = "";
    public FrameRelationType Type { get; init; }
    public string SourceFrameId { get; init; } = "";
    public string TargetFrameId { get; init; } = "";
}

public enum FrameRelationType
{
    PreviousFrame,
    NextFrame,
    SameSession,
    DerivedFrom,
    ObservedAfterAction,
    ObservedBeforeAction,
    CauseContext,
}

/// <summary>A capture session with explicit ordered Frame IDs.</summary>
public sealed record CaptureSession
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string CaptureSessionId { get; init; } = "";
    public string? DeviceProfileId { get; init; }
    public string? StartedAt { get; init; }
    public string? Source { get; init; }
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public ImmutableArray<string> FrameIds { get; init; } = [];
    public string? TraceId { get; init; }
}

/// <summary>Ordered context only; it does not assert semantic page identity.</summary>
public sealed record FrameSequenceAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string FrameSequenceId { get; init; } = "";
    public ImmutableArray<string> FrameIds { get; init; } = [];
    public string? Description { get; init; }
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
}

/// <summary>A raw or derived artifact attached to a Frame.</summary>
public sealed record Artifact
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string ArtifactId { get; init; } = "";
    public string? FrameId { get; init; }
    public ArtifactType Type { get; init; }
    public string? ContentHash { get; init; }
    public string? Format { get; init; }
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public string? DerivedFromArtifactId { get; init; }
    public string? TransformDescription { get; init; }
}

public enum ArtifactType
{
    RawScreenshot,
    NormalizedScreenshot,
    AnnotatedScreenshot,
    OcrResult,
    DetectorResult,
    FusionResult,
    RuntimeObservation,
    SemanticDerivation,
}

/// <summary>First-class history asset. It is evidence history, never current-world authority.</summary>
public sealed record TraceAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string TraceId { get; init; } = "";
    public string? RuntimeVersion { get; init; }
    public string? Commit { get; init; }
    public string? ScenarioId { get; init; }
    public string? DeviceProfileId { get; init; }
    public string? CaptureSessionId { get; init; }
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public string? StartedAt { get; init; }
    public string? Source { get; init; }
    public ImmutableArray<TraceEventAsset> Events { get; init; } = [];
}

/// <summary>
/// Persisted typed event references. Reason and Message are diagnostic-only and must never be parsed.
/// </summary>
public sealed record TraceEventAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string TraceEventId { get; init; } = "";
    public int Order { get; init; }
    public TraceEventType EventType { get; init; }
    public string? FrameId { get; init; }
    public string? ObservationArtifactId { get; init; }
    public string? ActionId { get; init; }
    public string? ActionResultId { get; init; }
    public string? GoalEvidenceId { get; init; }
    public long? ObservationSequenceNumber { get; init; }
    public bool? Satisfied { get; init; }
    public string? Reason { get; init; }
    public string? Message { get; init; }
}

public enum TraceEventType
{
    IntentReceived,
    IntentCompiled,
    ObservationReceived,
    SemanticEvidenceProduced,
    BeliefUpdated,
    CapabilitySelected,
    SemanticActionAuthorized,
    ExecutionDispatched,
    ActionResult,
    FreshObservationReceived,
    VerificationResult,
    GoalEvidenceProduced,
    GoalCompleted,
    RunTerminated,
}

/// <summary>Behavior-oriented Scenario contract, independent of private Runtime implementation.</summary>
public sealed record ScenarioAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string ScenarioId { get; init; } = "";
    public BehavioralCategory Category { get; init; }
    public ImmutableArray<string> DomainTags { get; init; } = [];
    public SimulationMode Mode { get; init; }
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public ScenarioInputAsset Input { get; init; } = new();
    public ScenarioWorldAsset World { get; init; } = new();
    public ScenarioExpectedAsset Expected { get; init; } = new();
}

public sealed record ScenarioInputAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string? Intent { get; init; }
    public SemanticGoalInput? GoalInput { get; init; }
}

public sealed record ScenarioWorldAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string? ReplayId { get; init; }
    public string? FrameSequenceId { get; init; }
    public string? SimulationConfigId { get; init; }
}

public sealed record ScenarioExpectedAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public ScenarioOutcome Outcome { get; init; }
    public int? MaxDispatchCount { get; init; }
    public ImmutableArray<string> AllowedActionKinds { get; init; } = [];
    public ImmutableArray<string> ForbiddenActionKinds { get; init; } = [];
    public bool RequiresFreshObservation { get; init; }
    public bool RequiresGoalEvidence { get; init; }
    public bool MustNotComplete { get; init; }
    public bool MustNotDispatch { get; init; }
}

public enum BehavioralCategory
{
    HappyPath,
    AlreadySatisfied,
    UnknownWorld,
    ContradictedWorld,
    DynamicWorld,
    ActionFailure,
    Recovery,
    Adversarial,
    BudgetNonConvergence,
    BindingDrift,
    GroundingAmbiguity,
    PerceptionInsufficiency,
}

public enum ScenarioOutcome
{
    Satisfied,
    StateEvidenceRequired,
    BindingUnresolved,
    SemanticContradiction,
    BudgetExhausted,
    ExecutionFailed,
}

/// <summary>Persistent replay contract. Ordered Frame IDs and external responses are explicit.</summary>
public sealed record ReplayAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string ReplayId { get; init; } = "";
    public ReplayMode Mode { get; init; } = ReplayMode.Observation;
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public ImmutableArray<string> FrameIds { get; init; } = [];
    public ImmutableArray<RecordedDispatchAsset> Dispatches { get; init; } = [];
}

/// <summary>Recorded external dispatch expectation/result; it carries no semantic decision.</summary>
public sealed record RecordedDispatchAsset
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string DispatchId { get; init; } = "";
    public string ExpectedActionKind { get; init; } = "";
    public string? ApplicationId { get; init; }
    public int? TargetElementIndex { get; init; }
    public bool? TargetState { get; init; }
    public ElementBounds? TargetBounds { get; init; }
    public ActionResultOutcome Outcome { get; init; }
    public string? ActionDescription { get; init; }
    public string? Info { get; init; }
}

/// <summary>One version-controlled persistent manifest. It may contain mixed referenced assets.</summary>
public sealed record HarnessAssetManifest
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string ManifestId { get; init; } = "";
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public string? Source { get; init; }
    public ImmutableArray<DeviceProfile> DeviceProfiles { get; init; } = [];
    public ImmutableArray<CaptureSession> CaptureSessions { get; init; } = [];
    public ImmutableArray<FrameAsset> Frames { get; init; } = [];
    public ImmutableArray<FrameSequenceAsset> FrameSequences { get; init; } = [];
    public ImmutableArray<Artifact> Artifacts { get; init; } = [];
    public ImmutableArray<TraceAsset> Traces { get; init; } = [];
    public ImmutableArray<ReplayAsset> Replays { get; init; } = [];
    public ImmutableArray<ScenarioAsset> Scenarios { get; init; } = [];
}

/// <summary>Bounded JSON loader for the versioned Harness manifest contract.</summary>
public static class HarnessAssetManifestJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static HarnessAssetManifest Deserialize(string json)
        => JsonSerializer.Deserialize<HarnessAssetManifest>(json, Options)
           ?? throw new InvalidDataException("Harness asset manifest deserialized to null.");

    public static string Serialize(HarnessAssetManifest manifest)
        => JsonSerializer.Serialize(manifest, Options);
}

/// <summary>
/// Bounded reference/version validator. This is not a generic asset graph or mutable asset service.
/// </summary>
public static class HarnessAssetManifestValidator
{
    public static ImmutableArray<string> Validate(HarnessAssetManifest manifest)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        CheckVersion(manifest.SchemaVersion, "manifest", errors);
        RequireId(manifest.ManifestId, "manifest", errors);

        CheckUnique(manifest.DeviceProfiles, x => x.DeviceProfileId, "device profile", errors);
        CheckUnique(manifest.CaptureSessions, x => x.CaptureSessionId, "capture session", errors);
        CheckUnique(manifest.Frames, x => x.FrameId, "frame", errors);
        CheckUnique(manifest.FrameSequences, x => x.FrameSequenceId, "frame sequence", errors);
        CheckUnique(manifest.Artifacts, x => x.ArtifactId, "artifact", errors);
        CheckUnique(manifest.Traces, x => x.TraceId, "trace", errors);
        CheckUnique(manifest.Replays, x => x.ReplayId, "replay", errors);
        CheckUnique(manifest.Scenarios, x => x.ScenarioId, "scenario", errors);

        var deviceIds = manifest.DeviceProfiles.Select(x => x.DeviceProfileId).ToHashSet(StringComparer.Ordinal);
        var sessionIds = manifest.CaptureSessions.Select(x => x.CaptureSessionId).ToHashSet(StringComparer.Ordinal);
        var frameIds = manifest.Frames.Select(x => x.FrameId).ToHashSet(StringComparer.Ordinal);
        var artifactIds = manifest.Artifacts.Select(x => x.ArtifactId).ToHashSet(StringComparer.Ordinal);
        var traceIds = manifest.Traces.Select(x => x.TraceId).ToHashSet(StringComparer.Ordinal);
        var replayIds = manifest.Replays.Select(x => x.ReplayId).ToHashSet(StringComparer.Ordinal);
        var frameSequenceIds = manifest.FrameSequences.Select(x => x.FrameSequenceId).ToHashSet(StringComparer.Ordinal);

        foreach (var device in manifest.DeviceProfiles)
            CheckVersion(device.SchemaVersion, $"device profile '{device.DeviceProfileId}'", errors);

        foreach (var session in manifest.CaptureSessions)
        {
            CheckVersion(session.SchemaVersion, $"capture session '{session.CaptureSessionId}'", errors);
            if (session.DeviceProfileId is not null && !deviceIds.Contains(session.DeviceProfileId))
                errors.Add($"Capture session '{session.CaptureSessionId}' references missing device '{session.DeviceProfileId}'.");
            foreach (var frameId in session.FrameIds)
                RequireReference(frameIds, frameId, $"capture session '{session.CaptureSessionId}' frame", errors);
            if (session.TraceId is not null)
                RequireReference(traceIds, session.TraceId, $"capture session '{session.CaptureSessionId}' trace", errors);
        }

        foreach (var frame in manifest.Frames)
        {
            CheckVersion(frame.SchemaVersion, $"frame '{frame.FrameId}'", errors);
            if (frame.CaptureSessionId is not null)
                RequireReference(sessionIds, frame.CaptureSessionId, $"frame '{frame.FrameId}' session", errors);
            foreach (var artifactId in frame.ArtifactIds)
                RequireReference(artifactIds, artifactId, $"frame '{frame.FrameId}' artifact", errors);
            if (frame.ScreenshotArtifactId is not null)
                RequireArtifactType(manifest, frame.FrameId, frame.ScreenshotArtifactId, ArtifactType.RawScreenshot, errors);
            if (frame.NormalizedScreenshotArtifactId is not null)
                RequireArtifactType(manifest, frame.FrameId, frame.NormalizedScreenshotArtifactId, ArtifactType.NormalizedScreenshot, errors);
            foreach (var relation in frame.Relations)
            {
                CheckVersion(relation.SchemaVersion, $"frame relation '{relation.FrameRelationId}'", errors);
                RequireId(relation.FrameRelationId, "frame relation", errors);
                RequireReference(frameIds, relation.SourceFrameId, $"relation '{relation.FrameRelationId}' source", errors);
                RequireReference(frameIds, relation.TargetFrameId, $"relation '{relation.FrameRelationId}' target", errors);
            }
        }

        foreach (var artifact in manifest.Artifacts)
        {
            CheckVersion(artifact.SchemaVersion, $"artifact '{artifact.ArtifactId}'", errors);
            if (artifact.FrameId is not null)
                RequireReference(frameIds, artifact.FrameId, $"artifact '{artifact.ArtifactId}' frame", errors);
            if (artifact.DerivedFromArtifactId is not null)
                RequireReference(artifactIds, artifact.DerivedFromArtifactId, $"artifact '{artifact.ArtifactId}' derivation", errors);
            if (artifact.Type == ArtifactType.RawScreenshot
                && (artifact.ContentHash is null || !artifact.ContentHash.StartsWith("sha256:", StringComparison.Ordinal)))
                errors.Add($"Raw screenshot '{artifact.ArtifactId}' requires a sha256 content hash.");
        }

        foreach (var sequence in manifest.FrameSequences)
        {
            CheckVersion(sequence.SchemaVersion, $"frame sequence '{sequence.FrameSequenceId}'", errors);
            foreach (var frameId in sequence.FrameIds)
                RequireReference(frameIds, frameId, $"frame sequence '{sequence.FrameSequenceId}' frame", errors);
        }

        foreach (var trace in manifest.Traces)
        {
            CheckVersion(trace.SchemaVersion, $"trace '{trace.TraceId}'", errors);
            if (trace.DeviceProfileId is not null)
                RequireReference(deviceIds, trace.DeviceProfileId, $"trace '{trace.TraceId}' device", errors);
            if (trace.CaptureSessionId is not null)
                RequireReference(sessionIds, trace.CaptureSessionId, $"trace '{trace.TraceId}' session", errors);
            var ordered = trace.Events.OrderBy(x => x.Order).Select(x => x.Order).ToArray();
            if (!ordered.SequenceEqual(Enumerable.Range(0, ordered.Length)))
                errors.Add($"Trace '{trace.TraceId}' event ordering must be contiguous from zero.");
            foreach (var traceEvent in trace.Events)
            {
                CheckVersion(traceEvent.SchemaVersion, $"trace event '{traceEvent.TraceEventId}'", errors);
                RequireId(traceEvent.TraceEventId, "trace event", errors);
                if (traceEvent.FrameId is not null)
                    RequireReference(frameIds, traceEvent.FrameId, $"trace event '{traceEvent.TraceEventId}' frame", errors);
                if (traceEvent.ObservationArtifactId is not null)
                    RequireReference(artifactIds, traceEvent.ObservationArtifactId, $"trace event '{traceEvent.TraceEventId}' observation", errors);
            }
        }

        foreach (var replay in manifest.Replays)
        {
            CheckVersion(replay.SchemaVersion, $"replay '{replay.ReplayId}'", errors);
            foreach (var frameId in replay.FrameIds)
                RequireReference(frameIds, frameId, $"replay '{replay.ReplayId}' frame", errors);
            foreach (var dispatch in replay.Dispatches)
            {
                CheckVersion(dispatch.SchemaVersion, $"dispatch '{dispatch.DispatchId}'", errors);
                RequireId(dispatch.DispatchId, "dispatch", errors);
            }
        }

        foreach (var scenario in manifest.Scenarios)
        {
            CheckVersion(scenario.SchemaVersion, $"scenario '{scenario.ScenarioId}'", errors);
            CheckVersion(scenario.Input.SchemaVersion, $"scenario '{scenario.ScenarioId}' input", errors);
            CheckVersion(scenario.World.SchemaVersion, $"scenario '{scenario.ScenarioId}' world", errors);
            CheckVersion(scenario.Expected.SchemaVersion, $"scenario '{scenario.ScenarioId}' expected", errors);
            if (scenario.World.ReplayId is not null)
                RequireReference(replayIds, scenario.World.ReplayId, $"scenario '{scenario.ScenarioId}' replay", errors);
            if (scenario.World.FrameSequenceId is not null)
                RequireReference(frameSequenceIds, scenario.World.FrameSequenceId, $"scenario '{scenario.ScenarioId}' frame sequence", errors);
        }

        return errors.ToImmutable();
    }

    private static void RequireArtifactType(
        HarnessAssetManifest manifest,
        string frameId,
        string artifactId,
        ArtifactType expectedType,
        ImmutableArray<string>.Builder errors)
    {
        var artifact = manifest.Artifacts.FirstOrDefault(x => x.ArtifactId == artifactId);
        if (artifact is null)
        {
            errors.Add($"Frame '{frameId}' references missing artifact '{artifactId}'.");
            return;
        }
        if (artifact.Type != expectedType || artifact.FrameId != frameId)
            errors.Add($"Frame '{frameId}' artifact '{artifactId}' must be its {expectedType}.");
    }

    private static void CheckVersion(int version, string label, ImmutableArray<string>.Builder errors)
    {
        if (version != HarnessAssetSchema.CurrentVersion)
            errors.Add($"{label} has unsupported schema version {version}.");
    }

    private static void RequireId(string value, string label, ImmutableArray<string>.Builder errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{label} requires a stable non-empty ID.");
    }

    private static void CheckUnique<T>(
        IEnumerable<T> values,
        Func<T, string> id,
        string label,
        ImmutableArray<string>.Builder errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var key = id(value);
            RequireId(key, label, errors);
            if (!seen.Add(key))
                errors.Add($"Duplicate {label} ID '{key}'.");
        }
    }

    private static void RequireReference(
        HashSet<string> knownIds,
        string id,
        string label,
        ImmutableArray<string>.Builder errors)
    {
        if (!knownIds.Contains(id))
            errors.Add($"{label} references missing ID '{id}'.");
    }
}
