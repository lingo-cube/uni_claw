using System.Collections.Immutable;

namespace UniClaw.Runtime.Harness.Capture;

/// <summary>Result of a persistence operation.</summary>
public sealed record TraceCapturePersistenceResult
{
    public bool Success { get; init; }
    public string? StorePath { get; init; }
    public ImmutableArray<string> Errors { get; init; } = [];

    public static TraceCapturePersistenceResult Ok(string path) => new() { Success = true, StorePath = path };
    public static TraceCapturePersistenceResult Fail(params string[] errors) => new() { Errors = [.. errors] };
}

/// <summary>
/// Narrow persistence boundary — append-only atomic store for TraceCaptureBundles.
/// Not a Provider, registry, or generic repository.
/// </summary>
public interface ITraceCaptureStore
{
    ValueTask<TraceCapturePersistenceResult> SaveAsync(
        TraceCaptureBundle bundle,
        CancellationToken cancellationToken = default);
}
