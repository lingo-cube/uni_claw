namespace UniClaw.Core.Traversal;

/// <summary>
/// ScreenStateResult — RefreshAsync 的 Core 返回类型 (host-target-architecture 冲突 C1)。
/// 取代 Device-only 的 AdbScreenStateResult: 完整替换, 非共存。
/// 字段 (决策 2026-08-05): Succeeded / Status / HasScroll / IsEndOfList / Failure。
/// UIA 层级已移除 (delete-uia): HierarchyXml / HierarchyFingerprint 不再存在。
/// 不包含 Progress —— Progress 由锁定的
/// IScreenStateProvider.GetScrollProgress() 方法拥有, 不在此重复。
/// </summary>
public sealed record class ScreenStateResult(
    bool Succeeded,
    string Status,
    bool HasScroll,
    bool IsEndOfList,
    ScreenFailure? Failure);
