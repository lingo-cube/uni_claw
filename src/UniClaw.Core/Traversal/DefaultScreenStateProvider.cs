namespace UniClaw.Core.Traversal;

/// <summary>
/// DefaultScreenStateProvider — 返回 IVisionProvider 原虚拟默认值。
/// HasScroll=false, GetScrollProgress=0.0, IsEndOfList=true, GetScrollSwipeConfig=null。
/// 用于非滚动场景的测试和基线构造。
/// </summary>
public sealed class DefaultScreenStateProvider : IScreenStateProvider
{
    /// <inheritdoc />
    public bool HasScroll() => false;

    /// <inheritdoc />
    public double GetScrollProgress() => 0.0;

    /// <inheritdoc />
    public bool IsEndOfList() => true;

    /// <inheritdoc />
    public ScrollSwipeConfig? GetScrollSwipeConfig() => null;
}
