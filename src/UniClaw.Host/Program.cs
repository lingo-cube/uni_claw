using UniClaw.Host;

// HOST-001 v0 入口（SIM-002 G1 后仅真实档）：
//   --live          全真闭环（实屏截图 → 真推理 → 真 ADB 投递 → 实屏复查）
//   --device <id>   ADB 设备号（默认 emulator-5554）
//   --target <s>    目标态（默认按当前态翻转）
//   --runs <dir>    产物根目录（默认 runs）
//   --analyze <png> 标定诊断：真感知服务推理该截图，打印 switch 检测
//   <path> 位置参数 = runs 根（兼容旧用法）
// 仿真/回放/半真档已随 SIM-002 G1 移出产品 Host（tests 侧 DevLoopRunner
// 组合）；无 --live / --analyze 的调用 fail-closed 退出。

// 诊断模式：--analyze <png> —— 起真感知服务（产品面 VisionServiceSession），
// 打印该截图的 switch 检测（标定工具：对录制锚与实屏各跑一次即可对比
// 布局漂移；纯真推理诊断，不进入 run 环路）
if (args.Length >= 2 && args[0] == "--analyze")
{
    var png = args[1];
    var repo = RepoRoot();
    using var session = new VisionServiceSession(
        Path.Combine(repo, "platforms", "perception"),
        Path.Combine(repo, ".perception", "venv", "bin", "python"),
        Path.Combine(repo, ".perception", "cache"));
    var responseJson = session.Analyze(File.ReadAllBytes(png));
    var detection = HostUtilities.ExtractJson(responseJson, "switch", png);
    Console.WriteLine($"{Path.GetFileName(png)}: switch bounds = ({detection.X1:F4},{detection.Y1:F4})-({detection.X2:F4},{detection.Y2:F4})");
    return 0;
}

var runsRoot = "runs";
string? deviceId = null;
string? targetState = null;
var live = false;

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
            live = true;
            break;
        case "--device":
            deviceId = args[++i];
            break;
        case "--target":
            targetState = args[++i];
            break;
        default:
            Console.Error.WriteLine($"未知参数：{args[i]}");
            return 2;
    }
}

if (!live)
{
    // fail-closed：产品 Host 无仿真默认档（SIM-002 G1）——须显式 --live；
    // agent 咨询实现尚未接入产品 Host，全真闭环暂经 HostLiveFullTests
    // （注入确定性咨询 double）验证，CLI 接线待真 agent change。
    Console.Error.WriteLine("无操作档：须 --live（全真闭环）或 --analyze <png>（标定诊断）。");
    Console.Error.WriteLine("仿真/回放/半真档已移出产品 Host（SIM-002 G1）——由 Simulation Host（测试侧）组合。");
    return 2;
}

var device = deviceId ?? "emulator-5554";
HostRunner.HostRunResult result;
try
{
    result = HostRunner.RunOnce(runsRoot, new HostRunner.HostOptions
    {
        DeviceId = device,
        // 咨询缝 fail-closed（SIM-002 G1）：CLI 尚无可注入的真 agent 实现
        ConsultAgent = null,
        // PER-014 R3：TargetState 词汇 on/off → checked/unchecked；wifi 探针
        // 读数（设备无线电态 on/off）仅作翻转方向输入（egress writer 零生产读者）。
        TargetState = targetState ?? (LivePerception.LiveFrameFeed.ReadWifiState(device) == "on" ? "unchecked" : "checked"),
        Live = new LivePerception.LiveAssets(
            device,
            "wifi-settings",
            Path.Combine(RepoRoot(), "platforms", "perception"),
            Path.Combine(RepoRoot(), ".perception", "venv", "bin", "python"),
            Path.Combine(RepoRoot(), ".perception", "cache")),
    });
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
Console.WriteLine($"status   : {result.Status}" + (result.Reason is null ? "" : $" ({result.Reason})"));
Console.WriteLine($"outcome  : {result.OutcomeClassification ?? "-"}");
Console.WriteLine($"delivered: {result.DeliveredEffects} [{string.Join(",", result.ReceiptOutcomes)}]");
Console.WriteLine($"run dir  : {result.RunDir}");
Console.WriteLine($"digest   : {result.FactsDigest}");
return HostRunner.ExitCode(result.Status);

// ---- 仓库根定位（向上寻找）----

static string RepoRoot()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent!)
        if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))
            && File.Exists(Path.Combine(dir.FullName, "UniClaw.Kernel.slnx")))
            return dir.FullName;
    throw new InvalidOperationException("未定位到仓库根（请在仓库内运行）");
}
