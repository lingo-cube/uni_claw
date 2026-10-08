using UniClaw.Agent.Dsh;
using UniClaw.Host;
using UniClaw.Host.Capability;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Capability;
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
LanguageInspectionSelection? agentLanguageInspectionSelection = null;
HostRunner.HostRunResult? result = null;
UniClaw.Kernel.Capability.CapabilityRegistry? capabilityRegistry = null;
// CAP-012：Runtime Integration 域独立注册表（ADR-0035 三域不共享注册状态）。
UniClaw.Kernel.Capability.CapabilityRegistry? runtimeIntegrationRegistry = null;
SettingsCoverageRunner.RunResult? coverageResult = null;
var productSessionId = $"{(settingsCoverage ? "settings-coverage" : "settings-traversal")}-session-{Guid.NewGuid():N}";
try
{
    // PRF-002（ADR-0041）：产品 profile（host-neutral）与 DSH 绑定分轨加载。
    var agentProfile = UniClaw.Agent.Profile.UniAgentProfileYaml.LoadDefault();
    var bindings = UniagentDshBindingsYaml.LoadDefault();
    // CAP-006：模型管理声明为产品能力组件（Kernel 公开缝 ModelManagement）；
    // 缺省 realization 借用 DSH——binding 从 DSH 绑定（modelSelection）+ 产品
    // modelRoles 声明推导注入，决策/slow 模型一律经缝 resolve，不再直连 yaml。
    var models = DshModelManagement.FromBindings(
        bindings, agentProfile,
        Environment.GetEnvironmentVariable("UNICLAW_UNIAGENT_PROD_MODEL"));
    var model = DshModelManagement.ResolveDshModel(models, LogicalProfileId.AgentDecision);
    var slowTextModel = DshModelManagement.ResolveDshModel(models, LogicalProfileId.Text);
    Console.WriteLine($"agent.provider={model.Provider} agent.model={model.Name} dsh.endpoint={bindings.Service.BaseUri} model.realization={DshModelManagement.RealizationName}");
    // AGT-017：peer 提前创建，决策通道与 Slow 桥共享同一 attached 会话
    //（/slow 服务端要求 attached 且单飞行）。peer 构造零网络副作用。
    dshPeer = new DshOpenedHttpPeer(bindings.Service, model: model);
    var slowBridge = new DshSlowConsult(
        (requestId, prompt, slowModel, imagePng, cancellationToken) =>
            dshPeer.ExecuteSlowAsync(requestId, prompt, slowModel, imagePng, cancellationToken),
        slowTextModel);
    // PER-019：UniPerception 异步 fetch 缝（Host 形状；fetch 界随 slow 配置）。
    var slowSettings = SettingsCoverageConfig.LoadDefault().SlowSettings;
    var slowFetchBound = TimeSpan.FromMilliseconds(slowSettings?.BoundedWaitMs ?? 15000);
    UniClaw.Host.UniPerceptionPipeline.Fetch uniPerceptionFetch =
        (request, sessionCorrelation, cancellationToken) => slowBridge
            .FetchAsync(request, sessionCorrelation, slowFetchBound, cancellationToken)
            .ContinueWith(t => new UniClaw.Host.UniPerceptionFetchResult(
                t.Result.Status, t.Result.Admitted, t.Result.Proposals,
                t.Result.SemanticDisposition, t.Result.Diagnostic), TaskScheduler.Default);
    // PER-019：组合根能力注册（管理面；描述无 provider/model 名）。
    // CAP-009：uni.perception 注册为可执行实例（双协议 marker + 健康聚合
    // owner）——fast 探针查资产可观测面（懒执行），模型端探针直接复用
    // CAP-008 的 ModelManagement.CheckHealth；无接口的源缺席=诚实 Unknown。
    var fastAssetsProbe = new UniClaw.Kernel.Capability.PerceptionHealthSource(
        "fast.yolo-ocr-assets",
        () =>
        {
            var root = RepoRoot();
            var missing = new List<string>();
            if (!Directory.Exists(Path.Combine(root, "platforms", "perception")))
                missing.Add("platforms/perception");
            if (!File.Exists(Path.Combine(root, ".perception", "venv", "bin", "python")))
                missing.Add(".perception/venv/bin/python");
            return missing.Count == 0
                ? new UniClaw.Kernel.Capability.CapabilityHealthReport(UniClaw.Kernel.Capability.HealthStatus.Healthy, null)
                : new UniClaw.Kernel.Capability.CapabilityHealthReport(UniClaw.Kernel.Capability.HealthStatus.Degraded,
                    $"missing: {string.Join(",", missing)}");
        });
    var modelHealthProbe = new UniClaw.Kernel.Capability.PerceptionHealthSource(
        "slow.text-models", models.CheckHealth);
    capabilityRegistry = UniClaw.Host.SettingsCoverage.PerceptionCapabilityComposition
        .RegisterProductPerception(uniPerception: new UniClaw.Kernel.Capability.UniPerceptionCapability(
            new[] { fastAssetsProbe, modelHealthProbe }));
    // CAP-012：Runtime Integration 域注册语言检查能力（skill 首次全新实战：
    // 确定性 Unicode 脚本规则，Finding 非权威）。
    runtimeIntegrationRegistry = UniClaw.Host.Capability.RuntimeIntegrationCapabilityComposition
        .RegisterLanguageInspector();
    // CAP-008：注册**可执行实例**（声明与运行时缝同一事实，Resolve 可取回）；
    // realization 名显式取 adapter 侧常量（消除 Host/DSH 双写字符串）。
    UniClaw.Host.Capability.ModelManagementCapabilityComposition.RegisterModelManagement(
        capabilityRegistry, models, DshModelManagement.RealizationName);

    RunAdb(device, "shell", "am", "start", "-S", "-a", "android.settings.SETTINGS");
    Thread.Sleep(2000);

    AgentDecision? Consult(AgentDecisionContext context)
    {
        if (dshAgent is null)
        {
            dshChannel = new DshOpenedDecisionChannel(dshPeer, attachTimeout: TimeSpan.FromSeconds(30));
            dshAgent = new DshAgentAdapter(dshChannel, productSessionId, context.RunId,
                turnTimeout: TimeSpan.FromSeconds(200)); // PER-019: 高于服务端 180s 界
        }
        var decision = dshAgent.Consult(context);
        if (agentLanguageInspectionSelection is null
            && dshAgent.TakeInitialTaskInitialization()?.CapabilitySelection is { } selection)
        {
            agentLanguageInspectionSelection = new LanguageInspectionSelection(
                selection.CapabilityId,
                selection.ExpectedLanguage ?? string.Empty,
                selection.IgnoreRoutes);
        }
        return decision;
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
            DshSessionIdAccessor: () => dshAgent?.DshSessionId,
            SlowConsult: uniPerceptionFetch,
            LanguageInspection: coverageConfig.LanguageInspectionRequest,
            LanguageInspectionSelectionProvider: () => agentLanguageInspectionSelection,
            RuntimeIntegrationCapabilities: runtimeIntegrationRegistry));
    }
    else
    {
        var settingsPolicyConfig = SettingsCoverageConfig.LoadDefault();
        // AGT-015：traversal 目标必须与策略声明的 targeted toggle 绑定；
        // 冲突在任何设备动作/咨询前 fail-closed（静态授权不随 CLI 扩大）。
        var targetConflict = settingsPolicyConfig.ActionPolicy?
            .ValidateTraversalTargetDescriptor(targetDescriptor);
        if (targetConflict is not null)
        {
            Console.Error.WriteLine($"CONFIG_CONFLICT: {targetConflict}");
            return 2;
        }
        result = HostRunner.RunOnce(runsRoot, new HostRunner.HostOptions
        {
            DeviceId = device,
            ConsultAgent = Consult,
            SettingsTraversal = true,
            SettingsActionPolicy = settingsPolicyConfig.ActionPolicy,
            TargetState = targetState ?? "checked",
            TargetSemanticDescriptor = targetDescriptor,
            UniPerceptionFetch = uniPerceptionFetch,
            LanguageInspection = settingsPolicyConfig.LanguageInspectionRequest,
            LanguageInspectionSelectionProvider = () => agentLanguageInspectionSelection,
            RuntimeIntegrationCapabilities = runtimeIntegrationRegistry,
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
    // PER-019：组合根注册事实落盘（管理面证据；直接写已知 run dir）。
    var capabilityFactsDir = coverage.RunDir;
    if (capabilityRegistry is not null && capabilityFactsDir is not null)
    {
        try
        {
            WriteCapabilityProfiles(capabilityFactsDir!, capabilityRegistry, runtimeIntegrationRegistry);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(capabilityFactsDir, "capability-facts.json"),
                System.Text.Json.JsonSerializer.Serialize(
                    // CAP-012：合并双域注册表事实（Product + Runtime Integration）。
                    capabilityRegistry.Facts
                        .Concat(runtimeIntegrationRegistry?.Facts ?? Array.Empty<UniClaw.Kernel.Capability.CapabilityLifecycleFact>())
                        .Select(f => new
                    {
                        f.CapabilityId, f.Version, f.Lifecycle, f.Source, f.Sequence, domain = f.Domain.ToString(),
                    })));
        }
        catch (Exception factsError)
        {
            Console.Error.WriteLine($"capability-facts write failed: {factsError.Message}");
        }
    }
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
// PER-019：组合根注册事实落盘（traversal 模式同款）。
if (capabilityRegistry is not null && result.RunDir is not null)
{
    try
    {
        WriteCapabilityProfiles(result.RunDir, capabilityRegistry, runtimeIntegrationRegistry);
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(result.RunDir, "capability-facts.json"),
            System.Text.Json.JsonSerializer.Serialize(
                // CAP-012：合并双域注册表事实（Product + Runtime Integration）。
                capabilityRegistry.Facts
                    .Concat(runtimeIntegrationRegistry?.Facts ?? Array.Empty<UniClaw.Kernel.Capability.CapabilityLifecycleFact>())
                    .Select(f => new
                    {
                        f.CapabilityId, f.Version, f.Lifecycle, f.Source, f.Sequence, domain = f.Domain.ToString(),
                })));
    }
    catch (Exception factsError)
    {
        Console.Error.WriteLine($"capability-facts write failed: {factsError.Message}");
    }
}
return HostRunner.ExitCode(result.Status);

static void WriteCapabilityProfiles(
    string dir,
    UniClaw.Kernel.Capability.CapabilityRegistry? product,
    UniClaw.Kernel.Capability.CapabilityRegistry? runtimeIntegration)
{
    // CAP-012 D9：剖面事实源落盘（消费面=uni agent + skill 剖面消费模板）。
    try
    {
        var profiles = new List<object>();
        foreach (var registry in new[] { product, runtimeIntegration })
        {
            if (registry is null) continue;
            foreach (var id in registry.Facts.Select(f => f.CapabilityId).Distinct(StringComparer.Ordinal))
            {
                if (registry.Resolve(id) is not UniClaw.Kernel.Capability.ICapabilityProfileReporting reporting)
                    continue;
                var report = reporting.DescribeProfile();
                profiles.Add(new
                {
                    capabilityId = id,
                    report.Summary,
                    effectiveConfiguration = report.EffectiveConfiguration,
                    impactDisclosures = report.ImpactDisclosures.Select(d => new { d.Condition, d.Impact }),
                    report.Limitations,
                });
            }
        }
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(dir, "capability-profiles.json"),
            System.Text.Json.JsonSerializer.Serialize(profiles));
    }
    catch (Exception profileError)
    {
        Console.Error.WriteLine($"capability-profiles write failed: {profileError.Message}");
    }
}

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
    if (!process.WaitForExit(10000))
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // 进程已退出；仍按超时报告，不能把启动阶段误报为成功。
        }
        throw new InvalidOperationException(
            $"ENVIRONMENT_UNAVAILABLE: adb timed out after 10s (device={device})");
    }
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"ENVIRONMENT_UNAVAILABLE: adb failed: {process.StandardError.ReadToEnd()}");
}
