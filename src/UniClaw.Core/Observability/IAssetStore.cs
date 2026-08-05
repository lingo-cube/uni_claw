namespace UniClaw.Core.Observability;

/// <summary>
/// Storage abstraction for asset bytes (screenshots, analysis, evidence).
/// The key space is <c>{runId}/{relativePath}</c> — runId is injected at assembly,
/// producers submit only the relative path.
/// Event-side storage reuses the existing <see cref="ITraceStorage"/> / FileTraceStorage.
/// </summary>
public interface IAssetStore
{
    /// <summary>
    /// Persist asset bytes. Implementations must be thread-safe.
    /// When <paramref name="append"/> is true, bytes are appended to the existing file;
    /// otherwise the file is overwritten atomically.
    /// </summary>
    Task WriteAsync(string runId, string relativePath, byte[] bytes, CancellationToken ct = default, bool append = false);

    /// <summary>Read asset bytes, or null when the key does not exist.</summary>
    Task<byte[]?> ReadAsync(string runId, string relativePath, CancellationToken ct = default);

    /// <summary>Check whether an asset key exists.</summary>
    Task<bool> ExistsAsync(string runId, string relativePath, CancellationToken ct = default);

    /// <summary>
    /// List every relative path under a run prefix.
    /// The returned paths are relative (no runId segment).
    /// </summary>
    Task<IReadOnlyList<string>> ListAsync(string runId, CancellationToken ct = default);
}
