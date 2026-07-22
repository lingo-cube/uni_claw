using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// IPageAnalyzer — 页面感知+验证能力。
/// 单一职责: "当前屏幕是什么？是期望页面吗？"
/// 替换: IVisionProvider (页面分析 + 入口查找部分)。
/// 不含滚动方法 (HasScroll/GetScrollProgress/IsEndOfList/GetScrollSwipeConfig → IScreenStateProvider)。
/// 不含 VerifyPageWithVisionAsync (Host 层便利方法, YAGNI)。
/// </summary>
public interface IPageAnalyzer
{
    /// <summary>分析当前页面截图 → PageAnalysis (元素列表、菜单、弹窗等)</summary>
    Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default);

    /// <summary>在启动器中查找目标 app 的图标坐标</summary>
    Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default);

    /// <summary>验证当前页面是否匹配期望类型 (元数据版本, 非 vision)</summary>
    Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken ct = default);
}
