namespace UniClaw.Core.Observability;

/// <summary>
/// Sink notified when a batched asset write fails. The pipeline emits;
/// Host subscribes at assembly to write issue entries.
/// </summary>
public interface IPipelineFailureSink
{
    /// <summary>Called synchronously by the pipeline writer on each write failure.</summary>
    void OnWriteFailed(AssetSubmission submission, Exception exception);
}
