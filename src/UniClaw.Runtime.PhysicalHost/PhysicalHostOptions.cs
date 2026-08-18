namespace UniClaw.Runtime.PhysicalHost;

/// <summary>
/// 生产组合根的宿主配置 — 纯命令行注入，无配置文件发现 / 无 provider 选择标志
/// （Slice 1 实现约束：禁止 provider 注册表 / 发现 / 选择）。
///
/// Vision 运行时模式（vision-runtime-bootstrap）：
///   - VisionSocketPath = null（默认）→ MANAGED 模式：PhysicalHost 组合根启动
///     VisionServiceHost，端点由 host.SocketPath 产出（绝不猜测）。
///   - VisionSocketPath 显式提供（--vision-socket &lt;path&gt;）→ EXTERNAL_ATTACH
///     模式：PhysicalHost 消费外部管理的端点，不拥有 Vision 进程。
///   - VisionPythonExecutable（--vision-python &lt;path&gt;）：managed 模式 python
///     解析优先级 1（显式）；缺省回落仓库管理运行时（.venv-local-vision）。
/// </summary>
public sealed record PhysicalHostOptions(
    string AdbExecutable,
    string? Serial,
    string TargetApplication,
    string? VisionSocketPath,
    int DisplayWidth,
    int DisplayHeight,
    string? LaunchIntentAction = null,
    bool Slice2Proof = false,
    bool MultilevelProof = false,
    bool ScrollProof = false,
    bool CorpusProof = false,
    string? VisionPythonExecutable = null)
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

    /// <summary>历史默认端点（vision-runtime-bootstrap 后仅作显式 EXTERNAL_ATTACH 的合法值，
    /// 不再是 managed 模式的隐式真值）。</summary>
    public const string DefaultVisionSocketPath = "/tmp/uniclaw-vision.sock";

    /// <summary>
    /// 解析命令行参数。支持的键：--adb、--serial、--app、--vision-socket、--vision-python、
    /// --width、--height、--launch-intent（机制级启动意图）、--slice2、--multilevel、--scroll。
    /// 未知键或缺失值抛 FormatException（组合根不静默忽略配置错误）。
    /// </summary>
    public static PhysicalHostOptions Parse(string[] args)
    {
        string adbExecutable = "adb";
        string? serial = null;
        string targetApplication = DefaultTargetApplication;
        string? visionSocketPath = null; // null = MANAGED（组合根启动 Vision host）
        string? visionPythonExecutable = null;
        int width = DefaultDisplayWidth;
        int height = DefaultDisplayHeight;
        string? launchIntentAction = null;
        bool slice2Proof = false;
        bool multilevelProof = false;
        bool scrollProof = false;
        bool corpusProof = false;

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
                    visionSocketPath = args[++i]; // 显式外部端点 → EXTERNAL_ATTACH 模式
                    break;
                case "--vision-python" when i + 1 < args.Length:
                    visionPythonExecutable = args[++i]; // managed python 优先级 1
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
                case "--corpus":
                    corpusProof = true;
                    break;
                default:
                    throw new FormatException($"无法识别的参数 '{args[i]}'（或缺少值）。用法：--adb &lt;path&gt; --serial &lt;serial&gt; --app &lt;pkg&gt; --vision-socket &lt;path&gt; --vision-python &lt;path&gt; --width N --height N [--launch-intent &lt;action&gt;] [--slice2] [--multilevel] [--scroll] [--corpus]");
            }
        }

        return new PhysicalHostOptions(
            adbExecutable, serial, targetApplication, visionSocketPath, width, height,
            launchIntentAction, slice2Proof, multilevelProof, scrollProof, corpusProof, visionPythonExecutable);
    }
}
