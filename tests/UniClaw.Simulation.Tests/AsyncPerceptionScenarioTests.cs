using UniClaw.Kernel.Effects;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// UAP-001 — Phase 4 统一异步 Perception 八组场景（RED→GREEN）。
///
/// 纪律：测试只经合法面驱动（AdmitContract → Activate → DriveOnce /
/// Feed 虚拟时间推进），只读 owner 投影断言；零 sleep 竞态（全部虚拟时
/// 间）、零直接注入期望 owner state（观察只经 feed 的 NextInput seam 以
/// ObservationProposal 进入 P2）。
/// </summary>
public sealed class AsyncPerceptionScenarioTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
    private readonly ITestOutputHelper _output;

    public AsyncPerceptionScenarioTests(ITestOutputHelper output) => _output = output;

    // ---- 共享 helpers ----

    private static TruthClassificationFacts Facts() => AsyncPerceptionTruth.Load();

    private static AsyncPerceptionTruth.ElementSpec Element(
        TruthClassificationFacts facts, string page, string text) =>
        AsyncPerceptionTruth.ElementsOfPage(facts, page).Single(e => e.Text == text);

    private static string FastPred(TruthClassificationFacts facts, AsyncPerceptionTruth.ElementSpec element) =>
        AsyncPerceptionTruth.PredictedTypeFor(facts, element.Page, element.Text, element.TruthType);

    private static string TruthTypingSubject(AsyncPerceptionTruth.ElementSpec element) =>
        $"ui.typing.{element.ElementId}";

    /// <summary>契约（tap 场景）：MaterialEffect 义务由 post-action state claim 清偿。</summary>
    private static ExecutionContract TapContract(
        IEnumerable<AsyncPerceptionTruth.ElementSpec> elements,
        string obligationSubject,
        string requiredValue = "true",
        params string[] extraScope) => new(
        "uap-v1",
        "open-target-menu",
        AsyncRealizations.ScopeOf(elements, extraScope.Append(obligationSubject).ToArray()),
        new HashSet<string> { "tap" },
        new HashSet<string>(),
        new[] { "obj-open" },
        new[]
        {
            new RunObligation(
                "open", RunObligationKind.MaterialEffect, obligationSubject, requiredValue, Mandatory: true),
        });

    private static ScriptedOperation InitialOp(
        IReadOnlyList<AsyncPerceptionTruth.ElementSpec> scopeElements,
        DateTimeOffset openedAt,
        string captureArtifactId,
        IReadOnlyList<ScheduledResult> schedule,
        string operationKey = "initial-1") => new(
        ObservationContext.External,
        operationKey,
        "initial-observation",
        scopeElements.Select(e => e.ElementId).ToList(),
        TimeSpan.FromSeconds(30),
        openedAt,
        captureArtifactId,
        schedule);

    private static ScriptedOperation PostActionOp(
        IReadOnlyList<AsyncPerceptionTruth.ElementSpec> scopeElements,
        DateTimeOffset openedAt,
        string captureArtifactId,
        IReadOnlyList<ScheduledResult> schedule,
        string operationKey = "post-1") => new(
        ObservationContext.PostActionEffectFlow,
        operationKey,
        "post-action-observation",
        scopeElements.Select(e => e.ElementId).ToList(),
        TimeSpan.FromSeconds(30),
        openedAt,
        captureArtifactId,
        schedule);

    private static AsyncPerceptionHost Activate(AsyncScenario scenario)
    {
        var host = new AsyncPerceptionHost(scenario);
        Assert.True(host.KernelCore.AdmitContract(scenario.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        return host;
    }


    /// <summary>内容派生 capture id（typed 元素集 → provider response bytes → id）。</summary>
    private static string CaptureIdFor(IEnumerable<TypedElement> typed) =>
        AsyncRealizations.CaptureIdOf(AsyncPerceptionTruth.BuildProviderResponse(
            typed.Select(t => (t.Element, t.AssumedType))));

    private static double CenterY(AsyncPerceptionTruth.ElementSpec element) =>
        AsyncPerceptionTruth.CenterYOf(element.Cy);

    // =====================================================================
    // ⑧-0 真值分类核对：static_title ≠ section_label ≠ row_subtitle ≠
    // row_title；预测文件的历史错误如实加载；不可暗中合并。
    // =====================================================================

    [Fact]
    public void S0_TruthClassification_StaticsAndSubtitlesDistinct_NotMerged()
    {
        var facts = Facts();

        // canonical truth（type-truth.json）：四类互异
        var color = facts.Truth.Single(e => e.Page == "display-child" && e.Text == "Color");
        var colors = facts.Truth.Single(e => e.Page == "display-child" && e.Text == "Colors");
        var settings = facts.Truth.Single(e => e.Page == "root-top" && e.Text == "Settings");
        var subtitle = facts.Truth.Single(e => e.Page == "root-top" && e.Text == "Mobile, Wi‑Fi, hotspot");
        var network = facts.Truth.Single(e => e.Page == "root-top" && e.Text == "Network & internet");

        Assert.Equal("static_title", color.TruthType);
        Assert.False(color.Clickable);
        Assert.Equal("row_title", colors.TruthType);
        Assert.True(colors.Clickable);
        Assert.Equal("section_label", settings.TruthType);
        Assert.False(settings.Clickable);
        Assert.Equal("row_subtitle", subtitle.TruthType);
        Assert.Equal("row_title", network.TruthType);

        // 分类差异显式断言：static_title 与 section_label 是两个真值类
        Assert.NotEqual(color.TruthType, settings.TruthType);
        var roles = new[] { color.TruthType, settings.TruthType, subtitle.TruthType, colors.TruthType }
            .Select(AsyncPerceptionTruth.RoleForType)
            .ToHashSet();
        Assert.Equal(4, roles.Count); // 四类 → 四个互异 role，不合并

        // dual probe 的 3-class truth 列与 canonical 4-class truth 存在粒度差
        // （Color 在 dual 中记 section_label，canonical 为 static_title）——
        // 显式记录差异，fixture 以 canonical 为准，不暗中合并。
        var colorDual = facts.Predictions.Single(p => p.Page == "display-child" && p.Target == "Color");
        Assert.Equal("section_label", colorDual.Truth);
        Assert.NotEqual(colorDual.Truth, color.TruthType);

        // 历史预测错误如实加载（录制错误臂输入）
        Assert.Equal("row_title", colorDual.Pred); // 分组标题误判 row_title
        Assert.Equal("row_title",
            facts.Predictions.Single(p => p.Page == "root-top" && p.Target == "Mobile, Wi‑Fi, hotspot").Pred);
        Assert.Equal("row_title",
            facts.Predictions.Single(p => p.Page == "display-child" && p.Target == "Colors").Pred);
    }

    // =====================================================================
    // ① Fast 漏目标 → 定向 Slow 找回（omission ≠ absence）
    // =====================================================================

    [Fact]
    public void S1_FastMissesTarget_TargetedSlowRecovers_OneEffectOnRealTarget()
    {
        var facts = Facts();
        var network = Element(facts, "root-top", "Network & internet");
        var others = new[]
        {
            Element(facts, "root-top", "Settings"),
            Element(facts, "root-top", "Search settings"),
            Element(facts, "root-top", "Connected devices"),
            Element(facts, "root-top", "Bluetooth, pairing"),
        };
        var scope = others.Append(network).ToList();

        // capture = fast 覆盖集的 provider response（内容寻址；漏 network 行）
        var fastTyped = others.Select(e => new TypedElement(e, FastPred(facts, e))).ToList();
        var fastResponse = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));
        var captureId = AsyncRealizations.CaptureIdOf(fastResponse);

        var initial = InitialOp(scope, T0, captureId, Array.Empty<ScheduledResult>());
        var fastResult = AsyncRealizations.ModelStageResult(
            initial, "res-fast", "fast", T0.AddSeconds(1), IsFinal: false, fastTyped);
        var slowResult = AsyncRealizations.RecordedStageResult(
            initial, "res-slow-targeted", "targeted-slow", T0.AddSeconds(8), IsFinal: true,
            new[] { new TypedElement(network, network.TruthType) });

        var post = PostActionOp(scope, T0.AddSeconds(30), CaptureIdFor(scope.Select(e => new TypedElement(e, e.TruthType))),
            Array.Empty<ScheduledResult>());
        var postResult = AsyncRealizations.RecordedStageResult(
            post, "res-post", "slow", T0.AddSeconds(33), IsFinal: true,
            scope.Select(e => new TypedElement(e, e.TruthType)).ToList(),
            new[] { ("page.network.opened", "true") });

        var host = Activate(new AsyncScenario(
            "s1-fast-miss-targeted-slow",
            T0,
            TapContract(scope, "page.network.opened"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", "Network & internet", "tap", null) },
                "open network settings"),
            new[] { initial with { Schedule = new[] { fastResult, slowResult } },
                    post with { Schedule = new[] { postResult } } }));

        // T0：无结果到达 → 合法等待（driver 不见 fast/slow 标签）
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status);
        // T0+2：fast 到达但 operation 未完成（覆盖缺口）→ 继续等待（hold）
        host.Clock.AdvanceTo(T0.AddSeconds(2));
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status);
        var opSnapshot = host.Feed.Operations.Single(o => o.Purpose == "initial-observation");
        Assert.Equal(ObservationOperationStatus.Pending, opSnapshot.Status);
        Assert.Contains("res-fast", opSnapshot.ArrivedResultIds);
        // T0+8：targeted slow 到达 → 覆盖并集完成 → 合并投递 → act → 等待 post-action
        host.Clock.AdvanceTo(T0.AddSeconds(8));
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status);
        // T0+33：post-action 证据到达 → 验证 → 终态
        host.Clock.AdvanceTo(T0.AddSeconds(33));
        var final = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, final.Status);

        opSnapshot = host.Feed.Operations.Single(o => o.Purpose == "initial-observation");
        Assert.Equal(ObservationOperationStatus.Complete, opSnapshot.Status);
        Assert.Equal(new[] { "res-fast", "res-slow-targeted" }, opSnapshot.DeliveredResultIds);

        // omission ≠ absence：belief 无任何「目标不存在」负声明（WorldState
        // 只含出现元素的 typing；漏检元素零声明），定向 slow 找回后唯一接地
        var resolution = host.WorldCore.ResolveCurrent(
            new TargetDescriptor("menu.row", "Network & internet"));
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, resolution.Result);
        var occurrence = resolution.Candidates.Single();
        var occurrenceLocator = host.KernelCore.CurrentBelief!.Occurrences!
            .Single(o => o.OccurrenceId == occurrence.OccurrenceId).Locator!;
        Assert.Equal(CenterY(network), Math.Round(occurrenceLocator.CenterY, 6, MidpointRounding.AwayFromZero));

        // 恰一 Effect，且接地到真实行（binding locator 中心 = 真值 cy；
        // occurrence id 是 revision-local 的——act 时 binding 绑定的是决策时
        // revision 的 occurrence，post-action revision 后同指涉换新 id，故
        // 断言以 locator 指涉为准）
        Assert.Equal(1, host.EffectDriver.DeliveryCount);
        var binding = host.EffectBoundaryCore.BindingLog.Single(b => b.Canonical is not null).Canonical!;
        Assert.NotNull(binding.TargetOccurrenceId);
        Assert.Equal(CenterY(network), Math.Round(binding.TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));

        // Runtime 面零 fast/slow 泄漏：全部 admitted evidence 的 producer 同一
        Assert.All(host.LedgerCore.CanonicalRecords.Values.Where(r => r.Kind == IngressKind.Observation),
            r => Assert.Equal("perception.runtime", r.Provenance.Producer));

        Assert.Equal(TerminalClassification.Completion, final.Outcome!.Classification);
        _output.WriteLine($"S1: revisions={host.WorldCore.RevisionHistory.Count} " +
            $"canonical={host.LedgerCore.CanonicalRecords.Count} " +
            $"effects={host.EffectDriver.DeliveryCount} " +
            $"deliveryLatency={opSnapshot.VirtualDeliveryLatency}");
    }

    // =====================================================================
    // ② Fast 误判状态 → decision 前纠正；Fast-only 对照 fail closed；
    //    旧 revision 绑定失效。
    // =====================================================================

    [Fact]
    public void S2a_FastMisjudgesState_SlowCorrectsBeforeDecision()
    {
        var facts = Facts();
        var colors = Element(facts, "display-child", "Colors");
        var colorHeader = Element(facts, "display-child", "Color");
        var contrast = Element(facts, "display-child", "Color contrast");
        var scope = new[] { colorHeader, colors, contrast };

        var fastTyped = scope.Select(e => new TypedElement(e, FastPred(facts, e), State: "enabled")).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));
        var captureId = AsyncRealizations.CaptureIdOf(response);

        var initial = InitialOp(scope, T0, captureId, Array.Empty<ScheduledResult>());
        // fast 误判：Colors 状态 enabled（真相 disabled）→ 非最终
        var fastResult = AsyncRealizations.ModelStageResult(
            initial, "res-fast", "fast", T0.AddSeconds(1), IsFinal: false, fastTyped,
            new[] { ("ui.state.colors", "enabled") });
        // slow 纠正（全 scope 覆盖）：状态 disabled + 正确 typing → 最终
        var slowTyped = scope.Select(e => new TypedElement(
            e, e.TruthType, State: e.Text == colors.Text ? "disabled" : null)).ToList();
        var slowResult = AsyncRealizations.RecordedStageResult(
            initial, "res-slow", "targeted-slow", T0.AddSeconds(6), IsFinal: true, slowTyped,
            new[] { ("ui.state.colors", "disabled") });

        var postTyped = new[] { new TypedElement(colors, colors.TruthType, State: "enabled") };
        var post = PostActionOp(new[] { colors }, T0.AddSeconds(30), CaptureIdFor(postTyped),
            Array.Empty<ScheduledResult>());
        var postResult = AsyncRealizations.RecordedStageResult(
            post, "res-post", "slow", T0.AddSeconds(32), IsFinal: true, postTyped,
            new[] { ("ui.state.colors", "enabled") });

        var host = Activate(new AsyncScenario(
            "s2a-state-misjudge-corrected",
            T0,
            TapContract(scope, "ui.state.colors", requiredValue: "enabled"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", "Colors", "tap", "enabled") },
                "enable colors row"),
            new[] { initial with { Schedule = new[] { fastResult, slowResult } },
                    post with { Schedule = new[] { postResult } } }));

        host.Clock.AdvanceTo(T0.AddSeconds(6));
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status); // act 已发，等 post
        host.Clock.AdvanceTo(T0.AddSeconds(32));
        var final = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, final.Status);

        // 纠正在 decision boundary 之前：agent consultation 上下文含纠正值
        var call = host.ScriptedAgent.Calls.Single();
        Assert.Equal("disabled", call.CurrentWorldClaims["ui.state.colors"]);
        // 纠正后 desired-state 不满足 → 执行动作（而非被误判 skip）
        Assert.Equal(1, host.EffectDriver.DeliveryCount);
        // 同流再观察 = Revise（非 Conflict）
        Assert.Contains(host.WorldCore.ClaimEvolutionLog,
            d => d.Subject == "ui.state.colors" && d.Kind == ClaimEvolutionKind.Revise);
        Assert.Equal(TerminalClassification.Completion, final.Outcome!.Classification);
    }

    [Fact]
    public void S2b_FastOnlyContrast_UncorrectedMisjudge_FailsClosed_NoCompletion()
    {
        var facts = Facts();
        var colors = Element(facts, "display-child", "Colors");
        var colorHeader = Element(facts, "display-child", "Color");
        var contrast = Element(facts, "display-child", "Color contrast");
        var scope = new[] { colorHeader, colors, contrast };

        // Fast-only realization：fast 即最终——误判状态无人纠正
        var fastTyped = scope.Select(e => new TypedElement(e, FastPred(facts, e), State: "enabled")).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));
        var initial = InitialOp(scope, T0, AsyncRealizations.CaptureIdOf(response), Array.Empty<ScheduledResult>());
        var fastResult = AsyncRealizations.ModelStageResult(
            initial, "res-fast", "fast", T0.AddSeconds(1), IsFinal: true, fastTyped,
            new[] { ("ui.state.colors", "enabled") });

        var host = Activate(new AsyncScenario(
            "s2b-fast-only-misjudge",
            T0,
            TapContract(scope, "ui.state.colors", requiredValue: "enabled"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", "Colors", "tap", "enabled") },
                "enable colors row"),
            new[] { initial with { Schedule = new[] { fastResult } } }));

        host.Clock.AdvanceTo(T0.AddSeconds(1));
        var result = host.DriveOnce();

        // 误判 enabled → desired-state 已满足 → policy 不签发 act → fail closed
        Assert.Equal(RunDriveStatus.GroundingFailed, result.Status);
        Assert.Equal("control-issued-non-act-intent", result.Reason);
        Assert.Equal(0, host.EffectDriver.DeliveryCount);
        // MaterialEffect 义务无 post-action 证据 → 不得伪装 Completion
        Assert.False(host.KernelCore.IsRunTerminal);
        Assert.Null(result.Outcome);
    }

    [Fact]
    public void S2c_BindingFromEarlierRevision_InvalidAfterCorrectionRevision()
    {
        var facts = Facts();
        var colors = Element(facts, "display-child", "Colors");
        var colorHeader = Element(facts, "display-child", "Color");
        var contrast = Element(facts, "display-child", "Color contrast");
        var scope = new[] { colorHeader, colors, contrast };

        var fastTyped = scope.Select(e => new TypedElement(e, FastPred(facts, e))).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));
        var captureId = AsyncRealizations.CaptureIdOf(response);
        var initial = InitialOp(scope, T0, captureId, Array.Empty<ScheduledResult>());
        var fastResult = AsyncRealizations.ModelStageResult(
            initial, "res-fast", "fast", T0.AddSeconds(1), IsFinal: false, fastTyped);
        var slowCorrected = AsyncRealizations.RecordedStageResult(
            initial, "res-slow", "targeted-slow", T0.AddSeconds(5), IsFinal: true,
            scope.Select(e => new TypedElement(e, e.TruthType)).ToList());

        var truthTyped = scope.Select(e => new TypedElement(e, e.TruthType)).ToList();
        var post1 = PostActionOp(scope, T0.AddSeconds(30), captureId, Array.Empty<ScheduledResult>(),
            operationKey: "post-1");
        var post1Fast = AsyncRealizations.ModelStageResult(
            post1, "res-post-fast", "fast", T0.AddSeconds(31), IsFinal: false, fastTyped);
        var post1Slow = AsyncRealizations.RecordedStageResult(
            post1, "res-post-slow", "targeted-slow", T0.AddSeconds(34), IsFinal: true, truthTyped,
            new[] { ("page.colors.opened", "true") });
        var post2 = PostActionOp(scope, T0.AddSeconds(60), CaptureIdFor(truthTyped), Array.Empty<ScheduledResult>(),
            operationKey: "post-2");
        var post2Result = AsyncRealizations.RecordedStageResult(
            post2, "res-post2", "slow", T0.AddSeconds(62), IsFinal: true, truthTyped,
            new[] { ("page.contrast.opened", "true") });

        var host = Activate(new AsyncScenario(
            "s2c-stale-binding",
            T0,
            TapContract(scope, "page.colors.opened", extraScope: "page.contrast.opened"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[]
                {
                    new ScriptActionStep("menu.row", "Colors", "tap", null),
                    new ScriptActionStep("menu.row", "Color contrast", "tap", null),
                },
                "two taps"),
            new[]
            {
                initial with { Schedule = new[] { fastResult, slowCorrected } },
                post1 with { Schedule = new[] { post1Fast, post1Slow } },
                post2 with { Schedule = new[] { post2Result } },
            }));

        host.Clock.AdvanceTo(T0.AddSeconds(5));
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status); // act1 完成，等 post1
        host.Clock.AdvanceTo(T0.AddSeconds(34));
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status); // verify1 过，act2 完成，等 post2
        host.Clock.AdvanceTo(T0.AddSeconds(62));
        var final = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, final.Status);

        // 两个 binding 各自 fresh grounding（不同 revision、不同 occurrence）
        var canonicals = host.EffectBoundaryCore.BindingLog
            .Where(b => b.Canonical is not null)
            .Select(b => b.Canonical!)
            .ToList();
        Assert.Equal(2, canonicals.Count);
        Assert.NotEqual(canonicals[0].TargetOccurrenceId, canonicals[1].TargetOccurrenceId);
        Assert.NotEqual(canonicals[0].RevisionId, canonicals[1].RevisionId);

        // 旧 revision 的 occurrence 在纠正后的 current revision 中不复存在：
        // stale UiTarget → DeriveBindingView fail closed
        var staleView = host.WorldCore.DeriveBindingView(
            "menu.row:Colors", canonicals[0].TargetOccurrenceId);
        Assert.False(staleView.HasTargetOccurrence);

        // 两步各自落真实目标（中心 y 与真值一致）
        Assert.Equal(CenterY(colors), Math.Round(canonicals[0].TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));
        Assert.Equal(CenterY(contrast), Math.Round(canonicals[1].TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));
        Assert.Equal(2, host.EffectDriver.DeliveryCount);
    }

    // =====================================================================
    // ③ 补充性 Slow：accepted Evidence 可不增 revision（irrelevant /
    // exact duplicate）；basis/冲突/不确定性变化 → 新 revision。
    // =====================================================================

    [Fact]
    public void S3a_IrrelevantAndExactDuplicateEvidence_DoNotGrowRevision()
    {
        var facts = Facts();
        var colors = Element(facts, "display-child", "Colors");
        var colorHeader = Element(facts, "display-child", "Color");
        var scope = new[] { colorHeader, colors };

        var typedA = new[] { new TypedElement(colorHeader, colorHeader.TruthType) };
        var typedB = new[] { new TypedElement(colors, colors.TruthType) };
        var initial = InitialOp(scope, T0, CaptureIdFor(typedA.Concat(typedB).ToList()), Array.Empty<ScheduledResult>());

        // rA（部分覆盖，t1，携带 scope 外 subject 的补充 proposal）；
        // rB（最终覆盖，t2）→ 完成事件发生在 t2。
        // 精确重复：rA 的逐字节重发（不同 ResultId）在 t1.5 到达——
        // **完成事件之前**（D13：完成后到达的重发只记 Duplicates，不再
        // 入账）。两轮投递的 proposals 逐字节相同（record `with` 共享
        // Proposals 引用，lineage 一致）→ 同 EvidenceId → ledger 幂等。
        var rA = AsyncRealizations.RecordedStageResult(
            initial, "res-main-partial", "slow", T0.AddSeconds(1), IsFinal: false, typedA,
            new[] { ("ui.text.ocr7", "irrelevant-out-of-scope") });
        var rARetransmit = rA with { ResultId = "res-main-retransmit", ArrivalTime = T0.AddSeconds(1.5) };
        var rB = AsyncRealizations.RecordedStageResult(
            initial, "res-main-final", "slow", T0.AddSeconds(2), IsFinal: true, typedB);

        var host = Activate(new AsyncScenario(
            "s3a-no-revision-growth",
            T0,
            TapContract(scope, "page.noop"),
            new AgentScriptStep(AgentScriptKind.NoAction, Array.Empty<ScriptActionStep>(), "inspect"),
            new[] { initial with { Schedule = new[] { rA, rARetransmit, rB } } }));

        host.Clock.AdvanceTo(T0.AddSeconds(3));
        Assert.Equal(RunDriveStatus.TerminalNotProven, host.DriveOnce().Status); // 无义务证据

        // irrelevant：admission accepted + relevance=false → 零 revision 贡献
        //（rA 与逐字节重发各判一次——同 EvidenceId 幂等）
        var irrelevantRecord = host.LedgerCore.CanonicalRecords.Values
            .Single(r => r.Claim.Subject == "ui.text.ocr7");
        var irrelevantJudgments = host.WorldCore.RelevanceLog
            .Where(j => j.EvidenceId == irrelevantRecord.EvidenceId).ToList();
        Assert.Equal(2, irrelevantJudgments.Count);
        Assert.All(irrelevantJudgments, j => Assert.False(j.IsRelevant));

        // exact duplicate（完成事件前的逐字节重发）：8 次 proposal 投递 →
        // 5 条 canonical（重发全幂等）；4 个 revision（rA: frame+typing；
        // 重发: 零新增；rB: frame Revise+typing）
        Assert.Equal(8, host.LedgerCore.AdmissionLog.Count(a => a.Decision == AdmissionDecision.Accepted));
        Assert.Equal(5, host.LedgerCore.CanonicalRecords.Count);
        Assert.Equal(4, host.WorldCore.RevisionHistory.Count);

        _output.WriteLine($"S3a: revisions={host.WorldCore.RevisionHistory.Count} " +
            $"canonical={host.LedgerCore.CanonicalRecords.Count} " +
            $"admissions={host.LedgerCore.AdmissionLog.Count}");
    }

    [Fact]
    public void S3b_BasisConflictUncertaintyChange_GrowsRevision()
    {
        var facts = Facts();
        var colors = Element(facts, "display-child", "Colors");
        var colorHeader = Element(facts, "display-child", "Color");
        var scope = new[] { colorHeader, colors };

        var fastTyped = scope.Select(e => new TypedElement(e, FastPred(facts, e))).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));
        var initial = InitialOp(scope, T0, AsyncRealizations.CaptureIdOf(response), Array.Empty<ScheduledResult>());

        // ① fast：Color→row_title（误）；Colors→row_title
        var fast = AsyncRealizations.ModelStageResult(
            initial, "res-fast", "fast", T0.AddSeconds(1), IsFinal: false, fastTyped);
        // ② slow 同值再观察（同 producer 异 scope）：visible claim 不变、
        //    basis 增长 → 新 revision（Reaffirm 留痕）
        var slowSameValue = AsyncRealizations.RecordedStageResult(
            initial, "res-slow-same", "slow", T0.AddSeconds(4), IsFinal: false, fastTyped);
        // ③ review（异 producer）异值挑战 Color typing → Conflict + Uncertainty↑
        var reviewChallenge = AsyncRealizations.RecordedStageResult(
            initial, "res-review", "review", T0.AddSeconds(6), IsFinal: true,
            scope.Select(e => new TypedElement(e, e.TruthType)).ToList(),
            producer: AsyncRealizations.ProducerReview);

        var host = Activate(new AsyncScenario(
            "s3b-revision-growth",
            T0,
            TapContract(scope, "page.noop"),
            new AgentScriptStep(AgentScriptKind.NoAction, Array.Empty<ScriptActionStep>(), "inspect"),
            new[] { initial with { Schedule = new[] { fast, slowSameValue, reviewChallenge } } }));

        host.Clock.AdvanceTo(T0.AddSeconds(7));
        Assert.Equal(RunDriveStatus.TerminalNotProven, host.DriveOnce().Status);

        var revisions = host.WorldCore.RevisionHistory;
        Assert.True(revisions.Count >= 3);

        // basis 增长产生新 revision：同一 visible claim 值不变但 basis 跨
        // revision 增长（Reaffirm 留痕）
        var current = host.KernelCore.CurrentBelief!;
        Assert.Equal("row_title", current.WorldState[TruthTypingSubject(colorHeader)].Value);
        Assert.Contains(host.WorldCore.ClaimEvolutionLog,
            d => d.Subject == TruthTypingSubject(colorHeader) && d.Kind == ClaimEvolutionKind.Reaffirm);

        // 跨 producer 异值 → Conflict 保持既存 + Uncertainty 增长（新 revision）
        var conflict = Assert.Single(current.Conflicts, c => c.Subject == TruthTypingSubject(colorHeader));
        Assert.Equal("row_title", conflict.EstablishedValue);
        Assert.Equal("static_title", conflict.ChallengingValue);
        Assert.True(current.Uncertainty.ConflictingClaimCount >= 1);

        _output.WriteLine($"S3b: revisions={revisions.Count} " +
            $"uncertainty={current.Uncertainty.ConflictingClaimCount} conflicts={current.Conflicts.Count}");
    }

    // =====================================================================
    // ④ 旧 capture 的 Slow 迟到（页面/采集推进后）→ Superseded 隔离
    // =====================================================================

    [Fact]
    public void S4_StaleSlowAfterPageAdvance_Quarantined_ZeroBeliefPollution()
    {
        var facts = Facts();
        var network = Element(facts, "root-top", "Network & internet");
        var settings = Element(facts, "root-top", "Settings");
        var colors = Element(facts, "display-child", "Colors");
        var scopeA = new[] { settings, network };
        var scopeB = new[] { colors };
        var allScope = scopeA.Concat(scopeB).ToList();

        // capture A（旧页）：fast 部分覆盖；slow 迟到（t+20）
        var fastTypedA = new[] { new TypedElement(settings, settings.TruthType) };
        var responseA = AsyncPerceptionTruth.BuildProviderResponse(
            fastTypedA.Select(t => (t.Element, t.AssumedType)));
        var captureA = AsyncRealizations.CaptureIdOf(responseA);
        var opA = InitialOp(scopeA, T0, captureA, Array.Empty<ScheduledResult>());
        var fastA = AsyncRealizations.ModelStageResult(
            opA, "res-a-fast", "fast", T0.AddSeconds(1), IsFinal: false, fastTypedA);
        var slowA = AsyncRealizations.RecordedStageResult(
            opA, "res-a-slow", "slow", T0.AddSeconds(20), IsFinal: true,
            new[] { new TypedElement(network, network.TruthType) });

        // capture B（新页）：完整覆盖
        var typedB = scopeB.Select(e => new TypedElement(e, e.TruthType)).ToList();
        var responseB = AsyncPerceptionTruth.BuildProviderResponse(
            typedB.Select(t => (t.Element, t.AssumedType)));
        var captureB = AsyncRealizations.CaptureIdOf(responseB);
        var opB = InitialOp(scopeB, T0.AddSeconds(3), captureB, Array.Empty<ScheduledResult>(),
            operationKey: "initial-2");
        var fastB = AsyncRealizations.ModelStageResult(
            opB, "res-b-fast", "fast", T0.AddSeconds(5), IsFinal: true, typedB);

        var post = PostActionOp(scopeB, T0.AddSeconds(30), captureB, Array.Empty<ScheduledResult>());
        var postResult = AsyncRealizations.RecordedStageResult(
            post, "res-post", "slow", T0.AddSeconds(32), IsFinal: true, typedB,
            new[] { ("page.colors.opened", "true") });

        var host = Activate(new AsyncScenario(
            "s4-stale-slow",
            T0,
            TapContract(allScope, "page.colors.opened"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", "Colors", "tap", null) },
                "open colors"),
            new[] { opA with { Schedule = new[] { fastA, slowA } },
                    opB with { Schedule = new[] { fastB } },
                    post with { Schedule = new[] { postResult } } }));

        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status);
        host.Clock.AdvanceTo(T0.AddSeconds(2));
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status); // opA partial hold

        // 页面推进：宿主隔离 opA（Superseded）；迟到 slow 归入 stale 隔离
        host.Feed.SupersedePending();
        host.Clock.AdvanceTo(T0.AddSeconds(20));
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status); // capture B act 完成，等 post
        host.Clock.AdvanceTo(T0.AddSeconds(32));
        var final = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, final.Status);

        var staleOp = host.Feed.Operations.Single(o => o.CaptureArtifactId == captureA);
        Assert.Equal(ObservationOperationStatus.Superseded, staleOp.Status);
        Assert.Contains("res-a-slow", staleOp.StaleQuarantinedResultIds);
        Assert.Empty(staleOp.DeliveredResultIds);

        // 零 belief 污染：无任何 capture A 证据被 admit（hold 期即被隔离）
        Assert.All(host.LedgerCore.CanonicalRecords.Values,
            r => Assert.DoesNotContain($"capture:{captureA}", r.Provenance.TransformationLineage));
        Assert.False(host.KernelCore.CurrentBelief!.WorldState.ContainsKey(TruthTypingSubject(network)));

        // 负对照（Review 建议）：不隔离的平行臂中 capture A 证据确实可见——
        // 证明上方 DoesNotContain 断言具备检出能力（非构造性通过）
        var controlHost = Activate(new AsyncScenario(
            "s4-negative-control",
            T0,
            TapContract(allScope, "page.colors.opened"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", "Colors", "tap", null) },
                "open colors"),
            new[] { opA with { Schedule = new[] { fastA, slowA } } }));
        controlHost.Clock.AdvanceTo(T0.AddSeconds(20));
        controlHost.DriveOnce(); // opA 完成（无隔离）→ capture A 证据入账
        Assert.Contains(controlHost.LedgerCore.CanonicalRecords.Values,
            r => r.Provenance.TransformationLineage.Contains($"capture:{captureA}"));

        // 动作只来自 capture B 流：恰一 effect、接地 Colors 真实行
        Assert.Equal(1, host.EffectDriver.DeliveryCount);
        var binding = host.EffectBoundaryCore.BindingLog.Single(b => b.Canonical is not null).Canonical!;
        Assert.Equal(CenterY(colors), Math.Round(binding.TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));
    }

    // =====================================================================
    // ⑤ 同 capture 乱序 + 精确重复投递 → 确定性合并、不重复动作
    // =====================================================================

    [Fact]
    public void S5_OutOfOrderAndExactDuplicate_Deterministic_NoDuplicateAction()
    {
        var facts = Facts();
        var colors = Element(facts, "display-child", "Colors");
        var colorHeader = Element(facts, "display-child", "Color");
        var contrast = Element(facts, "display-child", "Color contrast");
        var scope = new[] { colorHeader, colors, contrast };
        var typed = scope.Select(e => new TypedElement(e, e.TruthType)).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            typed.Select(t => (t.Element, t.AssumedType)));
        var captureId = AsyncRealizations.CaptureIdOf(response);

        AsyncPerceptionHost Run(bool treatment)
        {
            var initial = InitialOp(scope, T0, captureId, Array.Empty<ScheduledResult>());
            var r1 = AsyncRealizations.RecordedStageResult(
                initial, "res-r1", "slow", T0.AddSeconds(treatment ? 5 : 3), IsFinal: false,
                new[] { typed[0] });
            var r2 = AsyncRealizations.RecordedStageResult(
                initial, "res-r2", "slow", T0.AddSeconds(treatment ? 3 : 5), IsFinal: true,
                typed.Skip(1).ToList());

            var schedule = new List<ScheduledResult> { r1, r2 };
            if (treatment)
            {
                schedule.Add(r2 with { ResultId = "res-r2-retransmit" }); // 精确重复（异 ResultId）
                schedule.Add(r1);                                        // 同 ResultId 重投
            }

            var post = PostActionOp(scope, T0.AddSeconds(30), CaptureIdFor(typed), Array.Empty<ScheduledResult>());
            var postResult = AsyncRealizations.RecordedStageResult(
                post, "res-post", "slow", T0.AddSeconds(32), IsFinal: true, typed,
                new[] { ("page.colors.opened", "true") });

            var host = Activate(new AsyncScenario(
                treatment ? "s5-treatment" : "s5-control",
                T0,
                TapContract(scope, "page.colors.opened"),
                new AgentScriptStep(
                    AgentScriptKind.Act,
                    new[] { new ScriptActionStep("menu.row", "Colors", "tap", null) },
                    "open colors"),
                new[] { initial with { Schedule = schedule },
                        post with { Schedule = new[] { postResult } } }));

            host.Clock.AdvanceTo(T0.AddSeconds(10));
            Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status);
            host.Clock.AdvanceTo(T0.AddSeconds(32));
            Assert.Equal(RunDriveStatus.Completed, host.DriveOnce().Status);
            return host;
        }

        var controlHost = Run(false);
        var treatmentHost = Run(true);

        // 同 ResultId 重投：feed 去重（deliver 一次）；异 ResultId 精确重复：
        // ledger EvidenceId 幂等 → 世界结论一致
        var treatmentSnapshot = treatmentHost.Feed.Operations
            .Single(o => o.Context == ObservationContext.External);
        Assert.Contains("res-r1", treatmentSnapshot.DuplicateResultIds);

        // 规范化排除 order/identity 派生 subjects：container id 由每 Run 首个
        // 处理证据派生（control/treatment 首达结果不同 → 合法不同）；
        // live.frame claim 值 = 最后到达帧的字符串（D9 合并景观与之解耦）。
        bool SemanticSubject(string key) =>
            key != "live.frame"
            && key != "ui.container.current"
            && key.StartsWith("ui.container.signature.", StringComparison.Ordinal) == false;
        List<(string Key, string Value)> NormalizedClaims(AsyncPerceptionHost host) =>
            host.KernelCore.CurrentBelief!.WorldState
                .Where(kv => SemanticSubject(kv.Key))
                .OrderBy(kv => kv.Key)
                .Select(kv => (kv.Key, kv.Value.Value))
                .ToList();
        List<(string Role, string? Descriptor, double CenterY)> NormalizedOccurrences(AsyncPerceptionHost host) =>
            host.KernelCore.CurrentBelief!.Occurrences!
                .Select(o => (o.Role, o.SemanticDescriptor,
                    Math.Round(o.Locator!.CenterY, 6, MidpointRounding.AwayFromZero)))
                .OrderBy(t => t.SemanticDescriptor, StringComparer.Ordinal)
                .ToList();
        Assert.Equal(NormalizedClaims(controlHost), NormalizedClaims(treatmentHost));
        Assert.Equal(NormalizedOccurrences(controlHost), NormalizedOccurrences(treatmentHost));

        // 不重复动作：两臂都恰一 effect、同一真实目标
        foreach (var host in new[] { controlHost, treatmentHost })
        {
            Assert.Equal(1, host.EffectDriver.DeliveryCount);
            var binding = host.EffectBoundaryCore.BindingLog.Single(b => b.Canonical is not null).Canonical!;
            Assert.Equal(CenterY(colors), Math.Round(binding.TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));
        }
    }

    // =====================================================================
    // ⑤b 拉取节奏不变性（事件时间完成语义；Human 复审要求 2026-09-14）：
    // 同一 operation、同一批 ArrivalTime——slow final @t2 已满足 coverage，
    // 错误 fast @t6 才到。无论 driver 在 t3 还是 t6 拉取，必须交付同一
    // 批次、得到同一规范化世界结论；t6 的结果作为完成后迟到结果隔离。
    // 另加「完成后才到的 failure」反例：不得把已完成 op 翻转为 Failed。
    // =====================================================================

    [Fact]
    public void S5b_EventTimeCompletion_PullCadenceInvariant_LateArrivalsQuarantined()
    {
        var facts = Facts();
        var colorHeader = Element(facts, "display-child", "Color");
        var colors = Element(facts, "display-child", "Colors");
        var contrast = Element(facts, "display-child", "Color contrast");
        var scope = new[] { colorHeader, colors, contrast };
        var fastTyped = scope.Select(e => new TypedElement(e, FastPred(facts, e))).ToList();
        var truthTyped = scope.Select(e => new TypedElement(e, e.TruthType)).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));
        var captureId = AsyncRealizations.CaptureIdOf(response);

        AsyncPerceptionHost RunCadence(bool pullAtSix, bool withLateFailure)
        {
            var initial = InitialOp(scope, T0, captureId, Array.Empty<ScheduledResult>());
            // slow final @t2：truth typing 全覆盖 → 完成事件发生在 t2
            var slowFinal = AsyncRealizations.RecordedStageResult(
                initial, "res-slow", "targeted-slow", T0.AddSeconds(2), IsFinal: true, truthTyped);
            // 错误 fast @t6：完成后迟到（不得进入交付批、不得覆写纠正）
            var lateFast = AsyncRealizations.ModelStageResult(
                initial, "res-fast-late", "fast", T0.AddSeconds(6), IsFinal: false, fastTyped);
            var schedule = new List<ScheduledResult> { slowFinal, lateFast };
            if (withLateFailure)
                schedule.Add(AsyncRealizations.FailureResult(
                    initial, "res-failure-late", "fast", T0.AddSeconds(5), "late transport failure"));

            var post = PostActionOp(scope, T0.AddSeconds(30), CaptureIdFor(truthTyped),
                Array.Empty<ScheduledResult>());
            var postResult = AsyncRealizations.RecordedStageResult(
                post, "res-post", "slow", T0.AddSeconds(32), IsFinal: true, truthTyped,
                new[] { ("page.colors.opened", "true") });

            var host = Activate(new AsyncScenario(
                withLateFailure ? "s5b-cadence-failure" : pullAtSix ? "s5b-cadence-late" : "s5b-cadence-early",
                T0,
                TapContract(scope, "page.colors.opened"),
                new AgentScriptStep(
                    AgentScriptKind.Act,
                    new[] { new ScriptActionStep("menu.row", "Colors", "tap", null) },
                    "open colors"),
                new[] { initial with { Schedule = schedule },
                        post with { Schedule = new[] { postResult } } }));

            host.Clock.AdvanceTo(pullAtSix || withLateFailure ? T0.AddSeconds(6) : T0.AddSeconds(3));
            Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status); // act 完成，等 post
            host.Clock.AdvanceTo(T0.AddSeconds(32));
            Assert.Equal(RunDriveStatus.Completed, host.DriveOnce().Status);
            return host;
        }

        var early = RunCadence(pullAtSix: false, withLateFailure: false); // t3 拉取
        var late = RunCadence(pullAtSix: true, withLateFailure: false);   // t6 拉取
        var withFailure = RunCadence(pullAtSix: true, withLateFailure: true);

        // 交付同一批次：两节奏 DeliveredResultIds 一致（仅 res-slow；
        // 迟到的 res-fast-late 零进入）
        var earlySnapshot = early.Feed.Operations.Single(o => o.Context == ObservationContext.External);
        var lateSnapshot = late.Feed.Operations.Single(o => o.Context == ObservationContext.External);
        Assert.Equal(new[] { "res-slow" }, earlySnapshot.DeliveredResultIds);
        Assert.Equal(earlySnapshot.DeliveredResultIds, lateSnapshot.DeliveredResultIds);
        // 完成事件时间与拉取节奏无关（= slow 到达事件 t2）
        Assert.Equal(T0.AddSeconds(2), earlySnapshot.CompletedAt);
        Assert.Equal(lateSnapshot.CompletedAt, earlySnapshot.CompletedAt);

        // 第 6 秒结果作为完成后迟到结果隔离
        Assert.Contains("res-fast-late", lateSnapshot.StaleQuarantinedResultIds);

        // 同一规范化世界结论（语义 claims + occurrence 景观）
        Assert.Equal(
            early.KernelCore.CurrentBelief!.WorldState
                .OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value)).ToList(),
            late.KernelCore.CurrentBelief!.WorldState
                .OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value)).ToList());
        Assert.Equal(
            early.KernelCore.CurrentBelief!.Occurrences!
                .Select(o => (o.Role, o.SemanticDescriptor,
                    Math.Round(o.Locator!.CenterY, 6, MidpointRounding.AwayFromZero)))
                .OrderBy(t => t.SemanticDescriptor, StringComparer.Ordinal).ToList(),
            late.KernelCore.CurrentBelief!.Occurrences!
                .Select(o => (o.Role, o.SemanticDescriptor,
                    Math.Round(o.Locator!.CenterY, 6, MidpointRounding.AwayFromZero)))
                .OrderBy(t => t.SemanticDescriptor, StringComparer.Ordinal).ToList());

        // 同一安全行为：恰一 Effect、落真实 Colors 行；纠正稳定不被覆写
        foreach (var host in new[] { early, late })
        {
            Assert.Equal(1, host.EffectDriver.DeliveryCount);
            var binding = host.EffectBoundaryCore.BindingLog.Single(b => b.Canonical is not null).Canonical!;
            Assert.Equal(CenterY(colors), Math.Round(binding.TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));
            Assert.Equal("static_title",
                host.KernelCore.CurrentBelief!.WorldState[TruthTypingSubject(colorHeader)].Value);
        }

        // 反例：完成后才到的 failure——op 保持 Complete（不翻转 Failed）、
        // 隔离为迟到、零 admission、世界结论不变
        var failureSnapshot = withFailure.Feed.Operations.Single(o => o.Context == ObservationContext.External);
        Assert.Equal(ObservationOperationStatus.Complete, failureSnapshot.Status);
        Assert.Null(failureSnapshot.FailureReason);
        Assert.Contains("res-failure-late", failureSnapshot.StaleQuarantinedResultIds);
        Assert.Equal(new[] { "res-slow" }, failureSnapshot.DeliveredResultIds);
        Assert.Equal(1, withFailure.EffectDriver.DeliveryCount);
        Assert.Equal(
            early.KernelCore.CurrentBelief!.WorldState
                .OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value)).ToList(),
            withFailure.KernelCore.CurrentBelief!.WorldState
                .OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value)).ToList());
    }

    // =====================================================================
    // ⑥ partial / 真空 / failure / timeout 严格区分；未覆盖 region 不得
    //    推断 absence；failure ≠ OK_EMPTY。
    // =====================================================================

    [Fact]
    public void S6_FourTerminalCompletionStates_StrictlyDistinct()
    {
        var facts = Facts();
        var colors = Element(facts, "display-child", "Colors");
        var colorHeader = Element(facts, "display-child", "Color");
        var scope = new[] { colorHeader, colors };
        var typed = scope.Select(e => new TypedElement(e, e.TruthType)).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            typed.Select(t => (t.Element, t.AssumedType)));

        AsyncScenario ScenarioOf(string id, IReadOnlyList<ScheduledResult> schedule, string? captureOverride = null) =>
            new(id, T0,
                TapContract(scope, "page.noop"),
                new AgentScriptStep(AgentScriptKind.NoAction, Array.Empty<ScriptActionStep>(), "inspect"),
                new[] { InitialOp(scope, T0, captureOverride ?? AsyncRealizations.CaptureIdOf(response), schedule) });

        // (a) partial：只覆盖 color header，预算到期 → PartialAtDeadline
        var partialHost = Activate(ScenarioOf("s6-partial", new[]
        {
            AsyncRealizations.RecordedStageResult(
                InitialOp(scope, T0, AsyncRealizations.CaptureIdOf(response), Array.Empty<ScheduledResult>()),
                "res-partial", "slow", T0.AddSeconds(2), IsFinal: false, new[] { typed[0] }),
        }));

        // (b) 真空：覆盖全 scope 的 final 空帧（显式负观察，≠ failure）；
        //     覆盖声明 = 全 scope（对整帧扫描一无所获）
        var emptyResponse = AsyncPerceptionTruth.BuildProviderResponse(
            Array.Empty<(AsyncPerceptionTruth.ElementSpec, string)>());
        var emptyCapture = AsyncRealizations.CaptureIdOf(emptyResponse);
        var emptyHost = Activate(ScenarioOf("s6-empty", new[]
        {
            AsyncRealizations.ModelStageResult(
                InitialOp(scope, T0, emptyCapture, Array.Empty<ScheduledResult>()),
                "res-empty", "oneshot", T0.AddSeconds(2), IsFinal: true,
                Array.Empty<TypedElement>(),
                coveredRegionsOverride: scope.Select(e => e.ElementId).ToList()),
        }, emptyCapture));

        // (c) failure：producer 失败记录（零 proposal）
        var failHost = Activate(ScenarioOf("s6-failure", new[]
        {
            AsyncRealizations.FailureResult(
                InitialOp(scope, T0, AsyncRealizations.CaptureIdOf(response), Array.Empty<ScheduledResult>()),
                "res-failure", "fast", T0.AddSeconds(2), "provider transport failure"),
        }));

        // (d) timeout：结果排在预算之后
        var timeoutHost = Activate(ScenarioOf("s6-timeout", new[]
        {
            AsyncRealizations.RecordedStageResult(
                InitialOp(scope, T0, AsyncRealizations.CaptureIdOf(response), Array.Empty<ScheduledResult>()),
                "res-late", "slow", T0.AddSeconds(120), IsFinal: true, typed),
        }));

        partialHost.Clock.AdvanceTo(T0.AddSeconds(31)); // 预算 30s 到限
        emptyHost.Clock.AdvanceTo(T0.AddSeconds(3));
        failHost.Clock.AdvanceTo(T0.AddSeconds(3));
        timeoutHost.Clock.AdvanceTo(T0.AddSeconds(31));

        // partial/empty：批已投递 → NoAction → 无义务证据 → TerminalNotProven
        Assert.Equal(RunDriveStatus.TerminalNotProven, partialHost.DriveOnce().Status);
        Assert.Equal(RunDriveStatus.TerminalNotProven, emptyHost.DriveOnce().Status);
        // failure/timeout：零投递 → driver 停留合法等待
        Assert.Equal(RunDriveStatus.WaitingForInput, failHost.DriveOnce().Status);
        Assert.Equal(RunDriveStatus.WaitingForInput, timeoutHost.DriveOnce().Status);

        var partialStatus = partialHost.Feed.Operations[0].Status;
        var emptyStatus = emptyHost.Feed.Operations[0].Status;
        var failStatus = failHost.Feed.Operations[0].Status;
        var timeoutStatus = timeoutHost.Feed.Operations[0].Status;

        // 四态严格区分
        Assert.Equal(ObservationOperationStatus.PartialAtDeadline, partialStatus);
        Assert.Equal(ObservationOperationStatus.Complete, emptyStatus); // 空帧 = 完成的显式负观察
        Assert.Equal(ObservationOperationStatus.Failed, failStatus);
        Assert.Equal(ObservationOperationStatus.TimedOut, timeoutStatus);
        Assert.NotEqual(partialStatus, emptyStatus);
        Assert.NotEqual(emptyStatus, failStatus);
        Assert.NotEqual(failStatus, timeoutStatus);

        // partial：已覆盖 region 有声明；未覆盖 region（Colors）零声明、
        // occurrence 景观不含 Colors（不得推断 absence/存在）
        var partialBelief = partialHost.KernelCore.CurrentBelief!;
        Assert.True(partialBelief.WorldState.ContainsKey(TruthTypingSubject(colorHeader)));
        Assert.False(partialBelief.WorldState.ContainsKey(TruthTypingSubject(colors)));
        Assert.DoesNotContain(partialBelief.Occurrences!, o => o.SemanticDescriptor == "Colors");

        // 真空 = 显式空帧 claim；failure = 零 belief（无空帧、无任何 claim）
        var emptyBelief = emptyHost.KernelCore.CurrentBelief!;
        Assert.Equal("{\"detects\":[]}", emptyBelief.WorldState["live.frame"].Value);
        Assert.Empty(emptyBelief.Occurrences!);
        Assert.Null(failHost.KernelCore.CurrentBelief);
        Assert.Empty(failHost.LedgerCore.AdmissionLog); // failure ≠ OK_EMPTY：零 admission

        // timeout：零 belief、零 admission
        Assert.Null(timeoutHost.KernelCore.CurrentBelief);
        Assert.Empty(timeoutHost.LedgerCore.AdmissionLog);
        Assert.Equal("provider transport failure", failHost.Feed.Operations[0].FailureReason);

        _output.WriteLine("S6: partial↔empty↔failure↔timeout 四态互异；" +
            $"failure/timeout 零 admission={failHost.LedgerCore.AdmissionLog.Count}/" +
            $"{timeoutHost.LedgerCore.AdmissionLog.Count}");
    }

    // =====================================================================
    // ⑦ 等待期 cancel → SafeStop terminal；此后结果到达零复活、零 late Effect
    // =====================================================================

    [Fact]
    public void S7_CancelDuringWait_ThenLateResult_NoRevivalZeroLateEffect()
    {
        var facts = Facts();
        var colors = Element(facts, "display-child", "Colors");
        var colorHeader = Element(facts, "display-child", "Color");
        var scope = new[] { colorHeader, colors };
        var typed = scope.Select(e => new TypedElement(e, e.TruthType)).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            typed.Select(t => (t.Element, t.AssumedType)));

        var initial = InitialOp(scope, T0, AsyncRealizations.CaptureIdOf(response), Array.Empty<ScheduledResult>());
        var slowLate = AsyncRealizations.RecordedStageResult(
            initial, "res-slow-late", "slow", T0.AddSeconds(25), IsFinal: true, typed);

        var contract = new ExecutionContract(
            "uap-v1", "open-target-menu",
            AsyncRealizations.ScopeOf(scope, "page.colors.opened", "run.cancel-requested"),
            new HashSet<string> { "tap" },
            new HashSet<string>(),
            new[] { "obj-open" },
            new[]
            {
                new RunObligation("open", RunObligationKind.MaterialEffect, "page.colors.opened", "true", true),
                new RunObligation("cancel", RunObligationKind.SafeStop, "run.cancel-requested", "true", true),
            });

        var host = Activate(new AsyncScenario(
            "s7-cancel-then-late",
            T0,
            contract,
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", "Colors", "tap", null) },
                "open colors"),
            new[] { initial with { Schedule = new[] { slowLate } } }));

        // 等待期（slow 未到）→ 合法等待；随后 cancel 到达 → SafeStop terminal
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status);
        host.Feed.QueueCancel("host-cancel", T0.AddSeconds(5));
        host.Clock.AdvanceTo(T0.AddSeconds(5));
        var cancelled = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, cancelled.Status);
        Assert.Equal(TerminalClassification.SafeStop, cancelled.Outcome!.Classification);
        Assert.Equal(0, host.EffectDriver.DeliveryCount);

        var revisionsAtTerminal = host.WorldCore.RevisionHistory.Count;
        var admissionsAtTerminal = host.LedgerCore.AdmissionLog.Count;

        // 此后结果到达：不复活 Run、零 late Effect、零新 admission
        host.Clock.AdvanceTo(T0.AddSeconds(30));
        host.Feed.PumpNow(); // terminal 后 driver 不再 pull；诊断面泵送
        var already = host.DriveOnce();
        Assert.Equal(RunDriveStatus.AlreadyTerminal, already.Status);
        Assert.Equal(0, host.EffectDriver.DeliveryCount);
        Assert.Equal(revisionsAtTerminal, host.WorldCore.RevisionHistory.Count);
        Assert.Equal(admissionsAtTerminal, host.LedgerCore.AdmissionLog.Count);

        var opSnapshot = host.Feed.Operations.Single();
        Assert.Equal(ObservationOperationStatus.Cancelled, opSnapshot.Status);
        Assert.Contains("res-slow-late", opSnapshot.CancelledQuarantinedResultIds);
        Assert.Empty(opSnapshot.DeliveredResultIds);
        Assert.Empty(host.ScriptedAgent.CheckDiscipline(0)); // 决策前取消：零 consultation
    }

    // =====================================================================
    // ⑧-A 小标题/副标题误判 → Slow 纠正成功：无第二可点击目标；
    //    Colors 只接地真实菜单；含糊零点击。
    // =====================================================================

    [Fact]
    public void S8a_SubtitleAndGroupHeaderMisjudged_CorrectedBySlow_NoSecondClickableTarget()
    {
        var facts = Facts();
        var display = new[]
        {
            Element(facts, "display-child", "Brightness"),
            Element(facts, "display-child", "Brightness level"),
            Element(facts, "display-child", "Color"),
            Element(facts, "display-child", "Colors"),
            Element(facts, "display-child", "Color contrast"),
        };
        var rootTop = new[]
        {
            Element(facts, "root-top", "Settings"),
            Element(facts, "root-top", "Network & internet"),
            Element(facts, "root-top", "Mobile, Wi‑Fi, hotspot"),
            Element(facts, "root-top", "Connected devices"),
            Element(facts, "root-top", "Bluetooth, pairing"),
        };
        var scope = display.Concat(rootTop).ToList();
        var colors = scope.Single(e => e.Text == "Colors");
        var colorHeader = scope.Single(e => e.Text == "Color");
        var subtitle = scope.Single(e => e.Text == "Mobile, Wi‑Fi, hotspot");

        // fast = dual 预测（历史错误：副标题/分组标题 → row_title）
        var fastTyped = scope.Select(e => new TypedElement(e, FastPred(facts, e))).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));
        var captureId = AsyncRealizations.CaptureIdOf(response);

        var initial = InitialOp(scope, T0, captureId, Array.Empty<ScheduledResult>());
        var fast = AsyncRealizations.ModelStageResult(
            initial, "res-fast", "fast", T0.AddSeconds(1), IsFinal: false, fastTyped);
        // targeted slow（truth 对齐 double）纠正全部误判 typing
        var slowCorrected = AsyncRealizations.RecordedStageResult(
            initial, "res-slow", "targeted-slow", T0.AddSeconds(7), IsFinal: true,
            scope.Select(e => new TypedElement(e, e.TruthType)).ToList());

        var post = PostActionOp(scope, T0.AddSeconds(30),
            CaptureIdFor(scope.Select(e => new TypedElement(e, e.TruthType))),
            Array.Empty<ScheduledResult>());
        var postResult = AsyncRealizations.RecordedStageResult(
            post, "res-post", "slow", T0.AddSeconds(32), IsFinal: true,
            scope.Select(e => new TypedElement(e, e.TruthType)).ToList(),
            new[] { ("page.colors.opened", "true") });

        var host = Activate(new AsyncScenario(
            "s8a-subtitle-corrected",
            T0,
            TapContract(scope, "page.colors.opened"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", "Colors", "tap", null) },
                "open colors"),
            new[] { initial with { Schedule = new[] { fast, slowCorrected } },
                    post with { Schedule = new[] { postResult } } }));

        host.Clock.AdvanceTo(T0.AddSeconds(7));
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status); // act 完成，等 post
        host.Clock.AdvanceTo(T0.AddSeconds(32));
        var final = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, final.Status);

        var belief = host.KernelCore.CurrentBelief!;
        var rowDescriptors = belief.Occurrences!
            .Where(o => o.Role == "menu.row")
            .Select(o => o.SemanticDescriptor!)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        // 副标题/分组标题未形成第二个可点击目标（menu.row 恰 = 真值行集）
        Assert.Equal(
            new[] { "Brightness level", "Color contrast", "Colors", "Connected devices", "Network & internet" },
            rowDescriptors);
        Assert.Single(belief.Occurrences!.Where(o =>
            o.Role == "menu.row" && o.SemanticDescriptor == "Network & internet")); // 行恰一个可点击目标
        Assert.Equal("menu.subtitle",
            belief.Occurrences!.Single(o => o.SemanticDescriptor == subtitle.Text).Role);
        Assert.Equal("menu.static",
            belief.Occurrences!.Single(o => o.SemanticDescriptor == colorHeader.Text).Role);
        Assert.Equal("menu.section",
            belief.Occurrences!.Single(o => o.SemanticDescriptor == "Settings").Role);

        // 纠正在 decision 前：agent 上下文含纠正后的 typing claims
        var call = host.ScriptedAgent.Calls.Single();
        Assert.Equal("static_title", call.CurrentWorldClaims[TruthTypingSubject(colorHeader)]);
        Assert.Equal("row_subtitle", call.CurrentWorldClaims[TruthTypingSubject(subtitle)]);
        Assert.Equal("row_title", call.CurrentWorldClaims[TruthTypingSubject(colors)]);

        // 请求打开 Colors：唯一接地真实菜单行（真值 cy 2202.5）
        var resolution = host.WorldCore.ResolveCurrent(new TargetDescriptor("menu.row", "Colors"));
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, resolution.Result);
        Assert.Equal(1, host.EffectDriver.DeliveryCount);
        var binding = host.EffectBoundaryCore.BindingLog.Single(b => b.Canonical is not null).Canonical!;
        Assert.Equal(CenterY(colors), Math.Round(binding.TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));

        // 目标含糊（role-only）：多义 → 零点击（seam 面断言；in-run 见 S8b）
        Assert.Equal(
            CurrentCandidateSetResultKind.MultipleCandidates,
            host.WorldCore.ResolveCurrent(new TargetDescriptor("menu.row")).Result);
        // 请求 "Color"（分组标题）：非 menu.row → NoCandidate（不点击标题）
        Assert.Equal(
            CurrentCandidateSetResultKind.NoCandidate,
            host.WorldCore.ResolveCurrent(new TargetDescriptor("menu.row", "Color")).Result);
    }

    // =====================================================================
    // ⑧-B 后续识别仍错误 / 冲突：Slow 不因更慢成真值；歧义零点击
    // =====================================================================

    [Fact]
    public void S8b_StillWrongOrConflicting_SlowNotAutoTruth_AmbiguityZeroClicks()
    {
        var facts = Facts();
        var colorHeader = Element(facts, "display-child", "Color");
        var colors = Element(facts, "display-child", "Colors");
        var contrast = Element(facts, "display-child", "Color contrast");
        var scope = new[] { colorHeader, colors, contrast };
        var fastTyped = scope.Select(e => new TypedElement(e, FastPred(facts, e))).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));

        // ---- B1：slow 仍错误（同 fast 误判）→ role-only 含糊零点击 ----
        var initialB1 = InitialOp(scope, T0, AsyncRealizations.CaptureIdOf(response), Array.Empty<ScheduledResult>());
        var fastB1 = AsyncRealizations.ModelStageResult(
            initialB1, "res-fast", "fast", T0.AddSeconds(1), IsFinal: false, fastTyped);
        var slowStillWrong = AsyncRealizations.RecordedStageResult(
            initialB1, "res-slow-wrong", "slow", T0.AddSeconds(7), IsFinal: true, fastTyped);

        var hostB1 = Activate(new AsyncScenario(
            "s8b1-still-wrong",
            T0,
            TapContract(scope, "page.colors.opened"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", null, "tap", null) }, // role-only（含糊目标）
                "open a color menu"),
            new[] { initialB1 with { Schedule = new[] { fastB1, slowStillWrong } } }));
        hostB1.Clock.AdvanceTo(T0.AddSeconds(7));
        var b1 = hostB1.DriveOnce();

        // 误判未纠正：Color 与 Colors 均为 menu.row → role-only 多义 → 零点击
        Assert.Equal(RunDriveStatus.GroundingFailed, b1.Status);
        Assert.Contains("MultipleCandidates", b1.Reason, StringComparison.Ordinal);
        Assert.Equal(0, hostB1.EffectDriver.DeliveryCount);
        var b1Belief = hostB1.KernelCore.CurrentBelief!;
        Assert.Equal(2, b1Belief.Occurrences!.Count(o =>
            o.Role == "menu.row" && o.SemanticDescriptor is "Color" or "Colors"));
        // 但 "Colors" 精确请求仍只接地真实菜单行（不点 Color）
        var b1Colors = hostB1.WorldCore.ResolveCurrent(new TargetDescriptor("menu.row", "Colors"));
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, b1Colors.Result);
        Assert.Equal("Colors", b1Colors.Candidates.Single().SemanticDescriptor);

        // ---- B2：跨 producer 冲突——slow 先立误判，review（truth）后到挑战 ----
        // 语义（CLE-001 + revision-local 景观）：claim 层异 producer 挑战不覆
        // 写既存（latest 不获胜）；occurrence 景观跟随最后 reconcile 的帧
        // （review 帧）→ Color=menu.static → 请求 "Color" 无 act → 零点击。
        var initialB2 = InitialOp(scope, T0, AsyncRealizations.CaptureIdOf(response), Array.Empty<ScheduledResult>());
        var slowWrongFirst = AsyncRealizations.RecordedStageResult(
            initialB2, "res-slow-wrong", "slow", T0.AddSeconds(1), IsFinal: false, fastTyped);
        var reviewTruthLast = AsyncRealizations.RecordedStageResult(
            initialB2, "res-review", "review", T0.AddSeconds(9), IsFinal: true,
            scope.Select(e => new TypedElement(e, e.TruthType)).ToList(),
            producer: AsyncRealizations.ProducerReview);

        var hostB2 = Activate(new AsyncScenario(
            "s8b2-conflict",
            T0,
            TapContract(scope, "page.colors.opened"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", "Color", "tap", null) }, // 若误判成立则点标题
                "open color menu"),
            new[] { initialB2 with { Schedule = new[] { slowWrongFirst, reviewTruthLast } } }));
        hostB2.Clock.AdvanceTo(T0.AddSeconds(9));
        var b2 = hostB2.DriveOnce();

        // 零点击：最终景观（review 帧）中 Color=menu.static → 无 act → fail closed
        Assert.Equal(RunDriveStatus.GroundingFailed, b2.Status);
        Assert.Equal(0, hostB2.EffectDriver.DeliveryCount);
        var b2Belief = hostB2.KernelCore.CurrentBelief!;
        Assert.Equal("menu.static",
            b2Belief.Occurrences!.Single(o => o.SemanticDescriptor == "Color").Role);

        // claim 层：review 挑战不覆写 slow 已确立误判值——「更晚」不自动成
        // 真值；分歧显式化为 Conflict + Uncertainty（不静默、不猜测）
        Assert.Equal("row_title", b2Belief.WorldState[TruthTypingSubject(colorHeader)].Value);
        var conflict = Assert.Single(b2Belief.Conflicts, c => c.Subject == TruthTypingSubject(colorHeader));
        Assert.Equal("row_title", conflict.EstablishedValue);
        Assert.Equal("static_title", conflict.ChallengingValue);
        Assert.True(b2Belief.Uncertainty.ConflictingClaimCount >= 1);
    }

    // =====================================================================
    // ⑧-C 交换到达顺序 + 重复投递：不变量保持
    // =====================================================================

    [Fact]
    public void S8c_ArrivalOrderSwapAndDuplicates_InvariantsHold()
    {
        var facts = Facts();
        var colorHeader = Element(facts, "display-child", "Color");
        var colors = Element(facts, "display-child", "Colors");
        var contrast = Element(facts, "display-child", "Color contrast");
        var scope = new[] { colorHeader, colors, contrast };
        var fastTyped = scope.Select(e => new TypedElement(e, FastPred(facts, e))).ToList();
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            fastTyped.Select(t => (t.Element, t.AssumedType)));
        var captureId = AsyncRealizations.CaptureIdOf(response);

        var initial = InitialOp(scope, T0, captureId, Array.Empty<ScheduledResult>());
        // 交换到达顺序：slow（纠正）在完成窗口内先到齐（partial @t1 +
        // copy @t1.5 + final @t2 → 完成事件 t2），错误 fast @t6 才到——
        // 完成后迟到，隔离且不覆写纠正（D13 事件时间语义）。
        var truthTyped = scope.Select(e => new TypedElement(e, e.TruthType)).ToList();
        var slowPartial = AsyncRealizations.RecordedStageResult(
            initial, "res-slow-partial", "targeted-slow", T0.AddSeconds(1), IsFinal: false,
            new[] { new TypedElement(colorHeader, colorHeader.TruthType) });
        var slowFinal = AsyncRealizations.RecordedStageResult(
            initial, "res-slow-final", "targeted-slow", T0.AddSeconds(2), IsFinal: true,
            new[] { new TypedElement(colors, colors.TruthType), new TypedElement(contrast, contrast.TruthType) });
        var fastLate = AsyncRealizations.ModelStageResult(
            initial, "res-fast", "fast", T0.AddSeconds(6), IsFinal: false, fastTyped);
        var schedule = new List<ScheduledResult>
        {
            slowPartial,
            slowPartial with { ResultId = "res-slow-copy", ArrivalTime = T0.AddSeconds(1.5) }, // 精确重复（异 ResultId）
            slowPartial with { ArrivalTime = T0.AddSeconds(1.5) },                              // 同 ResultId 重投
            slowFinal,
            fastLate, // 完成后迟到 → 隔离
        };

        var post = PostActionOp(scope, T0.AddSeconds(30), CaptureIdFor(scope.Select(e => new TypedElement(e, e.TruthType))),
            Array.Empty<ScheduledResult>());
        var postResult = AsyncRealizations.RecordedStageResult(
            post, "res-post", "slow", T0.AddSeconds(32), IsFinal: true,
            scope.Select(e => new TypedElement(e, e.TruthType)).ToList(),
            new[] { ("page.colors.opened", "true") });

        var host = Activate(new AsyncScenario(
            "s8c-swap-duplicate",
            T0,
            TapContract(scope, "page.colors.opened"),
            new AgentScriptStep(
                AgentScriptKind.Act,
                new[] { new ScriptActionStep("menu.row", "Colors", "tap", null) },
                "open colors"),
            new[] { initial with { Schedule = schedule },
                    post with { Schedule = new[] { postResult } } }));

        host.Clock.AdvanceTo(T0.AddSeconds(6));
        Assert.Equal(RunDriveStatus.WaitingForInput, host.DriveOnce().Status); // act 完成，等 post
        host.Clock.AdvanceTo(T0.AddSeconds(32));
        var final = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, final.Status);

        // 不变量 1：恰一 Effect、不重复（重复投递零放大）
        Assert.Equal(1, host.EffectDriver.DeliveryCount);
        // 不变量 2：请求 Colors 只接地真实菜单行
        var binding = host.EffectBoundaryCore.BindingLog.Single(b => b.Canonical is not null).Canonical!;
        Assert.Equal(CenterY(colors), Math.Round(binding.TargetLocator!.CenterY, 6, MidpointRounding.AwayFromZero));
        // 不变量 3：含糊零点击——最终 belief（纠正稳定）role-only 仍多义
        //（Colors 与 Color contrast 两条真值行）
        Assert.Equal(
            CurrentCandidateSetResultKind.MultipleCandidates,
            host.WorldCore.ResolveCurrent(new TargetDescriptor("menu.row")).Result);
        // 不变量 4（D13）：完成事件 t2 交付；同 ResultId 重投在完成事件前 →
        // Duplicates；错误 fast 完成后迟到 → 隔离、零进入交付批
        var snapshot = host.Feed.Operations.Single(o => o.Context == ObservationContext.External);
        Assert.Equal(T0.AddSeconds(2), snapshot.CompletedAt);
        Assert.Equal(new[] { "res-slow-partial", "res-slow-copy", "res-slow-final" },
            snapshot.DeliveredResultIds);
        Assert.Contains("res-slow-partial", snapshot.DuplicateResultIds);
        Assert.Contains("res-fast", snapshot.StaleQuarantinedResultIds);
        // 纠正对迟到错误结果稳定（决策时与最终 WorldState 一致——不再有
        // 「后到 Fast 覆写纠正」路径：完成窗口外的同流结果不进入 belief）
        var decisionClaims = host.ScriptedAgent.Calls.Single().CurrentWorldClaims;
        Assert.Equal("static_title", decisionClaims[TruthTypingSubject(colorHeader)]);
        Assert.Equal("static_title",
            host.KernelCore.CurrentBelief!.WorldState[TruthTypingSubject(colorHeader)].Value);
    }
}
