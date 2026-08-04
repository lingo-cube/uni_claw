namespace UniClaw.Core.Observability;

/// <summary>
/// IAssetQuery — read-only asset query facet (ISP, design D-6). Analyzers and
/// verification consumers receive this, never the write-capable
/// <see cref="IAssetStore"/>. Assembled per-run with runId injected at construction —
/// consumers call with relative paths only; the runId/path composition stays internal.
/// Implementations (e.g. FileAssetStore) may implement both this facet and
/// <see cref="IAssetStore"/> on the same object, exposing a different facet per consumer.
/// </summary>
public interface IAssetQuery
{
    /// <summary>
    /// Read asset bytes by relative path, or null when the path does not exist.
    /// </summary>
    Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Check whether a relative path exists under this run's asset space.</summary>
    Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);
}
