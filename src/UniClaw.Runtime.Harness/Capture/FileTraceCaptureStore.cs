using System.Text.Json;

namespace UniClaw.Runtime.Harness.Capture;

/// <summary>
/// Append-only filesystem persistence for TraceCaptureBundles.
/// Atomic publish: write to staging, then rename. Existing IDs fail closed.
/// </summary>
public sealed class FileTraceCaptureStore : ITraceCaptureStore
{
    private readonly string _rootDir;

    public FileTraceCaptureStore(string rootDir)
    {
        _rootDir = rootDir ?? throw new ArgumentNullException(nameof(rootDir));
        Directory.CreateDirectory(_rootDir);
    }

    public ValueTask<TraceCapturePersistenceResult> SaveAsync(
        TraceCaptureBundle bundle,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetDir = Path.Combine(_rootDir, bundle.CaptureSessionId);
        if (Directory.Exists(targetDir))
            return ValueTask.FromResult(TraceCapturePersistenceResult.Fail(
                $"Capture '{bundle.CaptureSessionId}' already exists — append-only, cannot overwrite."));

        var stagingDir = Path.Combine(_rootDir, $".staging-{bundle.CaptureSessionId}-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stagingDir);

            // Write manifest
            var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var manifestPath = Path.Combine(stagingDir, "capture-manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(bundle, options));

            // Write records
            var recordsPath = Path.Combine(stagingDir, "records.json");
            File.WriteAllText(recordsPath, JsonSerializer.Serialize(bundle.Records, options));

            // Atomic publish
            Directory.Move(stagingDir, targetDir);

            return ValueTask.FromResult(TraceCapturePersistenceResult.Ok(targetDir));
        }
        catch (Exception ex)
        {
            TryDelete(stagingDir);
            return ValueTask.FromResult(TraceCapturePersistenceResult.Fail(ex.Message));
        }
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { /* best effort */ }
    }
}
