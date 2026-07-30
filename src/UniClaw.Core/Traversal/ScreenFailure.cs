namespace UniClaw.Core.Traversal;

/// <summary>
/// ScreenFailure — Core 屏幕状态失败抽象 (Core.Traversal, 与 IScreenStateProvider 同层)。
/// 设备层 (Device) 的 AdbCommandFailure 在 provider 边界映射为此类型,
/// 使 Core 的 ScreenStateResult 不依赖 Device 类型 (host-target-architecture 冲突 C1)。
/// 字段与 AdbCommandFailure (Kind/Message/ExceptionType) 形状一致, 边界映射零信息丢失。
/// </summary>
public sealed record class ScreenFailure(
    string Kind,
    string Message,
    string? ExceptionType = null);