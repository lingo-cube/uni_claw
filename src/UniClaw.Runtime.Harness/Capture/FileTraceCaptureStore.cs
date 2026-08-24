using System.Text;
using System.Text.Json;

namespace UniClaw.Runtime.Harness.Capture;

/// <summary>Append-only local persistence with validated atomic publication.</summary>
public sealed class FileTraceCaptureStore : ITraceCaptureStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private readonly string _rootDir;

    public FileTraceCaptureStore(string rootDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDir);
        _rootDir = Path.GetFullPath(rootDir);
        Directory.CreateDirectory(_rootDir);
    }

    public async ValueTask<TraceCapturePersistenceResult> SaveAsync(TraceCaptureBundle bundle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        cancellationToken.ThrowIfCancellationRequested();
        var errors = Validate(bundle);
        if (errors.Count > 0) return TraceCapturePersistenceResult.Fail([.. errors]);

        var targetDir = Path.Combine(_rootDir, bundle.CaptureSessionId);
        if (Directory.Exists(targetDir))
            return TraceCapturePersistenceResult.Fail($"Capture '{bundle.CaptureSessionId}' already exists — append-only, cannot overwrite.");

        var stagingDir = Path.Combine(_rootDir, $".staging-{bundle.CaptureSessionId}-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stagingDir);
            cancellationToken.ThrowIfCancellationRequested();
            await WriteJsonAsync(Path.Combine(stagingDir, "capture-manifest.json"), bundle, cancellationToken);
            await WriteJsonAsync(Path.Combine(stagingDir, "records.json"), bundle.Records, cancellationToken);
            if (bundle.ObservabilityTrace is { } trace)
                await WriteJsonAsync(Path.Combine(stagingDir, "observability-trace.json"), trace, cancellationToken);

            var checksums = new StringBuilder();
            if (!bundle.Artifacts.IsDefaultOrEmpty)
            {
                var artifactDir = Path.Combine(stagingDir, "artifacts");
                Directory.CreateDirectory(artifactDir);
                foreach (var artifact in bundle.Artifacts.OrderBy(x => x.ArtifactId, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relativePath = $"artifacts/{artifact.ArtifactId}.bin";
                    var path = Path.Combine(stagingDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    await File.WriteAllBytesAsync(path, artifact.Content.ToArray(), cancellationToken);
                    checksums.Append(artifact.ContentHash).Append("  ").Append(relativePath).Append('\n');
                }
            }
            await File.WriteAllTextAsync(Path.Combine(stagingDir, "checksums.sha256"), checksums.ToString(), Encoding.UTF8, cancellationToken);
            ValidateStaging(stagingDir, bundle);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(stagingDir, targetDir);
            return TraceCapturePersistenceResult.Ok(targetDir);
        }
        catch (OperationCanceledException)
        {
            TryDelete(stagingDir);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(stagingDir);
            return TraceCapturePersistenceResult.Fail(ex.Message);
        }
    }

    private static List<string> Validate(TraceCaptureBundle bundle)
    {
        var errors = new List<string>();
        if (bundle.SchemaVersion != 1) errors.Add($"Unsupported capture schema {bundle.SchemaVersion}.");
        if (!IsSafeId(bundle.CaptureSessionId)) errors.Add("CaptureSessionId must be a safe single path segment.");
        if (!bundle.Records.Select(x => x.Order).SequenceEqual(Enumerable.Range(1, bundle.Records.Length)))
            errors.Add("Capture record ordering must be contiguous from one.");

        var artifactIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in bundle.Artifacts)
        {
            if (!IsSafeId(artifact.ArtifactId)) errors.Add($"Artifact ID '{artifact.ArtifactId}' is unsafe.");
            if (!artifactIds.Add(artifact.ArtifactId)) errors.Add($"Duplicate artifact ID '{artifact.ArtifactId}'.");
            if (artifact.Content.IsDefault) errors.Add($"Artifact '{artifact.ArtifactId}' has no content bytes.");
            if (artifact.ByteCount != artifact.Content.Length) errors.Add($"Artifact '{artifact.ArtifactId}' byte count mismatch.");
            var actual = TraceCaptureSession.ComputeHash(artifact.Content.AsSpan());
            if (!string.Equals(actual, artifact.ContentHash, StringComparison.Ordinal))
                errors.Add($"Artifact '{artifact.ArtifactId}' content hash mismatch.");
            if (artifact.DerivedFromArtifactId is not null && !bundle.Artifacts.Any(x => x.ArtifactId == artifact.DerivedFromArtifactId))
                errors.Add($"Artifact '{artifact.ArtifactId}' references missing derivation '{artifact.DerivedFromArtifactId}'.");
        }
        return errors;
    }

    private static void ValidateStaging(string stagingDir, TraceCaptureBundle bundle)
    {
        if (!File.Exists(Path.Combine(stagingDir, "capture-manifest.json"))
            || !File.Exists(Path.Combine(stagingDir, "records.json"))
            || !File.Exists(Path.Combine(stagingDir, "checksums.sha256")))
            throw new InvalidDataException("Capture staging is incomplete.");
        foreach (var artifact in bundle.Artifacts)
        {
            var path = Path.Combine(stagingDir, "artifacts", $"{artifact.ArtifactId}.bin");
            if (!File.Exists(path)) throw new InvalidDataException($"Artifact '{artifact.ArtifactId}' was not staged.");
            if (!string.Equals(TraceCaptureSession.ComputeHash(File.ReadAllBytes(path)), artifact.ContentHash, StringComparison.Ordinal))
                throw new InvalidDataException($"Artifact '{artifact.ArtifactId}' failed staged hash validation.");
        }
    }

    private static Task WriteJsonAsync(string path, object value, CancellationToken cancellationToken)
        => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions), cancellationToken);

    private static bool IsSafeId(string value)
        => !string.IsNullOrWhiteSpace(value) && value is not "." and not ".."
           && value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0
           && !Path.IsPathRooted(value);

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { }
    }
}
