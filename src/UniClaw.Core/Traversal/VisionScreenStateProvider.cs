using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Traversal;

/// <summary>
/// IObservableScreenStateProvider that reads scroll state from a previously
/// analysed PageAnalysis (local-vision scenarios). Optional UIA provider supplies
/// hierarchy XML as a redundant side-channel; UIA failure never blocks the Vision path.
/// </summary>
public sealed class VisionScreenStateProvider : IObservableScreenStateProvider
{
    private readonly Func<PageAnalysis?> _getCurrentAnalysis;
    private readonly IObservableScreenStateProvider? _uia;

    public VisionScreenStateProvider(
        Func<PageAnalysis?> getCurrentAnalysis,
        IObservableScreenStateProvider? uia = null)
    {
        _getCurrentAnalysis = getCurrentAnalysis
            ?? throw new ArgumentNullException(nameof(getCurrentAnalysis));
        _uia = uia;
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
    /// RefreshAsync — 主路径返回 Vision-derived 滚动状态 (PageAnalysis)。
    /// 可选 UIA provider 作为冗余侧信道提供 hierarchy XML; UIA 失败 (异常或失败结果)
    /// 均不影响 Vision 主路径。无 UIA 时 hierarchy 为 null。
    /// </summary>
    public async Task<ScreenStateResult> RefreshAsync(
        string? previousHierarchyXml = null,
        bool afterScroll = false,
        CancellationToken cancellationToken = default)
    {
        var analysis = _getCurrentAnalysis();
        string? hierarchyXml = null;
        string? fingerprint = null;

        // UIA redundancy — try/catch, failure does not affect Vision main path
        if (_uia is not null)
        {
            try
            {
                var uiaResult = await _uia.RefreshAsync(
                        previousHierarchyXml, afterScroll, cancellationToken)
                    .ConfigureAwait(false);
                if (uiaResult.Succeeded)
                {
                    hierarchyXml = uiaResult.HierarchyXml;
                    fingerprint = uiaResult.HierarchyFingerprint;
                }
            }
            catch
            {
                // UIA failure is non-fatal — Vision main path continues
            }
        }

        return new ScreenStateResult(
            Succeeded: true,
            Status: "vision",
            HierarchyXml: hierarchyXml,
            HierarchyFingerprint: fingerprint,
            HasScroll: analysis?.HasScroll ?? false,
            IsEndOfList: analysis?.IsEndOfList ?? true,
            Failure: null);
    }
}
