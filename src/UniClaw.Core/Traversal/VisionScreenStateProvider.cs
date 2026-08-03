using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Traversal;

/// <summary>
/// Thin IScreenStateProvider wrapper that reads scroll state from a previously
/// analysed PageAnalysis. For local-vision scenarios where no UIAutomator is available.
/// Does NOT implement IObservableScreenStateProvider — InterceptionHandler auto-falls
/// through to the AI seen-set diffing safe path.
/// </summary>
public sealed class VisionScreenStateProvider : IScreenStateProvider
{
    private readonly Func<PageAnalysis?> _getCurrentAnalysis;

    public VisionScreenStateProvider(Func<PageAnalysis?> getCurrentAnalysis)
    {
        _getCurrentAnalysis = getCurrentAnalysis
            ?? throw new ArgumentNullException(nameof(getCurrentAnalysis));
    }

    /// <inheritdoc />
    public bool HasScroll() =>
        _getCurrentAnalysis()?.HasScroll ?? false;

    /// <inheritdoc />
    public bool IsEndOfList() =>
        _getCurrentAnalysis()?.IsEndOfList ?? true;

    /// <inheritdoc />
    /// <returns>0.0 — local-vision has no scrollbar position tracking.</returns>
    public double GetScrollProgress() => 0.0;

    /// <inheritdoc />
    /// <returns>null — use engine defaults.</returns>
    public ScrollSwipeConfig? GetScrollSwipeConfig() => null;
}
