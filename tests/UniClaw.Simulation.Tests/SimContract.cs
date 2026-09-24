using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using UniClaw.Agent;
using UniClaw.Agent.Evaluation;
using UniClaw.Kernel.Outcome;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Simulation.Tests;

// ============================================================================
// RFS-001 — Simulation Host 共享契约（Leader 定义；chunk 间 seam）
// Minimal Scenario Bundle v0（roadmap §7.2 / H7）：只锁字段语义类别，schema
// 形状随 Phase 3 治理演进。所有类型 test-side，永不进入产品程序集。
// internal 可见性（D23 后 API drift 适配）：多个成员暴露 Kernel internal
// runtime seam 类型（ActivationResult 等），public 会造成可访问性不一致。
// ============================================================================

/// <summary>golden-run 资产根（内容寻址输入源）。</summary>
internal static class GoldenPaths
{
    public const string BundleRoot = "platforms/perception/evaluation/assets/captures/golden-run-v1";

    public static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根（AGENTS.md + slnx 标记）");
    }
}

/// <summary>内容寻址资产访问（fail closed：未知 id / 缺文件 / hash 不符 → 异常）。</summary>
internal static class BundleAssetFiles
{
    public static string HashOf(string relativePath)
    {
        var path = Path.Combine(GoldenPaths.RepoRoot(), GoldenPaths.BundleRoot, relativePath);
        if (!File.Exists(path))
            throw new ScenarioBundleException($"missing bundle asset file: {relativePath}");
        return HashBytes(relativePath, File.ReadAllBytes(path));
    }

    /// <summary>
    /// 统一哈希（CRLF→LF 归一化仅限文本资产）：manifest 哈希锚定 LF 版本；
    /// Windows core.autocrlf 检出会把文本文件换成 CRLF。二进制保持原字节。
    /// HashOf 与 ReadArtifact 共用——同一文件同一哈希，跨平台一致。
    /// </summary>
    internal static string HashBytes(string relativePath, byte[] bytes)
    {
        if (relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            var text = System.Text.Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n");
            bytes = System.Text.Encoding.UTF8.GetBytes(text);
        }
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

/// <summary>每个已验证 bundle/host 独占的内容寻址资产注册表。</summary>
internal sealed class BundleAssetRegistry
{
    private readonly IReadOnlyDictionary<string, BundleAssetEntry> _index;

    public BundleAssetRegistry(IReadOnlyList<BundleAssetEntry> assets)
    {
        var index = new Dictionary<string, BundleAssetEntry>(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            if (!index.TryAdd(asset.AssetId, asset))
                throw new ScenarioBundleException($"duplicate asset id: {asset.AssetId}");
        }
        _index = index;
    }

    public byte[] ReadArtifact(string assetId)
    {
        if (!_index.TryGetValue(assetId, out var entry))
            throw new ScenarioBundleException($"unknown bundle asset: {assetId}");
        var path = Path.Combine(GoldenPaths.RepoRoot(), GoldenPaths.BundleRoot, entry.RelativePath);
        if (!File.Exists(path))
            throw new ScenarioBundleException($"missing bundle asset: {assetId} -> {entry.RelativePath}");
        var bytes = File.ReadAllBytes(path);
        var hash = BundleAssetFiles.HashBytes(entry.RelativePath, bytes);
        if (hash != entry.Sha256)
            throw new ScenarioBundleException($"integrity mismatch: {assetId} expected {entry.Sha256} got {hash}");
        return bytes;
    }
}

/// <summary>bundle 校验失败（fail closed；不得静默降级）。</summary>
internal sealed class ScenarioBundleException(string message) : Exception(message);

/// <summary>内容寻址资产条目（path 不是 identity；hash 才是）。</summary>
internal sealed record BundleAssetEntry(
    string AssetId,
    string RelativePath,
    string Sha256,
    string Kind);

/// <summary>精确 Product Runtime artifact identity（同一 artifact 证据）。</summary>
internal sealed record RuntimeArtifactIdentity(
    string KernelAssembly,
    string KernelAssemblySha256,
    string AgentAssembly,
    string AgentAssemblySha256,
    string TargetFramework)
{
    public static RuntimeArtifactIdentity CaptureCurrent()
    {
        string HashOfAssembly(Assembly assembly) =>
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant();
        var kernel = typeof(UniClaw.Kernel.UniKernel).Assembly;
        var agent = typeof(UniAgent).Assembly;
        return new RuntimeArtifactIdentity(
            kernel.GetName().Name!, HashOfAssembly(kernel),
            agent.GetName().Name!, HashOfAssembly(agent),
            kernel.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
                ?? throw new ScenarioBundleException("kernel target framework metadata missing"));
    }
}

/// <summary>
/// 目标 UI system/app/build identity（Core Metadata 最小面）。
/// AppBuild：golden manifest / device-profile 均未记录 app build 版本——
/// 显式 sentinel "not-recorded"（不伪造；来源资产无此字段）。
/// </summary>
internal sealed record TargetUiSystemIdentity(
    string Platform,
    string OsVersion,
    string AppPackage,
    string AppBuild,
    string DeviceProfileId,
    int DisplayWidth,
    int DisplayHeight);

/// <summary>producer / schema / config identities。</summary>
internal sealed record ProducerSchemaConfigIdentity(
    string PerceptionProducer,
    string StrategyIdentity,
    string StrategyVersion,
    string EffectDriverIdentity,
    int ManifestSchemaVersion);

// ============================================================================
// SIM-004 — 相位感知脚本模型（收敛 RUN-005 Slice C 的 dual 形态；机制
// 沿用 Slice C，词表收敛）。「第几轮、期待什么相位、什么行为」是
// test-side 语义；「返回什么」的语义类型全部直接来自 Product（ABS-003
// 裁决：删除 AgentScriptKind/ScriptActionStep/AgentScriptStep 与
// ScriptDecisionKind/ScriptPredicateSpec/ScriptGuardSpec/ScriptPolicySpec
// 两套镜像）。test-side only。
// ============================================================================

/// <summary>
/// scripted double 的行为分类（SIM-004 Step 7 裁决）——double behavior
/// taxonomy，<b>非</b> Product semantic taxonomy：NoResponse 是 seam 允许的
/// null 应答（AgentDecision union 之外的第三种 seam 结果），不是决策变体。
/// </summary>
internal enum ScriptedDoubleBehavior
{
    /// <summary>按 turn 的 Product 载荷构造并返回 AgentDecision。</summary>
    Respond,

    /// <summary>显式 no-response（seam 返回 null）。</summary>
    NoResponse,
}

/// <summary>
/// 一个 scripted consultation turn（SIM-004）。ExpectedPhase = Product
/// <see cref="AgentDecisionPhase"/>（失配 = 纪律违规，fail closed）；Respond
/// 载荷全部为 Product 类型：Act = <see cref="AgentActionStep"/> 列表；
/// NoAction = Justification + 可选 <see cref="CompletionEvidence"/>；Defer =
/// <see cref="ObserveSpec"/>；Policy = <see cref="PolicyProposal"/> 原样（可
/// 携带 <see cref="RogueScriptPredicate"/> 经正式 seam 入场）。Respond 下
/// Act/Defer/Policy 至多其一非空（全空 = NoAction）；NoResponse 不得携带
/// 载荷。构造纪律由 ScriptedUniAgent ctor 执法（fail closed）。
/// DecisionId 不在脚本内：由 double 以 ctx.DecisionId 回带（D2 防串话）。
/// </summary>
internal sealed record ScriptedTurn(
    AgentDecisionPhase ExpectedPhase,
    ScriptedDoubleBehavior Behavior,
    IReadOnlyList<AgentActionStep>? Act = null,
    string? Justification = null,
    CompletionEvidence? Completion = null,
    ObserveSpec? Defer = null,
    PolicyProposal? Policy = null)
{
    /// <summary>authoring sugar（构造助手，非 taxonomy）。</summary>
    public static ScriptedTurn ActAt(
        AgentDecisionPhase phase, IReadOnlyList<AgentActionStep> steps, string? justification = null) =>
        new(phase, ScriptedDoubleBehavior.Respond, Act: steps, Justification: justification);

    /// <summary>NoAction turn（可选 Product CompletionEvidence 载荷）。</summary>
    public static ScriptedTurn NoActionAt(
        AgentDecisionPhase phase, string justification, CompletionEvidence? completion = null) =>
        new(phase, ScriptedDoubleBehavior.Respond,
            Justification: justification, Completion: completion);

    /// <summary>显式 no-response turn（double behavior，非决策变体）。</summary>
    public static ScriptedTurn NoResponseAt(AgentDecisionPhase phase) =>
        new(phase, ScriptedDoubleBehavior.NoResponse);

    /// <summary>Defer turn（Product ObserveSpec 全表达：Subject + MaxRounds）。</summary>
    public static ScriptedTurn DeferAt(AgentDecisionPhase phase, ObserveSpec spec) =>
        new(phase, ScriptedDoubleBehavior.Respond, Defer: spec);

    /// <summary>Policy turn（Product PolicyProposal 原样，零映射）。</summary>
    public static ScriptedTurn PolicyAt(AgentDecisionPhase phase, PolicyProposal proposal) =>
        new(phase, ScriptedDoubleBehavior.Respond, Policy: proposal);
}

/// <summary>相位感知脚本（有序 turn 序列；耗尽后再咨询 = 纪律违规）。</summary>
internal sealed record PhaseAwareAgentScript(IReadOnlyList<ScriptedTurn> Turns);

/// <summary>
/// P11 malformed-input 注入位：closed vocabulary 之外的 rogue 派生谓词
/// （test-side 派生记录）。经 <see cref="ScriptedTurn"/> 的 Policy 载荷进入
/// 正式 seam，由 Product V6a 入口拒绝（fail closed）——不是 double 旁路。
/// </summary>
internal sealed record RogueScriptPredicate : PolicyPredicate;

/// <summary>
/// reviewed state claim（authored）：Subject/Value 之外可选 Scope——null =
/// 既有约定 scope:{subject}（同 subject 跨帧值变化 → 显式 Conflict，真实
/// 观察流矛盾语义）；非 null = authoring 声明的「同流再观察呈现」（CLE-001
/// Revise：同 producer 异 scope → 值替换 + 痕迹链）。RUN-005 Slice C：policy
/// 场景的 claim 演化（如 temp 24→…→20）经后者表达。
/// </summary>
internal sealed record ReviewedStateClaim(string Subject, string Value, string? Scope = null);

/// <summary>场景期望（semantic assertions；runner 核对并写入 report）。</summary>
internal sealed record ScenarioExpectation(
    string ExpectedStatus,
    string? ExpectedClassification,
    int ExpectedEffects,
    int ExpectedAgentConsultations,
    int ExpectedUnconsumedStimuli,
    string? ExpectedGoalSatisfaction);

/// <summary>PrimaryGoal authoring spec（runner 构造真实 UniClaw.Agent.PrimaryGoal）。</summary>
internal sealed record GoalSpec(
    string GoalId,
    string Statement,
    string? RequiredClassification,
    IReadOnlyList<string> RequiredObligationIds);

/// <summary>Minimal Scenario Bundle v0。</summary>
internal sealed record MinimalScenarioBundle
{
    public required string BundleId { get; init; }
    public required string BundleVersion { get; init; }
    public required string ScenarioId { get; init; }
    public required string ScenarioVersion { get; init; }
    public required RuntimeArtifactIdentity RuntimeArtifact { get; init; }
    public required TargetUiSystemIdentity TargetUiSystem { get; init; }
    public required IReadOnlyList<BundleAssetEntry> Assets { get; init; }
    public required IReadOnlyList<ScenarioStimulus> Stimuli { get; init; }
    public required ProducerSchemaConfigIdentity ProducerIdentities { get; init; }
    /// <summary>
    /// SIM-004：相位感知脚本（唯一 script 形态——legacy AgentScriptStep
    /// 镜像已删除）。host 以本脚本构造默认 ScriptedUniAgent realization
    /// （组合根自选 concrete 合法）；Seams.Agent 可注入任意 Product-seam
    /// realization（ABS-001）。
    /// </summary>
    public required PhaseAwareAgentScript PhaseScript { get; init; }
    public required ScenarioExpectation Expected { get; init; }
    public required ExecutionContract Contract { get; init; }
    public required GoalSpec Goal { get; init; }

    /// <summary>
    /// integrity/digest（canonical rendering 的 SHA-256；自引用排除本字段）。
    /// 由 authoring 侧计算并钉扎；Verify() 重算比对。
    /// </summary>
    public required string BundleDigest { get; init; }

    /// <summary>
    /// fail-closed 校验：资产存在 + hash 匹配 + runtime artifact 匹配当前加载
    /// 程序集 + stimulus id 唯一 + digest 一致。通过后返回 bundle 实例专属资产注册表。
    /// </summary>
    public BundleAssetRegistry Verify()
    {
        if (Assets.Count == 0)
            throw new ScenarioBundleException("empty assets");
        if (Stimuli.Count == 0)
            throw new ScenarioBundleException("empty stimuli");
        if (Stimuli.GroupBy(s => s.StimulusId).Any(g => g.Count() > 1))
            throw new ScenarioBundleException("duplicate stimulus id");
        foreach (var asset in Assets)
        {
            var actual = BundleAssetFiles.HashOf(asset.RelativePath);
            if (actual != asset.Sha256)
                throw new ScenarioBundleException($"asset integrity mismatch: {asset.AssetId}");
        }
        var current = RuntimeArtifactIdentity.CaptureCurrent();
        if (current != RuntimeArtifact)
            throw new ScenarioBundleException("runtime artifact mismatch: bundle pins a different Product Runtime build");
        var recomputed = ScenarioBundleDigest.Compute(this with { BundleDigest = "" });
        if (recomputed != BundleDigest)
            throw new ScenarioBundleException($"bundle digest mismatch: expected {BundleDigest} got {recomputed}");
        return new BundleAssetRegistry(Assets);
    }
}

/// <summary>
/// bundle canonical digest（D19 完整面）：canonical 覆盖 bundle 的每一个
/// 语义字段（ids/versions、runtime artifact 全字段、target UI 全字段、
/// producer 全字段、全部 stimuli 全字段、contract 全字段、goal spec、
/// 全部 ScenarioExpectation 字段、AgentScript 全步骤），length 帧 +
/// invariant culture。任何代表性 with-篡改都必须改变本 digest
/// （BundleIntegrityTests 系统化锁定）。
/// </summary>
internal static class ScenarioBundleDigest
{
    public static string Compute(MinimalScenarioBundle bundle)
    {
        static string F(string s) => s.Length.ToString(CultureInfo.InvariantCulture) + ":" + s;
        static string D(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string Set(IReadOnlySet<string>? values) =>
            values is null ? "-" : string.Join(",", values.OrderBy(v => v, StringComparer.Ordinal).Select(F));
        static string List(IReadOnlyList<string>? values) =>
            values is null ? "-" : string.Join(",", values.Select(F));

        var parts = new List<string>
        {
            F(bundle.BundleId), F(bundle.BundleVersion),
            F(bundle.ScenarioId), F(bundle.ScenarioVersion),

            // runtime artifact（两程序集名 + hash + tfm）
            F(bundle.RuntimeArtifact.KernelAssembly), F(bundle.RuntimeArtifact.KernelAssemblySha256),
            F(bundle.RuntimeArtifact.AgentAssembly), F(bundle.RuntimeArtifact.AgentAssemblySha256),
            F(bundle.RuntimeArtifact.TargetFramework),

            // target UI system identity（全部 7 字段）
            F(bundle.TargetUiSystem.Platform), F(bundle.TargetUiSystem.OsVersion),
            F(bundle.TargetUiSystem.AppPackage), F(bundle.TargetUiSystem.AppBuild),
            F(bundle.TargetUiSystem.DeviceProfileId),
            bundle.TargetUiSystem.DisplayWidth.ToString(CultureInfo.InvariantCulture),
            bundle.TargetUiSystem.DisplayHeight.ToString(CultureInfo.InvariantCulture),

            // producer / schema / config identity（全部 5 字段）
            F(bundle.ProducerIdentities.PerceptionProducer), F(bundle.ProducerIdentities.StrategyIdentity),
            F(bundle.ProducerIdentities.StrategyVersion), F(bundle.ProducerIdentities.EffectDriverIdentity),
            bundle.ProducerIdentities.ManifestSchemaVersion.ToString(CultureInfo.InvariantCulture),

            // 资产（id=path-kind=hash，id 排序）
            string.Join(",", bundle.Assets.OrderBy(a => a.AssetId, StringComparer.Ordinal)
                .Select(a => F(a.AssetId + "=" + a.RelativePath + ":" + a.Kind + ":" + a.Sha256))),
        };

        // stimuli（全字段、保序）
        foreach (var stimulus in bundle.Stimuli)
        {
            var head = string.Join("|", F(stimulus.GetType().Name), F(stimulus.StimulusId),
                F(stimulus.VirtualTime.ToString("O", CultureInfo.InvariantCulture)));
            string body;
            switch (stimulus)
            {
                case ScenarioStimulus.ObservationFrame frame:
                {
                    var elements = string.Join(",", frame.ReviewedElements.Select(e =>
                        F(e.Role + ":" + (e.State ?? "")
                            + ":" + D(e.X1) + "," + D(e.Y1) + "," + D(e.X2) + "," + D(e.Y2))));
                    var claims = string.Join(",", frame.ReviewedStateClaims
                        .Select(c => F(c.Subject + "=" + c.Value
                            + (c.Scope is null ? "" : "@" + c.Scope))));
                    body = F(frame.PerceptionArtifactId) + "|" + F(frame.Context.ToString())
                        + "|" + elements + "|" + claims;
                    break;
                }
                case ScenarioStimulus.CancelRequest cancel:
                    body = F("reason:" + cancel.Reason);
                    break;
                default:
                    throw new ScenarioBundleException(
                        "unknown stimulus kind in digest: " + stimulus.GetType().Name);
            }
            parts.Add("stimulus=" + head + "|" + body);
        }

        // contract（version、objective、sorted scope/allowed/forbidden、proof
        // criteria、每条 obligation tuple id+kind+subject+required+mandatory）
        parts.Add("contract=" + string.Join("|",
            F(bundle.Contract.Version), F(bundle.Contract.Objective),
            Set(bundle.Contract.Scope), Set(bundle.Contract.AllowedEffects),
            Set(bundle.Contract.ForbiddenEffects), List(bundle.Contract.ProofCriteria),
            string.Join(",", (bundle.Contract.Obligations ?? Array.Empty<RunObligation>())
                .Select(o => string.Join("~",
                    F(o.ObligationId), F(o.Kind.ToString()), F(o.Subject), F(o.RequiredValue),
                    F(o.Mandatory ? "mandatory" : "optional"),
                    F(o.EntityScope?.Role ?? ""),
                    F(o.EntityScope?.SemanticDescriptor ?? ""),
                    F(o.EntityScope?.OwningContainerId ?? ""))))));

        // goal spec（id、statement、classification、obligation ids）
        parts.Add("goal=" + string.Join("|",
            F(bundle.Goal.GoalId), F(bundle.Goal.Statement),
            F(bundle.Goal.RequiredClassification ?? ""), List(bundle.Goal.RequiredObligationIds)));

        // ScenarioExpectation（全部 6 字段）
        var expected = bundle.Expected;
        parts.Add("expected=" + string.Join("|",
            F(expected.ExpectedStatus), F(expected.ExpectedClassification ?? ""),
            expected.ExpectedEffects.ToString(CultureInfo.InvariantCulture),
            expected.ExpectedAgentConsultations.ToString(CultureInfo.InvariantCulture),
            expected.ExpectedUnconsumedStimuli.ToString(CultureInfo.InvariantCulture),
            F(expected.ExpectedGoalSatisfaction ?? "")));

        // SIM-004：相位感知脚本（唯一 script 形态）。载荷为 Product 类型：
        // closed-union 成员按成员值渲染，rogue 节点按类型名确定性渲染
        // （P11 注入位——digest 对任何 turn 篡改敏感，BundleIntegrityTests 锁定）。
        static string Preds(IReadOnlyList<PolicyPredicate> predicates) => string.Join(",", predicates.Select(p => p switch
        {
            PolicyPredicate.ClaimEquals equals => F("ClaimEquals:" + equals.Subject + ":" + equals.Value),
            PolicyPredicate.ClaimInSet inSet => F("ClaimInSet:" + inSet.Subject + ":"
                + string.Join("/", inSet.Values)),
            _ => F("rogue:" + p.GetType().Name),
        }));
        static string GuardsOf(IReadOnlyList<PolicyGuard> guards) => string.Join(",", guards.Select(g => g switch
        {
            PolicyGuard.ObservationUnchanged unchanged => F("ObservationUnchanged:" + unchanged.Subject
                + ":" + unchanged.AfterRounds.ToString(CultureInfo.InvariantCulture)),
            _ => F("rogue:" + g.GetType().Name),
        }));
        static string PolicyOf(PolicyProposal p) => string.Join("|",
            F(p.PolicyId), Preds(p.Match),
            F(p.ActionTemplate.TargetRole + ":" + (p.ActionTemplate.TargetDescriptor ?? "")
                + ":" + p.ActionTemplate.EffectClass + ":" + (p.ActionTemplate.DesiredState ?? "")),
            Preds(p.Termination), GuardsOf(p.Guards),
            p.MaxApplications.ToString(CultureInfo.InvariantCulture));
        parts.Add("phasescript=" + string.Join("|", bundle.PhaseScript.Turns.Select(t => string.Join("|",
            F(t.ExpectedPhase.ToString()),
            F(t.Behavior.ToString()),
            t.Act is null ? "-" : string.Join(",", t.Act.Select(step =>
                F(step.TargetRole + ":" + (step.TargetDescriptor ?? "") + ":" + step.EffectClass
                    + ":" + (step.DesiredState ?? "")))),
            F(t.Justification ?? ""),
            t.Completion is null
                ? "-"
                : F(t.Completion.Basis + ":" + string.Join("/", t.Completion.Checklist)),
            t.Defer is null
                ? "-"
                : F((t.Defer.Subject ?? "") + ":" + t.Defer.MaxRounds.ToString(CultureInfo.InvariantCulture)),
            t.Policy is null ? "-" : PolicyOf(t.Policy)))));

        var canonical = string.Join("\n", parts);
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static MinimalScenarioBundle Sealed(MinimalScenarioBundle bundle) =>
        bundle with { BundleDigest = Compute(bundle with { BundleDigest = "" }) };
}

/// <summary>runner 选项（S3 重复激活 / Trace 三臂 / D21 phased 场景）。</summary>
internal enum TraceArm
{
    Enabled,
    Disabled,
    FailingRecorder,

    /// <summary>TRW-001：真实异步 writer 臂（AsyncFileTraceWriter + 文件持久化）。</summary>
    AsyncFile,
}

internal sealed record RunOptions
{
    public TraceArm TraceArm { get; init; } = TraceArm.Enabled;

    /// <summary>
    /// TRW-001：AsyncFile 臂的 writer 注入点（容量 / 批量 / 延迟 / 崩溃 / 目录）；
    /// null → Host 默认独占临时目录。
    /// </summary>
    public UniClaw.Kernel.Trace.AsyncTraceWriterOptions? AsyncTrace { get; init; }

    /// <summary>S3：Drive 前后各做一次重复 admit + activate（断言单一 Primary Run）。</summary>
    public bool DuplicateActivation { get; init; }

    /// <summary>
    /// D21 phased 场景：runner 只做一次 Drive（预期返回 Waiting 等），
    /// AcceptancePassed=false 且 Reason 带 PhasedPending 标记；测试经
    /// Host.DriveOnce/Host.SubmitStimulus 完成各 phase 后调用
    /// ScenarioRunner.FinalizePhased(bundle, host) 重算 report 与验收。
    /// </summary>
    public bool Phased { get; init; }

    /// <summary>
    /// SIM-001：外部缝注入旋钮（null = 全部走工厂默认，向后兼容）。
    /// 与 Phased/DuplicateActivation 等行为开关分属不同关注点。
    /// </summary>
    public SeamOverrides? Seams { get; init; }
}

/// <summary>
/// SIM-001：仿真缝注入（全部可选；null = 工厂默认值）。
/// 每个参数对应一个可替换的外部缝——与 Product Host 同缝可互换
/// 的仿真面旋钮。确定性纪律由既有 digest 可复现测试间接执法（D4）。
/// </summary>
internal sealed record SeamOverrides
{
    /// <summary>容器 association 策略（默认 SeedingAssociationStrategy）。</summary>
    public UniClaw.Kernel.World.UiRealization.IAssociationStrategy? Association { get; init; }

    /// <summary>观察推导策略（默认 ReplayFrameObservationStrategy）。</summary>
    public UniClaw.Kernel.World.UiRealization.IUiObservationStrategy? Observation { get; init; }

    /// <summary>新鲜度评估器（默认 SatisfyingFreshness）。</summary>
    public UniClaw.Kernel.Assurance.IFreshnessEvaluator? Freshness { get; init; }

    /// <summary>效果驱动（默认 DeterministicEffectDriver）。</summary>
    public UniClaw.Kernel.Effects.IEffectDriver? Driver { get; init; }

    /// <summary>连续性策略（默认 RoleContinuityStrategy）。</summary>
    public UniClaw.Kernel.World.UiRealization.IContinuityStrategy? Continuity { get; init; }

    /// <summary>
    /// SIM-004（ABS-001）：agent 缝注入旋钮 = Product 冻结 consultation seam
    /// （RUN-004 D6 / AGT-001 §1，Func 形态）。任意 realization（测试 double、
    /// 未来 DSH conformance）经同一缝进入 Kernel；观测一律经 host 的
    /// ConsultationJournal（seam 消费侧），不因 probe 要求 concrete。
    /// null = bundle PhaseScript 构造的默认 ScriptedUniAgent。
    /// </summary>
    public Func<AgentDecisionContext, AgentDecision?>? Agent { get; init; }
}

/// <summary>结构化计数（latency/结构计数 graduation evidence；N/A 显式非 0）。</summary>
internal sealed record ScenarioMetricsSnapshot(
    double CriticalPathLatencyMs,
    long Observations,
    long AdmissionsRejected,
    long ReconciliationsNew,
    long ReconciliationsIdempotent,
    long Regrounds,
    string VerificationMode,
    string ModelCalls,
    string InputTokens);

/// <summary>一次场景运行的全部观察（owner records 只读聚合 + digest）。</summary>
internal sealed record ScenarioReport(
    string ScenarioId,
    string RunDriveStatus,
    string? Reason,
    RuntimeOutcome? Outcome,
    GoalEvaluation? GoalEvaluation,
    int EffectDeliveries,
    int AgentConsultations,
    IReadOnlyList<string> AgentViolations,
    IReadOnlyList<string> ConsumedStimulusIds,
    IReadOnlyList<string> UnconsumedStimulusIds,
    IReadOnlyList<string> UnexpectedStimuli,
    string SemanticDigest,
    ScenarioMetricsSnapshot Metrics,
    ActivationResult FirstActivation,
    ActivationResult? SecondActivation,
    bool AcceptancePassed)
{
    /// <summary>验收语义断言（expected vs actual；失败聚合原因）。</summary>
    public static string DescribeAcceptance(MinimalScenarioBundle bundle, ScenarioReport report) =>
        string.Join("; ",
            $"status={report.RunDriveStatus} (expected {bundle.Expected.ExpectedStatus})",
            $"classification={report.Outcome?.Classification.ToString() ?? "none"} (expected {bundle.Expected.ExpectedClassification ?? "none"})",
            $"effects={report.EffectDeliveries} (expected {bundle.Expected.ExpectedEffects})",
            $"agentCalls={report.AgentConsultations} (expected {bundle.Expected.ExpectedAgentConsultations})",
            $"unconsumed={report.UnconsumedStimulusIds.Count} (expected {bundle.Expected.ExpectedUnconsumedStimuli})",
            $"goal={report.GoalEvaluation?.Satisfaction.ToString() ?? "none"} (expected {bundle.Expected.ExpectedGoalSatisfaction ?? "none"})");
}
