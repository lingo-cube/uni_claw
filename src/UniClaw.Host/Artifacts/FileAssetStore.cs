using UniClaw.Core.Observability;

namespace UniClaw.Host.Artifacts;

/// <summary>
/// File-backed <see cref="IAssetStore"/> implementation. Assets are stored under
/// a single root directory (the V2 asset space: {runDir}/assets/{runId}/).
/// The runId parameter on each method is accepted but unused — the root is already
/// scoped to one run at construction (per-run assembly). Write is atomic (tmp+move).
/// Also implements <see cref="IAssetQuery"/> for the read-only analyzer facet (same object, different facet).
/// </summary>
public sealed class FileAssetStore : IAssetStore, IAssetQuery
{
    private readonly string _assetsRoot;

    public FileAssetStore(string assetsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsRoot);
        _assetsRoot = Path.GetFullPath(assetsRoot);
    }

    // ── IAssetStore ──

    public async Task WriteAsync(string runId, string relativePath, byte[] bytes, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(bytes);
        var fullPath = Path.Combine(_assetsRoot, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);
        await AssetStagingWriter.WriteBytesAsync(fullPath, bytes, ct);
    }

    public Task<byte[]?> ReadAsync(string runId, string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var fullPath = Path.Combine(_assetsRoot, relativePath);
        if (!File.Exists(fullPath))
            return Task.FromResult<byte[]?>(null);
        return Task.FromResult<byte[]?>(File.ReadAllBytes(fullPath));
    }

    public Task<bool> ExistsAsync(string runId, string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Task.FromResult(File.Exists(Path.Combine(_assetsRoot, relativePath)));
    }

    public Task<IReadOnlyList<string>> ListAsync(string runId, CancellationToken ct = default)
    {
        if (!Directory.Exists(_assetsRoot))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var files = Directory.GetFiles(_assetsRoot, "*", SearchOption.AllDirectories);
        var relativePaths = files
            .Select(f => Path.GetRelativePath(_assetsRoot, f))
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(relativePaths);
    }

    // ── IAssetQuery (same object, different facet — D-6) ──

    Task<byte[]?> IAssetQuery.ReadAsync(string relativePath, CancellationToken ct)
        => ReadAsync(string.Empty, relativePath, ct);

    Task<bool> IAssetQuery.ExistsAsync(string relativePath, CancellationToken ct)
        => ExistsAsync(string.Empty, relativePath, ct);
}
