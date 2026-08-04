namespace UniClaw.Core.Observability;

/// <summary>
/// Post-drain pipeline statistics. Counters live in the event/log domain —
/// consumers read them after <see cref="ITracePipeline.DrainAsync"/> and write
/// summary trace events. Manifest is never written back.
/// </summary>
public sealed class PipelineStats
{
    /// <summary>Submissions accepted by the channel.</summary>
    public long Accepted { get; internal set; }

    /// <summary>Submissions dropped due to channel saturation (P2 designed-in behavior).</summary>
    public long Dropped { get; internal set; }

    /// <summary>Accepted submissions whose write to <see cref="IAssetStore"/> failed.</summary>
    public long WriteFailures { get; internal set; }
}
