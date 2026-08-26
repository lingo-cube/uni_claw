namespace UniClaw.Runtime.PhysicalHost;

using System.Collections.Immutable;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Adapters.Perception;
using UniClaw.Runtime.Capabilities.Brain;
using UniClaw.Runtime.Capabilities.Perception.Vision;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.World;
using Agent = UniClaw.Runtime.Agent.Agent;
using Container = UniClaw.Runtime.Container.Container;
using Recovery = UniClaw.Runtime.Recovery.Recovery;
using Startup = UniClaw.Runtime.Startup.Startup;
using Traversal = UniClaw.Runtime.Traversal.Traversal;

/// <summary>
/// Slice 1 生产组合根（REALITY_COMPOSITION_FOUNDATION）— 唯一允许「用真实 Provider 组合
/// PhysicalEnvironment」的代码位置（设计.md  Authority Boundary Audit 第 1 行）。
/// 职责边界：
///   • 生产 host / 组合入口：命令式、显式、直线组合 — 无 provider 注册表 / 发现 / 选择逻辑。
///   • Runtime 语义权威不变：Agent 保持决策 owner，Environment 保持传输 owner（本类不触碰语义）。
///   • 禁止项（实现约束）：Fake/Replay/Simulation 环境不得进入本工程（F1）；无运行时环境选择标志。
/// </summary>
public static class PhysicalHostComposition
{
    /// <summary>
    /// 设备解析 — 组合前置（serial 是构造 PhysicalEnvironment 的必需输入）。
    /// 唯一允许的「选择」是显式 CLI serial（--serial）或 adb 解析出的设备；无注册表、无轮换。
    /// </summary>
    public static async Task<AdbDeviceResolution> ResolveDeviceAsync(
        PhysicalHostOptions options,
        CancellationToken cancellationToken)
    {
        var resolver = new AdbDeviceResolver(options.AdbExecutable, options.Serial);
        return await resolver.ResolveAsync(cancellationToken);
    }

    /// <summary>
    /// 真实 Provider 组合 — 生产唯一路径。构造 Real Environment 只允许三个真实 IO Provider：
    /// AdbScreenshotSource（截图）、LocalVisionPerceptionSource（视觉/感知，Unix domain socket）、
    /// AdbDispatchTarget（物理动作分发）。Fake 环境进入生产的唯一通道不存在（F1 断言）。
    ///
    /// Vision 端点（vision-runtime-bootstrap A4）：必须显式解析 —
    ///   · managed 模式：传 VisionServiceHost.SocketPath（host 输出，绝不猜测）；
    ///   · external 模式：传显式外部端点；
    /// 无隐式回退到历史默认 /tmp/uniclaw-vision.sock（该值仅在显式 EXTERNAL 配置下合法）。
    /// </summary>
    public static PhysicalEnvironment BuildRealEnvironment(
        PhysicalHostOptions options,
        string serial,
        string? visionSocketPath = null,
        IStructuredUiHierarchySource? structuredUiSource = null,
        IVisualControlStateReaderFactory? visualControlFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var resolvedSocket = visionSocketPath ?? options.VisionSocketPath
            ?? throw new InvalidOperationException(
                "Vision 端点未解析：managed 模式必须传入 VisionServiceHost.SocketPath；" +
                "external 模式必须提供 --vision-socket <path>。");

        return new PhysicalEnvironment(
            new AdbScreenshotSource(serial, options.AdbExecutable),
            new LocalVisionPerceptionSource(resolvedSocket),
            new AdbDispatchTarget(serial, options.AdbExecutable),
            options.TargetApplication,
            options.DisplayWidth,
            options.DisplayHeight,
            structuredUiSource,
            visualControlFactory: visualControlFactory);
    }

    /// <summary>
    /// Attach 物理就绪检查（Startup.AttachAsync 注入点 — I-12）：AdbDevicePreflight 四轴门控。
    /// 返回 null = attach 成功；返回非 null = 显式失败原因（Startup 以 NotReady 终止，零动作分发 — SC-P1-002）。
    /// 这是 F2 的强制点：Attach 是 Startup §19 step 1，先于任何 LaunchApp / Traversal 执行。
    /// </summary>
    public static Func<CancellationToken, Task<string?>> CreateAttach(
        PhysicalHostOptions options,
        string serial)
    {
        var preflight = new AdbDevicePreflight(new AdbDeviceResolver(options.AdbExecutable, serial));
        return async cancellationToken =>
        {
            var result = await preflight.CheckAsync(cancellationToken);
            return result.IsReady ? null : $"device-not-ready: {result.FailureReason ?? "unknown reason"}";
        };
    }

    /// <summary>
    /// Runtime 图（domain wiring）— 组合根对 Startup/Traversal/Recovery/Container/Agent 的显式依赖装配。
    /// 接受任意 IEnvironment（生产传 Real Environment；测试可注入替身 — 组合根在注入替身下可构造，任务 2.5）。
    /// Domain criteria and semantic identity resolution are explicit caller-provided
    /// bindings. PhysicalHost does not provide scenario defaults.
    /// </summary>
    public static HostRuntimeGraph BuildRuntimeGraph(
        IEnvironment environment,
        PhysicalHostOptions options,
        Func<CancellationToken, Task<string?>>? attach,
        ElementBindingCriteria? elementCriteria = null,
        PageAnalysisCriteria? pageCriteria = null,
        string? launchIntentAction = null,
        Func<Observation, string?>? resolveSemanticPage = null,
        IAssistanceProvider? assistanceProvider = null)
    {
        // No semantic fallback is permitted at the physical composition boundary.
        resolveSemanticPage ??= _ => null;

        var startup = new Startup(
            environment,
            options.TargetApplication,
            resolveSemanticPage,
            launchIntentAction: launchIntentAction,
            restoreRecipe: null,
            entryStrategy: null,
            attach: attach);

        var traversal = new Traversal(environment);

        var recovery = new Recovery(
            environment,
            parseRestoreRecipe: _ => ImmutableArray<DeviceAction>.Empty,
            resolveRecoveryAction: (_, _) => null,
            verifyCriteria: (_, _) => true);

        // 容器 identity 规则 = 观测解析到的页面即本页（跨页观测 → IsStillMine 为 false → 容器转场反证）。
        // A caller-provided resolver determines identity; absent a binding, identity is unknown.
        var containerFactory = new Func<string, Container>(page => new Container(
            page,
            identityRule: observation => string.Equals(
                resolveSemanticPage(observation), page, StringComparison.Ordinal),
            stepExecutor: traversal.ExecuteStep));

        // 空页面锚点 + 空已知对象：探测 run 确定性终止于语义决策边界（BindingUnresolved），
        // 不进入能力选择/授权/降低/分发（证明 B 的终止点；证明 C 的零能力执行）。
        pageCriteria ??= new PageAnalysisCriteria(
            options.TargetApplication,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty);
        elementCriteria ??= ElementBindingCriteria.Empty;

        var agent = new Agent(
            startup,
            traversal,
            observeInitial: ct => environment.ObserveAsync(ct),
            resolveSemanticPage,
            containerFactory,
            recovery,
            pageCriteria,
            elementCriteria,
            assistanceProvider);

        return new HostRuntimeGraph(startup, traversal, recovery, agent, resolveSemanticPage, options.TargetApplication);
    }

    /// <summary>
    /// Multi-level 逐页识别器（宿主注入的页面身份知识 — 裁决 11）：
    /// 对一次 fresh Observation 运行四源 PageAnalysis，融合出「唯一」页面名。
    /// 候选页 = TEXT_ANCHOR / SWITCH_DISTRIBUTION Supports（正锚命中）∧ 无
    /// TEXT_ANCHOR_NEGATIVE Contradicts（negative 锚未出现）；恰好一个 → 该页，
    /// 零个或多个 → null（页面身份 UNKNOWN — 语义环 fail closed，F4）。
    /// 识别知识独立于导航候选（导航用导航 criteria；身份用本 identity criteria），
    /// 均为调用侧注入的静态声明，非运行期状态。
    /// </summary>
    public static Func<Observation, string?> CreateMultiPageResolver(
        PageAnalysisCriteria identityCriteria,
        string targetApplication)
    {
        return observation =>
        {
            if (!string.Equals(observation.ForegroundApplication, targetApplication, StringComparison.Ordinal))
                return null;
            var evidence = PageAnalysis.Analyze(observation, identityCriteria);
            var candidates = new List<string>();
            var contradicted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in evidence)
            {
                if (!item.Claim.StartsWith("page is ", StringComparison.Ordinal))
                    continue;
                var pageName = item.Claim["page is ".Length..];
                if (item.Source == "TEXT_ANCHOR_NEGATIVE" && item.Stance == SemanticEvidenceStance.Contradicts)
                {
                    contradicted.Add(pageName);
                    continue;
                }
                if ((item.Source == "TEXT_ANCHOR" || item.Source == "SWITCH_DISTRIBUTION")
                    && item.Stance == SemanticEvidenceStance.Supports
                    && !candidates.Contains(pageName, StringComparer.Ordinal))
                    candidates.Add(pageName);
            }
            var valid = candidates.Where(p => !contradicted.Contains(p)).ToArray();
            return valid.Length == 1 ? valid[0] : null;
        };
    }

    // ── dsh-runtime-agent-subagent-run-entry: run.start 生产组合 seam ──────────
    //
    // 组合根显式映射 DeviceSelector → 当前 Android 路径的 Runtime 图。设备差异全部
    // 留在组合根；Agent 只接收既有注入依赖（IEnvironment + criteria），零 ADB/Android/
    // DSH 感知。无注册表 / 发现 / 反射。未知选择器 → DeviceSelectorUnsupportedException
    // （协调器映射为 REQUEST_REJECTED，绝不静默回退到默认设备）。

    /// <summary>
    /// DeviceSelector → 当前 Android 路径 RunExecutionGraph 的组合根工厂。
    /// 支持的显式形式：<c>serial:&lt;adb-serial&gt;</c>（其它形式 → 不支持 → REQUEST_REJECTED）。
    /// 复用既有生产组件：AdbScreenshotSource / LocalVisionPerceptionSource /
    /// AdbDispatchTarget / BuildRealEnvironment / CreateAttach / BuildRuntimeGraph —
    /// 不重实现任何设备执行栈。
    /// </summary>
    public static RunGraphFactory CreateAndroidRunGraphFactory(
        PhysicalHostOptions options,
        IAssistanceProvider? assistanceProvider = null,
        string? visionSocketPath = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return selector =>
        {
            if (!string.Equals(selector.Kind, DeviceSelector.SerialKind, StringComparison.Ordinal))
            {
                throw new DeviceSelectorUnsupportedException(
                    selector.Key,
                    "first slice supports only 'serial:<adb-serial>' (current Android path)");
            }

            var environment = BuildRealEnvironment(options, selector.Value, visionSocketPath);
            var attach = CreateAttach(options, selector.Value);
            var graph = BuildRuntimeGraph(environment, options, attach, assistanceProvider: assistanceProvider);
            return new RunExecutionGraph(graph.Agent, environment);
        };
    }

    /// <summary>
    /// 生产 DriverHost host 组合：只读 control surface + RunExecutionCoordinator +
    /// 当前 Android 设备工厂 + 共享 Assistance pending 注册表（wire provider 与
    /// wire surface 操作同一注册表；provider 注入 Agent），装配为一个 loopback
    /// JSON-RPC listener（caller 负责 Start()/Dispose()）。DriverHost 拥有自己的
    /// 进程生命周期 — 本 seam 只返回装配好的 server，不负责进程监督。
    /// consultTimeout / pendingCapacity 是 COMPOSITION_POLICY（非契约语义）。
    ///
    /// Deterministic testability seam (uniclaw-driverhost-production-server-mode
    /// graduation repair): an explicit RunGraphFactory may be injected to prove
    /// the production composition path with a deterministic/scripted environment.
    /// null (default) preserves the current production Android factory — zero
    /// behavior change for existing callers. The injected factory changes
    /// composition only; it does NOT change RuntimeAgent semantics, DriverHost
    /// protocol, Surface A/B, or wire DTOs. RuntimeAgent remains unaware of
    /// whether the environment is real or scripted (IEnvironment contract only).
    /// </summary>
    public static UniClawDriverHostServer BuildDriverHostServer(
        PhysicalHostOptions options,
        DriverHostServerOptions? serverOptions = null,
        TimeSpan? consultTimeout = null,
        int? pendingCapacity = null,
        string? visionSocketPath = null,
        StrategyContractCompiler? strategyCompiler = null,
        RunGraphFactory? runGraphFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var observability = new DriverHostObservability();

        // 共享 Assistance pending 注册表：ConsultAsync 注册/等待 与 wire
        // assistance.pending/assistance.resolve 操作同一实例；provider 注入 Agent。
        var registry = new AssistancePendingRegistry(pendingCapacity);
        var wireProvider = new AssistanceWireProvider(registry, consultTimeout);

        // Production default: the current Android RunGraphFactory. When an
        // explicit factory is injected (testability seam), use it instead — the
        // coordinator and server wiring are identical; only the physical-world
        // binding differs. RuntimeAgent receives only IEnvironment either way.
        var factory = runGraphFactory
            ?? CreateAndroidRunGraphFactory(options, wireProvider, visionSocketPath);
        var execution = new RunExecutionCoordinator(
            observability,
            factory,
            strategyCompiler: strategyCompiler);
        return new UniClawDriverHostServer(
            new UniClawControlSurface(observability),
            serverOptions,
            execution: execution,
            assistance: registry,
            strategyExecution: execution);
    }
}

/// <summary>组合根装配出的 Runtime 图（只读快照；所有权仍在各组件）。</summary>
public sealed record HostRuntimeGraph(
    Startup Startup,
    Traversal Traversal,
    Recovery Recovery,
    Agent Agent,
    Func<Observation, string?> ResolveSemanticPage,
    string TargetApplication);
