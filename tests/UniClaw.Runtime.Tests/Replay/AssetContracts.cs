using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Replay;

/// <summary>Asset provenance — immutable historical evidence classification.</summary>
public enum AssetMaturity
{
    /// <summary>Artificially constructed by tests/simulator.</summary>
    Synthetic = 0,

    /// <summary>Derived from real assets, but one or more fields were manually
    /// added, normalized, reconstructed, inferred, or synthesized.</summary>
    RealitySeeded = 1,

    /// <summary>Captured directly from an actual emulator/device/runtime event
    /// without semantic fabrication for the recorded field.</summary>
    RecordedReality = 2,

    /// <summary>Current active physical/emulator execution.</summary>
    LiveCapture = 3,
}

/// <summary>Simulation mode for a test/scenario.</summary>
public enum SimulationMode
{
    /// <summary>Pure isolated component test — no Agent, no Environment.</summary>
    S0_Component = 0,

    /// <summary>Real Runtime against deterministic programmable world.</summary>
    S1_Runtime = 1,

    /// <summary>Replay recorded Observation/ActionResult sequences.</summary>
    S2_ObservationReplay = 2,

    /// <summary>Reprocess raw perception assets into Observation.</summary>
    S3_PerceptionReplay = 3,

    /// <summary>Live emulator/physical device calibration.</summary>
    S4_LiveCalibration = 4,
}

/// <summary>Device platform.</summary>
public enum DevicePlatform { Android, iOS, Windows, macOS, Browser, Synthetic }

/// <summary>Device kind.</summary>
public enum DeviceKind { Synthetic, Emulator, Physical }

/// <summary>Device profile — context for reproducing environment differences.</summary>
public sealed record DeviceProfile
{
    public string DeviceProfileId { get; init; } = "";
    public DevicePlatform Platform { get; init; } = DevicePlatform.Synthetic;
    public DeviceKind Kind { get; init; } = DeviceKind.Synthetic;
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

/// <summary>A single captured/simulated observation frame.</summary>
public sealed record FrameAsset
{
    public string FrameId { get; init; } = "";
    public string? CaptureSessionId { get; init; }
    public int SequenceIndex { get; init; }
    public string? Timestamp { get; init; }
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public string? ScreenshotId { get; init; }
    public Observation? Observation { get; init; }
    public ImmutableArray<FrameRelation> Relations { get; init; } = [];
}

/// <summary>Explicit association between two frames.</summary>
public sealed record FrameRelation
{
    public FrameRelationType Type { get; init; }
    public string SourceFrameId { get; init; } = "";
    public string TargetFrameId { get; init; } = "";
}

/// <summary>Frame relation types.</summary>
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

/// <summary>A capture session grouping related frames.</summary>
public sealed record CaptureSession
{
    public string CaptureSessionId { get; init; } = "";
    public string? DeviceProfileId { get; init; }
    public string? StartedAt { get; init; }
    public string? Source { get; init; }
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public ImmutableArray<string> FrameIds { get; init; } = [];
    public string? TraceId { get; init; }
    public int SchemaVersion { get; init; } = 1;
}

/// <summary>A perception/semantic artifact attached to a frame.</summary>
public sealed record Artifact
{
    public string ArtifactId { get; init; } = "";
    public string? FrameId { get; init; }
    public ArtifactType Type { get; init; }
    public string? ContentHash { get; init; }
    public string? Format { get; init; }
    public AssetMaturity Provenance { get; init; } = AssetMaturity.Synthetic;
    public string? DerivedFromArtifactId { get; init; }
    public string? TransformDescription { get; init; }
}

/// <summary>Artifact types.</summary>
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
