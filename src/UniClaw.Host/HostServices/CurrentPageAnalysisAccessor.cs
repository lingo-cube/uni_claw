using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Host.HostServices;

/// <summary>
/// Shared state holder connecting the AnalysisWritingDecorator (writer) and
/// VisionScreenStateProvider (reader). Created once at assembly time and injected
/// into both sides. Thread-safe via volatile read/write on the reference.
/// </summary>
public sealed class CurrentPageAnalysisAccessor
{
    private volatile PageAnalysis? _current;

    /// <summary>
    /// The most recent PageAnalysis written by the AnalysisWritingDecorator.
    /// null before the first analysis completes.
    /// </summary>
    public PageAnalysis? Current
    {
        get => _current;
        set => _current = value;
    }
}
