using UniClaw.Host;

// HOST-001 v0 入口：单次 headless run，产物落 ./runs/<runid>/，按终局退出。
// 档位（可组合）：
//   （默认）        仿真帧源 + 确定性投递
//   --replay       感知锚回放（录制响应 JSON 驱动完整环路，无需服务/设备）
//   --service      服务回放（录制截图 → 真 Python 视觉推理 → 完整环路）
//   --effect adb   真机 ADB 投递（需模拟器；帧坐标用录制标定）
//   --device <id>  ADB 设备号（默认 emulator-5554）
//   --target <s>   目标态（默认 on；flip 语义）
//   --runs <dir>   产物根目录（默认 runs）
//   <path> 位置参数 = runs 根（兼容旧用法）

var runsRoot = "runs";
HostRunner.HostOptions options = new();

// 诊断模式：--analyze <png> —— 起真感知服务，打印该截图的 switch 检测
// （标定工具：对录制锚与实屏各跑一次即可对比布局漂移）
if (args.Length >= 2 && args[0] == "--analyze")
{
    var png = args[1];
    var repo = RepoRoot();
    using var feed = new ServicePerception.ServiceReplayFrameFeed(
        new V0Runtime.VirtualClock(),
        new ServicePerception.ServiceReplayAssets(png, png, "diagnostic",
            Path.Combine(repo, "platforms", "perception"),
            Path.Combine(repo, ".perception", "venv", "bin", "python"),
            Path.Combine(repo, ".perception", "cache")));
    var detection = feed.AnalyzePublic(png);
    Console.WriteLine($"{Path.GetFileName(png)}: switch bounds = ({detection.X1:F4},{detection.Y1:F4})-({detection.X2:F4},{detection.Y2:F4})");
    return 0;
}

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--runs":
            runsRoot = args[++i];
            break;
        case var p when !p.StartsWith("--"):
            runsRoot = p;
            break;
        case "--live":
            options = options with
            {
                Effect = HostRunner.EffectProfile.AdbLive,
                Live = new LivePerception.LiveAssets(
                    options.DeviceId ?? "emulator-5554",
                    "wifi-settings",
                    Path.Combine(RepoRoot(), "platforms", "perception"),
                    Path.Combine(RepoRoot(), ".perception", "venv", "bin", "python"),
                    Path.Combine(RepoRoot(), ".perception", "cache")),
                TargetState = LivePerception.LiveFrameFeed.ReadWifiState(options.DeviceId ?? "emulator-5554") == "on"
                    ? "off" : "on",
            };
            break;
        case "--replay":
            options = options with { Replay = WifiAnchors() };
            break;
        case "--service":
            options = options with { ServiceReplay = WifiService() };
            break;
        case "--effect" when args[++i] == "adb":
            options = options with { Effect = HostRunner.EffectProfile.AdbLive, Bounds = WifiCalibration() };
            break;
        case "--device":
            options = options with { DeviceId = args[++i] };
            break;
        case "--target":
            options = options with { TargetState = args[++i] };
            break;
        default:
            Console.Error.WriteLine($"未知参数：{args[i]}");
            return 2;
    }
}

var result = HostRunner.RunOnce(runsRoot, options);
Console.WriteLine($"status   : {result.Status}" + (result.Reason is null ? "" : $" ({result.Reason})"));
Console.WriteLine($"outcome  : {result.OutcomeClassification ?? "-"}");
Console.WriteLine($"delivered: {result.DeliveredEffects} [{string.Join(",", result.ReceiptOutcomes)}]");
Console.WriteLine($"run dir  : {result.RunDir}");
Console.WriteLine($"digest   : {result.FactsDigest}");
return HostRunner.ExitCode(result.Status);

// ---- 默认资产定位（wifi-slice2-calibration 录制对；仓库根向上寻找）----

static string RepoRoot()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent!)
        if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))
            && File.Exists(Path.Combine(dir.FullName, "UniClaw.Kernel.slnx")))
            return dir.FullName;
    throw new InvalidOperationException("未定位到仓库根（请在仓库内运行）");
}

static string CapturePath(string name) => Path.Combine(
    RepoRoot(), "platforms", "perception", "evaluation", "assets", "captures",
    "wifi-slice2-calibration", name);

static ReplayPerception.ReplayAssets WifiAnchors() => new(
    CapturePath("perception/wifi-off-emulator-5554.json"),
    CapturePath("perception/wifi-on-emulator-5554.json"),
    ScreenId: "wifi-settings");

static ServicePerception.ServiceReplayAssets WifiService() => new(
    CapturePath("frames/wifi-off-emulator-5554.png"),
    CapturePath("frames/wifi-on-emulator-5554.png"),
    ScreenId: "wifi-settings",
    ProviderRoot: Path.Combine(RepoRoot(), "platforms", "perception"),
    PythonExecutable: Path.Combine(RepoRoot(), ".perception", "venv", "bin", "python"),
    CacheRoot: Path.Combine(RepoRoot(), ".perception", "cache"));

static HostRunner.Calibration WifiCalibration() => new(0.834722, 0.407031, 0.958333, 0.450781);
