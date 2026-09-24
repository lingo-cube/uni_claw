using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 golden bundle authoring：运行时读取 golden-run-v1 manifest
/// （Human-reviewed 资产），派生 Minimal Scenario Bundle（各自
/// ScenarioBundleDigest.Sealed 钉扎）。资产 integrity = manifest 声明
/// contentHash 与重算 hash 双核对（mismatch → ScenarioBundleException）。
///
/// SIM-003 G7：期望不再手写——每个工厂的 Expected 由
/// ScenarioExpectations.Load(expectationScenarioId) 从 certified JSON
/// 投影（authoring truth 唯一 = scenarios/SCN-*.json）；参数默认值 =
/// 该 carrier 的 primary SCN 条目，供非注册 fixture 复用。正式 SCN 场景
/// 经 ScenarioLibrary.Load(scenarioId) 解析（certified execution 绑定）。
///
/// DUAL-SOURCE evidence（评审 D18 事实澄清）：主场景证据是双源的——
/// occurrence 景观锚定在 golden response JSON 上，经真实 FastPerception +
/// LiveVisionStrategy（perception 侧）派生；而 obligation 相关的 switch
/// 状态（ReviewedStateClaims）来自 Human-reviewed manifest claims
/// （reviewed 侧），作为独立的 recorded external observation 入证。
/// 不主张任何结果「仅由 response JSON 派生」——两源在 join 后共同进入
/// E2B 管线。
/// </summary>
internal static class GoldenScenarioBundles
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddSeconds(10);
    private static readonly DateTimeOffset T2 = T0.AddSeconds(20);
    private static readonly DateTimeOffset Tcancel = T0.AddSeconds(5);

    /// <summary>SIM-004：脚本步 = Product AgentActionStep（零镜像）。</summary>
    private static AgentActionStep Step(string role, string? descriptor, string effectClass, string? desiredState) =>
        new(role, descriptor, effectClass, desiredState);

    // ---- (a) S1：wifi off → tap → post-action on（happy path）----

    public static MinimalScenarioBundle WifiToggleOffToOn(
        string expectationScenarioId = "SCN-WIFI-001") => ScenarioBundleDigest.Sealed(
        new MinimalScenarioBundle
        {
            BundleId = "golden-wifi-off-to-on",
            BundleVersion = "v1",
            ScenarioId = "wifi-off-to-on",
            ScenarioVersion = "v1",
            RuntimeArtifact = RuntimeArtifactIdentity.CaptureCurrent(),
            TargetUiSystem = LoadTargetUiSystem(),
            Assets = LoadAssets(),
            Stimuli = new ScenarioStimulus[]
            {
                ObservationFrame("obs-1-initial", T0, "golden-v1-case-b-off-perception",
                    "golden-v1-case-b-off", new ReviewedStateClaim("switch.wifi", "false"),
                    ObservationContext.External),
                ObservationFrame("obs-2-post", T1, "golden-v1-case-b-on-perception",
                    "golden-v1-case-b-on", new ReviewedStateClaim("switch.wifi", "true"),
                    ObservationContext.PostActionEffectFlow),
            },
            ProducerIdentities = LoadProducerIdentities(),
            // SIM-004 显式迁移：legacy Act 单步 + StepVerified 自动 NoAction 兜底
            // → 显式两 turn（行为等价；certified 六字段不变）
            PhaseScript = new PhaseAwareAgentScript(new[]
            {
                ScriptedTurn.ActAt(
                    AgentDecisionPhase.InitialPlanning,
                    new[] { Step("toggle", null, "tap", "true") },
                    "flip the wifi switch on"),
                ScriptedTurn.NoActionAt(
                    AgentDecisionPhase.StepVerified, "script-exhausted-goal-should-be-met"),
            }),
            Expected = ScenarioExpectations.Load(expectationScenarioId),
            Contract = new ExecutionContract(
                "s1-v1", "make-wifi-switch-on",
                new HashSet<string> { "live.frame", "switch.wifi" },
                new HashSet<string> { "tap" },
                new HashSet<string>(),
                new[] { "wifi-on" },
                new[] { new RunObligation("wifi", RunObligationKind.MaterialEffect, "switch.wifi", "true", true) }),
            Goal = new GoalSpec("goal-wifi", "turn the wifi switch on",
                RequiredClassification: "Completion", RequiredObligationIds: new[] { "wifi" }),
            BundleDigest = "",
        });

    // ---- (b) S2：已开 → NoAction 零 effect ----

    public static MinimalScenarioBundle AlreadyOnZeroEffect(
        string expectationScenarioId = "SCN-WIFI-002") => ScenarioBundleDigest.Sealed(
        new MinimalScenarioBundle
        {
            BundleId = "golden-wifi-already-on",
            BundleVersion = "v1",
            ScenarioId = "wifi-already-on",
            ScenarioVersion = "v1",
            RuntimeArtifact = RuntimeArtifactIdentity.CaptureCurrent(),
            TargetUiSystem = LoadTargetUiSystem(),
            Assets = LoadAssets(),
            Stimuli = new ScenarioStimulus[]
            {
                ObservationFrame("obs-1-initial", T0, "golden-v1-case-a-before-perception",
                    "golden-v1-case-a-before", new ReviewedStateClaim("switch.wifi", "true"),
                    ObservationContext.External),
            },
            ProducerIdentities = LoadProducerIdentities(),
            PhaseScript = new PhaseAwareAgentScript(new[]
            {
                ScriptedTurn.NoActionAt(AgentDecisionPhase.InitialPlanning, "switch already on"),
            }),
            Expected = ScenarioExpectations.Load(expectationScenarioId),
            Contract = new ExecutionContract(
                "s1-v1", "make-wifi-switch-on",
                new HashSet<string> { "live.frame", "switch.wifi" },
                new HashSet<string> { "tap" },
                new HashSet<string>(),
                new[] { "wifi-on" },
                new[] { new RunObligation("wifi", RunObligationKind.Objective, "switch.wifi", "true", true) }),
            Goal = new GoalSpec("goal-wifi", "turn the wifi switch on",
                RequiredClassification: "Completion", RequiredObligationIds: new[] { "wifi" }),
            BundleDigest = "",
        });

    // ---- (c) 缺 post-action stimulus → WaitingForInput ----

    public static MinimalScenarioBundle MissingPostActionStimulus(
        string expectationScenarioId = "SCN-WIFI-004")
    {
        var full = WifiToggleOffToOn();
        return ScenarioBundleDigest.Sealed(full with
        {
            BundleId = "golden-wifi-missing-post",
            ScenarioId = "wifi-missing-post-action",
            Stimuli = new[] { full.Stimuli[0] },
            // SIM-004 显式迁移：缺 post-action 帧 → 停在 StepVerify，唯一咨询
            // 是 InitialPlanning——turn 2 不可达，脚本只 authoring 首轮
            PhaseScript = new PhaseAwareAgentScript(new[] { full.PhaseScript.Turns[0] }),
            Expected = ScenarioExpectations.Load(expectationScenarioId),
        });
    }

    // ---- (d) S5（D21 rework）：初始帧仅 obs-1-initial；cancel + late 由
    // phased 测试经 Host.SubmitStimulus 两阶段注入（genuine waiting→cancel→late）。

    public static MinimalScenarioBundle CancelThenLateStimulus(
        string expectationScenarioId = "SCN-WIFI-005") => ScenarioBundleDigest.Sealed(
        new MinimalScenarioBundle
        {
            BundleId = "golden-wifi-cancel-late",
            BundleVersion = "v1",
            ScenarioId = "wifi-cancel-late",
            ScenarioVersion = "v1",
            RuntimeArtifact = RuntimeArtifactIdentity.CaptureCurrent(),
            TargetUiSystem = LoadTargetUiSystem(),
            Assets = LoadAssets(),
            Stimuli = new ScenarioStimulus[]
            {
                ObservationFrame("obs-1-initial", T0, "golden-v1-case-b-off-perception",
                    "golden-v1-case-b-off", new ReviewedStateClaim("switch.wifi", "false"),
                    ObservationContext.External),
            },
            ProducerIdentities = LoadProducerIdentities(),
            // SIM-004 显式迁移：cancel 截断 run——StepVerified 再咨询不可达，
            // 只 authoring 首轮 turn
            PhaseScript = new PhaseAwareAgentScript(new[]
            {
                ScriptedTurn.ActAt(
                    AgentDecisionPhase.InitialPlanning,
                    new[] { Step("toggle", null, "tap", "true") },
                    "flip the wifi switch on"),
            }),
            // 终态期望（FinalizePhased 之后核对）：RUN-004 多轮协议下
            // cancel 由 driver 自主处理（预算截断），分类 Completed（run
            // 生命周期结束），goal 仍 Unsatisfied（目标未达成）。
            Expected = ScenarioExpectations.Load(expectationScenarioId),
            Contract = new ExecutionContract(
                "s1-v1", "make-wifi-switch-on",
                new HashSet<string> { "live.frame", "switch.wifi", "run.cancel-requested" },
                new HashSet<string> { "tap" },
                new HashSet<string>(),
                new[] { "wifi-on", "cancel-safe" },
                new[]
                {
                    new RunObligation("wifi", RunObligationKind.MaterialEffect, "switch.wifi", "true", true),
                    new RunObligation("cancel", RunObligationKind.SafeStop, "run.cancel-requested", "true", true),
                }),
            Goal = new GoalSpec("goal-wifi", "turn the wifi switch on",
                RequiredClassification: "Completion", RequiredObligationIds: new[] { "wifi" }),
            BundleDigest = "",
        });

    /// <summary>S5 phased 测试注入用：cancel stimulus。</summary>
    internal static ScenarioStimulus.CancelRequest SafeStopCancelStimulus() =>
        new("host-cancel") { StimulusId = "cancel-1", VirtualTime = Tcancel };

    /// <summary>S5 phased 测试注入用：late post-action 观察帧（保持 unconsumed）。</summary>
    internal static ScenarioStimulus.ObservationFrame LatePostActionStimulus() =>
        ObservationFrame("obs-2-late", T1, "golden-v1-case-b-on-perception",
            "golden-v1-case-b-on", new ReviewedStateClaim("switch.wifi", "true"),
            ObservationContext.PostActionEffectFlow);

    // ---- (e) D22：两步串行场景——toggle（有期望终态）→ menuItem（无期望终态）----

    /// <summary>
    /// D22 两步 happy path：step1 tap toggle（DesiredState "true"，受不变量
    /// 43 屏障：消费 obs-2-post 证据后才允许 step2）→ step2 tap menuItem
    /// （DesiredState null，无终态检查）→ 消费 obs-3-post → terminal。
    /// step2 的 menuItem 元素在两个 manifest frame（case-b-on /
    /// case-a-before）中均有 bounds 与 perceptionType "menuItem"——adapter
    /// 对 reviewed elements 全量发 occurrence，接地可解析。
    /// </summary>
    public static MinimalScenarioBundle TwoStepToggleThenMenuItem(
        string expectationScenarioId = "SCN-BARRIER-001") => ScenarioBundleDigest.Sealed(
        new MinimalScenarioBundle
        {
            BundleId = "golden-wifi-two-step",
            BundleVersion = "v1",
            ScenarioId = "wifi-two-step-toggle-menu",
            ScenarioVersion = "v1",
            RuntimeArtifact = RuntimeArtifactIdentity.CaptureCurrent(),
            TargetUiSystem = LoadTargetUiSystem(),
            Assets = LoadAssets(),
            Stimuli = new ScenarioStimulus[]
            {
                ObservationFrame("obs-1-initial", T0, "golden-v1-case-b-off-perception",
                    "golden-v1-case-b-off", new ReviewedStateClaim("switch.wifi", "false"),
                    ObservationContext.External),
                ObservationFrame("obs-2-post", T1, "golden-v1-case-b-on-perception",
                    "golden-v1-case-b-on", new ReviewedStateClaim("switch.wifi", "true"),
                    ObservationContext.PostActionEffectFlow),
                ObservationFrame("obs-3-post", T2, "golden-v1-case-a-before-perception",
                    "golden-v1-case-a-before", new ReviewedStateClaim("switch.wifi", "true"),
                    ObservationContext.PostActionEffectFlow),
            },
            ProducerIdentities = LoadProducerIdentities(),
            // SIM-004 显式迁移：两步 proposal 耗尽 → StepVerified 再咨询 → NoAction
            PhaseScript = new PhaseAwareAgentScript(new[]
            {
                ScriptedTurn.ActAt(
                    AgentDecisionPhase.InitialPlanning,
                    new[]
                    {
                        Step("toggle", null, "tap", "true"),
                        Step("menuItem", null, "tap", null),
                    },
                    "flip the wifi switch on, then open the menu"),
                ScriptedTurn.NoActionAt(
                    AgentDecisionPhase.StepVerified, "script-exhausted-goal-should-be-met"),
            }),
            Expected = ScenarioExpectations.Load(expectationScenarioId),
            Contract = new ExecutionContract(
                "s1-v1", "make-wifi-switch-on",
                new HashSet<string> { "live.frame", "switch.wifi" },
                new HashSet<string> { "tap" },
                new HashSet<string>(),
                new[] { "wifi-on" },
                new[] { new RunObligation("wifi", RunObligationKind.MaterialEffect, "switch.wifi", "true", true) }),
            Goal = new GoalSpec("goal-wifi", "turn the wifi switch on",
                RequiredClassification: "Completion", RequiredObligationIds: new[] { "wifi" }),
            BundleDigest = "",
        });

    /// <summary>
    /// D22 缺中间证据：同 TwoStepToggleThenMenuItem 的 contract/script（另加
    /// SafeStop obligation——cancel 路径需要 contract 声明），stimuli 只有
    /// obs-1-initial：step1 dispatch 后停在 StepVerify 等待 post-action
    /// 证据，step2 被不变量 43 串行验证屏障阻塞（第二次 effect 不得发生）。
    /// Expected 描述等待中状态（effects=1）；终态由 phased 测试驱动 cancel
    /// 后以 FinalizePhased report 核对（SafeStop、effects 仍 1）。
    /// </summary>
    public static MinimalScenarioBundle TwoStepMissingMiddleEvidence(
        string expectationScenarioId = "SCN-BARRIER-002") => ScenarioBundleDigest.Sealed(
        new MinimalScenarioBundle
        {
            BundleId = "golden-wifi-two-step-missing-middle",
            BundleVersion = "v1",
            ScenarioId = "wifi-two-step-missing-middle",
            ScenarioVersion = "v1",
            RuntimeArtifact = RuntimeArtifactIdentity.CaptureCurrent(),
            TargetUiSystem = LoadTargetUiSystem(),
            Assets = LoadAssets(),
            Stimuli = new ScenarioStimulus[]
            {
                ObservationFrame("obs-1-initial", T0, "golden-v1-case-b-off-perception",
                    "golden-v1-case-b-off", new ReviewedStateClaim("switch.wifi", "false"),
                    ObservationContext.External),
            },
            ProducerIdentities = LoadProducerIdentities(),
            // SIM-004 显式迁移：缺中间证据 → step1 后停在 StepVerify（不变量 43
            // 屏障）——唯一咨询是 InitialPlanning，单 turn
            PhaseScript = new PhaseAwareAgentScript(new[]
            {
                ScriptedTurn.ActAt(
                    AgentDecisionPhase.InitialPlanning,
                    new[]
                    {
                        Step("toggle", null, "tap", "true"),
                        Step("menuItem", null, "tap", null),
                    },
                    "flip the wifi switch on, then open the menu"),
            }),
            Expected = ScenarioExpectations.Load(expectationScenarioId),
            Contract = new ExecutionContract(
                "s1-v1", "make-wifi-switch-on",
                new HashSet<string> { "live.frame", "switch.wifi", "run.cancel-requested" },
                new HashSet<string> { "tap" },
                new HashSet<string>(),
                new[] { "wifi-on", "cancel-safe" },
                new[]
                {
                    new RunObligation("wifi", RunObligationKind.MaterialEffect, "switch.wifi", "true", true),
                    new RunObligation("cancel", RunObligationKind.SafeStop, "run.cancel-requested", "true", true),
                }),
            Goal = new GoalSpec("goal-wifi", "turn the wifi switch on",
                RequiredClassification: "Completion", RequiredObligationIds: new[] { "wifi" }),
            BundleDigest = "",
        });

    // ---- (f) BARRIER-003：post-action 帧与 DesiredState 矛盾 → Assurance
    // fail closed，第二次 effect 被阻塞（TerminalNotProven）。SIM-003 G6：
    // 从 test-local 变换升格为注册 carrier（certified execution 绑定）。----

    /// <summary>
    /// TwoStepToggleThenMenuItem 的 contradictory-post 变体：obs-2-post 的
    /// toggle 元素 state 与 ReviewedStateClaims 全部翻转为 false——
    /// post-action frame 存在 ≠ 上一步已验证，Assurance verification 必须
    /// fail closed，不得仅凭 context 正确就放行 menuItem 的第二次 Effect。
    /// </summary>
    public static MinimalScenarioBundle TwoStepContradictoryPost(
        string expectationScenarioId = "SCN-BARRIER-003")
    {
        var original = TwoStepToggleThenMenuItem();
        var post = (ScenarioStimulus.ObservationFrame)original.Stimuli[1];
        var contradictory = post with
        {
            ReviewedElements = post.ReviewedElements
                .Select(element => element.Role == "toggle"
                    ? element with { State = "false" }
                    : element)
                .ToList(),
            ReviewedStateClaims = new[] { new ReviewedStateClaim("switch.wifi", "false") },
        };
        return ScenarioBundleDigest.Sealed(original with
        {
            ScenarioId = "wifi-two-step-contradictory-post",
            Stimuli = new[] { original.Stimuli[0], contradictory, original.Stimuli[2] },
            // SIM-004 显式迁移：step1 验证失败 → 再咨询到达 VerificationFailed
            // 相位，显式 no-response（legacy 形态在此伪记 duplicate-call 违规；
            // certified 六字段不变，acceptance 由伪违规必假翻真）
            PhaseScript = new PhaseAwareAgentScript(new[]
            {
                original.PhaseScript.Turns[0],
                ScriptedTurn.NoResponseAt(AgentDecisionPhase.VerificationFailed),
            }),
            Expected = ScenarioExpectations.Load(expectationScenarioId),
        });
    }

    // ---- (g) WIFI-006：SCN-002 D7 生成式等价场景（ScenarioBuilder 模板派生）----

    /// <summary>
    /// SCN-WIFI-006 注册 carrier：FromTemplate(wifi-off-to-on) 派生（同资产
    /// /契约/脚本底座，ScenarioId 换生成身份）。certified Expected 是最后
    /// 投影——模板期望被本场景条目的 certified 期望覆盖（G6 规则）。
    /// </summary>
    public static MinimalScenarioBundle WifiToggleOffToOnGenerated(
        string expectationScenarioId = "SCN-WIFI-006")
    {
        var built = ScenarioBuilder
            .FromTemplate(() => WifiToggleOffToOn())
            .WithScenarioId("generated-wifi-off-to-on", "wifi-off-to-on-generated")
            .Build();
        return ScenarioBundleDigest.Sealed(built with
        {
            Expected = ScenarioExpectations.Load(expectationScenarioId),
            BundleDigest = "",
        });
    }

    // ====================================================================
    // RUN-005 Slice C — P1-P11 Policy 场景 carriers（设计稿 §11；claim 演化
    // 经 authored Scope（CLE-001 Revise），occurrence 景观复用 golden 资产）
    // ====================================================================

    private static readonly DateTimeOffset PT0 = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PT1 = PT0.AddSeconds(10);
    private static readonly DateTimeOffset PT2 = PT0.AddSeconds(20);
    private static readonly DateTimeOffset PT3 = PT0.AddSeconds(30);
    private static readonly DateTimeOffset PT4 = PT0.AddSeconds(40);
    private static readonly DateTimeOffset PT5 = PT0.AddSeconds(50);

    /// <summary>SIM-004：谓词/守卫直接用 Product closed-AST 成员（零镜像）。</summary>
    private static PolicyPredicate Eq(string subject, string value) =>
        new PolicyPredicate.ClaimEquals(subject, value);
    private static PolicyPredicate InDomain(string subject) =>
        new PolicyPredicate.ClaimInSet(subject, new[] { "24", "23", "22", "21" });

    /// <summary>claim 演化（CLE-001 Revise 通道）：per-frame authored scope。</summary>
    private static ReviewedStateClaim Temp(string value, string scope) =>
        new("hvac.temp", value, "scope:hvac.temp:" + scope);

    /// <summary>固定 scope（跨帧值变化 → 显式 Conflict——P6 conflicted-claim 触发）。</summary>
    private static ReviewedStateClaim FixedScope(string subject, string value) =>
        new(subject, value, "scope:" + subject);

    private static ScenarioStimulus.ObservationFrame PolicyFrame(
        string stimulusId, DateTimeOffset t, string assetId, string frameId,
        IReadOnlyList<ReviewedStateClaim> claims, ObservationContext context) =>
        new(assetId, ReviewedElementsOf(frameId), claims, context)
        {
            StimulusId = stimulusId,
            VirtualTime = t,
        };

    /// <summary>
    /// SIM-004：temp policy 直接构造 Product PolicyProposal（matchPredicate =
    /// rogue 注入位，P11 用）。
    /// </summary>
    private static PolicyProposal TempPolicy(
        string policyId = "pol-temp-1",
        int maxApplications = 4,
        IReadOnlyList<PolicyPredicate>? match = null,
        IReadOnlyList<PolicyPredicate>? termination = null,
        IReadOnlyList<PolicyGuard>? guards = null,
        string templateEffectClass = "tap",
        PolicyPredicate? matchPredicate = null) => new(
        policyId,
        match ?? new[] { matchPredicate ?? InDomain("hvac.temp") },
        new PolicyActionTemplate("toggle", null, templateEffectClass, "true"),
        termination ?? new[] { Eq("hvac.temp", "20") },
        guards ?? Array.Empty<PolicyGuard>(),
        maxApplications,
        Justification: null);

    private static ScriptedTurn PolicyTurn(PolicyProposal policy) =>
        ScriptedTurn.PolicyAt(AgentDecisionPhase.InitialPlanning, policy);

    private static ExecutionContract PolicyContract(int maxTotalSteps = 256) => new(
        "policy-v1", "cool-to-20",
        new HashSet<string> { "live.frame", "hvac.temp" },
        new HashSet<string> { "tap" },
        new HashSet<string>(),
        new[] { "temp-20" },
        new[] { new RunObligation("temp", RunObligationKind.Objective, "hvac.temp", "20", true) },
        MaxTotalSteps: maxTotalSteps);

    private static GoalSpec PolicyGoal() => new(
        "goal-temp", "cool the setting down to 20",
        RequiredClassification: "Completion", RequiredObligationIds: new[] { "temp" });

    private static MinimalScenarioBundle PolicyBundle(
        string bundleId, string scenarioId, string expectationScenarioId,
        IReadOnlyList<ScenarioStimulus> stimuli, PhaseAwareAgentScript script,
        int maxTotalSteps = 256) => ScenarioBundleDigest.Sealed(
        new MinimalScenarioBundle
        {
            BundleId = bundleId,
            BundleVersion = "v1",
            ScenarioId = scenarioId,
            ScenarioVersion = "v1",
            RuntimeArtifact = RuntimeArtifactIdentity.CaptureCurrent(),
            TargetUiSystem = LoadTargetUiSystem(),
            Assets = LoadAssets(),
            Stimuli = stimuli,
            ProducerIdentities = LoadProducerIdentities(),
            PhaseScript = script,
            Expected = ScenarioExpectations.Load(expectationScenarioId),
            Contract = PolicyContract(maxTotalSteps),
            Goal = PolicyGoal(),
            BundleDigest = "",
        });

    private static ScriptedTurn NoActionAt(AgentDecisionPhase phase, string why) =>
        ScriptedTurn.NoActionAt(phase, why);

    // ---- P1：immediate termination（0-application 即时满足）----

    public static MinimalScenarioBundle PolicyImmediateTermination(
        string expectationScenarioId = "SCN-POLICY-001") =>
        PolicyBundle("golden-policy-p1", "policy-immediate-termination", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p1-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("20", "init") }, ObservationContext.External),
                PolicyFrame("p1-round-1", PT1, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("20", "r1") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                PolicyTurn(TempPolicy()),
                NoActionAt(AgentDecisionPhase.StepVerified, "already-at-20"),
            }));

    // ---- P2：repeated application → success（claim 演化 24→23→22→20）----

    public static MinimalScenarioBundle PolicyRepeatedApplicationSuccess(
        string expectationScenarioId = "SCN-POLICY-002") =>
        PolicyBundle("golden-policy-p2", "policy-repeated-application", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p2-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init") }, ObservationContext.External),
                PolicyFrame("p2-r1", PT1, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("23", "r1") }, ObservationContext.External),
                PolicyFrame("p2-v1", PT2, "golden-v1-case-b-on-perception", "golden-v1-case-b-on",
                    new[] { Temp("23", "v1") }, ObservationContext.PostActionEffectFlow),
                PolicyFrame("p2-r2", PT3, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("22", "r2") }, ObservationContext.External),
                PolicyFrame("p2-v2", PT4, "golden-v1-case-b-on-perception", "golden-v1-case-b-on",
                    new[] { Temp("22", "v2") }, ObservationContext.PostActionEffectFlow),
                PolicyFrame("p2-r3", PT5, "golden-v1-case-b-on-perception", "golden-v1-case-b-on",
                    new[] { Temp("20", "r3") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                PolicyTurn(TempPolicy()),
                NoActionAt(AgentDecisionPhase.StepVerified, "reached-20"),
            }));

    // ---- P3：no match（ClaimInSet 出集 → 再咨询 PolicyInvalidated）----

    public static MinimalScenarioBundle PolicyNoMatch(
        string expectationScenarioId = "SCN-POLICY-003") =>
        PolicyBundle("golden-policy-p3", "policy-no-match", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p3-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init") }, ObservationContext.External),
                PolicyFrame("p3-r1", PT1, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("19", "r1") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                PolicyTurn(TempPolicy()),
                NoActionAt(AgentDecisionPhase.PolicyInvalidated, "overshot-below-domain"),
            }));

    // ---- P4：bounds exhausted（MaxApplications=1，temp 不动）----

    public static MinimalScenarioBundle PolicyBoundsExhausted(
        string expectationScenarioId = "SCN-POLICY-004") =>
        PolicyBundle("golden-policy-p4", "policy-bounds-exhausted", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p4-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init") }, ObservationContext.External),
                PolicyFrame("p4-r1", PT1, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "r1") }, ObservationContext.External),
                PolicyFrame("p4-v1", PT2, "golden-v1-case-b-on-perception", "golden-v1-case-b-on",
                    new[] { Temp("24", "v1") }, ObservationContext.PostActionEffectFlow),
                PolicyFrame("p4-r2", PT3, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "r2") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                PolicyTurn(TempPolicy(maxApplications: 1)),
                NoActionAt(AgentDecisionPhase.PolicyInvalidated, "stuck-at-24"),
            }));

    // ---- P5：guard violated（连续 unchanged 轮触发；2 applications）----

    public static MinimalScenarioBundle PolicyGuardViolated(
        string expectationScenarioId = "SCN-POLICY-005") =>
        PolicyBundle("golden-policy-p5", "policy-guard-violated", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p5-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init") }, ObservationContext.External),
                PolicyFrame("p5-r1", PT1, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "r1") }, ObservationContext.External),
                PolicyFrame("p5-v1", PT2, "golden-v1-case-b-on-perception", "golden-v1-case-b-on",
                    new[] { Temp("24", "v1") }, ObservationContext.PostActionEffectFlow),
                PolicyFrame("p5-r2", PT3, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "r2") }, ObservationContext.External),
                PolicyFrame("p5-v2", PT4, "golden-v1-case-b-on-perception", "golden-v1-case-b-on",
                    new[] { Temp("24", "v2") }, ObservationContext.PostActionEffectFlow),
                PolicyFrame("p5-r3", PT5, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "r3") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                PolicyTurn(TempPolicy(guards: new[] { new PolicyGuard.ObservationUnchanged("hvac.temp", 1) })),
                NoActionAt(AgentDecisionPhase.PolicyInvalidated, "temp-not-moving"),
            }));

    // ---- P6：guard Unknown（conflicted claim——固定 scope 跨帧值变化）----

    public static MinimalScenarioBundle PolicyGuardUnknown(
        string expectationScenarioId = "SCN-POLICY-006") =>
        PolicyBundle("golden-policy-p6", "policy-guard-unknown", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p6-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init"), FixedScope("hvac.mode", "cool") }, ObservationContext.External),
                PolicyFrame("p6-r1", PT1, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "r1"), FixedScope("hvac.mode", "heat") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                PolicyTurn(TempPolicy(guards: new[] { new PolicyGuard.ObservationUnchanged("hvac.mode", 1) })),
                NoActionAt(AgentDecisionPhase.PolicyInvalidated, "mode-sensor-conflicted"),
            }));

    // ---- P7：verification failure midway（post-action 与 DesiredState 矛盾）----

    public static MinimalScenarioBundle PolicyVerificationFailureMidway(
        string expectationScenarioId = "SCN-POLICY-007") =>
        PolicyBundle("golden-policy-p7", "policy-verification-failure", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p7-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init") }, ObservationContext.External),
                PolicyFrame("p7-r1", PT1, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "r1") }, ObservationContext.External),
                // post-action 仍 off（desired true）→ 既有 VerificationFailed 转移 + policy 作废
                PolicyFrame("p7-v1", PT2, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "v1") }, ObservationContext.PostActionEffectFlow),
            },
            new PhaseAwareAgentScript(new[]
            {
                PolicyTurn(TempPolicy()),
                NoActionAt(AgentDecisionPhase.VerificationFailed, "tap-did-not-flip"),
            }));

    // ---- P8：fresh-world change between applications（lease invalidation；
    //      drift association 由测试经 SeamOverrides.Association 注入）----

    public static MinimalScenarioBundle PolicyLeaseDrift(
        string expectationScenarioId = "SCN-POLICY-008") =>
        PolicyBundle("golden-policy-p8", "policy-lease-drift", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p8-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init") }, ObservationContext.External),
                PolicyFrame("p8-r1", PT1, "golden-v1-case-b-on-perception", "golden-v1-case-b-on",
                    new[] { Temp("24", "r1") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                PolicyTurn(TempPolicy()),
                NoActionAt(AgentDecisionPhase.PolicyInvalidated, "screen-changed"),
            }));

    // ---- P9：forbidden effect class → V6d reject ----

    public static MinimalScenarioBundle PolicyForbiddenEffect(
        string expectationScenarioId = "SCN-POLICY-009") =>
        PolicyBundle("golden-policy-p9", "policy-forbidden-effect", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p9-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                PolicyTurn(TempPolicy(templateEffectClass: "swipe")),
            }));

    // ---- P10：policy budget > remaining contract budget → V6c reject ----

    public static MinimalScenarioBundle PolicyBudgetExceeds(
        string expectationScenarioId = "SCN-POLICY-010") =>
        PolicyBundle("golden-policy-p10", "policy-budget-exceeds", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p10-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                // MaxApplications(3) > StepsRemaining(2)：不得扩大合同步数预算
                PolicyTurn(TempPolicy(maxApplications: 3)),
            }),
            maxTotalSteps: 2);

    // ---- P11：unknown/malformed AST → V6a reject（rogue 谓词经正式 seam）----

    public static MinimalScenarioBundle PolicyUnknownAst(
        string expectationScenarioId = "SCN-POLICY-011") =>
        PolicyBundle("golden-policy-p11", "policy-unknown-ast", expectationScenarioId,
            new ScenarioStimulus[]
            {
                PolicyFrame("p11-init", PT0, "golden-v1-case-b-off-perception", "golden-v1-case-b-off",
                    new[] { Temp("24", "init") }, ObservationContext.External),
            },
            new PhaseAwareAgentScript(new[]
            {
                // 脚本携带 rogue 派生谓词（SIM-004：Product closed AST 之外的
                // test-side 注入位）→ 经正式 seam 入场 → V6a fail closed
                PolicyTurn(TempPolicy(matchPredicate: new RogueScriptPredicate())),
            }));

    // ---- manifest 派生 helpers（fail closed：结构/内容不符 → 异常）----

    private static ScenarioStimulus.ObservationFrame ObservationFrame(
        string stimulusId, DateTimeOffset virtualTime, string perceptionAssetId,
        string frameId, ReviewedStateClaim claim,
        ObservationContext context) =>
        new(perceptionAssetId, ReviewedElementsOf(frameId),
            new[] { claim }, context)
        {
            StimulusId = stimulusId,
            VirtualTime = virtualTime,
        };

    private static JsonElement ManifestRoot()
    {
        var path = Path.Combine(GoldenPaths.RepoRoot(), GoldenPaths.BundleRoot, "scenario-manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return JsonDocument.Parse(document.RootElement.GetRawText()).RootElement.Clone();
    }

    /// <summary>资产清单：manifest.artifacts 全量 + manifest/device-profile 两个元资产。</summary>
    private static IReadOnlyList<BundleAssetEntry> LoadAssets()
    {
        var root = ManifestRoot();
        var assets = new List<BundleAssetEntry>();
        foreach (var artifact in root.GetProperty("artifacts").EnumerateArray())
        {
            var artifactId = artifact.GetProperty("artifactId").GetString()!;
            var relativePath = artifact.GetProperty("relativePath").GetString()!;
            var declared = artifact.GetProperty("contentHash").GetString()!;
            if (!declared.StartsWith("sha256:", StringComparison.Ordinal))
                throw new ScenarioBundleException($"artifact {artifactId} contentHash 缺 sha256 前缀: {declared}");
            var recomputed = BundleAssetFiles.HashOf(relativePath);
            if (recomputed != declared["sha256:".Length..])
                throw new ScenarioBundleException(
                    $"golden asset hash 与 manifest 不符: {artifactId} manifest={declared} recomputed=sha256:{recomputed}");
            assets.Add(new BundleAssetEntry(artifactId, relativePath, recomputed, artifact.GetProperty("type").GetString()!));
        }
        assets.Add(new BundleAssetEntry(
            "golden-manifest", "scenario-manifest.json",
            BundleAssetFiles.HashOf("scenario-manifest.json"), "manifest"));
        assets.Add(new BundleAssetEntry(
            "device-profile", "device-profile.json",
            BundleAssetFiles.HashOf("device-profile.json"), "device-profile"));
        return assets;
    }

    /// <summary>manifest frame 的 reviewed elements → ReviewedElement 列表。</summary>
    private static IReadOnlyList<ReviewedElement> ReviewedElementsOf(string frameId)
    {
        var root = ManifestRoot();
        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            if (frame.GetProperty("frameId").GetString() != frameId)
                continue;
            var elements = new List<ReviewedElement>();
            foreach (var element in frame.GetProperty("observation").GetProperty("elements").EnumerateArray())
            {
                var switchState = element.GetProperty("switchState");
                string? state = switchState.ValueKind switch
                {
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => throw new ScenarioBundleException($"frame {frameId} switchState 类型异常: {switchState.ValueKind}"),
                };
                var bounds = element.GetProperty("bounds");
                elements.Add(new ReviewedElement(
                    element.GetProperty("perceptionType").GetString()!,
                    state,
                    bounds.GetProperty("x1").GetDouble(),
                    bounds.GetProperty("y1").GetDouble(),
                    bounds.GetProperty("x2").GetDouble(),
                    bounds.GetProperty("y2").GetDouble()));
            }
            return elements;
        }
        throw new ScenarioBundleException($"manifest frame 不存在: {frameId}");
    }

    /// <summary>
    /// device-profile.json + manifest 前台应用 → 目标 UI system identity。
    /// AppBuild：manifest / device-profile 均未记录 app build 版本——
    /// sentinel "not-recorded"（不伪造）。
    /// </summary>
    private static TargetUiSystemIdentity LoadTargetUiSystem()
    {
        var profilePath = Path.Combine(GoldenPaths.RepoRoot(), GoldenPaths.BundleRoot, "device-profile.json");
        using var profile = JsonDocument.Parse(File.ReadAllText(profilePath));
        var p = profile.RootElement;
        var appPackage = ForegroundApplicationOf("golden-v1-case-b-off");
        return new TargetUiSystemIdentity(
            Platform: p.GetProperty("platform").GetString()!.ToLowerInvariant(),
            OsVersion: p.GetProperty("osVersion").GetString()!,
            AppPackage: appPackage,
            AppBuild: "not-recorded",
            DeviceProfileId: p.GetProperty("deviceProfileId").GetString()!,
            DisplayWidth: p.GetProperty("displayWidth").GetInt32(),
            DisplayHeight: p.GetProperty("displayHeight").GetInt32());
    }

    private static string ForegroundApplicationOf(string frameId)
    {
        var root = ManifestRoot();
        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            if (frame.GetProperty("frameId").GetString() == frameId)
                return frame.GetProperty("observation").GetProperty("foregroundApplication").GetString()!;
        }
        throw new ScenarioBundleException($"manifest frame 不存在: {frameId}");
    }

    /// <summary>producer / schema / config identity（LiveVisionStrategy 实际 identity 值）。</summary>
    private static ProducerSchemaConfigIdentity LoadProducerIdentities()
    {
        var strategy = new LiveVisionStrategy();
        return new ProducerSchemaConfigIdentity(
            PerceptionProducer: "perception.replay.vision",
            StrategyIdentity: strategy.StrategyIdentity,
            StrategyVersion: strategy.StrategyVersion,
            EffectDriverIdentity: "sim.deterministic-driver",
            ManifestSchemaVersion: ManifestRoot().GetProperty("schemaVersion").GetInt32());
    }
}
