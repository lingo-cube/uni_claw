using UniClaw.Core.Observability;
using UniClaw.Host.Artifacts;

namespace UniClaw.TraceTool;

/// <summary>
/// V2-aware read-only asset query for TraceTool consumption.
/// Wraps a <see cref="FileAssetStore"/> with the correct asset-space root.
/// SchemaVersion dispatch: V2 → assets/{runId}/; V1 → run root fallback.
/// </summary>
public sealed class FileAssetQuery : IAssetQuery
{
    private readonly FileAssetStore _v2Store;
    private readonly string _runDir;
    private readonly string _runId;
    private readonly string _schemaVersion;

    public FileAssetQuery(string runDir, string runId, string schemaVersion)
    {
        _runDir = runDir;
        _runId = runId;
        _schemaVersion = schemaVersion;
        if (schemaVersion == "2")
        {
            var assetsRoot = Path.Combine(runDir, "assets", runId);
            _v2Store = new FileAssetStore(assetsRoot);
        }
        else
        {
            _v2Store = new FileAssetStore(runDir);  // V1: assets at run root
        }
    }

    public Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default)
        => _v2Store.ReadAsync(_runId, relativePath, ct);

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
        => _v2Store.ExistsAsync(_runId, relativePath, ct);
}
