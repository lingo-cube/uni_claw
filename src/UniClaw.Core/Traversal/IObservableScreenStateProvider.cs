namespace UniClaw.Core.Traversal;

/// <summary>
/// IObservableScreenStateProvider — 继承锁定的 IScreenStateProvider,
/// 追加唯一新方法 RefreshAsync 返回 Core-lifted ScreenStateResult。
/// 解决 host-target-architecture 冲突 C1: Host 不再向下转型到 AdbScreenStateProvider
/// 取 Device-only AdbScreenStateResult, 而经由本接口拿到 Core 的 ScreenStateResult。
/// UIA 层级已移除 (delete-uia): RefreshAsync 不再接收 previousHierarchyXml / afterScroll,
/// 仅返回当前 Vision-derived 滚动状态快照。
/// 4 个锁定方法 (HasScroll/GetScrollProgress/IsEndOfList/GetScrollSwipeConfig) 由
/// IScreenStateProvider 继承, 签名字节不变; ArchitectureGuardTests 锁定 4 方法不受影响
/// (反射按 DeclaringType == IScreenStateProvider 过滤, 子接口的新方法不计入)。
/// </summary>
public interface IObservableScreenStateProvider : IScreenStateProvider
{
    /// <summary>
    /// 刷新屏幕状态, 返回 Core ScreenStateResult。
    /// </summary>
    Task<ScreenStateResult> RefreshAsync(CancellationToken cancellationToken = default);
}
