namespace UniClaw.Settings.ValidationHost;

using UniClaw.Runtime.PhysicalHost;

using System.Collections.Immutable;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Traversal;
using UniClaw.Vision.Host;
using Agent = UniClaw.Runtime.Agent.Agent;

/// <summary>
/// Slice 1 生产组合入口（REALITY_COMPOSITION_FOUNDATION 证明 runner）。
/// 职责：解析 CLI 配置 → 设备解析（组合前置）→ 真实 Provider 组合 → Runtime 图装配 →
/// 运行 Agent 语义闭环（探测目标）→ 输出结构化证明证据。
///
/// 证明目标（design.md Implementation Slices Slice 1）：
///   A. 生产组合可启动：设备解析 + 真实 Provider 组合 + Runtime 图装配成功。
///   B. 生命周期 Cold → Attach → Ready → Fresh Observe → Initial WorldBelief：
///      Startup.Ready(RecoveryAnchor) + Reconcile 产出 belief（证明 run 内完成）。
///   C. 无 WiFi 能力执行：证明 run 只含 Startup 的 LaunchApp 分发，零 SetSwitch/Tap/Scroll。
///   F2. 无物理就绪 → 无分发、无 Traversal 执行：设备解析失败 / Attach 失败 → NotReady，零动作。
///
/// Slice 2（--slice2）：WIFI_SEMANTIC_LOOP 真实闭环证明 —
///   Goal → Agent Decision → Capability(SetEnabled) → Authorization → Lowering → Provider 机制
///   （am start 落 Internet 页 + tap 开关）→ 物理变化 → fresh Observation（seq 推进）→
///   perception（ImageSwitchStateProvider）→ GoalEvidence(SourceObservationSequence=fresh)。
///   唯一成功条件 = Action receipt + Fresh Observation + Perception Evidence + GoalEvidence。
///   F6-live：perception 失败（无候选）→ 非 SATISFIED（STATE_EVIDENCE_REQUIRED / BINDING_UNRESOLVED）。
///
/// 退出码：0 = 证明通过；1 = 运行期错误或证明断言失败；2 = NotReady（设备不可用 — F2 路径）或
///        语义环非 SATISFIED 终止（F6 路径）；64 = 参数错误。
/// </summary>
internal static class LegacyProofRunner
{
    /// <summary>探测对象身份 — 与任何 WiFi 语义无关（证明 C 的结构保证之一）。</summary>
    private const string ProbeObjectIdentity = "Slice1Probe";

    private const string ProbeStateDimension = "Enabled";

    public static async Task<int> RunAsync(string[] args)
    {
        ScenarioValidationOptions options;
        try
        {
            options = ScenarioValidationOptions.Parse(args);
        }
        catch (FormatException exception)
        {
            Console.Error.WriteLine($"ARGUMENT_ERROR {exception.Message}");
            return 64;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var cancellationToken = cts.Token;

        try
        {
            if (options.ScrollProof)
                return await RunScrollProofAsync(options, cancellationToken);
            if (options.MultilevelProof)
                return await RunMultiLevelProofAsync(options, cancellationToken);
            if (options.CorpusProof)
                return await RunCorpusProofAsync(options, cancellationToken);
            return options.Slice2Proof
                ? await RunSlice2ProofAsync(options, cancellationToken)
                : await RunSlice1ProofAsync(options, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"HOST_ERROR {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 真实环境前置（vision-runtime-bootstrap A2）：设备解析成功后，解析 Vision 运行时配置 →
    /// managed：早验证 → CanonicalVisionHostFactory 创建 → StartAsync → HEALTHY →
    /// host.SocketPath → BuildRealEnvironment（端点 = host 输出，绝不猜测）；或 external：
    /// 直接消费显式外部端点（不拥有 Vision 进程）。返回环境 + 可选 managed host——
    /// 调用方负责 finally dispose（恰好一次；VisionServiceHost.Dispose 执行
    /// KillProcess + CleanStaleSocket，无孤儿进程）。
    /// </summary>
    private static async Task<(PhysicalEnvironment Environment, VisionServiceHost? ManagedHost)> BuildEnvironmentAsync(
        PhysicalHostOptions options,
        string serial,
        CancellationToken cancellationToken)
    {
        var appRoot = VisionRuntimeBootstrap.ResolveAppRoot();
        var config = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(options, appRoot);

        if (config.Mode == VisionRuntimeMode.External)
        {
            VisionRuntimeBootstrap.ValidateVisionRuntimeConfiguration(config);
            return (PhysicalHostComposition.BuildRealEnvironment(options, serial, config.ExternalSocketPath), null);
        }

        VisionRuntimeBootstrap.ValidateVisionRuntimeConfiguration(config);
        var host = VisionRuntimeBootstrap.CreateManagedVisionHost(config);
        try
        {
            await host.StartAsync(cancellationToken);
            if (host.State != VisionHostState.Healthy)
            {
                throw new InvalidOperationException($"Vision host 未就绪：State={host.State}");
            }

            var environment = PhysicalHostComposition.BuildRealEnvironment(options, serial, host.SocketPath);
            Console.WriteLine($"HOST vision=managed socket={host.SocketPath} state={host.State}");
            return (environment, host);
        }
        catch
        {
            host.Dispose(); // 恰好一次；KillProcess + CleanStaleSocket（无孤儿进程）
            throw;
        }
    }

    /// <summary>
    /// REAL-WORLD FAILURE DISTRIBUTION corpus runner（REAL_WORLD_FAILURE_DISTRIBUTION_GATE）。
    ///
    /// 证据收集 gate：在真实模拟器上运行一组「正常」语义任务（复用已毕业/已校准的既有能力 —
    /// WiFi multilevel 与 Developer-options Automatic system updates，零新能力），记录每次
    /// terminal 结果与关键指标，输出结构化 per-run 矩阵供分类（A–Q）。
    ///
    /// 使用普通生产路径：Goal → Runtime.Agent → Navigation → Binding → Traversal → Environment
    /// → real Vision → Action → fresh verification → terminal。L1 按现状（本宿主组合不注入
    /// assistance provider = 生产 L0 路径）；不制造 Contradicted/Unresolved，不强制咨询。
    ///
    /// 场景矩阵（24 个 run，覆盖 OFF→ON / ON→OFF / already-satisfied / idempotent 重复）：
    ///   WiFi 组（multilevel：SettingsRoot → NetworkAndInternet → WifiInternet，冷启动根页）：
    ///     W1..W8 — OFF→ON（含重复）、W9..W10 — already-satisfied ON、W11..W14 — ON→OFF、W15..W16 —
    ///     already-satisfied OFF（含重复）
    ///   ASU 组（Developer options 单页 + bounded scroll）：
    ///     A1..A6 — OFF→ON（含重复）、A7..A8 — already-satisfied ON、A9..A10 — ON→OFF
    /// 每个 run 独立构造 fresh Runtime graph（Agent 单次 run 语义）；Vision host 按 run 复建
    /// （managed 生命周期，恰一次 dispose）。
    /// </summary>
    private static async Task<int> RunCorpusProofAsync(PhysicalHostOptions options, CancellationToken cancellationToken)
    {
        var resolution = await PhysicalHostComposition.ResolveDeviceAsync(options, cancellationToken);
        Console.WriteLine($"HOST deviceResolved={resolution.IsResolved}");
        if (!resolution.IsResolved)
        {
            Console.WriteLine("HOST startup=NotReady");
            Console.WriteLine($"HOST notReadyReason={resolution.FailureReason ?? "device not resolved"}");
            return 2;
        }
        var serial = resolution.Serial!;
        Console.WriteLine($"HOST serial={serial}");
        Console.WriteLine("HOST corpusMode=REAL_WORLD_FAILURE_DISTRIBUTION (24 runs; existing capabilities only; L1 not injected)");

        var scenarios = BuildCorpusScenarios();
        var results = new List<CorpusRunResult>(scenarios.Count);
        var startTotal = System.Diagnostics.Stopwatch.StartNew();

        foreach (var scenario in scenarios)
        {
            var runId = $"corpus-{scenario.Id}";
            Console.WriteLine($"---- CORPUS RUN {scenario.Id} task={scenario.Task} desired={scenario.DesiredState} prep={scenario.PrepareTo} ----");
            var runStart = System.Diagnostics.Stopwatch.StartNew();
            CorpusRunResult result;
            try
            {
                result = await RunCorpusScenarioAsync(options, serial, scenario, runId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                result = new CorpusRunResult(scenario.Id, scenario.Task, scenario.DesiredState, "HOST_ERROR",
                    exception.GetType().Name, RunState.Failed, 0, 0, 0, 0, 0, runStart.Elapsed, null);
                Console.WriteLine($"HOST_ERROR {exception.GetType().Name}: {exception.Message}");
            }
            result.Duration = runStart.Elapsed;
            results.Add(result);
            Console.WriteLine($"CORPUS-RUN id={result.Id} task={result.Task} desired={result.DesiredState} terminal={result.Terminal} reason={result.Reason ?? "(null)"} state={result.RunState} setSwitch={result.SetSwitchCount} settle={result.TotalSettleCount} navTaps={result.NavTapCount} scrolls={result.ScrollCount} journal={result.JournalEntries} durationMs={result.Duration.TotalMilliseconds:0}");
        }

        startTotal.Stop();
        PrintCorpusSummary(results, startTotal.Elapsed);
        return 0; // 证据收集 gate：exit 0 = corpus 完成（分类在 decision doc；不设证明断言）
    }

    private sealed record CorpusScenario(
        string Id,
        string Task,            // "Wifi" | "Asu"
        bool DesiredState,
        bool PrepareTo);        // host-run 物理准备的目标状态（WiFi/ASU 基线）

    private static List<CorpusScenario> BuildCorpusScenarios()
    {
        var scenarios = new List<CorpusScenario>();
        // WiFi multilevel：OFF→ON（含 idempotent 重复）、already-satisfied ON、ON→OFF、already-satisfied OFF
        scenarios.Add(new("W1", "Wifi", true, false));
        scenarios.Add(new("W2", "Wifi", true, false));
        scenarios.Add(new("W3", "Wifi", true, false));
        scenarios.Add(new("W4", "Wifi", true, true));
        scenarios.Add(new("W5", "Wifi", true, true));
        scenarios.Add(new("W6", "Wifi", true, true));
        scenarios.Add(new("W7", "Wifi", false, true));
        scenarios.Add(new("W8", "Wifi", false, true));
        scenarios.Add(new("W9", "Wifi", false, false));
        scenarios.Add(new("W10", "Wifi", false, false));
        scenarios.Add(new("W11", "Wifi", false, true));
        scenarios.Add(new("W12", "Wifi", true, false));
        scenarios.Add(new("W13", "Wifi", true, false));
        scenarios.Add(new("W14", "Wifi", true, true));
        // ASU（Developer options）：OFF→ON（含重复）、already-satisfied ON、ON→OFF
        scenarios.Add(new("A1", "Asu", true, false));
        scenarios.Add(new("A2", "Asu", true, false));
        scenarios.Add(new("A3", "Asu", true, true));
        scenarios.Add(new("A4", "Asu", true, true));
        scenarios.Add(new("A5", "Asu", false, true));
        scenarios.Add(new("A6", "Asu", false, false));
        scenarios.Add(new("A7", "Asu", false, true));
        scenarios.Add(new("A8", "Asu", true, false));
        scenarios.Add(new("A9", "Asu", true, false));
        scenarios.Add(new("A10", "Asu", true, true));
        return scenarios;
    }

    private sealed class CorpusRunResult
    {
        public CorpusRunResult(string id, string task, bool desired, string terminal, string? reason,
            RunState runState, int setSwitchCount, int totalSettleCount, int navTapCount, int scrollCount,
            int journalEntries, TimeSpan duration, string? terminalDetail)
        {
            Id = id;
            Task = task;
            DesiredState = desired;
            Terminal = terminal;
            Reason = reason;
            RunState = runState;
            SetSwitchCount = setSwitchCount;
            TotalSettleCount = totalSettleCount;
            NavTapCount = navTapCount;
            ScrollCount = scrollCount;
            JournalEntries = journalEntries;
            Duration = duration;
            TerminalDetail = terminalDetail;
        }

        public string Id { get; }
        public string Task { get; }
        public bool DesiredState { get; }
        public string Terminal { get; set; }
        public string? Reason { get; set; }
        public RunState RunState { get; set; }
        public int SetSwitchCount { get; set; }
        public int TotalSettleCount { get; set; }
        public int NavTapCount { get; set; }
        public int ScrollCount { get; set; }
        public int JournalEntries { get; set; }
        public TimeSpan Duration { get; set; }
        public string? TerminalDetail { get; set; }
    }

    /// <summary>执行单个 corpus 场景（生产路径；fresh graph per run；host-run 物理基线准备）。</summary>
    private static async Task<CorpusRunResult> RunCorpusScenarioAsync(
        PhysicalHostOptions options, string serial, CorpusScenario scenario, string runId, CancellationToken cancellationToken)
    {
        // ── Step 1: host-run 物理基线准备（同已毕业证明先例；不进语义路径、不计入 ActionHistory）──
        if (scenario.Task == "Wifi")
        {
            await PrepareWifiBaselineAsync(options, serial, scenario.PrepareTo, cancellationToken);
            await PrepareSettingsRootColdStartAsync(options, serial, cancellationToken);
        }
        else
        {
            await PrepareAsuBaselineAsync(options, serial, scenario.PrepareTo, cancellationToken);
        }

        // ── Step 2: 语义装配 + 真实组合 + Runtime graph（fresh per run）──────────
        var (environment, visionHost) = await BuildEnvironmentAsync(options, serial, cancellationToken);
        try
        {
            var attach = PhysicalHostComposition.CreateAttach(options, serial);
            HostRuntimeGraph graph;
            var wifi = SemanticObject.Define(WifiObjectIdentity, "ConnectivitySetting", [WifiStateDimension]);
            var setEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", WifiStateDimension);
            SemanticObject obj;
            Capability capability;
            SemanticGoalInput goal;

            if (scenario.Task == "Wifi")
            {
                obj = wifi;
                capability = setEnabled;
                goal = new SemanticGoalInput(WifiObjectIdentity, WifiStateDimension, scenario.DesiredState);
                var elementCriteria = new ElementBindingCriteria(
                    [obj],
                    ImmutableDictionary<string, string>.Empty.Add(WifiObjectIdentity, WifiTextAnchor),
                    ImmutableDictionary<string, string>.Empty.Add(WifiObjectIdentity, "toggle"));
                var navigationPageCriteria = new PageAnalysisCriteria(
                    options.TargetApplication,
                    ImmutableDictionary<string, ImmutableArray<string>>.Empty
                        .Add(SettingsRootPage, ["Connected devices", "Apps", "Notifications", "Battery"])
                        .Add(NetworkAndInternetPage, ["Network & internet", "Internet", "SIMs", "Airplane mode", "Hotspot & tethering"])
                        .Add(WifiInternetPage, ["Internet", "Wi-Fi", "Add network"]),
                    PageNegativeAnchors: null,
                    ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(WifiInternetPage, ["Wi-Fi"]));
                var identityCriteria = new PageAnalysisCriteria(
                    options.TargetApplication,
                    ImmutableDictionary<string, ImmutableArray<string>>.Empty
                        .Add(SettingsRootPage, ["Connected devices", "Apps", "Notifications", "Battery"])
                        .Add(NetworkAndInternetPage, ["Network & internet", "Internet", "SIMs", "Airplane mode", "Hotspot & tethering"])
                        .Add(WifiInternetPage, ["Internet", "Wi-Fi", "Add network"]),
                    ImmutableDictionary<string, ImmutableArray<string>>.Empty
                        .Add(SettingsRootPage, ["Internet", "Wi-Fi", "Add network", "SIMs", "Airplane mode", "Hotspot & tethering"])
                        .Add(NetworkAndInternetPage, ["Connected devices", "Apps", "Notifications", "Battery", "Add network",
                            "Wi-Fi", "Network preferences", "Non-carrier data usage"])
                        .Add(WifiInternetPage, ["Network & internet", "Connected devices", "Apps", "Notifications", "Battery", "SIMs", "Airplane mode", "Hotspot & tethering"]),
                    ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(WifiInternetPage, ["Wi-Fi"]));
                var resolver = PhysicalHostComposition.CreateMultiPageResolver(identityCriteria, options.TargetApplication);
                graph = PhysicalHostComposition.BuildRuntimeGraph(
                    environment, options, attach, elementCriteria, navigationPageCriteria,
                    ScenarioValidationOptions.SettingsRootLaunchIntentAction, resolver);
            }
            else
            {
                obj = SemanticObject.Define(AutomaticSystemUpdatesObjectIdentity, "SystemUpdateSetting", [AutomaticSystemUpdatesStateDimension]);
                capability = Capability.Define("SetEnabled", "SystemUpdateSetting", AutomaticSystemUpdatesStateDimension);
                goal = new SemanticGoalInput(AutomaticSystemUpdatesObjectIdentity, AutomaticSystemUpdatesStateDimension, scenario.DesiredState);
                var elementCriteria = new ElementBindingCriteria(
                    [obj],
                    ImmutableDictionary<string, string>.Empty.Add(
                        AutomaticSystemUpdatesObjectIdentity, AutomaticSystemUpdatesTextAnchor),
                    ImmutableDictionary<string, string>.Empty.Add(
                        AutomaticSystemUpdatesObjectIdentity, "toggle"));
                var pageCriteria = new PageAnalysisCriteria(
                    options.TargetApplication,
                    ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(
                        DeveloperOptionsPage, ["Developer options", "Developeroptions"]));
                var resolver = PhysicalHostComposition.CreateMultiPageResolver(pageCriteria, options.TargetApplication);
                graph = PhysicalHostComposition.BuildRuntimeGraph(
                    environment, options, attach, elementCriteria, pageCriteria,
                    ScenarioValidationOptions.DeveloperOptionsLaunchIntentAction, resolver);
            }

            // ── Step 3: 语义闭环 run（普通生产路径；L1 未注入）──────────────────────
            var maxIterations = scenario.Task == "Wifi" ? 12 : 20;
            var runResult = await graph.Agent.RunSemanticGoalAsync(
                goal, [obj], [capability], runId, cancellationToken, maxIterations,
                viewportExplorationEvaluator: scenario.Task == "Asu" ? ContinueIfViewportChanged : null);

            // ── Step 3.5: 诊断 —— 初始观测元素（分类证据：setup/landing vs runtime）──
            if (graph.Agent.Trace.Count > 0)
            {
                var initialObs = environment.ObservationHistory.FirstOrDefault();
                if (initialObs is not null)
                {
                    var texts = string.Join(" | ", initialObs.Elements
                        .Where(e => !string.IsNullOrWhiteSpace(e.Text))
                        .Select(e => $"{e.Text}({e.PerceptionType})"));
                    Console.WriteLine($"CORPUS-DETAIL id={scenario.Id} initialObservationSeq={initialObs.SequenceNumber} fg={initialObs.ForegroundApplication ?? "(null)"} texts=[{texts}]");
                }
            }

            // ── Step 4: 结构化证据（per-run 分类输入）────────────────────────────
            var actions = environment.ActionHistory;
            var journal = graph.Traversal.Journal;
            var setSwitches = actions.OfType<DeviceAction.SetSwitch>().Count();
            var navTaps = actions.OfType<DeviceAction.Tap>().Count();
            var scrolls = actions.OfType<DeviceAction.ScrollForward>().Count();
            var settleCount = journal.Sum(e => e.PostActionSettleCount);
            var terminal = runResult.GetType().Name.Replace("SemanticRunResult+", "", StringComparison.Ordinal);
            var reason = ReasonOf(runResult);
            var beliefPage = graph.Agent.Belief?.SemanticPage;

            Console.WriteLine($"CORPUS-DETAIL id={scenario.Id} beliefPage={beliefPage ?? "(null)"} runState={graph.Agent.State} launchApp={actions.OfType<DeviceAction.LaunchApp>().Count()} terminal={terminal}");
            Console.WriteLine($"CORPUS-DETAIL id={scenario.Id} settleByJournal=[{string.Join(",", journal.Select(e => e.PostActionSettleCount))}]");
            Console.WriteLine($"CORPUS-DETAIL id={scenario.Id} journalSeq=[{string.Join(",", journal.Select(e => e.PostActionObservation is null ? "-" : e.PostActionObservation.SequenceNumber.ToString()))}]");

            // ── Step 4.5: 诊断 —— 全观测链（ASU 身份链重建 — 仅失败/scenario 组）──
            var obsHistory = environment.ObservationHistory;
            if (scenario.Task == "Asu" && obsHistory.Count > 0)
            {
                Console.WriteLine($"CORPUS-CHAIN id={scenario.Id} journalCount={journal.Count} obsCount={obsHistory.Count}");
                for (int idx = 0; idx < obsHistory.Count; idx++)
                {
                    var obs = obsHistory[idx];
                    var page = graph.ResolveSemanticPage(obs);
                    var texts = string.Join(" | ", obs.Elements
                        .Where(e => !string.IsNullOrWhiteSpace(e.Text))
                        .Select(e => $"{e.Text}({e.PerceptionType})"));
                    var toggle = obs.Elements.FirstOrDefault(e => e.PerceptionType == "toggle");
                    var switchState = toggle?.SwitchState?.ToString() ?? "null";
                    Console.WriteLine($"CORPUS-CHAIN id={scenario.Id} obs_{idx} seq={obs.SequenceNumber} page={page ?? "(null)"} fg={obs.ForegroundApplication ?? "(null)"} switchState={switchState} n_elements={obs.Elements.Length} texts=[{texts}]");
                }
                for (int idx = 0; idx < journal.Count; idx++)
                {
                    var entry = journal[idx];
                    var actionType = entry.DispatchedAction?.GetType().Name ?? "(null)";
                    var obsSeq = entry.PostActionObservation?.SequenceNumber.ToString() ?? "-";
                    var page = entry.PostActionObservation is null ? null : graph.ResolveSemanticPage(entry.PostActionObservation);
                    Console.WriteLine($"CORPUS-CHAIN id={scenario.Id} journal_{idx} action={actionType} postObsSeq={obsSeq} page={page ?? "(null)"} retryCount={entry.RetryCount} settleCount={entry.PostActionSettleCount}");
                }
                var lastTraces = graph.Agent.Trace.TakeLast(5).ToArray();
                foreach (var t in lastTraces)
                    Console.WriteLine($"CORPUS-CHAIN id={scenario.Id} trace container={t.ContainerId ?? "(null)"} step={t.StepId ?? "(null)"} actionId={t.ActionId ?? "(null)"} reason={t.Reason ?? "(null)"}");
            }

            return new CorpusRunResult(
                scenario.Id, scenario.Task, scenario.DesiredState, terminal, reason,
                graph.Agent.State, setSwitches, settleCount, navTaps, scrolls, journal.Count,
                TimeSpan.Zero, beliefPage);
        }
        finally
        {
            visionHost?.Dispose();
        }
    }

    /// <summary>host-run WiFi 基线准备（录制坐标 tap — 已毕业证明先例；宿主 run 外，不进语义路径、
    /// 零语义坐标。注：坐标依布局可能漂移，现场两态 switch center 969,618 / 969,824 —— tap 后回读验证，
    /// 失败重试 ≤3 次）。</summary>
    private static async Task PrepareWifiBaselineAsync(
        PhysicalHostOptions options, string serial, bool wantOn, CancellationToken cancellationToken)
    {
        var current = await ReadGlobalWifiOnAsync(options, serial, cancellationToken);
        Console.WriteLine($"HOST wifiBaseline current={current} wantOn={wantOn}");
        var wantValue = wantOn ? "1" : "0";
        if (current == wantValue)
            return;
        for (var attempt = 1; attempt <= 3 && current != wantValue; attempt++)
        {
            await TapSwitchOffAsync(options, serial, cancellationToken); // 录制开关中心 tap（toggle）
            await Task.Delay(2500, cancellationToken);
            current = await ReadGlobalWifiOnAsync(options, serial, cancellationToken);
            Console.WriteLine($"HOST wifiBaseline attempt#{attempt} after={current}");
        }
        if (current != wantValue)
            throw new InvalidOperationException($"WiFi 基线准备失败：want={(wantOn ? "1" : "0")} got={current ?? "(null)"}");
    }

    /// <summary>host-run Settings 根页冷启动（同 multilevel 先例：force-stop + am start SETTINGS + settle）。</summary>
    private static async Task PrepareSettingsRootColdStartAsync(
        PhysicalHostOptions options, string serial, CancellationToken cancellationToken)
    {
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "force-stop", "com.android.settings");
        await Task.Delay(500, cancellationToken);
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "start", "-a", ScenarioValidationOptions.SettingsRootLaunchIntentAction);
        await Task.Delay(2500, cancellationToken);
    }

    /// <summary>host-run ASU 基线准备（settings put global ota_disable_automatic_update；INVERTED：0=ON，1=OFF）。
    /// 前置：Developer options 页可见需全局开关 development_settings_enabled=1（同 scroll 证明先例，系统前置，非语义状态）。</summary>
    private static async Task PrepareAsuBaselineAsync(
        PhysicalHostOptions options, string serial, bool wantOn, CancellationToken cancellationToken)
    {
        var wantValue = wantOn ? "0" : "1";
        Console.WriteLine($"HOST asuBaseline wantOn={wantOn} (ota_disable_automatic_update={wantValue})");
        // 前置：demo-mode system dialog 可能拦截 DEVELOPMENT_SETTINGS 落页（现场观测：
        // SystemUI demo mode 屏 "Enable/Show demo mode" 抢前台）→ 显式关闭 + 重启 SystemUI 清除
        // DemoMode activity 粘滞（现场验证：force-stop systemui 后 intent 正确落 DevelopmentSettings）。
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "settings", "put", "global", "sysui_demo_allowed", "0");
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "settings", "put", "global", "development_settings_enabled", "1");
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "force-stop", "com.android.systemui");
        await Task.Delay(1500, cancellationToken);
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "settings", "put", "global", OtaDisableAutomaticUpdateSettingKey, wantValue);
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "force-stop", "com.android.settings");
        await Task.Delay(500, cancellationToken);
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "start", "-a", ScenarioValidationOptions.DeveloperOptionsLaunchIntentAction);
        await Task.Delay(2500, cancellationToken);
    }

    private static void PrintCorpusSummary(List<CorpusRunResult> results, TimeSpan total)
    {
        Console.WriteLine("---- CORPUS SUMMARY ----");
        Console.WriteLine($"CORPUS-TOTAL runs={results.Count} totalDuration={total.TotalSeconds:0}s");
        var byTerminal = results.GroupBy(r => r.Terminal).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (terminal, count) in byTerminal.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"CORPUS-TERMINAL {terminal} = {count}");
        var wifi = results.Where(r => r.Task == "Wifi").ToArray();
        var asu = results.Where(r => r.Task == "Asu").ToArray();
        Console.WriteLine($"CORPUS-GROUP Wifi runs={wifi.Length} satisfied={wifi.Count(r => r.Terminal == "Satisfied")} stateEvidenceRequired={wifi.Count(r => r.Terminal == "StateEvidenceRequired")} bindingUnresolved={wifi.Count(r => r.Terminal == "BindingUnresolved")} executionFailed={wifi.Count(r => r.Terminal == "ExecutionFailed")} budgetExhausted={wifi.Count(r => r.Terminal == "BudgetExhausted")}");
        Console.WriteLine($"CORPUS-GROUP Asu runs={asu.Length} satisfied={asu.Count(r => r.Terminal == "Satisfied")} stateEvidenceRequired={asu.Count(r => r.Terminal == "StateEvidenceRequired")} bindingUnresolved={asu.Count(r => r.Terminal == "BindingUnresolved")} executionFailed={asu.Count(r => r.Terminal == "ExecutionFailed")} budgetExhausted={asu.Count(r => r.Terminal == "BudgetExhausted")}");
        Console.WriteLine($"CORPUS-SETTLE runsWithSettle={results.Count(r => r.TotalSettleCount > 0)} totalSettleObservations={results.Sum(r => r.TotalSettleCount)}");
        Console.WriteLine($"CORPUS-ACTIONS totalSetSwitch={results.Sum(r => r.SetSwitchCount)} totalNavTaps={results.Sum(r => r.NavTapCount)} totalScrolls={results.Sum(r => r.ScrollCount)} totalJournal={results.Sum(r => r.JournalEntries)}");
        Console.WriteLine($"CORPUS-L1 consultRate=0 (assistance provider NOT injected — production L0 path; no forced consultation)");
        Console.WriteLine($"CORPUS-DURATION avgPerRunMs={(int)results.Average(r => r.Duration.TotalMilliseconds)}");
    }

    private static async Task<int> RunSlice1ProofAsync(PhysicalHostOptions options, CancellationToken cancellationToken)
    {
        // ── Step 1: 设备解析（组合前置；无设备 = NotReady 路径 — F2）──────────────
        var resolution = await PhysicalHostComposition.ResolveDeviceAsync(options, cancellationToken);
        Console.WriteLine($"HOST deviceResolved={resolution.IsResolved}");
        if (!resolution.IsResolved)
        {
            Console.WriteLine("HOST startup=NotReady");
            Console.WriteLine($"HOST notReadyReason={resolution.FailureReason ?? "device not resolved"}");
            Console.WriteLine("PROOF-F2 deviceUnavailable=true zeroDispatch=true zeroTraversal=true");
            return 2;
        }

        var serial = resolution.Serial!;
        Console.WriteLine($"HOST serial={serial}");

        // ── Step 2: 真实 Provider 组合（生产唯一路径 — F1；Vision managed/external 前置）───────
        var (environment, visionHost) = await BuildEnvironmentAsync(options, serial, cancellationToken);
        try
        {
        var attach = PhysicalHostComposition.CreateAttach(options, serial);
        var graph = PhysicalHostComposition.BuildRuntimeGraph(environment, options, attach);
        Console.WriteLine("HOST composition=OK (real AdbScreenshotSource + LocalVisionPerceptionSource + AdbDispatchTarget)");

        // ── Step 3: Slice 1 证明 run — Agent 语义闭环 vs 真实 Environment ────────
        // 探测目标：空绑定（ElementBindingCriteria.Empty）→ 确定性终止于语义决策边界
        // （BindingUnresolved），不进入能力选择/授权/降低/分发 — 证明 B 终止点 + 证明 C 零能力执行。
        var probe = SemanticObject.Define(ProbeObjectIdentity, "Slice1ProbeCategory", [ProbeStateDimension]);
        var goal = new SemanticGoalInput(ProbeObjectIdentity, ProbeStateDimension, DesiredValue: true);
        var runResult = await graph.Agent.RunSemanticGoalAsync(
            goal, [probe], [], runId: "slice1-proof", cancellationToken);

        // ── Step 4: 结构化证据输出 ──────────────────────────────────────────────
        var actions = environment.ActionHistory;
        var switchOrTapDispatched = actions.Any(a => a is DeviceAction.SetSwitch or DeviceAction.Tap or DeviceAction.ScrollForward);
        var launchCount = actions.OfType<DeviceAction.LaunchApp>().Count();

        Console.WriteLine("---- SLICE1 PROOF EVIDENCE ----");
        Console.WriteLine($"HOST startup={PrintStartup(graph.Agent)}");
        Console.WriteLine($"HOST anchorApp={graph.Agent.RecoveryAnchor?.ApplicationIdentity ?? "(null)"}");
        Console.WriteLine($"HOST anchorEntry={graph.Agent.RecoveryAnchor?.ExpectedSemanticEntry ?? "(null)"}");
        Console.WriteLine($"HOST beliefPage={graph.Agent.Belief?.SemanticPage ?? "(null)"}");
        Console.WriteLine($"HOST beliefConfidence={graph.Agent.Belief?.Confidence.ToString("0.###") ?? "(null)"}");
        Console.WriteLine($"HOST beliefEvidence={graph.Agent.Belief?.Evidence ?? "(null)"}");
        Console.WriteLine($"HOST beliefSourceSequence={graph.Agent.Belief?.SourceObservationSequence?.ToString() ?? "(null)"}");
        Console.WriteLine($"HOST runState={graph.Agent.State}");
        Console.WriteLine($"HOST runTermination={runResult.GetType().Name}");
        Console.WriteLine($"HOST runReason={ReasonOf(runResult) ?? "(null)"}");
        Console.WriteLine($"HOST physicalDispatchCount={actions.Count}");
        Console.WriteLine($"HOST launchAppDispatches={launchCount}");
        Console.WriteLine($"HOST wifiCapabilityExecuted={switchOrTapDispatched}");

        // ── Step 5: 证明断言 ────────────────────────────────────────────────────
        var proofB = graph.Agent.RecoveryAnchor is not null
            && graph.Agent.Belief is { SemanticPage: not null }
            && graph.Agent.Trace.Any(t => t.RunState == RunState.Running)
            && runResult is SemanticRunResult.BindingUnresolved;
        var proofC = !switchOrTapDispatched && actions.Count == launchCount && launchCount == 1;

        Console.WriteLine($"PROOF-A compositionStarted=true");
        Console.WriteLine($"PROOF-B lifecycleComplete={proofB} (Cold→Attach→Ready→FreshObserve→InitialWorldBelief)");
        Console.WriteLine($"PROOF-C noWifiCapabilityExecuted={proofC} (dispatch history contains only Startup LaunchApp)");

        return proofB && proofC ? 0 : 1;
        }
        finally
        {
            visionHost?.Dispose();
        }
    }

    /// <summary>
    /// Slice 2 WiFi 语义闭环真实证明（WIFI_SEMANTIC_LOOP）— 任务 6.1 + F3-F6 现场可证部分 + 6.8 trace。
    /// 组合：WiFi 语义对象 + 唯一 capability(SetEnabled) + 录制现实 grounding criteria（Wi‑Fi 文本锚 +
    /// toggle 控件类型）+ 公开 Settings 启动意图（确定性落在含开关的 Internet 页）。
    /// 设备准备（非语义状态注入）：若 WiFi 已开，先用同一物理机制（tap 开关中心）翻到 OFF 基线 —
    /// 位置取自 5.1 录制校准资产（wifi-slice2-calibration/provenance.json）；Agent 路径零硬编码坐标（感知驱动）。
    /// 成功唯一条件：SATISFIED + GoalEvidence.SourceObservationSequence == fresh post-dispatch 观测序列
    /// + 该观测的 toggle 元素 SwitchState==true（perception 证据）+ 恰好 1 次 SetSwitch。
    /// </summary>
    private static async Task<int> RunSlice2ProofAsync(PhysicalHostOptions options, CancellationToken cancellationToken)
    {
        // ── Step 1: 设备解析（组合前置；无设备 = F2 NotReady 路径）──────────────
        var resolution = await PhysicalHostComposition.ResolveDeviceAsync(options, cancellationToken);
        Console.WriteLine($"HOST deviceResolved={resolution.IsResolved}");
        if (!resolution.IsResolved)
        {
            Console.WriteLine("HOST startup=NotReady");
            Console.WriteLine($"HOST notReadyReason={resolution.FailureReason ?? "device not resolved"}");
            Console.WriteLine("PROOF-F2 deviceUnavailable=true zeroDispatch=true zeroTraversal=true");
            return 2;
        }

        var serial = resolution.Serial!;
        Console.WriteLine($"HOST serial={serial}");

        // ── Step 2: 设备准备 — WiFi OFF 基线（物理 UI 机制；非语义状态注入）─────
        var wifiOn = await ReadGlobalWifiOnAsync(options, serial, cancellationToken);
        Console.WriteLine($"HOST baselineWifiOn={wifiOn}");
        if (wifiOn is null)
        {
            Console.WriteLine("HOST baseline=UNKNOWN (无法读取 wifi_on)");
            Console.WriteLine("PROOF-F6 perceptionOrDeviceUnavailable=true satisfied=false");
            return 2;
        }
        if (wifiOn == "1")
        {
            Console.WriteLine("HOST baselinePrep=preparing OFF (physical tap at recorded switch center)");
            await TapSwitchOffAsync(options, serial, cancellationToken);
            await Task.Delay(1500, cancellationToken);
            var after = await ReadGlobalWifiOnAsync(options, serial, cancellationToken);
            Console.WriteLine($"HOST baselineWifiOnAfterPrep={after}");
            if (after != "0")
            {
                Console.WriteLine("HOST baseline=OFF_FAILED (世界未到达 OFF 基线)");
                return 2;
            }
        }

        // ── Step 3: WiFi 语义装配（录制现实 grounding — 裁决 11 调用侧注入）──────
        var wifi = SemanticObject.Define(WifiObjectIdentity, "ConnectivitySetting", [WifiStateDimension]);
        var setEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", WifiStateDimension);
        var elementCriteria = new ElementBindingCriteria(
            [wifi],
            ImmutableDictionary<string, string>.Empty.Add(WifiObjectIdentity, WifiTextAnchor),
            ImmutableDictionary<string, string>.Empty.Add(WifiObjectIdentity, "toggle"));
        var pageCriteria = new PageAnalysisCriteria(
            options.TargetApplication,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(
                "Settings", ["Wi-Fi", "Internet", "Add network"]));
        var launchIntent = string.IsNullOrWhiteSpace(options.LaunchIntentAction)
            ? ScenarioValidationOptions.DefaultWifiLaunchIntentAction
            : options.LaunchIntentAction;

        // ── Step 4: 真实 Provider 组合 + Runtime 图（Slice 2 语义接线；Vision 前置）──
        var (environment, visionHost) = await BuildEnvironmentAsync(options, serial, cancellationToken);
        try
        {
        var attach = PhysicalHostComposition.CreateAttach(options, serial);
        var graph = PhysicalHostComposition.BuildRuntimeGraph(
            environment, options, attach, elementCriteria, pageCriteria, launchIntent);
        Console.WriteLine($"HOST composition=OK (wifi semantic graph: 1 object, 1 capability, launchIntent={launchIntent})");

        // ── Step 5: 语义闭环 run ──────────────────────────────────────────────
        var goal = new SemanticGoalInput(WifiObjectIdentity, WifiStateDimension, DesiredValue: true);
        var runResult = await graph.Agent.RunSemanticGoalAsync(
            goal, [wifi], [setEnabled], runId: "slice2-wifi-proof", cancellationToken);

        // ── Step 6: 结构化证据输出（6.8 trace 因果链重建）──────────────────────
        var actions = environment.ActionHistory;
        var setSwitches = actions.OfType<DeviceAction.SetSwitch>().ToList();
        var launchCount = actions.OfType<DeviceAction.LaunchApp>().Count();
        var journal = graph.Traversal.Journal;

        Console.WriteLine("---- SLICE2 PROOF EVIDENCE ----");
        Console.WriteLine($"HOST startup={PrintStartup(graph.Agent)}");
        Console.WriteLine($"HOST beliefPage={graph.Agent.Belief?.SemanticPage ?? "(null)"}");
        Console.WriteLine($"HOST runState={graph.Agent.State}");
        Console.WriteLine($"HOST runTermination={runResult.GetType().Name}");
        Console.WriteLine($"HOST runReason={ReasonOf(runResult) ?? "(null)"}");
        Console.WriteLine($"HOST launchAppDispatches={launchCount}");
        Console.WriteLine($"HOST setSwitchDispatches={setSwitches.Count}");
        Console.WriteLine($"HOST capabilitySelected={graph.Agent.Trace.Any(t => t.Reason is not null && t.Reason.Contains("semantic capability selected", StringComparison.Ordinal))}");
        Console.WriteLine($"HOST traceCapability={graph.Agent.Trace.FirstOrDefault(t => t.Reason is not null && t.Reason.Contains("semantic capability selected", StringComparison.Ordinal))?.Reason ?? "(none)"}");
        Console.WriteLine($"HOST journalEntries={journal.Count}");

        // 6.8 trace 因果链：Goal → capability → token → dispatch → fresh observation → perception → GoalEvidence
        Observation? fresh = null;
        if (journal.Count > 0)
        {
            var entry = journal[^1];
            fresh = entry.PostActionObservation;
            Console.WriteLine($"HOST dispatchAction={entry.DispatchedAction?.GetType().Name ?? "(null)"}");
            Console.WriteLine($"HOST dispatchResult={entry.Result.GetType().Name}");
            Console.WriteLine($"HOST postActionObservationSeq={fresh?.SequenceNumber.ToString() ?? "(null)"}");
            var toggle = fresh?.Elements.FirstOrDefault(e => e.PerceptionType == "toggle");
            Console.WriteLine($"HOST postActionSwitchState={(toggle?.SwitchState?.ToString() ?? "(null)")} (perception ISwitchStateReader evidence)");
            Console.WriteLine($"HOST postActionSwitchBounds={FormatBounds(toggle?.Bounds)}");
        }

        var goalSatisfied = runResult is SemanticRunResult.Satisfied;
        string? evidenceSourceSequence = goalSatisfied
            ? ((SemanticRunResult.Satisfied)runResult).Evidence.SourceObservationSequence.ToString()
            : null;
        Console.WriteLine($"HOST goalEvidenceSatisfied={goalSatisfied}");
        Console.WriteLine($"HOST goalEvidenceSourceObservationSequence={evidenceSourceSequence ?? "(null)"}");
        Console.WriteLine($"HOST goalEvidenceReason={ReasonOf(runResult) ?? "(null)"}");

        // ── Step 7: 独立物理读回（额外证据：世界确实变化 — 非成功条件，仅佐证）───
        var wifiOnAfter = await ReadGlobalWifiOnAsync(options, serial, cancellationToken);
        Console.WriteLine($"HOST postRunWifiOn={wifiOnAfter ?? "(unreadable)"}");

        // ── Step 8: 证明断言（成功唯一条件核对）────────────────────────────────
        var satisfied = goalSatisfied;
        var exactlyOneSetSwitch = setSwitches.Count == 1;
        var freshObsSeq = fresh?.SequenceNumber ?? 0;
        var freshAdvanced = freshObsSeq > 2; // post-dispatch 必须推进越过初始观测（seq=2，F4 fail-closed 反证）
        var sourcePointsAtFresh = satisfied
            && evidenceSourceSequence is not null
            && long.TryParse(evidenceSourceSequence, out var sourceSeq)
            && sourceSeq == freshObsSeq
            && freshObsSeq > 0;
        var perceptionSwitchOn = satisfied && fresh is { } freshObs
            && freshObs.Elements.Any(e => e.PerceptionType == "toggle" && e.SwitchState is true);

        var proofSlice2 = satisfied && exactlyOneSetSwitch && freshAdvanced && sourcePointsAtFresh && perceptionSwitchOn;
        var proofF6 = !satisfied
            && (runResult is SemanticRunResult.StateEvidenceRequired or SemanticRunResult.BindingUnresolved)
            && setSwitches.Count == 0;

        Console.WriteLine($"PROOF-SLICE2 satisfied={satisfied} exactlyOneSetSwitch={exactlyOneSetSwitch} freshObservationAdvanced={freshAdvanced} sourcePointsAtFresh={sourcePointsAtFresh} perceptionSwitchOn={perceptionSwitchOn}");
        Console.WriteLine($"PROOF-F6 perceptionFailureNoSemanticSuccess={proofF6} (非 SATISFIED 终止，零 SetSwitch)");
        Console.WriteLine($"PROOF-F2 deviceUnavailable={(resolution.IsResolved ? false : true)} zeroDispatch={!resolution.IsResolved} zeroTraversal={!resolution.IsResolved}");

        if (proofSlice2)
            return 0;
        if (proofF6)
            return 2;
        return 1;
        }
        finally
        {
            visionHost?.Dispose();
        }
    }

    /// <summary>
    /// Multi-level 页面遍历真实证明（PHYSICAL_SETTINGS_TO_WIFI_MULTI_LEVEL_TRAVERSAL）—
    /// 语义环最小缺失阶段：Settings 根页 → Network &amp; internet → Internet 页（WiFi 开关）。
    /// 宿主仅被允许启动 Settings 根页（android.settings.SETTINGS）；后续每一跳均由 Agent
    /// 依据 fresh 观测 + 注入的页面识别知识独立决策（零路由脚本、零坐标硬编码 — 坐标只来自感知 Bounds）。
    /// 每跳：Navigation decision → Agent 授权 Tap → Traversal 执行 → fresh 观测（seq 推进）→
    /// 页面身份变更反证（old Container !IsStillMine）→ 新 Container 仅由 fresh 观测派生 → 继续同一 Goal。
    /// 到 Internet 页后复用已毕业 Slice 2 链：SetEnabled → SetSwitch → fresh 观测 → SwitchState 证据 →
    /// GoalEvidence → Satisfied；恰好一次 SetSwitch（幂等 F6 保持）。
    /// 退出码：0 = 证明通过；1 = 断言失败/运行期错误；2 = NotReady（F2）或非 SATISFIED（F6）。
    /// </summary>
    private static async Task<int> RunMultiLevelProofAsync(PhysicalHostOptions options, CancellationToken cancellationToken)
    {
        // ── Step 1: 设备解析（组合前置；无设备 = F2 NotReady 路径）──────────────
        var resolution = await PhysicalHostComposition.ResolveDeviceAsync(options, cancellationToken);
        Console.WriteLine($"HOST deviceResolved={resolution.IsResolved}");
        if (!resolution.IsResolved)
        {
            Console.WriteLine("HOST startup=NotReady");
            Console.WriteLine($"HOST notReadyReason={resolution.FailureReason ?? "device not resolved"}");
            Console.WriteLine("PROOF-F2 deviceUnavailable=true zeroDispatch=true zeroTraversal=true");
            return 2;
        }

        var serial = resolution.Serial!;
        Console.WriteLine($"HOST serial={serial}");

        // ── Step 2: 设备准备 — WiFi OFF 基线（物理 UI 机制；非语义状态注入）─────
        // 与 Slice 2 相同：先落 Internet 页再以录制开关中心 tap 翻到 OFF；Agent 路径零硬编码坐标。
        var wifiOn = await ReadGlobalWifiOnAsync(options, serial, cancellationToken);
        Console.WriteLine($"HOST baselineWifiOn={wifiOn}");
        if (wifiOn is null)
        {
            Console.WriteLine("HOST baseline=UNKNOWN (无法读取 wifi_on)");
            Console.WriteLine("PROOF-F6 perceptionOrDeviceUnavailable=true satisfied=false");
            return 2;
        }
        if (wifiOn == "1")
        {
            // 有界重试：软件渲染模拟器上开关状态写回有 ~2s 延迟（现场实测），
            // 单次 tap + 1500ms 读取可能读到旧值；重试不超过 3 次，每次 tap 前
            // 重新落页。仍是宿主 run 外物理 UI 机制（录制开关中心 tap），不进语义路径。
            string? after = "1";
            for (var attempt = 1; attempt <= 3 && after != "0"; attempt++)
            {
                Console.WriteLine($"HOST baselinePrep=preparing OFF attempt#{attempt} (physical tap at recorded switch center)");
                await TapSwitchOffAsync(options, serial, cancellationToken);
                await Task.Delay(2500, cancellationToken);
                after = await ReadGlobalWifiOnAsync(options, serial, cancellationToken);
                Console.WriteLine($"HOST baselineWifiOnAfterPrep(attempt#{attempt})={after}");
            }
            if (after != "0")
            {
                Console.WriteLine("HOST baseline=OFF_FAILED (世界未到达 OFF 基线)");
                return 2;
            }
        }

        // ── Step 2b: 根页起点设备准备（宿主 run 外，同 Slice 2 OFF 基线先例，不进语义路径）────
        // 现场证据：Settings task 残留于 SubSettings（Slice 2 / 前次 run 遗留），`am start -a
        // android.settings.SETTINGS` 只会前台化既有 task 停在旧页（失败 run 初始 belief=NetworkAndInternet
        // 即此因）。force-stop 是进程生命周期重置（非导航：不点按任何行、不预置页面位置、不注入路线），
        // 保证语义路径唯一允许的启动命令 `am start -a android.settings.SETTINGS` 冷启动落 Settings 根页；
        // 预热等待根页渲染 settle 后，语义 run 的 LaunchApp 前台化既有根页 task（瞬时、无转场动画）。
        Console.WriteLine("HOST prep=force-stop com.android.settings (cold root start, host run 外)");
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "force-stop", "com.android.settings");
        await Task.Delay(500, cancellationToken);
        Console.WriteLine("HOST prep=am start -a android.settings.SETTINGS (root warm-up, host run 外)");
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "start", "-a", ScenarioValidationOptions.SettingsRootLaunchIntentAction);
        await Task.Delay(2500, cancellationToken); // 根页渲染 settle（冷启动首帧可能是空白/启动动画）

        // ── Step 3: 语义装配 — WiFi 对象/能力复用 Slice 2；页面词汇为现场校准锚点 ──
        var wifi = SemanticObject.Define(WifiObjectIdentity, "ConnectivitySetting", [WifiStateDimension]);
        var setEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", WifiStateDimension);
        var elementCriteria = new ElementBindingCriteria(
            [wifi],
            ImmutableDictionary<string, string>.Empty.Add(WifiObjectIdentity, WifiTextAnchor),
            ImmutableDictionary<string, string>.Empty.Add(WifiObjectIdentity, "toggle"));

        // 导航识别知识（Agent 侧）：正锚 = 可导航行文本；不含 negative 锚 —
        // negative 锚属身份消歧（resolver 侧），放入导航 criteria 会误杀合法跳转。
        var navigationPageCriteria = new PageAnalysisCriteria(
            options.TargetApplication,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add(SettingsRootPage, ["Connected devices", "Apps", "Notifications", "Battery"])
                .Add(NetworkAndInternetPage, ["Network & internet", "Internet", "SIMs", "Airplane mode", "Hotspot & tethering"])
                .Add(WifiInternetPage, ["Internet", "Wi-Fi", "Add network"]),
            PageNegativeAnchors: null,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(WifiInternetPage, ["Wi-Fi"]));

        // 页面身份知识（resolver 侧）：正锚 + negative 锚消歧共享标题文本（现场校准）。
        // 反例证据：WifiInternet 页的 "Internet" 标题与 N&I 页的 "Internet" 行共享文本 —
        // 仅靠 "Add network" 作 N&I negative 不可靠（真实视觉对 WifiInternet 页该行检测不确定，
        // 现场多帧缺失）。故 N&I negative 增加 WifiInternet 页稳定可见的消歧文本
        // （"Wi‑Fi" 行 / "Network preferences" / "Non-carrier data usage" — 均现场校准确认
        // 存在于 WifiInternet 页且不存在于 N&I 页）：WifiInternet 帧上 N&I 必被矛盾 → 唯一解析。
        var identityCriteria = new PageAnalysisCriteria(
            options.TargetApplication,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add(SettingsRootPage, ["Connected devices", "Apps", "Notifications", "Battery"])
                .Add(NetworkAndInternetPage, ["Network & internet", "Internet", "SIMs", "Airplane mode", "Hotspot & tethering"])
                .Add(WifiInternetPage, ["Internet", "Wi-Fi", "Add network"]),
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add(SettingsRootPage, ["Internet", "Wi-Fi", "Add network", "SIMs", "Airplane mode", "Hotspot & tethering"])
                .Add(NetworkAndInternetPage, ["Connected devices", "Apps", "Notifications", "Battery", "Add network",
                    "Wi-Fi", "Network preferences", "Non-carrier data usage"])
                .Add(WifiInternetPage, ["Network & internet", "Connected devices", "Apps", "Notifications", "Battery", "SIMs", "Airplane mode", "Hotspot & tethering"]),
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(WifiInternetPage, ["Wi-Fi"]));
        var resolver = PhysicalHostComposition.CreateMultiPageResolver(identityCriteria, options.TargetApplication);

        var launchIntent = string.IsNullOrWhiteSpace(options.LaunchIntentAction)
            ? ScenarioValidationOptions.SettingsRootLaunchIntentAction
            : options.LaunchIntentAction;

        // ── Step 4: 真实 Provider 组合 + Runtime 图（multi-level 语义接线；Vision 前置）─
        var (environment, visionHost) = await BuildEnvironmentAsync(options, serial, cancellationToken);
        try
        {
        var attach = PhysicalHostComposition.CreateAttach(options, serial);
        var graph = PhysicalHostComposition.BuildRuntimeGraph(
            environment, options, attach, elementCriteria, navigationPageCriteria, launchIntent, resolver);
        Console.WriteLine($"HOST composition=OK (multilevel graph: 3 pages, 1 object, 1 capability, launchIntent={launchIntent})");

        // ── Step 5: 语义闭环 run（同 Goal 跨容器延续；预算覆盖 3 跳 + 终态）──────
        var goal = new SemanticGoalInput(WifiObjectIdentity, WifiStateDimension, DesiredValue: true);
        var runResult = await graph.Agent.RunSemanticGoalAsync(
            goal, [wifi], [setEnabled], runId: "multilevel-wifi-proof", cancellationToken, maxIterations: 12);

        // ── Step 6: 结构化证据输出（逐跳决策 + 逐跳 fresh 验证 + 6.8 trace）───────
        var actions = environment.ActionHistory;
        var setSwitches = actions.OfType<DeviceAction.SetSwitch>().ToList();
        var launchCount = actions.OfType<DeviceAction.LaunchApp>().Count();
        var journal = graph.Traversal.Journal;
        var navDecisions = graph.Agent.Trace
            .Where(t => t.Reason is not null && t.Reason.Contains("navigation decision:", StringComparison.Ordinal))
            .Select(t => t.Reason!)
            .ToArray();
        // 容器生命周期序列 = 仅容器创建/进入事件（只设 ContainerId 的 trace；
        // RecordDispatchedStep 等动作分发记录也带 ContainerId（挂在当前/旧容器上），
        // 必须排除 — 否则每个容器会因其上的一次 dispatch 而重复出现，误导遍历证明。
        var containerSequence = graph.Agent.Trace
            .Where(t => t.ContainerId is not null && t.ActionId is null && t.StepId is null)
            .Select(t => t.ContainerId!)
            .ToArray();

        Console.WriteLine("---- MULTILEVEL PROOF EVIDENCE ----");
        Console.WriteLine($"HOST startup={PrintStartup(graph.Agent)}");
        Console.WriteLine($"HOST beliefPage={graph.Agent.Belief?.SemanticPage ?? "(null)"}");
        Console.WriteLine($"HOST runState={graph.Agent.State}");
        Console.WriteLine($"HOST runTermination={runResult.GetType().Name}");
        Console.WriteLine($"HOST runReason={ReasonOf(runResult) ?? "(null)"}");
        Console.WriteLine($"HOST launchAppDispatches={launchCount}");
        Console.WriteLine($"HOST setSwitchDispatches={setSwitches.Count}");
        Console.WriteLine($"HOST containerSequence=[{string.Join(" -> ", containerSequence)}]");
        Console.WriteLine($"HOST navDecisions={navDecisions.Length}");
        foreach (var decision in navDecisions)
            Console.WriteLine($"HOST navDecision={decision}");
        // 转场 settle 重观测取证轨迹（真实 UI 动画窗口的有界重观测 — Agent 决策 trace）
        var reObserves = graph.Agent.Trace
            .Where(t => t.Reason is not null && t.Reason.Contains("re-observe", StringComparison.Ordinal))
            .Select(t => t.Reason!)
            .ToArray();
        Console.WriteLine($"HOST navReObserves={reObserves.Length}");
        foreach (var attempt in reObserves)
            Console.WriteLine($"HOST navReObserve={attempt}");

        // 逐跳独立佐证：Agent 每跳「接受」的 fresh 观测（NavigationEvidence — 真实转场有动画窗口，
        // journal 首帧可能仍在滑动中；Agent 以有界重观测接受已 settle 的帧），宿主用自有 resolver
        // 独立反解页面名，必须推进（非 null、非前一页）— 与 Agent 运行期验证同源但由宿主独立重建。
        var navEvidence = graph.Agent.NavigationEvidence;
        var hopPages = new List<string?> { containerSequence.FirstOrDefault() ?? SettingsRootPage };
        var eachHopFreshVerified = navEvidence.Count >= 2;
        for (int i = 0; i < navEvidence.Count && eachHopFreshVerified; i++)
        {
            var hopFresh = navEvidence[i];
            var page = hopFresh is null ? null : graph.ResolveSemanticPage(hopFresh);
            Console.WriteLine($"HOST hop{i + 1}=from:{hopPages[i]} -> to:{page ?? "(null)"} freshSeq={hopFresh?.SequenceNumber.ToString() ?? "(null)"} result=Succeeded");
            if (page is null || string.Equals(page, hopPages[i], StringComparison.Ordinal))
                eachHopFreshVerified = false;
            else
                hopPages.Add(page);
        }

        Observation? fresh = null;
        if (journal.Count > 0)
        {
            var entry = journal[^1];
            fresh = entry.PostActionObservation;
            Console.WriteLine($"HOST dispatchAction={entry.DispatchedAction?.GetType().Name ?? "(null)"}");
            Console.WriteLine($"HOST dispatchResult={entry.Result.GetType().Name}");
            Console.WriteLine($"HOST postActionObservationSeq={fresh?.SequenceNumber.ToString() ?? "(null)"}");
            Console.WriteLine($"HOST postActionSettleCount={entry.PostActionSettleCount} (bounded fresh re-observations; 0 = no settle needed)");
            var toggle = fresh?.Elements.FirstOrDefault(e => e.PerceptionType == "toggle");
            Console.WriteLine($"HOST postActionSwitchState={(toggle?.SwitchState?.ToString() ?? "(null)")} (perception ISwitchStateReader evidence)");
            Console.WriteLine($"HOST postActionSwitchBounds={FormatBounds(toggle?.Bounds)}");
        }

        var goalSatisfied = runResult is SemanticRunResult.Satisfied;
        string? evidenceSourceSequence = goalSatisfied
            ? ((SemanticRunResult.Satisfied)runResult).Evidence.SourceObservationSequence.ToString()
            : null;
        Console.WriteLine($"HOST goalEvidenceSatisfied={goalSatisfied}");
        Console.WriteLine($"HOST goalEvidenceSourceObservationSequence={evidenceSourceSequence ?? "(null)"}");
        Console.WriteLine($"HOST goalEvidenceReason={ReasonOf(runResult) ?? "(null)"}");

        // ── Step 7: 独立物理读回（佐证 — 非成功条件）────────────────────────────
        var wifiOnAfter = await ReadGlobalWifiOnAsync(options, serial, cancellationToken);
        Console.WriteLine($"HOST postRunWifiOn={wifiOnAfter ?? "(unreadable)"}");

        // ── Step 8: 证明断言 ────────────────────────────────────────────────────
        var satisfied = goalSatisfied;
        var exactlyOneSetSwitch = setSwitches.Count == 1;
        var freshObsSeq = fresh?.SequenceNumber ?? 0;
        var freshAdvanced = freshObsSeq > 2; // post-dispatch 推进越过初始观测（seq=2）
        var sourcePointsAtFresh = satisfied
            && evidenceSourceSequence is not null
            && long.TryParse(evidenceSourceSequence, out var sourceSeq)
            && sourceSeq == freshObsSeq
            && freshObsSeq > 0;
        var perceptionSwitchOn = satisfied && fresh is { } freshObs2
            && freshObs2.Elements.Any(e => e.PerceptionType == "toggle" && e.SwitchState is true);
        var goalSurvivedTransition = satisfied && containerSequence.Length >= 3
            && string.Equals(containerSequence[0], SettingsRootPage, StringComparison.Ordinal)
            && string.Equals(containerSequence[^1], WifiInternetPage, StringComparison.Ordinal);

        var proofMultiLevel = satisfied
            && exactlyOneSetSwitch
            && navEvidence.Count >= 2
            && eachHopFreshVerified
            && goalSurvivedTransition
            && freshAdvanced
            && sourcePointsAtFresh
            && perceptionSwitchOn;
        var proofF6 = !satisfied
            && (runResult is SemanticRunResult.StateEvidenceRequired or SemanticRunResult.BindingUnresolved)
            && setSwitches.Count == 0;
        // F2 live 变体：导航 Tap 分发成功但 fresh 转场未被证明（页面未变/转场中途观测）→
        // 零语义推进、拒盲重发、ExecutionFailed 终止 — 与 M1F2（fake 页恒不变）同一 fail-closed 语义。
        var proofF2Live = !satisfied
            && runResult is SemanticRunResult.ExecutionFailed
            && (ReasonOf(runResult) ?? string.Empty).Contains("refusing blind redispatch", StringComparison.Ordinal)
            && setSwitches.Count == 0;

        Console.WriteLine($"PROOF-MULTILEVEL satisfied={satisfied} exactlyOneSetSwitch={exactlyOneSetSwitch} hops={navEvidence.Count} eachHopFreshVerified={eachHopFreshVerified} goalSurvivedTransition={goalSurvivedTransition} sourcePointsAtFresh={sourcePointsAtFresh} perceptionSwitchOn={perceptionSwitchOn}");
        Console.WriteLine($"PROOF-F6 perceptionFailureNoSemanticSuccess={proofF6} (非 SATISFIED 终止，零 SetSwitch)");
        Console.WriteLine($"PROOF-F2LIVE dispatchNoTransition={proofF2Live} (Tap 分发成功但 fresh 转场未证明 → 拒盲重发、零语义推进、零 SetSwitch)");
        Console.WriteLine($"PROOF-F2 deviceUnavailable={(resolution.IsResolved ? false : true)} zeroDispatch={!resolution.IsResolved} zeroTraversal={!resolution.IsResolved}");
        Console.WriteLine("HOST realityLevel=EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP (emulator-only 证明；不称 REAL_DEVICE_PROVEN)");

        if (proofMultiLevel)
            return 0;
        if (proofF6)
            return 2;
        return 1;
        }
        finally
        {
            visionHost?.Dispose();
        }
    }

    /// <summary>
    /// 同容器视口滚动语义闭环真实证明（SCROLL_CONTAINER_SEMANTIC_LOOP）。
    /// 组合：AutomaticSystemUpdates 语义对象 + 唯一 capability(SetEnabled) + 录制现实 grounding criteria
    /// （"Automatic system updates" 文本锚 + toggle 控件类型）+ Developer options 页启动意图
    /// （确定性落在含该开关的 Developer options 页；目标 below-fold，初始视口不可见）。
    /// 注入 ViewportExplorationEvaluator（内容推进判据，调用边界形参）：视口内容推进 → 授权一次有界滚动；
    /// 视口不变 → exhausted。滚动次数是证据涌现结果，非硬编码计数。
    /// 成功唯一条件：SATISFIED + 单容器贯穿全程（滚动不创建新容器）+ 视口滚动步数 ≥ 1 + 恰一次 SetSwitch
    /// + GoalEvidence.SourceObservationSequence == fresh post-dispatch 观测序列 + 感知 SwitchState==true。
    /// </summary>
    private static async Task<int> RunScrollProofAsync(PhysicalHostOptions options, CancellationToken cancellationToken)
    {
        // ── Step 1: 设备解析（组合前置；无设备 = F2 NotReady 路径）──────────────
        var resolution = await PhysicalHostComposition.ResolveDeviceAsync(options, cancellationToken);
        Console.WriteLine($"HOST deviceResolved={resolution.IsResolved}");
        if (!resolution.IsResolved)
        {
            Console.WriteLine("HOST startup=NotReady");
            Console.WriteLine($"HOST notReadyReason={resolution.FailureReason ?? "device not resolved"}");
            Console.WriteLine("PROOF-F2 deviceUnavailable=true zeroDispatch=true zeroTraversal=true");
            return 2;
        }

        var serial = resolution.Serial!;
        Console.WriteLine($"HOST serial={serial}");

        // ── Step 2: 设备准备（宿主 run 外物理机制，非语义状态注入，不进 ActionHistory）─────
        // 前置：Developer options 页可见需全局开关开启（系统前置，同 emulator 准备，非语义状态）。
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "settings", "put", "global", "development_settings_enabled", "1");

        // 目标开关 OFF 基线（现场校准：该开关实际写回 key = `ota_disable_automatic_update`（global，INVERTED）：
        // 0 ↔ 开关 ON（AOSP 默认），1 ↔ 开关 OFF；`automatic_system_updates` 是该 build 的无效 key）。
        // OFF→ON 证明必须从 OFF 基线出发：写 ota_disable_automatic_update=1，随后 force-stop + 冷启动重渲染 OFF（现场已验证）。
        // 宿主 run 外物理机制，同 Slice 2 OFF 基线先例，不进语义路径、不计入 ActionHistory。
        var baseline = await ReadOtaDisableAutomaticUpdateAsync(options, serial, cancellationToken);
        Console.WriteLine($"HOST baselineOtaDisableAutomaticUpdate={baseline ?? "(null)"}");
        Console.WriteLine("HOST baselinePrep=resetting OFF (settings put global ota_disable_automatic_update 1; cold relaunch re-renders OFF)");
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "settings", "put", "global", OtaDisableAutomaticUpdateSettingKey, "1");

        // 冷启动 Developer options 页（force-stop + am start，同 multi-level 根页起点先例）。
        Console.WriteLine("HOST prep=force-stop com.android.settings (cold developer options start, host run 外)");
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "force-stop", "com.android.settings");
        await Task.Delay(500, cancellationToken);
        Console.WriteLine("HOST prep=am start -a com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS (host run 外)");
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "start", "-a", ScenarioValidationOptions.DeveloperOptionsLaunchIntentAction);
        await Task.Delay(2500, cancellationToken); // 页面渲染 settle（冷启动首帧可能是空白/启动动画）

        // ── Step 3: 语义装配（录制现实 grounding — 裁决 11 调用侧注入）──────
        var automaticSystemUpdates = SemanticObject.Define(
            AutomaticSystemUpdatesObjectIdentity, "SystemUpdateSetting", [AutomaticSystemUpdatesStateDimension]);
        var setEnabled = Capability.Define("SetEnabled", "SystemUpdateSetting", AutomaticSystemUpdatesStateDimension);
        var elementCriteria = new ElementBindingCriteria(
            [automaticSystemUpdates],
            ImmutableDictionary<string, string>.Empty.Add(
                AutomaticSystemUpdatesObjectIdentity, AutomaticSystemUpdatesTextAnchor),
            ImmutableDictionary<string, string>.Empty.Add(
                AutomaticSystemUpdatesObjectIdentity, "toggle"));

        // 单页身份识别知识：正锚覆盖「展开标题 + 滚动后折叠标题」两种 OCR 形态（现场校准：
        // 初始视口标题 "Developer options"（带空格）；滚动后 app bar 折叠为 "Developeroptions"（OCR 合并）。
        // PageAnalysis 部分锚命中即 Supports → 任一形态即可唯一解析到 DeveloperOptions 页）。
        var pageCriteria = new PageAnalysisCriteria(
            options.TargetApplication,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(
                DeveloperOptionsPage, ["Developer options", "Developeroptions"]));
        var launchIntent = string.IsNullOrWhiteSpace(options.LaunchIntentAction)
            ? ScenarioValidationOptions.DeveloperOptionsLaunchIntentAction
            : options.LaunchIntentAction;

        // ── Step 4: 真实 Provider 组合 + Runtime 图（注入逐页识别器；Vision 前置）─
        var (environment, visionHost) = await BuildEnvironmentAsync(options, serial, cancellationToken);
        try
        {
        var attach = PhysicalHostComposition.CreateAttach(options, serial);
        var resolveSemanticPage = PhysicalHostComposition.CreateMultiPageResolver(pageCriteria, options.TargetApplication);
        var graph = PhysicalHostComposition.BuildRuntimeGraph(
            environment, options, attach, elementCriteria, pageCriteria, launchIntent, resolveSemanticPage);
        Console.WriteLine($"HOST composition=OK (scroll semantic graph: 1 object, 1 capability, launchIntent={launchIntent})");

        // ── Step 5: 语义闭环 run（注入 ViewportExplorationEvaluator — 内容推进判据）──────────
        var goal = new SemanticGoalInput(AutomaticSystemUpdatesObjectIdentity, AutomaticSystemUpdatesStateDimension, DesiredValue: true);
        var runResult = await graph.Agent.RunSemanticGoalAsync(
            goal, [automaticSystemUpdates], [setEnabled], runId: "scroll-container-proof", cancellationToken,
            maxIterations: 20, viewportExplorationEvaluator: ContinueIfViewportChanged);

        // ── Step 6: 结构化证据输出 ──────────────────────────────────────────
        var actions = environment.ActionHistory;
        var setSwitches = actions.OfType<DeviceAction.SetSwitch>().ToList();
        var scrolls = actions.OfType<DeviceAction.ScrollForward>().ToList();
        var launchCount = actions.OfType<DeviceAction.LaunchApp>().Count();
        var journal = graph.Traversal.Journal;

        // 容器生命周期序列（单容器贯穿全程 = 滚动不创建新容器）— 只计容器创建/进入事件
        //（RecordDispatchedStep 等动作分发记录也带 ContainerId，必须排除）。
        var containerSequence = graph.Agent.Trace
            .Where(t => t.ContainerId is not null && t.ActionId is null && t.StepId is null)
            .Select(t => t.ContainerId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Console.WriteLine("---- SCROLL CONTAINER PROOF EVIDENCE ----");
        Console.WriteLine($"HOST startup={PrintStartup(graph.Agent)}");
        Console.WriteLine($"HOST beliefPage={graph.Agent.Belief?.SemanticPage ?? "(null)"}");
        Console.WriteLine($"HOST runState={graph.Agent.State}");
        Console.WriteLine($"HOST runTermination={runResult.GetType().Name}");
        Console.WriteLine($"HOST runReason={ReasonOf(runResult) ?? "(null)"}");
        Console.WriteLine($"HOST launchAppDispatches={launchCount}");
        Console.WriteLine($"HOST scrollForwardDispatches={scrolls.Count}");
        Console.WriteLine($"HOST setSwitchDispatches={setSwitches.Count}");
        Console.WriteLine($"HOST containerCount={containerSequence.Length}");
        Console.WriteLine($"HOST journalEntries={journal.Count}");

        // 逐滚动 fresh 序列 + 同容器连续性佐证（每步 fresh 观测必须仍解析到 DeveloperOptions 页）
        var scrollJournal = journal.Where(e => e.DispatchedAction is DeviceAction.ScrollForward).ToArray();
        foreach (var entry in scrollJournal)
        {
            var freshSeq = entry.PostActionObservation?.SequenceNumber.ToString() ?? "(null)";
            var page = entry.PostActionObservation is null ? null : graph.ResolveSemanticPage(entry.PostActionObservation);
            Console.WriteLine($"HOST scrollStep result={entry.Result.GetType().Name} freshSeq={freshSeq} page={page ?? "(null)"}");
        }

        Observation? fresh = null;
        if (journal.Count > 0)
        {
            var entry = journal[^1];
            fresh = entry.PostActionObservation;
            Console.WriteLine($"HOST dispatchAction={entry.DispatchedAction?.GetType().Name ?? "(null)"}");
            Console.WriteLine($"HOST dispatchResult={entry.Result.GetType().Name}");
            Console.WriteLine($"HOST postActionObservationSeq={fresh?.SequenceNumber.ToString() ?? "(null)"}");
            var toggle = fresh is null ? null : FindTargetToggle(fresh);
            Console.WriteLine($"HOST postActionSwitchState={(toggle?.SwitchState?.ToString() ?? "(null)")} (perception ISwitchStateReader evidence, target row)");
            Console.WriteLine($"HOST postActionSwitchBounds={FormatBounds(toggle?.Bounds)}");
        }

        var goalSatisfied = runResult is SemanticRunResult.Satisfied;
        string? evidenceSourceSequence = goalSatisfied
            ? ((SemanticRunResult.Satisfied)runResult).Evidence.SourceObservationSequence.ToString()
            : null;
        Console.WriteLine($"HOST goalEvidenceSatisfied={goalSatisfied}");
        Console.WriteLine($"HOST goalEvidenceSourceObservationSequence={evidenceSourceSequence ?? "(null)"}");
        Console.WriteLine($"HOST goalEvidenceReason={ReasonOf(runResult) ?? "(null)"}");

        // ── Step 7: 独立物理读回（额外佐证：世界确实变化 — 非成功条件，仅佐证）───
        // 开关 ON 时 ota_disable_automatic_update=0（INVERTED）；证明目标 ON → 期望读回 "0"。
        var after = await ReadOtaDisableAutomaticUpdateAsync(options, serial, cancellationToken);
        Console.WriteLine($"HOST otaDisableAutomaticUpdateAfter={after ?? "(null)"}");

        // ── Step 8: 证明断言 ──────────────────────────────────────────────────
        var sameContainerContinuityProved = containerSequence.Length == 1
            && scrollJournal.Length > 0
            && scrollJournal.All(e => e.Result is TraversalStepResult.Succeeded
                && e.PostActionObservation is not null
                && string.Equals(graph.ResolveSemanticPage(e.PostActionObservation), DeveloperOptionsPage, StringComparison.Ordinal));
        var exactlyOneSetSwitch = setSwitches.Count == 1;
        var viewportStepCount = scrolls.Count;
        var freshAdvanced = fresh is not null;
        var targetToggle = fresh is null ? null : FindTargetToggle(fresh);
        var perceptionSwitchOn = targetToggle?.SwitchState is true;
        var sourcePointsAtFresh = goalSatisfied
            && fresh is not null
            && ((SemanticRunResult.Satisfied)runResult).Evidence.SourceObservationSequence == fresh.SequenceNumber;

        var proofScroll = goalSatisfied
            && sameContainerContinuityProved
            && viewportStepCount >= 1
            && exactlyOneSetSwitch
            && sourcePointsAtFresh
            && perceptionSwitchOn;
        var proofF6 = !goalSatisfied && setSwitches.Count == 0;

        Console.WriteLine($"PROOF-SCROLL satisfied={goalSatisfied} sameContainerContinuityProved={sameContainerContinuityProved} viewportStepCount={viewportStepCount} exactlyOneSetSwitch={exactlyOneSetSwitch} sourcePointsAtFresh={sourcePointsAtFresh} perceptionSwitchOn={perceptionSwitchOn}");
        Console.WriteLine($"PROOF-F6 perceptionFailureNoSemanticSuccess={proofF6} (非 SATISFIED 终止，零 SetSwitch)");
        Console.WriteLine("HOST realityLevel=EMULATOR_REALITY_SCROLL_CONTAINER_SEMANTIC_LOOP (emulator-only 证明；不称 REAL_DEVICE_PROVEN)");

        if (proofScroll)
            return 0;
        if (proofF6)
            return 2;
        return 1;
        }
        finally
        {
            visionHost?.Dispose();
        }
    }

    /// <summary>
    /// 视口探索判据（宿主注入，调用边界形参）— 目标无关的内容推进证据解释：
    /// 累积视口观测不足两步 → 授权一步；最近两次视口文本集不变 → exhausted（正面耗尽）；
    /// 内容推进 → continue。产出三值判据 + 非空 Reason（SC-P3-CAND-007 语义）。
    /// </summary>
    private static ViewportExplorationEvidence ContinueIfViewportChanged(ImmutableArray<Observation> observations)
    {
        if (observations.Length <= 1)
            return new ViewportExplorationEvidence(true, "initial viewport lacks target; one bounded step is justified");

        var previous = observations[^2].Elements.Select(e => e.Text).ToImmutableHashSet(StringComparer.Ordinal);
        var current = observations[^1].Elements.Select(e => e.Text).ToImmutableHashSet(StringComparer.Ordinal);
        var changed = !previous.SetEquals(current);
        return new ViewportExplorationEvidence(
            changed,
            changed ? "viewport content advanced; exploration not exhausted" : "viewport unchanged; exploration exhausted");
    }

    /// <summary>Multi-level 页面词汇（与现场校准证据一致；导航/身份识别知识 — 裁决 11）。</summary>
    private const string SettingsRootPage = "SettingsRoot";

    private const string NetworkAndInternetPage = "NetworkAndInternet";

    private const string WifiInternetPage = "WifiInternet";

    /// <summary>WiFi 语义对象身份（与录制校准资产一致；意图级领域知识，非执行过程）。</summary>
    private const string WifiObjectIdentity = "WifiConnectivity";

    private const string WifiStateDimension = "Enabled";

    /// <summary>Internet 页 Wi‑Fi 行文本锚（录制现实：wifi-slice2-calibration 感知证据 "Wi-Fi" 行）。</summary>
    private const string WifiTextAnchor = "Wi-Fi";

    /// <summary>同容器视口滚动证明的页面词汇（与现场校准证据一致 — 裁决 11）。</summary>
    private const string DeveloperOptionsPage = "DeveloperOptions";

    /// <summary>自动系统更新语义对象身份（意图级领域知识，非执行过程）。</summary>
    private const string AutomaticSystemUpdatesObjectIdentity = "AutomaticSystemUpdates";

    private const string AutomaticSystemUpdatesStateDimension = "Enabled";

    /// <summary>Developer options 页 "Automatic system updates" 行文本锚（录制现实感知证据）。</summary>
    private const string AutomaticSystemUpdatesTextAnchor = "Automatic system updates";

    /// <summary>
    /// 目标开关的实际写回 key（global 命名空间，INVERTED 语义）：0 ↔ 开关 ON（自动更新启用，AOSP 默认），
    /// 1 ↔ 开关 OFF。现场校准：该开关 tap 写回此 key，不写 `automatic_system_updates`。
    /// </summary>
    private const string OtaDisableAutomaticUpdateSettingKey = "ota_disable_automatic_update";

    /// <summary>
    /// 录制现实开关中心（5.1 校准资产 provenance.json + 当前设备布局实测 2026-08-17：
    /// switch bounds (901,555)-(1038,681) → 中心 969,618；早前提交值 968,824 已随布局更新）。
    /// 仅用于设备准备（翻到 OFF 基线）；Agent 语义路径零硬编码坐标。
    /// </summary>
    private static readonly (int X, int Y) RecordedSwitchCenterPx = (969, 618);

    private static async Task<string?> ReadGlobalWifiOnAsync(
        PhysicalHostOptions options, string serial, CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo(options.AdbExecutable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-s");
        process.StartInfo.ArgumentList.Add(serial);
        process.StartInfo.ArgumentList.Add("shell");
        process.StartInfo.ArgumentList.Add("settings");
        process.StartInfo.ArgumentList.Add("get");
        process.StartInfo.ArgumentList.Add("global");
        process.StartInfo.ArgumentList.Add("wifi_on");
        try
        {
            if (!process.Start())
                return null;
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return output.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 读取 "Automatic system updates" 开关的实际写回 key（`ota_disable_automatic_update`，global 命名空间，
    /// INVERTED 语义，仅佐证，非成功 authority）："0" ↔ 开关 ON（自动更新启用），"1" ↔ 开关 OFF。
    /// 现场校准：该开关 tap 写回 `ota_disable_automatic_update`（0→1 翻 OFF），不写 `automatic_system_updates`。
    /// 返回 "1"（OFF）/ "0"（ON）/ "null"（未设置）/ null（读取失败）。
    /// </summary>
    private static async Task<string?> ReadOtaDisableAutomaticUpdateAsync(
        PhysicalHostOptions options, string serial, CancellationToken cancellationToken)
        => await ReadSettingValueAsync(options, serial, cancellationToken, "global", OtaDisableAutomaticUpdateSettingKey);

    private static async Task<string?> ReadSettingValueAsync(
        PhysicalHostOptions options, string serial, CancellationToken cancellationToken, string ns, string key)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo(options.AdbExecutable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-s");
        process.StartInfo.ArgumentList.Add(serial);
        process.StartInfo.ArgumentList.Add("shell");
        process.StartInfo.ArgumentList.Add("settings");
        process.StartInfo.ArgumentList.Add("get");
        process.StartInfo.ArgumentList.Add(ns);
        process.StartInfo.ArgumentList.Add(key);
        try
        {
            if (!process.Start())
                return null;
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return output.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>执行一条 adb 命令（ArgumentList 逐 token 构建，无 shell 插值）。失败静默 — 由调用侧回读判定。</summary>
    private static async Task RunAdbSilentAsync(
        PhysicalHostOptions options, string serial, CancellationToken cancellationToken, params string[] args)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo(options.AdbExecutable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-s");
        process.StartInfo.ArgumentList.Add(serial);
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);
        try
        {
            if (process.Start())
                await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            // 设备准备失败由调用侧 wifi_on 回读判定
        }
    }

    private static async Task TapSwitchOffAsync(
        PhysicalHostOptions options, string serial, CancellationToken cancellationToken)
    {
        // 先确保落在 Internet 页（与 Agent 相同的公开机制 am start -a），再以录制开关中心 tap 翻到 OFF
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "am", "start", "-a", ScenarioValidationOptions.DefaultWifiLaunchIntentAction);
        await Task.Delay(1200, cancellationToken);
        await RunAdbSilentAsync(options, serial, cancellationToken,
            "shell", "input", "tap",
            RecordedSwitchCenterPx.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RecordedSwitchCenterPx.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string? FormatBounds(ElementBounds? bounds)
        => bounds is null ? null
            : $"({bounds.X1:0.###},{bounds.Y1:0.###})-({bounds.X2:0.###},{bounds.Y2:0.###})";

    /// <summary>
    /// 在 fresh 观测中定位目标开关（"Automatic system updates" 行 toggle）：
    /// 匹配 text anchor（menu_item）+ 同行 toggle（垂直重叠，镜像 BindingReconciler SameRow 语义）。
    /// 排除 sticky 顶部 "Use developer options" 主开关（其文本不匹配 anchor，且不在 anchor 行）。
    /// </summary>
    private static ObservedElement? FindTargetToggle(Observation observation)
    {
        var anchorRows = observation.Elements
            .Where(e => e.PerceptionType == "menu_item"
                && string.Equals(e.Text, AutomaticSystemUpdatesTextAnchor, StringComparison.Ordinal))
            .ToArray();
        if (anchorRows.Length == 0)
            return null;

        foreach (var anchor in anchorRows)
        {
            var toggle = observation.Elements.FirstOrDefault(e =>
                e.PerceptionType == "toggle" && VerticalOverlap(anchor.Bounds, e.Bounds));
            if (toggle is not null)
                return toggle;
        }

        return null;
    }

    private static bool VerticalOverlap(ElementBounds? a, ElementBounds? b)
        => a is not null && b is not null && a.Y1 < b.Y2 && b.Y1 < a.Y2;

    private static string PrintStartup(Agent agent)
        => agent.RecoveryAnchor is null ? "NotReady" : "Ready";

    private static string? ReasonOf(SemanticRunResult result)
        => result switch
        {
            SemanticRunResult.Satisfied => null,
            SemanticRunResult.StateEvidenceRequired x => x.Reason,
            SemanticRunResult.BindingUnresolved x => x.Reason,
            SemanticRunResult.SemanticContradiction x => x.Reason,
            SemanticRunResult.BudgetExhausted x => x.Reason,
            SemanticRunResult.ExecutionFailed x => x.Reason,
            _ => null,
        };
}

