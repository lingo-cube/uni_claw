using System.Collections.Immutable;

namespace UniClaw.Runtime.Harness.Capture;

/// <summary>Outcome of reading one capture.</summary>
public enum TraceCaptureReadStatus { Found, CaptureNotFound, TraceAbsent, UnsupportedSchema, IdentityMismatch, ValidationFailed }

/// <summary>Machine-readable reason for a rejected read.</summary>
public enum TraceCaptureReadIssueCode
{
    UnsafeCaptureSessionId, UnsafePath, MalformedJson, UnsupportedCaptureSchema, UnsupportedTraceSchema,
    NotPublished, CaptureSessionIdMismatch, RecordOrderInvalid, ArtifactIdInvalid, ArtifactMissing,
    ArtifactHashMismatch, ArtifactByteCountMismatch, ChecksumManifestInvalid, TraceFileMissing,
    TraceFileUnexpected, TraceIdentityMismatch, TraceStructureInvalid, ManifestMissing, RecordsMissing,
    ReadFailure
}

/// <summary>One fail-closed read diagnostic.</summary>
public sealed record TraceCaptureReadIssue(TraceCaptureReadIssueCode Code, string Message);

/// <summary>Discriminated result of a capture read.</summary>
public sealed record TraceCaptureReadResult
{
    /// <summary>Read status.</summary>
    public TraceCaptureReadStatus Status { get; init; }
    /// <summary>Complete validated bundle, when allowed by status.</summary>
    public TraceCaptureBundle? Bundle { get; init; }
    /// <summary>Typed validation diagnostics.</summary>
    public ImmutableArray<TraceCaptureReadIssue> Issues { get; init; } = [];
    /// <summary>Creates a read result.</summary>
    public static TraceCaptureReadResult Of(TraceCaptureReadStatus status, TraceCaptureBundle? bundle = null, params TraceCaptureReadIssue[] issues)
        => new() { Status = status, Bundle = bundle, Issues = [.. issues] };
}

/// <summary>Reads one explicitly identified published capture.</summary>
public interface ITraceCaptureReader
{
    /// <summary>Reads and validates a capture without mutating persistence.</summary>
    ValueTask<TraceCaptureReadResult> ReadAsync(string captureSessionId, string? requiredTraceRunId = null, CancellationToken cancellationToken = default);
}
