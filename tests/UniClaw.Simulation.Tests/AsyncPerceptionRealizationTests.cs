using System.Text.Json;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// UAP-001 — 四 realization 同构比较 + 真实 live one-shot/fast 只读采集
/// 路径接入证明。
///
/// 比较纪律（roadmap Phase 4 Acceptance）：四种 realization 在同语义输入
/// 下比较**规范化世界结论与安全行为**（grounding 解析、effect 计数与目
/// 标、occurrence role 直方图），不比较不同模型的原始字节。realization
/// 间差异报告首个分歧 checkpoint。
/// </summary>
public sealed class AsyncPerceptionRealizationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
    private readonly ITestOutputHelper _output;

    public AsyncPerceptionRealizationTests(ITestOutputHelper output) => _output = output;

    /// <summary>realization 种类（Perception 内部选择，Runtime 不可见）。</summary>
    private enum Realization
    {
        FastOnly,
        FastThenSlow,
        SlowOnly,
        OneShot,
    }

    /// <summary>规范化世界结论与安全行为（比较面；不比模型原始字节）。</summary>
    private sealed record NormalizedConclusion(
        string RoleOnlyResolution,
        string ColorsRequestResolution,
        string ColorRequestResolution,
        int MenuRowCount,
        int SubtitleRowCount,
        int EffectCount,
        double? EffectTargetCenterY);

    private static (AsyncPerceptionTruth.ElementSpec Color, AsyncPerceptionTruth.ElementSpec Colors,
        AsyncPerceptionTruth.ElementSpec Contrast) DisplayElements()
    {
        var facts = AsyncPerceptionTruth.Load();
        return (
            AsyncPerceptionTruth.ElementsOfPage(facts, "display-child").Single(e => e.Text == "Color"),
            AsyncPerceptionTruth.ElementsOfPage(facts, "display-child").Single(e => e.Text == "Colors"),
            AsyncPerceptionTruth.ElementsOfPage(facts, "display-child").Single(e => e.Text == "Color contrast"));
    }

    /// <summary>
    /// 同一语义输入（display-child ⑧ 页 + 请求打开 Colors）跑指定
    /// realization。inspect=true 为观察臂（NoAction——终态 belief = 该
    /// realization 交付的世界，realization 差异不被 post-action 帧冲掉）；
    /// inspect=false 为行为臂（真实 tap 链路验证安全行为）。
    /// </summary>
    private static AsyncPerceptionHost RunRealization(Realization realization, bool inspect = false)
    {
        var facts = AsyncPerceptionTruth.Load();
        var color = AsyncPerceptionTruth.ElementsOfPage(facts, "display-child").Single(e => e.Text == "Color");
        var colors = AsyncPerceptionTruth.ElementsOfPage(facts, "display-child").Single(e => e.Text == "Colors");
        var contrast = AsyncPerceptionTruth.ElementsOfPage(facts, "display-child").Single(e => e.Text == "Color contrast");
        var scope = new[] { color, colors, contrast };

        var fastTyped = scope.Select(e => new TypedElement(
            e, AsyncPerceptionTruth.PredictedTypeFor(facts, e.Page, e.Text, e.TruthType))).ToList();
        var truthTyped = scope.Select(e => new TypedElement(e, e.TruthType)).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));
        var captureId = AsyncRealizations.CaptureIdOf(response);

        var initial = new ScriptedOperation(
            ObservationContext.External, "initial-1", "initial-observation",
            scope.Select(e => e.ElementId).ToList(), TimeSpan.FromSeconds(30),
            T0, captureId, Array.Empty<ScheduledResult>());

        var schedule = realization switch
        {
            // Fast-only：fast 即最终（覆盖全 scope；误判 typing 保留）
            Realization.FastOnly => new[]
            {
                AsyncRealizations.ModelStageResult(
                    initial, "res-fast", "fast", T0.AddSeconds(1), IsFinal: true, fastTyped),
            },
            // Fast→Slow：fast partial + 定向 slow（truth 对齐）最终
            Realization.FastThenSlow => new ScheduledResult[]
            {
                AsyncRealizations.ModelStageResult(
                    initial, "res-fast", "fast", T0.AddSeconds(1), IsFinal: false, fastTyped),
                AsyncRealizations.RecordedStageResult(
                    initial, "res-slow", "targeted-slow", T0.AddSeconds(6), IsFinal: true, truthTyped),
            },
            // Slow-only：仅 slow（truth 对齐 double）
            Realization.SlowOnly => new ScheduledResult[]
            {
                AsyncRealizations.RecordedStageResult(
                    initial, "res-slow", "slow", T0.AddSeconds(6), IsFinal: true, truthTyped),
            },
            // one-shot full model：单次全模型（truth 对齐）最终
            Realization.OneShot => new ScheduledResult[]
            {
                AsyncRealizations.RecordedStageResult(
                    initial, "res-oneshot", "oneshot", T0.AddSeconds(4), IsFinal: true, truthTyped),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(realization)),
        };

        var post = new ScriptedOperation(
            ObservationContext.PostActionEffectFlow, "post-1", "post-action-observation",
            scope.Select(e => e.ElementId).ToList(), TimeSpan.FromSeconds(30),
            T0.AddSeconds(30), "art-post-realization", Array.Empty<ScheduledResult>());
        var postResult = AsyncRealizations.RecordedStageResult(
            post, "res-post", "slow", T0.AddSeconds(32), IsFinal: true, truthTyped,
            new[] { ("page.colors.opened", "true") });

        var contract = new UniClaw.Kernel.Run.ExecutionContract(
            "uap-v1", "open-target-menu",
            AsyncRealizations.ScopeOf(scope, "page.colors.opened"),
            new HashSet<string> { "tap" },
            new HashSet<string>(),
            new[] { "obj-open" },
            new[]
            {
                new UniClaw.Kernel.Run.RunObligation(
                    "open", UniClaw.Kernel.Run.RunObligationKind.MaterialEffect,
                    "page.colors.opened", "true", true),
            });
        var host = new AsyncPerceptionHost(new AsyncScenario(
            $"realization-{realization}",
            T0,
            contract,
            inspect
                ? new AgentScriptStep(AgentScriptKind.NoAction, Array.Empty<ScriptActionStep>(), "inspect world")
                : new AgentScriptStep(
                    AgentScriptKind.Act,
                    new[] { new ScriptActionStep("menu.row", "Colors", "tap", null) },
                    "open colors"),
            new[] { initial with { Schedule = schedule },
                    post with { Schedule = new[] { postResult } } }));
        Assert.True(host.KernelCore.AdmitContract(contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        host.Clock.AdvanceTo(T0.AddSeconds(10));
        if (inspect)
        {
            // 观察臂：NoAction（无 Completion 自证）+ mandatory 义务
            // page.colors.opened 未世界满足 → RUN-004 V4 hollow-completion
            // fail closed（SR-074：首询空洞收工是协议违规，不是诚实未证）。
            // 世界信念本身已由 realization 交付（下方 Normalize 消费
            // CurrentBelief，不受 drive 状态影响）。
            var inspectResult = host.DriveOnce();
            Assert.Equal(RunDriveStatus.AgentDecisionFailed, inspectResult.Status);
            return host;
        }
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status);
        host.Clock.AdvanceTo(T0.AddSeconds(32));
        Assert.Equal(RunDriveStatus.Completed, host.DriveOnce().Status);
        return host;
    }

    [Fact]
    public void FourRealizations_SameSemanticInput_NormalizedConclusionsAndSafetyCompared()
    {
        var hosts = new Dictionary<Realization, AsyncPerceptionHost>
        {
            [Realization.FastOnly] = RunRealization(Realization.FastOnly),
            [Realization.FastThenSlow] = RunRealization(Realization.FastThenSlow),
            [Realization.SlowOnly] = RunRealization(Realization.SlowOnly),
            [Realization.OneShot] = RunRealization(Realization.OneShot),
        };
        // 观察臂（NoAction）：终态 belief = 各 realization 交付的世界
        var inspectHosts = new Dictionary<Realization, AsyncPerceptionHost>
        {
            [Realization.FastOnly] = RunRealization(Realization.FastOnly, inspect: true),
            [Realization.FastThenSlow] = RunRealization(Realization.FastThenSlow, inspect: true),
            [Realization.SlowOnly] = RunRealization(Realization.SlowOnly, inspect: true),
            [Realization.OneShot] = RunRealization(Realization.OneShot, inspect: true),
        };

        // 规范化结论 checkpoint（有序——首个差异即 first divergence）。
        // 结论取观察臂（realization 交付的世界）；安全行为取行为臂。
        NormalizedConclusion Normalize(AsyncPerceptionHost host)
        {
            var belief = host.KernelCore.CurrentBelief!;
            string Resolve(string? descriptor) => host.WorldCore.ResolveCurrent(
                new TargetDescriptor("menu.row", descriptor)).Result.ToString();
            var binding = host.EffectBoundaryCore.BindingLog
                .Where(b => b.Canonical is not null)
                .Select(b => b.Canonical!)
                .SingleOrDefault();
            return new NormalizedConclusion(
                RoleOnlyResolution: Resolve(null),
                ColorsRequestResolution: Resolve("Colors"),
                ColorRequestResolution: Resolve("Color"),
                MenuRowCount: belief.Occurrences!.Count(o => o.Role == "menu.row"),
                SubtitleRowCount: belief.Occurrences!.Count(o => o.Role == "menu.subtitle"),
                EffectCount: host.EffectDriver.DeliveryCount,
                EffectTargetCenterY: binding is null
                    ? null
                    : (double?)Math.Round(binding.TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));
        }

        var conclusions = inspectHosts.ToDictionary(kv => kv.Key, kv => Normalize(kv.Value));
        foreach (var (realization, conclusion) in conclusions)
            _output.WriteLine($"inspect {realization}: {JsonSerializer.Serialize(conclusion)}");

        // 安全行为（所有 realization 一致，行为臂）：请求 Colors → 恰一
        // effect、落真实菜单行（cy 2202.5）；零次 effect 落非真值行
        var (_, colorsElement, _) = DisplayElements();
        var expectedCenterY = AsyncPerceptionTruth.CenterYOf(colorsElement.Cy);
        foreach (var actHost in hosts.Values)
        {
            Assert.Equal(1, actHost.EffectDriver.DeliveryCount);
            var binding = actHost.EffectBoundaryCore.BindingLog
                .Single(b => b.Canonical is not null).Canonical!;
            Assert.Equal(expectedCenterY, Math.Round(binding.TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));
        }

        // 规范化结论：纠正性 realization（Fast→Slow / Slow-only / one-shot）
        // 互相一致——Fast-only 在 role-only 与 Color 请求两个 counterfactual
        // checkpoint 上分歧（首个分歧 = RoleOnlyResolution）
        Assert.Equal(conclusions[Realization.FastThenSlow], conclusions[Realization.SlowOnly]);
        Assert.Equal(conclusions[Realization.FastThenSlow], conclusions[Realization.OneShot]);
        Assert.NotEqual(conclusions[Realization.FastOnly], conclusions[Realization.FastThenSlow]);

        // 有序 checkpoint 逐项比较——首个差异即 first divergence
        string? FirstDivergence(NormalizedConclusion a, NormalizedConclusion b) =>
            a.RoleOnlyResolution != b.RoleOnlyResolution ? "RoleOnlyResolution"
            : a.ColorsRequestResolution != b.ColorsRequestResolution ? "ColorsRequestResolution"
            : a.ColorRequestResolution != b.ColorRequestResolution ? "ColorRequestResolution"
            : a.MenuRowCount != b.MenuRowCount ? "MenuRowCount"
            : a.SubtitleRowCount != b.SubtitleRowCount ? "SubtitleRowCount"
            : null;
        var divergence = FirstDivergence(
            conclusions[Realization.FastOnly], conclusions[Realization.FastThenSlow]);
        Assert.Equal("ColorRequestResolution", divergence); // 危险点：FastOnly 会把 "Color" 唯一接地到不可点标题
        _output.WriteLine($"first divergence: {divergence}: " +
            $"FastOnly={conclusions[Realization.FastOnly].ColorRequestResolution} vs " +
            $"FastThenSlow={conclusions[Realization.FastThenSlow].ColorRequestResolution}");

        // Runtime 消费同构：全部 realization 的 admitted evidence producer 同一
        foreach (var host in hosts.Values)
            Assert.All(host.LedgerCore.CanonicalRecords.Values.Where(r => r.Kind == IngressKind.Observation),
                r => Assert.Equal("perception.runtime", r.Provenance.Producer));
    }

    /// <summary>
    /// 真实 live one-shot/fast 只读采集路径接入证明：golden-run-v1 真录制
    /// live provider response JSON → 真实 FastPerception + LiveVisionStrategy
    /// （产品 live 策略代码路径，只读）→ 确定性 observations；重复调用逐字
    /// 节一致（determinism anchor，ADR-0020）。无设备环境——不宣称真实采
    /// 集传输臂（Phase 7）。
    /// </summary>
    [Fact]
    public void LiveOneShotFastPath_RealGoldenResponse_DeterministicRealStrategy()
    {
        var root = GoldenPaths.RepoRoot();
        var responsePath = Path.Combine(
            root, GoldenPaths.BundleRoot, "perception", "case-b-off.json");
        Assert.True(File.Exists(responsePath), $"golden 真录制响应缺失: {responsePath}");
        var bytes = File.ReadAllBytes(responsePath);

        var metrics = new RuntimeStageMetrics();
        var perception = new FastPerception("perception.runtime", new LiveVisionStrategy(),
            trace: null, metrics);

        var artifact = RawArtifact.Capture(bytes, new ArtifactMetadata(
            1080, 1920, "artifact", T0, "capture:golden-live"));
        var first = perception.Observe(artifact, UniClaw.Kernel.Evidence.ObservationContext.External);
        var second = perception.Observe(artifact, UniClaw.Kernel.Evidence.ObservationContext.External);

        // 真实录制 live 响应非空且确定性
        Assert.NotEmpty(first);
        Assert.Equal(
            first.Select(o => (o.Claim.Subject, o.Claim.Value)),
            second.Select(o => (o.Claim.Subject, o.Claim.Value)));

        // 契约形态：yolo→ui.detect.*.class + spatial；ocr→ui.text.*
        Assert.Contains(first, o => o.Claim.Subject.StartsWith("ui.detect.", StringComparison.Ordinal)
            && o.Claim.Subject.EndsWith(".class", StringComparison.Ordinal));
        Assert.Contains(first, o => o.Claim.Subject.StartsWith("spatial.artifact.bounds.detect.", StringComparison.Ordinal));
        Assert.Contains(first, o => o.Claim.Subject.StartsWith("ui.text.ocr", StringComparison.Ordinal));

        // 真实策略代码路径确实执行（结构计数非零）
        Assert.True(metrics.Stages.TryGetValue(RuntimeStage.FastPerceptionStrategy, out var stage));
        Assert.Equal(2, stage.Invocations);

        // 空 yolo/ocr（OK_EMPTY）→ 零 observation（missing ≠ observation）——
        // failure ≠ OK_EMPTY 的 producer 侧前提
        var emptyPayload = System.Text.Encoding.UTF8.GetBytes("{\"yolo\":[],\"ocr\":[]}");
        var emptyArtifact = RawArtifact.Capture(emptyPayload, new ArtifactMetadata(
            1080, 1920, "artifact", T0, "capture:empty"));
        Assert.Empty(perception.Observe(emptyArtifact,
            UniClaw.Kernel.Evidence.ObservationContext.External));

        _output.WriteLine($"live-path: observations={first.Count} " +
            $"strategyInvocations={stage.Invocations} artifact={artifact.ArtifactId}");
    }
}
