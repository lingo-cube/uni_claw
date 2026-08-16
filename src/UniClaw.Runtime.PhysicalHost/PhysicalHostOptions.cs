namespace UniClaw.Runtime.PhysicalHost;

/// <summary>
/// 生产组合根的宿主配置 — 纯命令行注入，无配置文件发现 / 无 provider 选择标志
/// （Slice 1 实现约束：禁止 provider 注册表 / 发现 / 选择）。
/// </summary>
public sealed record PhysicalHostOptions(
    string AdbExecutable,
    string? Serial,
    string TargetApplication,
    string VisionSocketPath,
    int DisplayWidth,
    int DisplayHeight,
    string? LaunchIntentAction = null,
    bool Slice2Proof = false,
    bool MultilevelProof = false,
    bool ScrollProof = false)
{
    /// <summary>默认目标应用：Slice 1 证明场景使用系统设置（§33 范围；WiFi 行为属于 Slice 2）。</summary>
    public const string DefaultTargetApplication = "com.android.settings";

    /// <summary>Slice 2 语义闭环的公开 Settings 启动意图 — 确定性落在含 WiFi 开关的 Internet 页。</summary>
    public const string DefaultWifiLaunchIntentAction = "android.settings.WIFI_SETTINGS";

    /// <summary>Multi-level 遍历的根页启动意图 — 确定性落在 Settings 根页（Agent 自行逐跳导航）。</summary>
    public const string SettingsRootLaunchIntentAction = "android.settings.SETTINGS";

    /// <summary>同容器视口滚动证明的启动意图 — 确定性落在 Developer options 页（`Automatic system updates` 开关 below-fold）。</summary>
    public const string DeveloperOptionsLaunchIntentAction = "com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS";

    public const int DefaultDisplayWidth = 1080;

    public const int DefaultDisplayHeight = 1920;

    public const string DefaultVisionSocketPath = "/tmp/uniclaw-vision.sock";

    /// <summary>
    /// 解析命令行参数。支持的键：--adb、--serial、--app、--vision-socket、--width、--height、
    /// --launch-intent（机制级启动意图）、--slice2（运行 Slice 2 WiFi 语义闭环证明）、
    /// --multilevel（运行多级页面遍历证明：Settings 根页 → Network &amp; internet → Internet）、
    /// --scroll（运行同容器视口滚动语义闭环证明：Developer options 页 → Automatic system updates 开关）。
    /// 未知键或缺失值抛 FormatException（组合根不静默忽略配置错误）。
    /// </summary>
    public static PhysicalHostOptions Parse(string[] args)
    {
        string adbExecutable = "adb";
        string? serial = null;
        string targetApplication = DefaultTargetApplication;
        string visionSocketPath = DefaultVisionSocketPath;
        int width = DefaultDisplayWidth;
        int height = DefaultDisplayHeight;
        string? launchIntentAction = null;
        bool slice2Proof = false;
        bool multilevelProof = false;
        bool scrollProof = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--adb" when i + 1 < args.Length:
                    adbExecutable = args[++i];
                    break;
                case "--serial" when i + 1 < args.Length:
                    serial = args[++i];
                    break;
                case "--app" when i + 1 < args.Length:
                    targetApplication = args[++i];
                    break;
                case "--vision-socket" when i + 1 < args.Length:
                    visionSocketPath = args[++i];
                    break;
                case "--width" when i + 1 < args.Length:
                    width = int.Parse(args[++i]);
                    break;
                case "--height" when i + 1 < args.Length:
                    height = int.Parse(args[++i]);
                    break;
                case "--launch-intent" when i + 1 < args.Length:
                    launchIntentAction = args[++i];
                    break;
                case "--slice2":
                    slice2Proof = true;
                    break;
                case "--multilevel":
                    multilevelProof = true;
                    break;
                case "--scroll":
                    scrollProof = true;
                    break;
                default:
                    throw new FormatException($"无法识别的参数 '{args[i]}'（或缺少值）。用法：--adb &lt;path&gt; --serial &lt;serial&gt; --app &lt;pkg&gt; --vision-socket &lt;path&gt; --width N --height N [--launch-intent &lt;action&gt;] [--slice2] [--multilevel] [--scroll]");
            }
        }

        return new PhysicalHostOptions(adbExecutable, serial, targetApplication, visionSocketPath, width, height, launchIntentAction, slice2Proof, multilevelProof, scrollProof);
    }
}
