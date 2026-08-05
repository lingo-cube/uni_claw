using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Traversal;

/// <summary>
/// IObservableScreenStateProvider that reads scroll state from a previously
/// analysed PageAnalysis (local-vision scenarios). UIA side-channel removed
/// (delete-uia): the provider is purely vision-derived.
/// </summary>
public sealed class VisionScreenStateProvider : IObservableScreenStateProvider
{
    private readonly Func<PageAnalysis?> _getCurrentAnalysis;

    /// <summary>
    /// Construct the provider. <paramref name="getCurrentAnalysis"/> supplies
    /// the current <see cref="PageAnalysis"/> (local-vision accessor;
    /// null-safe elsewhere).
    /// </summary>
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

    /// <summary>
    /// RefreshAsync — 返回 Vision-derived 滚动状态快照。
    /// UIA 层级已移除: 无 hierarchy / fingerprint; 仅报告滚动状态
    /// (HasScroll=true, IsEndOfList=false), 成功结果, 无失败。
    /// </summary>
    public Task<ScreenStateResult> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ScreenStateResult(
            Succeeded: true,
            Status: "vision",
            HasScroll: true,
            IsEndOfList: false,
            Failure: null));
    }
}
