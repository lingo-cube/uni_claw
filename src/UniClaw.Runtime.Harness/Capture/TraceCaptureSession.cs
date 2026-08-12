using System.Collections.Immutable;
using System.Security.Cryptography;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Harness.Capture;

/// <summary>Lifecycle state of one capture attempt.</summary>
public enum CaptureState { Created, Capturing, Finalizing, Persisted, CaptureFailed, Quarantined }

/// <summary>Immutable bundle produced by finalizing a capture session.</summary>
public sealed record TraceCaptureBundle
{
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
    public bool RuntimeSucceeded { get; init; }
    public string? RuntimeOutcome { get; init; }
}

/// <summary>One ordered environment-call record.</summary>
public sealed record CaptureRecord
{
    public int Order { get; init; }
    public CaptureRecordKind Kind { get; init; }
    public long SequenceNumber { get; init; }
    public string? FrameId { get; init; }
    public string? ActionId { get; init; }
    public string? ResultOutcome { get; init; }
    public string? Info { get; init; }
}

public enum CaptureRecordKind { Observation, ActionDispatch, ActionResult, CaptureFault }

/// <summary>One artifact attached to a capture frame.</summary>
public sealed record CaptureArtifact
{
    public string ArtifactId { get; init; } = "";
    public string? FrameId { get; init; }
    public string? FileName { get; init; }
    public string? ContentHash { get; init; }
    public int ByteCount { get; init; }
}

/// <summary>Harness-owned transient capture state. Mechanism-local mutable owner only.</summary>
public sealed class TraceCaptureSession
{
    private readonly List<CaptureRecord> _records = [];
    private readonly List<CaptureArtifact> _artifacts = [];
    private readonly string _captureSessionId;
    private long _sequence;
    private int _order;

    public CaptureState State { get; private set; } = CaptureState.Created;
    public string CaptureSessionId => _captureSessionId;
    public IReadOnlyList<CaptureRecord> Records => _records;

    public TraceCaptureSession(string captureSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(captureSessionId);
        _captureSessionId = captureSessionId;
    }

    /// <summary>Begin capturing — state moves to Capturing.</summary>
    public void Begin(string? traceId = null)
    {
        if (State != CaptureState.Created)
            throw new InvalidOperationException($"Capture already started: {State}");
        State = CaptureState.Capturing;
    }

    /// <summary>Record an observation returned by the environment.</summary>
    public void RecordObservation(Observation obs, string? frameId = null)
    {
        EnsureCapturing();
        _records.Add(new CaptureRecord
        {
            Order = ++_order,
            Kind = CaptureRecordKind.Observation,
            SequenceNumber = obs.SequenceNumber,
            FrameId = frameId,
        });
        _sequence = obs.SequenceNumber;
    }

    /// <summary>Record an action dispatched to the environment.</summary>
    public void RecordDispatch(DeviceAction action, string? actionId = null)
    {
        EnsureCapturing();
        _records.Add(new CaptureRecord
        {
            Order = ++_order,
            Kind = CaptureRecordKind.ActionDispatch,
            ActionId = actionId,
        });
    }

    /// <summary>Record an action result returned by the environment.</summary>
    public void RecordResult(ActionResult result)
    {
        EnsureCapturing();
        _records.Add(new CaptureRecord
        {
            Order = ++_order,
            Kind = CaptureRecordKind.ActionResult,
            ResultOutcome = result.Outcome.ToString(),
            Info = result.Info,
        });
    }

    /// <summary>Attach a raw artifact to the current capture.</summary>
    public void AttachArtifact(string frameId, string fileName, byte[] content)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content));
        _artifacts.Add(new CaptureArtifact
        {
            ArtifactId = $"artifact-{_artifacts.Count + 1:D4}",
            FrameId = frameId,
            FileName = fileName,
            ContentHash = hash,
            ByteCount = content.Length,
        });
    }

    /// <summary>Finalize capture — produces immutable bundle.</summary>
    public TraceCaptureBundle Finalize(
        bool runtimeSucceeded,
        string? runtimeOutcome = null,
        string? scenarioId = null,
        string? deviceProfileId = null,
        string? source = null,
        string? startedAt = null)
    {
        if (State is CaptureState.Persisted or CaptureState.CaptureFailed)
            throw new InvalidOperationException($"Capture already finalized: {State}");
        State = CaptureState.Finalizing;
        try
        {
            var bundle = new TraceCaptureBundle
            {
                CaptureSessionId = _captureSessionId,
                ScenarioId = scenarioId,
                DeviceProfileId = deviceProfileId,
                Provenance = "LiveCapture",
                Source = source,
                StartedAt = startedAt,
                FinalState = CaptureState.Persisted,
                Records = [.. _records],
                Artifacts = [.. _artifacts],
                RuntimeSucceeded = runtimeSucceeded,
                RuntimeOutcome = runtimeOutcome,
            };
            State = CaptureState.Persisted;
            return bundle;
        }
        catch
        {
            State = CaptureState.CaptureFailed;
            throw;
        }
    }

    /// <summary>Mark capture as failed (keeps recorded data for diagnostics).</summary>
    public void MarkFailed()
    {
        if (State is CaptureState.Persisted)
            throw new InvalidOperationException("Cannot mark persisted capture as failed.");
        State = CaptureState.CaptureFailed;
    }

    private void EnsureCapturing()
    {
        if (State != CaptureState.Capturing)
            throw new InvalidOperationException($"Capture not in Capturing state: {State}");
    }
}
