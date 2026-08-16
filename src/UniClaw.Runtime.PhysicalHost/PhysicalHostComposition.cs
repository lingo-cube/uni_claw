namespace UniClaw.Runtime.PhysicalHost;

using System.Collections.Immutable;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Adapters.Perception;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
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
    /// </summary>
    public static PhysicalEnvironment BuildRealEnvironment(PhysicalHostOptions options, string serial)
    {
        return new PhysicalEnvironment(
            new AdbScreenshotSource(serial, options.AdbExecutable),
            new LocalVisionPerceptionSource(options.VisionSocketPath),
            new AdbDispatchTarget(serial, options.AdbExecutable),
            options.TargetApplication,
            options.DisplayWidth,
            options.DisplayHeight);
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
    /// Slice 1 证明场景的领域装配只含系统设置页 + 探测对象（Slice1Probe），不含任何 WiFi 对象/能力 —
    /// 证明 C（无 WiFi 能力执行）在组合层面即被结构保证。
    /// Slice 2：可选注入 WiFi 语义 criteria（elementCriteria/pageCriteria）与机制级 launchIntentAction —
    /// 均为调用侧注入的声明式领域知识（裁决 11），非场景状态注入；默认 null 保持 Slice 1 行为不变。
    /// Multi-level：可选注入 resolveSemanticPage（逐页识别器，默认常量 "Settings" 保持 Slice 1/2 行为不变）；
    /// 容器 identity 规则随解析器派生（IsStillMine = 该观测仍属于本页），跨页时自然反证页面变更。
    /// </summary>
    public static HostRuntimeGraph BuildRuntimeGraph(
        IEnvironment environment,
        PhysicalHostOptions options,
        Func<CancellationToken, Task<string?>>? attach,
        ElementBindingCriteria? elementCriteria = null,
        PageAnalysisCriteria? pageCriteria = null,
        string? launchIntentAction = null,
        Func<Observation, string?>? resolveSemanticPage = null)
    {
        // 调用侧注入的语义规则（裁决 11）：默认证明场景固定解析到系统设置页；multi-level 注入逐页识别器。
        // 静态规则，非场景状态注入。
        resolveSemanticPage ??= _ => "Settings";

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
        // Slice 1/2 的常量解析器（"Settings"）使该规则退化为恒真，行为与 identityRule: _ => true 一致。
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
            elementCriteria);

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
}

/// <summary>组合根装配出的 Runtime 图（只读快照；所有权仍在各组件）。</summary>
public sealed record HostRuntimeGraph(
    Startup Startup,
    Traversal Traversal,
    Recovery Recovery,
    Agent Agent,
    Func<Observation, string?> ResolveSemanticPage,
    string TargetApplication);
