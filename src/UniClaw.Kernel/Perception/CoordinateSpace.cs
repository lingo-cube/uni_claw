namespace UniClaw.Kernel.Perception;

/// <summary>
/// CSC-001 Slice A：屏幕方向声明。screencap 返回当前 framebuffer 方向原样
/// 像素——Rotation 是显式声明位（与 PixelWidth/Height 共同定义空间），
/// 不是变换指令。非法 cast 值在 <see cref="CoordinateSpace"/> 构造期拒绝
/// （ING-006 D2 同款 fail-closed）。
/// </summary>
public enum ScreenRotation
{
    /// <summary>无旋转（portrait 基准）。</summary>
    None = 0,

    /// <summary>90°。</summary>
    Rot90 = 90,

    /// <summary>180°。</summary>
    Rot180 = 180,

    /// <summary>270°。</summary>
    Rot270 = 270,
}

/// <summary>
/// CSC-001 Slice A：坐标空间 typed contract（per-capture 事实，非全局常量）。
/// 职责：让 screenshot / hierarchy observation / grounding target / effect
/// coordinate 各自可声明所属空间，两端（感知归一化基准 vs tap 投影基准）
/// 经 <see cref="Matches"/> 机械碰头——PER-013 E4 根因（HostOptions 默认
/// viewport 1080×2400 vs 实测 1080×1920，正确检测错位 tap）的结构修复。
/// 语义：
/// - record 相等 = provenance 级同一（id/dims/rotation/captureId 全等）；
/// - <see cref="Matches"/> = 坐标语义兼容（dims + rotation 相等——归一化
///   坐标在两空间间投影可互换）；CaptureId 是 provenance，不参与兼容判定。
/// Kernel 只拥有本 contract 与相等性执法，不拥有 viewport 数值——数值只能
/// 来自 capture 实测 / 设备实况查询 / 显式已验证配置（Slice B）。
/// </summary>
public sealed record CoordinateSpace
{
    /// <summary>设备全屏空间 id 前缀（与 AdbEffectDriver.SupportedFrame 词汇族对齐）。</summary>
    public const string DeviceViewportPrefix = "device-viewport";

    /// <summary>稳定机械可比对键（如 device-viewport:1080x1920@0）。</summary>
    public string CoordinateSpaceId { get; }

    /// <summary>实测像素宽。</summary>
    public int PixelWidth { get; }

    /// <summary>实测像素高。</summary>
    public int PixelHeight { get; }

    /// <summary>方向声明。</summary>
    public ScreenRotation Rotation { get; }

    /// <summary>来源 capture（provenance；不参与语义兼容判定）。</summary>
    public string? CaptureId { get; }

    /// <summary>构造期 fail-closed：id 非空、dims 为正、rotation 为已定义成员。</summary>
    public CoordinateSpace(
        string coordinateSpaceId,
        int pixelWidth,
        int pixelHeight,
        ScreenRotation rotation = ScreenRotation.None,
        string? captureId = null)
    {
        if (string.IsNullOrWhiteSpace(coordinateSpaceId))
        {
            throw new ArgumentException("coordinate space 没有 id = 无效协议载荷（CSC-001 规则 A）", nameof(coordinateSpaceId));
        }

        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            throw new ArgumentException($"dimensions 必须为正：{pixelWidth}x{pixelHeight}", nameof(pixelWidth));
        }

        if (!Enum.IsDefined(rotation))
        {
            throw new ArgumentException($"rotation 非法：{(int)rotation}（期望 0/90/180/270）", nameof(rotation));
        }

        CoordinateSpaceId = coordinateSpaceId;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Rotation = rotation;
        CaptureId = captureId;
    }

    /// <summary>
    /// 设备全屏空间工厂：id 确定性渲染 <c>device-viewport:{W}x{H}@{rotation}</c>
    /// （同参同 id；携带实测尺寸——frame 词汇不再是裸字符串）。
    /// </summary>
    public static CoordinateSpace DeviceViewport(
        int pixelWidth,
        int pixelHeight,
        ScreenRotation rotation = ScreenRotation.None,
        string? captureId = null) =>
        new($"{DeviceViewportPrefix}:{pixelWidth}x{pixelHeight}@{(int)rotation}", pixelWidth, pixelHeight, rotation, captureId);

    /// <summary>
    /// 坐标语义兼容：dims + rotation 相等。grounding 侧空间与 effect 侧
    /// 空间 <c>Matches</c> 才允许投影 dispatch（Slice C/D 执法点）；
    /// 不 Matches → RE-GROUND / RE-OBSERVE，不 dispatch。
    /// </summary>
    public bool Matches(CoordinateSpace other) =>
        other is not null
        && PixelWidth == other.PixelWidth
        && PixelHeight == other.PixelHeight
        && Rotation == other.Rotation;
}
