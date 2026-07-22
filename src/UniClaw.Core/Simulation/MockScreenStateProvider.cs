using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation;

/// <summary>
/// MockScreenStateProvider — 返回编程值的 IScreenStateProvider 实现。
/// 对齐: ScrollableMockVisionService 滚动方法 → 独立接口。
/// Simulation mock 不走 AI, 直接返回编程值。
/// </summary>
public sealed class MockScreenStateProvider : IScreenStateProvider
{
    private readonly bool _hasScroll;
    private readonly double _scrollProgress;
    private readonly bool _isEndOfList;
    private readonly ScrollSwipeConfig? _scrollSwipeConfig;

    /// <summary>默认构造 — 无滚动 (对齐 DefaultScreenStateProvider)</summary>
    public MockScreenStateProvider()
    {
        _hasScroll = false;
        _scrollProgress = 0.0;
        _isEndOfList = true;
        _scrollSwipeConfig = null;
    }

    /// <summary>编程值构造 — 测试用</summary>
    public MockScreenStateProvider(
        bool hasScroll,
        double scrollProgress = 0.0,
        bool isEndOfList = true,
        ScrollSwipeConfig? scrollSwipeConfig = null)
    {
        _hasScroll = hasScroll;
        _scrollProgress = scrollProgress;
        _isEndOfList = isEndOfList;
        _scrollSwipeConfig = scrollSwipeConfig;
    }

    /// <inheritdoc />
    public bool HasScroll() => _hasScroll;

    /// <inheritdoc />
    public double GetScrollProgress() => _scrollProgress;

    /// <inheritdoc />
    public bool IsEndOfList() => _isEndOfList;

    /// <inheritdoc />
    public ScrollSwipeConfig? GetScrollSwipeConfig() => _scrollSwipeConfig;
}
