using UniClaw.Agent.Dsh;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using UniClaw.Host.Dsh;

// DSH-backed Product Host composition root. The core Product Host remains
// closed over UniClaw.Kernel; this executable is the explicit realization
// adapter that adds UniClaw.Agent.Dsh for real Settings traversal.
//
//   --settings-traversal 真实 DSH Agent 驱动的 Android Settings 多步导航
//   --settings-coverage  AGT-005 可配置有界 Settings 菜单覆盖遍历
//   --device <id>         ADB 设备号（或 UNICLAW_ANDROID_DEVICE）
//   --runs <dir>          产物根目录（默认 runs）
//   --target <state>      目标态（Settings traversal 默认 checked）
//   --target-descriptor   目标控件语义名（默认 Wi-Fi）
//   --runtime-http        启动 DSH Host 的 Runtime HTTP launch surface
//   --runtime-port <p>    Runtime HTTP 监听端口（默认 5080）

var runsRoot = "runs";
string? deviceId = null;
string? targetState = null;
var targetDescriptor = "Wi-Fi";
var settingsTraversal = false;
var settingsCoverage = false;
var runtimeHttp = false;
var runtimePort = 5080;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--runs":
            runsRoot = args[++i];
            break;
        case "--runtime-http":
            runtimeHttp = true;
            break;
        case "--runtime-port":
            runtimePort = int.Parse(args[++i]);
            break;
        case var p when !p.StartsWith("--"):
            runsRoot = p;
            break;
        case "--settings-traversal":
            settingsTraversal = true;
            break;
        case "--settings-coverage":
            settingsCoverage = true;
            break;
        case "--device":
            deviceId = args[++i];
            break;
        case "--target":
            targetState = args[++i];
            break;
        case "--target-descriptor":
            targetDescriptor = args[++i];
            break;
        default:
            Console.Error.WriteLine($"未知参数：{args[i]}");
            return 2;
    }
}

if (runtimeHttp)
{
    var runtimeDevice = deviceId ?? Environment.GetEnvironmentVariable("UNICLAW_ANDROID_DEVICE");
    if (string.IsNullOrWhiteSpace(runtimeDevice))
    {
        Console.Error.WriteLine("ENVIRONMENT_UNAVAILABLE: --device 或 UNICLAW_ANDROID_DEVICE 是必需的。");
        return 2;
    }
    var runtimeServer = RuntimeHttpServer.Create(Array.Empty<string>(), runsRoot, runtimeDevice);
    Console.WriteLine($"runtime-http listening on http://127.0.0.1:{runtimePort}");
    await runtimeServer.RunAsync(runtimePort);
    return 0;
}

if (settingsTraversal == settingsCoverage)
{
    Console.Error.WriteLine("须恰指定 --settings-traversal 或 --settings-coverage 之一。");
    return 2;
}

var device = deviceId ?? Environment.GetEnvironmentVariable("UNICLAW_ANDROID_DEVICE");
if (string.IsNullOrWhiteSpace(device))
{
    Console.Error.WriteLine("ENVIRONMENT_UNAVAILABLE: --device 或 UNICLAW_ANDROID_DEVICE 是必需的。");
    return 2;
}

DshAgentAdapter? dshAgent = null;
DshOpenedDecisionChannel? dshChannel = null;
DshOpenedHttpPeer? dshPeer = null;
HostRunner.HostRunResult? result = null;
SettingsCoverageRunner.RunResult? coverageResult = null;
var productSessionId = $"{(settingsCoverage ? "settings-coverage" : "settings-traversal")}-session-{Guid.NewGuid():N}";
try
{
    var config = UniagentProdYaml.LoadDefault();
    var configuredModel = config.Model;
    var modelName = Environment.GetEnvironmentVariable("UNICLAW_UNIAGENT_PROD_MODEL")
        ?? configuredModel.Name;
    var model = new ModelConfiguration(configuredModel.Provider, modelName);
    Console.WriteLine($"agent.provider={model.Provider} agent.model={modelName} dsh.endpoint={config.Service.BaseUri}");

    RunAdb(device, "shell", "svc", "wifi", "enable");
    RunAdb(device, "shell", "am", "start", "-S", "-a", "android.settings.SETTINGS");
    Thread.Sleep(2000);

    AgentDecision? Consult(AgentDecisionContext context)
    {
        if (dshAgent is null)
        {
            dshPeer = new DshOpenedHttpPeer(config.Service, model: model);
            dshChannel = new DshOpenedDecisionChannel(dshPeer, attachTimeout: TimeSpan.FromSeconds(30));
            dshAgent = new DshAgentAdapter(dshChannel, productSessionId, context.RunId,
                turnTimeout: TimeSpan.FromSeconds(110));
        }
        return dshAgent.Consult(context);
    }

    if (settingsCoverage)
    {
        var coverageConfig = SettingsCoverageConfig.LoadDefault();
        Console.WriteLine(
            $"coverage.taskTitle={coverageConfig.Session.TaskTitle} workspace={coverageConfig.Session.Workspace}"
            + $" reuse={coverageConfig.Session.WorkspaceReuse} autoCloseTurn={coverageConfig.Session.AutoCloseTurn}"
            + $" maxSteps={coverageConfig.Bounds.MaxSteps} maxScrolls={coverageConfig.Bounds.MaxScrolls}"
            + $" targets=[{string.Join(";", coverageConfig.TargetPages)}]");
        var liveAssets = new LivePerception.LiveAssets(
            device,
            "wifi-settings",
            Path.Combine(RepoRoot(), "platforms", "perception"),
            Path.Combine(RepoRoot(), ".perception", "venv", "bin", "python"),
            Path.Combine(RepoRoot(), ".perception", "cache"));
        coverageResult = SettingsCoverageRunner.Run(runsRoot, new SettingsCoverageRunner.Options(
            DeviceId: device,
            Live: liveAssets,
            Config: coverageConfig,
            UnderlyingConsult: Consult,
            DshSessionIdAccessor: () => dshAgent?.DshSessionId));
    }
    else
    {
        result = HostRunner.RunOnce(runsRoot, new HostRunner.HostOptions
        {
            DeviceId = device,
            ConsultAgent = Consult,
            SettingsTraversal = true,
            TargetState = targetState ?? "checked",
            TargetSemanticDescriptor = targetDescriptor,
            Live = new LivePerception.LiveAssets(
                device,
                "wifi-settings",
                Path.Combine(RepoRoot(), "platforms", "perception"),
                Path.Combine(RepoRoot(), ".perception", "venv", "bin", "python"),
                Path.Combine(RepoRoot(), ".perception", "cache")),
        });
    }
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
finally
{
    if (dshAgent is not null)
    {
        foreach (var diagnostic in dshAgent.Diagnostics)
            Console.Error.WriteLine($"dsh.diagnostic: {diagnostic.Code} {diagnostic.Message}");
        try { dshAgent.RevokeAttachmentAsync().GetAwaiter().GetResult(); }
        catch (Exception ex) { Console.Error.WriteLine($"dsh.cleanup: {ex.Message}"); }
    }
    dshAgent?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    dshChannel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    dshPeer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

if (coverageResult is not null)
{
    var coverage = coverageResult;
    Console.WriteLine($"status   : {coverage.Status}" + (coverage.Reason is null ? "" : $" ({coverage.Reason})"));
    Console.WriteLine($"outcome  : {coverage.Outcome ?? "-"}");
    Console.WriteLine($"coverage : {coverage.Report.Status} rate={coverage.Report.CoverageRate:P0} steps={coverage.Steps.Count}");
    foreach (var item in coverage.Report.Items)
        Console.WriteLine($"  {(item.Covered ? "[x]" : "[ ]")} {item.Requirement}: {item.Detail}");
    foreach (var uncovered in coverage.Report.UncoveredItems)
        Console.WriteLine($"  uncovered: {uncovered}");
    Console.WriteLine($"divergence: {coverage.Report.FirstDivergence ?? "NONE"}");
    Console.WriteLine($"run dir  : {coverage.RunDir}");
    Console.WriteLine($"digest   : {coverage.FactsDigest}");
    // Kernel 终态与覆盖判定分离：kernel 证明 end-state 义务（回到根页），
    // director 证明覆盖完成。退出码取覆盖判定（诚实报告部分覆盖）。
    if (coverage.Status != RunDriveStatus.Completed)
        return HostRunner.ExitCode(coverage.Status);
    return coverage.Report.Status == "CoverageComplete" ? 0 : 3;
}

Console.WriteLine($"status   : {result.Status}" + (result.Reason is null ? "" : $" ({result.Reason})"));
Console.WriteLine($"outcome  : {result.OutcomeClassification ?? "-"}");
Console.WriteLine($"delivered: {result.DeliveredEffects} [{string.Join(",", result.ReceiptOutcomes)}]");
Console.WriteLine($"run dir  : {result.RunDir}");
Console.WriteLine($"digest   : {result.FactsDigest}");
return HostRunner.ExitCode(result.Status);

static string RepoRoot()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent!)
        if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))
            && File.Exists(Path.Combine(dir.FullName, "UniClaw.Kernel.slnx")))
            return dir.FullName;
    throw new InvalidOperationException("未定位到仓库根（请在仓库内运行）");
}

static void RunAdb(string device, params string[] arguments)
{
    var info = new System.Diagnostics.ProcessStartInfo("adb")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    info.ArgumentList.Add("-s");
    info.ArgumentList.Add(device);
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    using var process = System.Diagnostics.Process.Start(info)
        ?? throw new InvalidOperationException("ENVIRONMENT_UNAVAILABLE: adb did not start");
    process.WaitForExit(10000);
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"ENVIRONMENT_UNAVAILABLE: adb failed: {process.StandardError.ReadToEnd()}");
}
