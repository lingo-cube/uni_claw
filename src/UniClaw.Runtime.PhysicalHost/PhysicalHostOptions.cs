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
    string? VisionPythonExecutable = null)
{
    /// <summary>Server mode: start the DriverHost server instead of running a proof scenario.</summary>
    public bool Serve { get; init; } = false;

    /// <summary>DriverHost listen port (default 5177; 0 = ephemeral).</summary>
    public int Port { get; init; } = 5177;

    /// <summary>Optional auto-shutdown timeout in seconds (null = run until Ctrl+C).</summary>
    public int? TimeoutSeconds { get; init; } = null;
    /// <summary>Default physical display width used to normalize captured coordinates.</summary>
    public const int DefaultDisplayWidth = 1080;

    /// <summary>Default physical display height used to normalize captured coordinates.</summary>
    public const int DefaultDisplayHeight = 1920;

    /// <summary>历史默认端点（vision-runtime-bootstrap 后仅作显式 EXTERNAL_ATTACH 的合法值，
    /// 不再是 managed 模式的隐式真值）。</summary>
    public const string DefaultVisionSocketPath = "/tmp/uniclaw-vision.sock";

    /// <summary>
    /// 解析命令行参数。支持的键：--adb、--serial、--app、--vision-socket、--vision-python、
    /// --width、--height、--launch-intent（机制级启动意图）、--serve、--port、--timeout。
    /// 未知键或缺失值抛 FormatException（组合根不静默忽略配置错误）。
    /// </summary>
    public static PhysicalHostOptions Parse(string[] args)
    {
        string adbExecutable = "adb";
        string? serial = null;
        string? targetApplication = null;
        string? visionSocketPath = null; // null = MANAGED（组合根启动 Vision host）
        string? visionPythonExecutable = null;
        int width = DefaultDisplayWidth;
        int height = DefaultDisplayHeight;
        string? launchIntentAction = null;
        bool serve = false;
        int port = 5177;
        int? timeoutSeconds = null;

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
                case "--serve":
                    serve = true;
                    break;
                case "--port" when i + 1 < args.Length:
                    port = int.Parse(args[++i]);
                    break;
                case "--timeout" when i + 1 < args.Length:
                    timeoutSeconds = int.Parse(args[++i]);
                    break;
                default:
                    throw new FormatException($"无法识别的参数 '{args[i]}'（或缺少值）。用法：--adb &lt;path&gt; --serial &lt;serial&gt; --app &lt;pkg&gt; --vision-socket &lt;path&gt; --vision-python &lt;path&gt; --width N --height N [--launch-intent &lt;action&gt;] [--serve] [--port N] [--timeout N]");
            }
        }

        if (string.IsNullOrWhiteSpace(targetApplication))
            throw new FormatException("--app <package> is required for production physical composition.");

        return new PhysicalHostOptions(
            adbExecutable, serial, targetApplication, visionSocketPath, width, height,
            launchIntentAction, visionPythonExecutable)
        {
            Serve = serve,
            Port = port,
            TimeoutSeconds = timeoutSeconds,
        };
    }
}
