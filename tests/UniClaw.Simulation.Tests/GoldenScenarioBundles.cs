using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Run;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 golden bundle authoring：运行时读取 golden-run-v1 manifest
/// （Human-reviewed 资产），派生 Minimal Scenario Bundle（各自
/// ScenarioBundleDigest.Sealed 钉扎）。资产 integrity = manifest 声明
/// contentHash 与重算 hash 双核对（mismatch → ScenarioBundleException）。
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

    private static ScriptActionStep Step(string role, string? descriptor, string effectClass, string? desiredState) =>
        new(role, descriptor, effectClass, desiredState);

    // ---- (a) S1：wifi off → tap → post-action on（happy path）----

    public static MinimalScenarioBundle WifiToggleOffToOn() => ScenarioBundleDigest.Sealed(
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
                    "golden-v1-case-b-off", ("switch.wifi", "false"),
                    ObservationContext.External),
                ObservationFrame("obs-2-post", T1, "golden-v1-case-b-on-perception",
                    "golden-v1-case-b-on", ("switch.wifi", "true"),
                    ObservationContext.PostActionEffectFlow),
            },
            ProducerIdentities = LoadProducerIdentities(),
            AgentScript = new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { Step("toggle", null, "tap", "true") },
                Justification: "flip the wifi switch on"),
            Expected = new ScenarioExpectation(
                "Completed", "Completion", ExpectedEffects: 1,
                ExpectedAgentConsultations: 2, ExpectedUnconsumedStimuli: 0,
                ExpectedGoalSatisfaction: "Satisfied"),
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

    public static MinimalScenarioBundle AlreadyOnZeroEffect() => ScenarioBundleDigest.Sealed(
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
                    "golden-v1-case-a-before", ("switch.wifi", "true"),
                    ObservationContext.External),
            },
            ProducerIdentities = LoadProducerIdentities(),
            AgentScript = new AgentScriptStep(
                AgentScriptKind.NoAction, Array.Empty<ScriptActionStep>(),
                Justification: "switch already on"),
            Expected = new ScenarioExpectation(
                "Completed", "Completion", ExpectedEffects: 0,
                ExpectedAgentConsultations: 2, ExpectedUnconsumedStimuli: 0,
                ExpectedGoalSatisfaction: "Satisfied"),
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

    public static MinimalScenarioBundle MissingPostActionStimulus()
    {
        var full = WifiToggleOffToOn();
        return ScenarioBundleDigest.Sealed(full with
        {
            BundleId = "golden-wifi-missing-post",
            ScenarioId = "wifi-missing-post-action",
            Stimuli = new[] { full.Stimuli[0] },
            Expected = full.Expected with
            {
                ExpectedStatus = "WaitingForInput",
                ExpectedClassification = null,
                ExpectedEffects = 1,
                ExpectedUnconsumedStimuli = 0,
                ExpectedGoalSatisfaction = null,
            },
        });
    }

    // ---- (d) S5（D21 rework）：初始帧仅 obs-1-initial；cancel + late 由
    // phased 测试经 Host.SubmitStimulus 两阶段注入（genuine waiting→cancel→late）。

    public static MinimalScenarioBundle CancelThenLateStimulus() => ScenarioBundleDigest.Sealed(
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
                    "golden-v1-case-b-off", ("switch.wifi", "false"),
                    ObservationContext.External),
            },
            ProducerIdentities = LoadProducerIdentities(),
            AgentScript = new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { Step("toggle", null, "tap", "true") },
                Justification: "flip the wifi switch on"),
            // 终态期望（FinalizePhased 之后核对）：cancel → SafeStop、
            // 1 effect、late stimulus 保持 unconsumed、goal Unsatisfied。
            Expected = new ScenarioExpectation(
                "Completed", "SafeStop", ExpectedEffects: 1,
                ExpectedAgentConsultations: 2, ExpectedUnconsumedStimuli: 1,
                ExpectedGoalSatisfaction: "Unsatisfied"),
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
            "golden-v1-case-b-on", ("switch.wifi", "true"),
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
    public static MinimalScenarioBundle TwoStepToggleThenMenuItem() => ScenarioBundleDigest.Sealed(
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
                    "golden-v1-case-b-off", ("switch.wifi", "false"),
                    ObservationContext.External),
                ObservationFrame("obs-2-post", T1, "golden-v1-case-b-on-perception",
                    "golden-v1-case-b-on", ("switch.wifi", "true"),
                    ObservationContext.PostActionEffectFlow),
                ObservationFrame("obs-3-post", T2, "golden-v1-case-a-before-perception",
                    "golden-v1-case-a-before", ("switch.wifi", "true"),
                    ObservationContext.PostActionEffectFlow),
            },
            ProducerIdentities = LoadProducerIdentities(),
            AgentScript = new AgentScriptStep(
                AgentScriptKind.Act,
                new[]
                {
                    Step("toggle", null, "tap", "true"),
                    Step("menuItem", null, "tap", null),
                },
                Justification: "flip the wifi switch on, then open the menu"),
            Expected = new ScenarioExpectation(
                "Completed", "Completion", ExpectedEffects: 2,
                ExpectedAgentConsultations: 2, ExpectedUnconsumedStimuli: 0,
                ExpectedGoalSatisfaction: "Satisfied"),
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
    public static MinimalScenarioBundle TwoStepMissingMiddleEvidence() => ScenarioBundleDigest.Sealed(
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
                    "golden-v1-case-b-off", ("switch.wifi", "false"),
                    ObservationContext.External),
            },
            ProducerIdentities = LoadProducerIdentities(),
            AgentScript = new AgentScriptStep(
                AgentScriptKind.Act,
                new[]
                {
                    Step("toggle", null, "tap", "true"),
                    Step("menuItem", null, "tap", null),
                },
                Justification: "flip the wifi switch on, then open the menu"),
            Expected = new ScenarioExpectation(
                "WaitingForInput", null, ExpectedEffects: 1,
                ExpectedAgentConsultations: 1, ExpectedUnconsumedStimuli: 0,
                ExpectedGoalSatisfaction: null),
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

    // ---- manifest 派生 helpers（fail closed：结构/内容不符 → 异常）----

    private static ScenarioStimulus.ObservationFrame ObservationFrame(
        string stimulusId, DateTimeOffset virtualTime, string perceptionAssetId,
        string frameId, (string Subject, string Value) claim,
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
