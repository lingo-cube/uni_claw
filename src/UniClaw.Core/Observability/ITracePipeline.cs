namespace UniClaw.Core.Observability;

/// <summary>
/// Run-scoped non-blocking asset submission pipeline. Producers call <see cref="Submit"/>
/// without awaiting; a single background writer persists assets outside the critical path.
/// Run finalization calls <see cref="DrainAsync"/> before the result is recorded.
/// </summary>
public interface ITracePipeline
{
    /// <summary>
    /// Submit an asset for batched persistence. Non-blocking — returns immediately.
    /// Returns false when the channel is full (submission dropped — counted in stats).
    /// Never throws.
    /// </summary>
    bool Submit(AssetSubmission submission);

    /// <summary>
    /// Complete the channel and await all accepted submissions. Idempotent.
    /// After the first call returns, all accepted bytes are on disk.
    /// </summary>
    Task DrainAsync(CancellationToken ct = default);

    /// <summary>Post-drain statistics (Accepted/Dropped/WriteFailures).</summary>
    PipelineStats Stats { get; }
}
