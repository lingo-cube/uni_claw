namespace UniClaw.Core.Traversal;

/// <summary>
/// IScreenStateProvider — 滚动+设备状态查询。
/// 从 IVisionProvider 分离 — 滚动是设备状态, 不是 AI 判断。
/// Traversal namespace (与 ScrollSwipeConfig 同层)。
/// 4 方法锁定: HasScroll, GetScrollProgress, IsEndOfList, GetScrollSwipeConfig。
/// </summary>
public interface IScreenStateProvider
{
    /// <summary>检查当前页面是否有滚动数据</summary>
    bool HasScroll();

    /// <summary>获取当前滚动进度 (0.0 = 顶部, 1.0 = 底部)</summary>
    double GetScrollProgress();

    /// <summary>检查是否到达滚动内容的末尾</summary>
    bool IsEndOfList();

    /// <summary>获取页面级滑动坐标配置, null 表示使用引擎默认值</summary>
    ScrollSwipeConfig? GetScrollSwipeConfig();
}
