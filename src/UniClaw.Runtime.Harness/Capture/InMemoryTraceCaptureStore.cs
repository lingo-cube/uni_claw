using System.Collections.Concurrent;

namespace UniClaw.Runtime.Harness.Capture;

/// <summary>Deterministic append-only store for capture lifecycle falsifiers.</summary>
public sealed class InMemoryTraceCaptureStore : ITraceCaptureStore
{
    private readonly ConcurrentDictionary<string, TraceCaptureBundle> _bundles = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, TraceCaptureBundle> Bundles => _bundles;

    public ValueTask<TraceCapturePersistenceResult> SaveAsync(TraceCaptureBundle bundle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(bundle);
        if (!_bundles.TryAdd(bundle.CaptureSessionId, bundle))
            return ValueTask.FromResult(TraceCapturePersistenceResult.Fail(
                $"Capture '{bundle.CaptureSessionId}' already exists — append-only, cannot overwrite."));
        return ValueTask.FromResult(TraceCapturePersistenceResult.Ok($"memory:{bundle.CaptureSessionId}"));
    }
}
