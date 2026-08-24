using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Harness.Capture;

/// <summary>Lifecycle state of one Harness-owned capture attempt.</summary>
public enum CaptureState { Created, Capturing, Finalizing, Persisted, CaptureFailed, Quarantined }

/// <summary>Immutable bundle produced by freezing a capture session.</summary>
public sealed record TraceCaptureBundle
{
    public int SchemaVersion { get; init; } = 1;
    public string CaptureSessionId { get; init; } = "";
    public string? TraceId { get; init; }
    public string? ScenarioId { get; init; }
    public string? DeviceProfileId { get; init; }
    public string Provenance { get; init; } = "LiveCapture";
    public string? Source { get; init; }
    public string? StartedAt { get; init; }
    public CaptureState FinalState { get; init; }
    public ImmutableArray<CaptureRecord> Records { get; init; } = [];
    public ImmutableArray<CaptureArtifact> Artifacts { get; init; } = [];
    public ImmutableArray<string> CaptureDiagnostics { get; init; } = [];
    public TraceRun? ObservabilityTrace { get; init; }
    public bool RuntimeSucceeded { get; init; }
    public string? RuntimeOutcome { get; init; }
}

/// <summary>One ordered public environment-boundary record.</summary>
public sealed record CaptureRecord
{
    public int Order { get; init; }
    public CaptureRecordKind Kind { get; init; }
    public long SequenceNumber { get; init; }
    public string? FrameId { get; init; }
    public string? ActionId { get; init; }
    public string? ActionKind { get; init; }
    public string? ApplicationId { get; init; }
    public int? TargetElementIndex { get; init; }
    public bool? TargetState { get; init; }
    public ElementBounds? TargetBounds { get; init; }
    public string? ResultOutcome { get; init; }
    public string? Info { get; init; }
    public Observation? Observation { get; init; }
}

public enum CaptureRecordKind { Observation, ActionDispatch, ActionResult, CaptureFault }

/// <summary>One content-addressed artifact attached to a capture frame.</summary>
public sealed record CaptureArtifact
{
    public string ArtifactId { get; init; } = "";
    public string? FrameId { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public string? DerivedFromArtifactId { get; init; }
    public string? ContentHash { get; init; }
    public int ByteCount { get; init; }

    /// <summary>Transient bytes written by a store; excluded from manifest JSON.</summary>
    [JsonIgnore]
    public ImmutableArray<byte> Content { get; init; } = [];
}

/// <summary>Separate Runtime and capture outcomes; neither rewrites the other.</summary>
public sealed record TraceCaptureResult
{
    public bool RuntimeSucceeded { get; init; }
    public string? RuntimeOutcome { get; init; }
    public bool CaptureSucceeded { get; init; }
    public CaptureState CaptureState { get; init; }
    public TraceCaptureBundle Bundle { get; init; } = new();
    public TraceCapturePersistenceResult Persistence { get; init; } = new();
}

/// <summary>Harness-owned transient capture state. It owns no Runtime semantics.</summary>
public sealed class TraceCaptureSession
{
    private readonly List<CaptureRecord> _records = [];
    private readonly List<CaptureArtifact> _artifacts = [];
    private readonly List<string> _diagnostics = [];
    private readonly string _captureSessionId;
    private string? _traceId;
    private int _order;

    public CaptureState State { get; private set; } = CaptureState.Created;
    public string CaptureSessionId => _captureSessionId;
    public IReadOnlyList<CaptureRecord> Records => _records;
    public IReadOnlyList<string> Diagnostics => _diagnostics;

    public TraceCaptureSession(string captureSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(captureSessionId);
        _captureSessionId = captureSessionId;
    }

    public void Begin(string? traceId = null)
    {
        if (State != CaptureState.Created)
            throw new InvalidOperationException($"Capture already started: {State}");
        _traceId = traceId;
        State = CaptureState.Capturing;
    }

    public void RecordObservation(Observation observation, string? frameId = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        EnsureCapturing();
        _records.Add(new CaptureRecord
        {
            Order = ++_order,
            Kind = CaptureRecordKind.Observation,
            SequenceNumber = observation.SequenceNumber,
            FrameId = frameId,
            Observation = observation,
        });
    }

    public void RecordDispatch(DeviceAction action, string? actionId = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureCapturing();
        _records.Add(ToDispatchRecord(++_order, action, actionId));
    }

    public void RecordResult(ActionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        EnsureCapturing();
        _records.Add(new CaptureRecord
        {
            Order = ++_order,
            Kind = CaptureRecordKind.ActionResult,
            ResultOutcome = result.Outcome.ToString(),
            Info = result.Info,
        });
    }

    /// <summary>Latch a Harness diagnostic without throwing into Runtime execution.</summary>
    public void RecordFault(string diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic)) return;
        _diagnostics.Add(diagnostic);
        if (State == CaptureState.Capturing)
        {
            _records.Add(new CaptureRecord
            {
                Order = ++_order,
                Kind = CaptureRecordKind.CaptureFault,
                Info = diagnostic,
            });
        }
    }

    public CaptureArtifact AttachArtifact(
        string? frameId,
        string fileName,
        ReadOnlySpan<byte> content,
        string? contentType = null,
        string? derivedFromArtifactId = null)
    {
        EnsureCapturing();
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var bytes = content.ToArray().ToImmutableArray();
        var artifact = new CaptureArtifact
        {
            ArtifactId = $"artifact-{_artifacts.Count + 1:D4}",
            FrameId = frameId,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            DerivedFromArtifactId = derivedFromArtifactId,
            ContentHash = ComputeHash(bytes.AsSpan()),
            ByteCount = bytes.Length,
            Content = bytes,
        };
        _artifacts.Add(artifact);
        return artifact;
    }

    /// <summary>Freeze buffers. Persistence is a separate external operation.</summary>
    public TraceCaptureBundle Finalize(
        bool runtimeSucceeded,
        string? runtimeOutcome = null,
        string? scenarioId = null,
        string? deviceProfileId = null,
        string? source = null,
        string? startedAt = null,
        TraceRun? observabilityTrace = null)
    {
        if (State != CaptureState.Capturing)
            throw new InvalidOperationException($"Capture cannot finalize from state: {State}");
        State = CaptureState.Finalizing;
        return BuildBundle(runtimeSucceeded, runtimeOutcome, scenarioId, deviceProfileId, source, startedAt, observabilityTrace);
    }

    public async ValueTask<TraceCaptureResult> FinalizeAndPersistAsync(
        ITraceCaptureStore store,
        bool runtimeSucceeded,
        string? runtimeOutcome = null,
        string? scenarioId = null,
        string? deviceProfileId = null,
        string? source = null,
        string? startedAt = null,
        TraceRun? observabilityTrace = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var bundle = Finalize(runtimeSucceeded, runtimeOutcome, scenarioId, deviceProfileId, source, startedAt, observabilityTrace);
        // A store publishes the immutable value it receives. Supply the only
        // state that can be catalog-visible so a successful publication never
        // persists a bundle that still claims to be mid-finalization.
        var publicationBundle = bundle with { FinalState = CaptureState.Persisted };
        try
        {
            var persistence = await store.SaveAsync(publicationBundle, cancellationToken);
            State = persistence.Success ? CaptureState.Persisted : CaptureState.CaptureFailed;
            return new TraceCaptureResult
            {
                RuntimeSucceeded = runtimeSucceeded,
                RuntimeOutcome = runtimeOutcome,
                CaptureSucceeded = persistence.Success,
                CaptureState = State,
                Bundle = persistence.Success
                    ? publicationBundle
                    : bundle with { FinalState = CaptureState.CaptureFailed },
                Persistence = persistence,
            };
        }
        catch (OperationCanceledException)
        {
            State = CaptureState.Quarantined;
            throw;
        }
    }

    public void MarkFailed(string? diagnostic = null)
    {
        if (State == CaptureState.Persisted)
            throw new InvalidOperationException("Cannot mark persisted capture as failed.");
        if (!string.IsNullOrWhiteSpace(diagnostic)) _diagnostics.Add(diagnostic);
        State = CaptureState.CaptureFailed;
    }

    public static string ComputeHash(ReadOnlySpan<byte> content)
        => $"sha256:{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}";

    private TraceCaptureBundle BuildBundle(
        bool runtimeSucceeded,
        string? runtimeOutcome,
        string? scenarioId,
        string? deviceProfileId,
        string? source,
        string? startedAt,
        TraceRun? observabilityTrace)
        => new()
        {
            CaptureSessionId = _captureSessionId,
            TraceId = _traceId,
            ScenarioId = scenarioId,
            DeviceProfileId = deviceProfileId,
            Source = source,
            StartedAt = startedAt,
            FinalState = CaptureState.Finalizing,
            Records = [.. _records],
            Artifacts = [.. _artifacts],
            CaptureDiagnostics = [.. _diagnostics],
            ObservabilityTrace = observabilityTrace,
            RuntimeSucceeded = runtimeSucceeded,
            RuntimeOutcome = runtimeOutcome,
        };

    private static CaptureRecord ToDispatchRecord(int order, DeviceAction action, string? actionId)
        => action switch
        {
            DeviceAction.LaunchApp launch => new CaptureRecord
            {
                Order = order, Kind = CaptureRecordKind.ActionDispatch, ActionId = actionId,
                ActionKind = nameof(DeviceAction.LaunchApp), ApplicationId = launch.ApplicationId,
            },
            DeviceAction.SetSwitch setSwitch => new CaptureRecord
            {
                Order = order, Kind = CaptureRecordKind.ActionDispatch, ActionId = actionId,
                ActionKind = nameof(DeviceAction.SetSwitch), TargetElementIndex = setSwitch.TargetElementIndex,
                TargetState = setSwitch.TargetState, TargetBounds = setSwitch.TargetBounds,
            },
            DeviceAction.Tap tap => new CaptureRecord
            {
                Order = order, Kind = CaptureRecordKind.ActionDispatch, ActionId = actionId,
                ActionKind = nameof(DeviceAction.Tap), TargetElementIndex = tap.TargetElementIndex,
                TargetBounds = tap.TargetBounds,
            },
            _ => new CaptureRecord
            {
                Order = order, Kind = CaptureRecordKind.ActionDispatch, ActionId = actionId,
                ActionKind = action.GetType().Name,
            },
        };

    private void EnsureCapturing()
    {
        if (State != CaptureState.Capturing)
            throw new InvalidOperationException($"Capture not in Capturing state: {State}");
    }
}
