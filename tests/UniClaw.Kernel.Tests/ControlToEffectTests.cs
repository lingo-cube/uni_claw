using System.Reflection;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// C2E-002 验收 1..10 —— 每条验收恰好一个用例。
/// 纯内存 fake world（level: DETERMINISTIC）；scripted driver / observation
/// provider / policy 均为确定性测试替身。测试验证行为，不验证实现细节。
/// </summary>
public sealed class ControlToEffectTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 7, 10, 5, 0, TimeSpan.Zero);

    // ---- scripted 替身 ---------------------------------------------------

    /// <summary>provenance 完整的观察（正路径输入；post-action 观察可覆写 producer/lineage）。</summary>
    private static ObservationRecord Observation(
        string subject,
        string value,
        DateTimeOffset captureTime,
        string producer = "provider.scripts",
        IReadOnlyList<string>? lineage = null) =>
        new(new ObservationClaim(subject, value),
            new Provenance(producer, captureTime, $"scope:{subject}",
                lineage ?? new[] { "raw://capture", "encode:v1" }));

    /// <summary>合法完整的最小 Execution Contract（UniAgent 侧产物，D4 scripted）。</summary>
    private static ExecutionContract Contract() => new(
        Version: "c1",
        Objective: "verify-home-screen",
        Scope: new HashSet<string> { "screen.home" },
        AllowedEffects: new HashSet<string> { "tap" },
        ForbiddenEffects: new HashSet<string> { "swipe" },
        ProofCriteria: new[] { "home-screen-observed" });

    /// <summary>确定性策略表（D9）：默认申请对 screen.home 的 tap。</summary>
    private sealed class ScriptedPolicy(string effectClass = "tap") : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, effectClass, "screen.home");
    }

    /// <summary>确定性 driver（Assumption：两态 outcome 队列，机械投递，无 retry）。</summary>
    private sealed class ScriptedDriver(params DispatchOutcome[] outcomes) : IEffectDriver
    {
        private readonly Queue<DispatchResult> _results =
            new(outcomes.Select((o, i) => new DispatchResult(o, $"scripted:{o}", T1.AddMinutes(i))));

        public DispatchResult Deliver(CanonicalBinding binding) => _results.Dequeue();
    }

    /// <summary>组装六 L2 kernel；relevance scope 只含 screen.home。</summary>
    private static (UniKernel Kernel, EvidenceLedger Ledger, WorldModel World, RunModel Run,
        ControlLoop Control, RuntimeAssurance Assurance, EffectBoundary Effects) NewKernel(
        IEffectDriver? driver = null, IControlPolicy? policy = null)
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var run = new RunModel();
        var control = new ControlLoop(policy ?? new ScriptedPolicy());
        var assurance = new RuntimeAssurance();
        var effects = new EffectBoundary(driver ?? new ScriptedDriver(DispatchOutcome.Delivered));
        var kernel = new UniKernel(ledger, world, run, control, assurance, effects);
        return (kernel, ledger, world, run, control, assurance, effects);
    }

    /// <summary>最小前置：contract 已接受 + rev-1（screen.home=idle）已确立。</summary>
    private static void PrimeWorld(UniKernel kernel) => kernel.Process(Observation("screen.home", "idle", T0));

    // ---- 验收 1：契约准入 fail-closed -----------------------------------

    [Fact]
    public void Accepted1_InvalidOrIncompleteContractIsRejectedWithZeroRunStateSideEffects()
    {
        var (kernel, _, _, run, _, _, _) = NewKernel();

        var badContracts = new[]
        {
            Contract() with { Version = "" },
            Contract() with { Objective = " " },
            Contract() with { Scope = null },
            Contract() with { AllowedEffects = new HashSet<string>() },
            Contract() with { ForbiddenEffects = null },
            Contract() with { ProofCriteria = null },
        };

        foreach (var contract in badContracts)
        {
            var admission = kernel.AdmitContract(contract);
            Assert.False(admission.Accepted);
            Assert.NotNull(admission.RejectionReason);
        }

        // 零 Run State 副作用：无 View、无 State、History 空
        Assert.Null(run.View);
        Assert.Null(run.State);
        Assert.Empty(run.History);
    }

    // ---- 验收 2：四产出独立留痕、次序不可合并 ---------------------------

    [Fact]
    public void Accepted2_ActIntentProducesFourIndependentArtifactsInUnmergeableOrder()
    {
        var (kernel, _, _, _, control, assurance, effects) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
        var candidate = new CandidateBinding("screen.home", "idle", "rev-1");
        var act = kernel.Act(intent, candidate);

        // 四类产出分别在四个 Owner 的 append-only log，IntentId 关联
        Assert.Equal(ControlIntentKind.Act, control.IntentLog.Single(i => i.IntentId == intent.IntentId).Kind);
        var judgment = assurance.JudgmentLog.Single(j => j.IntentId == intent.IntentId);
        Assert.True(judgment.IsAdmissible);
        Assert.NotNull(effects.BindingLog.Single(b => b.Canonical?.IntentId == intent.IntentId).Canonical);
        Assert.Equal(DispatchOutcome.Delivered, effects.ReceiptLog.Single(r => r.IntentId == intent.IntentId).Outcome);

        // 次序不可合并（短路证明）：被 Assurance 拒绝的 act-intent 不产生 binding / receipt
        var (k2, _, _, _, _, a2, e2) = NewKernel(policy: new ScriptedPolicy(effectClass: "swipe"));
        k2.AdmitContract(Contract());
        PrimeWorld(k2);
        var forbiddenIntent = k2.SelectIntent(k2.DeriveSlice("screen.home"));
        var act2 = k2.Act(forbiddenIntent, new CandidateBinding("screen.home", "idle", "rev-1"));

        Assert.False(act2.Judgment.IsAdmissible);   // safety guard：forbidden effect 拒绝
        Assert.Null(act2.Binding);                  // 短路：无 binding
        Assert.Null(act2.Receipt);                  // 短路：无 dispatch
        Assert.NotEmpty(a2.JudgmentLog);
        Assert.Empty(e2.BindingLog);
        Assert.Empty(e2.ReceiptLog);

        // forged intent（未经 Control Loop 签发）无路径进入 act pipeline
        var forged = new ControlIntent("intent-forged", ControlIntentKind.Act, "tap", "screen.home", "rev-1");
        Assert.Throws<InvalidOperationException>(() => kernel.Act(forged, candidate));
    }

    // ---- 验收 3：Candidate ≠ Canonical ----------------------------------

    [Fact]
    public void Accepted3_CandidateBindingIsNeverCanonicalAndCanonicalBindsCurrentRevision()
    {
        var (kernel, _, world, _, _, _, effects) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));

        // 三态拒绝（D8）：stale / ambiguous / unknown → 无 canonical、无 dispatch
        var stale = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-0"));
        Assert.True(stale.Judgment.IsAdmissible);   // judgment 是 action-local，先过
        Assert.Null(stale.Binding!.Canonical);
        Assert.Equal(BindingRejectionReason.StaleRevision, stale.Binding.RejectionReason);
        Assert.Null(stale.Receipt);

        var ambiguous = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1", IsAmbiguous: true));
        Assert.Equal(BindingRejectionReason.Ambiguous, ambiguous.Binding!.RejectionReason);
        Assert.Null(ambiguous.Receipt);

        var unknown = kernel.Act(intent, new CandidateBinding("screen.unknown", "?", "rev-1"));
        Assert.Equal(BindingRejectionReason.UnknownTarget, unknown.Binding!.RejectionReason);
        Assert.Null(unknown.Receipt);

        Assert.Empty(effects.ReceiptLog);

        // happy path：canonical 绑定 current WorldBelief revision（不变量 24）
        var ok = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.NotNull(ok.Binding!.Canonical);
        Assert.Equal(world.Current!.RevisionId, ok.Binding.Canonical!.RevisionId);
        Assert.NotNull(ok.Receipt);
        var canonical = ok.Binding.Canonical!;

        // §17 失效源一：dispatch 本身使 binding 失效（派生自 ReceiptLog，无 event）
        // —— 同 intent 同 target 再次 Act 被拒，一次 authorization 只产生一次投递
        var respent = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.True(respent.Judgment.IsAdmissible);       // action-local 判定仍过
        Assert.NotNull(respent.Binding!.Canonical);       // 重新认定出同一 BindingId
        Assert.Equal(canonical.BindingId, respent.Binding.Canonical!.BindingId);
        Assert.False(respent.Gate!.Allowed);              // gate 拒绝已消费 binding
        Assert.Equal("binding-already-dispatched", respent.Gate.Reason);
        Assert.Null(respent.Receipt);
        Assert.Single(effects.ReceiptLog);                // 真实投递仍只有一次
        Assert.False(effects.IsBindingValid(canonical, world.Current!));   // 已失效

        // §17 失效源二：freshness loss——新 revision 出现后 binding 失效
        kernel.Process(Observation("screen.home", "active", T1));   // rev-2
        Assert.False(effects.IsBindingValid(canonical, world.Current!));
    }

    // ---- 验收 4：Gate 只执法 ---------------------------------------------

    [Fact]
    public void Accepted4_GateOnlyEnforcesAuthorizationAndNeverRejudges()
    {
        var (kernel, _, world, _, _, _, effects) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
        var candidate = new CandidateBinding("screen.home", "idle", "rev-1");
        var canonical = effects.Bind(intent, candidate, world.Current!).Canonical!;

        // (a) 非 admissible judgment → 拒绝，无 receipt
        var rejected = new AssuranceJudgment(intent.IntentId, false, Array.Empty<AssuranceCheck>(), "safety-guard");
        var (gate1, receipt1) = effects.Dispatch(canonical, rejected, world.Current!);
        Assert.False(gate1.Allowed);
        Assert.Null(receipt1);

        // (b) judgment 属于其他 intent → 拒绝（不改 target、不代为修复）
        var mismatched = new AssuranceJudgment("intent-other", true, Array.Empty<AssuranceCheck>(), null);
        var (gate2, receipt2) = effects.Dispatch(canonical, mismatched, world.Current!);
        Assert.False(gate2.Allowed);
        Assert.Null(receipt2);

        // (c) 匹配的 admissible judgment → 执法通过 → 机械投递
        var authorized = new AssuranceJudgment(intent.IntentId, true, Array.Empty<AssuranceCheck>(), null);
        var (gate3, receipt3) = effects.Dispatch(canonical, authorized, world.Current!);
        Assert.True(gate3.Allowed);
        Assert.NotNull(receipt3);

        // (d) binding 派生失效（stale）→ 拒绝（fail-closed；非重判）
        kernel.Process(Observation("screen.home", "active", T1));   // rev-2
        var (gate4, receipt4) = effects.Dispatch(canonical, authorized, world.Current!);
        Assert.False(gate4.Allowed);
        Assert.Null(receipt4);
    }

    // ---- 验收 5：Attempt ≠ Effect ----------------------------------------

    [Fact]
    public void Accepted5_AttemptIsNotEffectOnlyPostActionObservationProducesRevision()
    {
        var (kernel, ledger, world, _, _, _, _) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);
        var before = world.Current!;
        Assert.Equal("idle", before.WorldState["screen.home"].Value);

        var intent = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
        var act = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));

        // Effect Receipt 留痕（attempt evidence）
        Assert.Equal(DispatchOutcome.Delivered, act.Receipt!.Outcome);

        // receipt 经既有 admission 路径回流，但零 effect-claim revision
        Assert.Equal(AdmissionDecision.Accepted, act.AttemptEvidenceReflux!.Admission.Decision);
        Assert.False(act.AttemptEvidenceReflux.Relevance!.IsRelevant);   // attempt.* 世界无关
        Assert.Same(before, world.Current);                              // 世界观未因 receipt 改变
        Assert.Equal("idle", world.Current!.WorldState["screen.home"].Value);
        Assert.Single(world.RevisionHistory);

        // 只有 post-action accepted observation 产生新 revision（producer 前缀 effect.boundary，D7）。
        // E2B conflict 词汇承载 effect evidence（Assumption 3）：不静默覆盖，显式冲突留痕
        var postAction = Observation("screen.home", "active", T1,
            producer: "effect.boundary.observer",
            lineage: new[] { $"dispatch:{act.Receipt.ReceiptId}" });
        var reflux = kernel.Process(postAction);

        Assert.NotNull(reflux.ResultingRevision);
        Assert.Equal(2, world.Current!.RevisionNumber);
        Assert.Equal("idle", world.Current.WorldState["screen.home"].Value);   // established 保留
        Assert.Contains(world.Current.Conflicts, c =>
            c.Subject == "screen.home"
            && c.EstablishedValue == "idle"
            && c.ChallengingValue == "active"
            && c.ChallengingEvidenceId == reflux.Admission.EvidenceId);
        Assert.Contains(reflux.Admission.EvidenceId!, world.Current.EvidenceBasis);
        Assert.StartsWith("effect.boundary",
            ledger.CanonicalRecords[reflux.Admission.EvidenceId!].Provenance.Producer);
    }

    // ---- 验收 6：Run State 边界 -------------------------------------------

    [Fact]
    public void Accepted6_ProgressAdvancesWithCyclesAndAssuranceStateStaysOutOfRunState()
    {
        var (kernel, _, _, run, control, assurance, _) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);
        var slice = kernel.DeriveSlice("screen.home");

        kernel.SelectIntent(slice);
        kernel.SelectIntent(slice);
        var third = kernel.SelectIntent(slice);

        Assert.Equal(3, run.State!.Progress.Cycles);   // Progress State 随 cycle 推进

        kernel.Act(third, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.Equal(1, run.State.Progress.Acts);

        // action-local assurance state 不进入 canonical Run State（反射对象图封闭）
        var visited = new HashSet<Type>();
        void Walk(Type? type)
        {
            if (type is null || !visited.Add(type))
                return;
            Assert.False(
                type.Namespace?.StartsWith("UniClaw.Kernel.Assurance", StringComparison.Ordinal) == true,
                $"Run State 对象图泄漏 Assurance 类型：{type.Name}");
            foreach (var property in type.GetProperties())
            {
                Walk(property.PropertyType);
                foreach (var argument in property.PropertyType.GetGenericArguments())
                    Walk(argument);
                if (property.PropertyType.IsArray)
                    Walk(property.PropertyType.GetElementType());
            }
        }

        Walk(typeof(RunState));

        // 对照：assurance 确有判定留痕，但它只存在于自己的 lifecycle 内
        Assert.NotEmpty(assurance.JudgmentLog);
        Assert.NotEmpty(control.HypothesisLog);
    }

    // ---- 验收 7：plan/hypothesis 负向签名封闭 ----------------------------

    [Fact]
    public void Accepted7_HypothesisAndPlanTypesHaveNoPathIntoAuthorities()
    {
        var bannedMethods = new[]
        {
            typeof(WorldModel).GetMethod(nameof(WorldModel.JudgeRelevance))!,
            typeof(WorldModel).GetMethod(nameof(WorldModel.Reconcile))!,
            typeof(RuntimeAssurance).GetMethod(nameof(RuntimeAssurance.Judge))!,
            typeof(EffectBoundary).GetMethod(nameof(EffectBoundary.Bind))!,
            typeof(EffectBoundary).GetMethod(nameof(EffectBoundary.Dispatch))!,
        };

        foreach (var method in bannedMethods)
        {
            var visited = new HashSet<Type>();
            void Walk(Type? type)
            {
                if (type is null || !visited.Add(type))
                    return;
                Assert.False(
                    type.Name.Contains("Hypothesis", StringComparison.Ordinal)
                        || type.Name.Contains("Plan", StringComparison.Ordinal),
                    $"{method.Name} 的参数类型图泄漏 {type.Name}（不变量 32）");
                foreach (var property in type.GetProperties())
                {
                    Walk(property.PropertyType);
                    foreach (var argument in property.PropertyType.GetGenericArguments())
                        Walk(argument);
                    if (property.PropertyType.IsArray)
                        Walk(property.PropertyType.GetElementType());
                }
            }

            foreach (var parameter in method.GetParameters())
                Walk(parameter.ParameterType);
        }

        // 对照：hypothesis 由 Control Loop 内部拥有，act-intent 与其零附着（P1）
        var (kernel, _, _, _, control, _, _) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);
        kernel.SelectIntent(kernel.DeriveSlice("screen.home"));

        Assert.NotEmpty(control.HypothesisLog);
        Assert.Empty(typeof(ControlIntent).GetProperties()
            .Where(p => p.PropertyType == typeof(TacticalHypothesis)));
    }

    // ---- 验收 8：E2B 权威零穿透 -------------------------------------------

    [Fact]
    public void Accepted8_EvidenceAndBeliefChangeOnlyThroughExistingPaths()
    {
        var (kernel, ledger, world, _, _, _, _) = NewKernel();
        kernel.AdmitContract(Contract());

        kernel.Process(Observation("screen.home", "idle", T0));           // admitted + relevant
        kernel.Process(Observation("device.rotation", "90", T0));         // admitted + irrelevant
        var intent = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
        var act = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));
        kernel.Process(Observation("screen.home", "active", T1,
            producer: "effect.boundary.observer",
            lineage: new[] { $"dispatch:{act.Receipt!.ReceiptId}" }));    // post-action

        // ledger 变化全部经 Admit：admission log 与全部提交观察（含 Act 自动回流的
        // attempt evidence）1:1，无平行写入
        Assert.Equal(4, ledger.AdmissionLog.Count);
        Assert.All(ledger.AdmissionLog, a => Assert.Equal(AdmissionDecision.Accepted, a.Decision));
        Assert.Equal(4, ledger.CanonicalRecords.Count);

        // world 变化全部经 Reconcile：每个 accepted record 都有 relevance 留痕，
        // revision 只由 relevant 记录产生，basis 全部可溯源到 canonical record
        Assert.Equal(4, world.RelevanceLog.Count);
        Assert.Equal(2, world.RevisionHistory.Count);
        foreach (var revision in world.RevisionHistory)
            Assert.All(revision.EvidenceBasis,
                id => Assert.True(ledger.CanonicalRecords.ContainsKey(id)));

        // post-action 观察进入 rev-2：established 保留 + 显式冲突（E2B 词汇承载）
        Assert.Equal("idle", world.Current!.WorldState["screen.home"].Value);
        Assert.Contains(world.Current.Conflicts, c =>
            c.Subject == "screen.home" && c.ChallengingValue == "active");
    }

    // ---- 验收 9：Contract View immutable ----------------------------------

    [Fact]
    public void Accepted9_ContractViewIsImmutableAcrossSameVersionReadmission()
    {
        var (kernel, _, _, run, _, _, _) = NewKernel();

        var first = kernel.AdmitContract(Contract());
        Assert.True(first.Accepted);
        var view = run.View!;

        // 同 version 重复 admit → 复用同一 View，不产生新 View，Run State 不重建
        var second = kernel.AdmitContract(Contract());
        Assert.True(second.Accepted);
        Assert.Same(view, run.View);
        Assert.Single(run.History);

        // 不同 version 不就地改写（显式取代语义不在本片 → fail-closed 拒绝）
        var third = kernel.AdmitContract(Contract() with { Version = "c2" });
        Assert.False(third.Accepted);
        Assert.Same(view, run.View);
        Assert.Single(run.History);
    }

    // ---- 验收 10：Recovery 重新入环，无 blind retry ------------------------

    [Fact]
    public void Accepted10_RecoveryReentersLoopAndBlindRetryWithoutNewRevisionIsRejected()
    {
        var (kernel, _, world, run, control, assurance, effects) =
            NewKernel(driver: new ScriptedDriver(DispatchOutcome.Failed, DispatchOutcome.Delivered));
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);   // rev-1

        // 第一次 act：dispatch 失败（canonical binding 合法，driver 两态之一）
        var intent1 = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
        var act1 = kernel.Act(intent1, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.Equal(DispatchOutcome.Failed, act1.Receipt!.Outcome);

        // dispatch 失败 → Control Loop 选择 recovery intent（re-observe；D10）
        var recovery = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
        Assert.Equal(ControlIntentKind.Recovery, recovery.Kind);

        // 同 target 无新 revision 的直接重试被 Assurance 拒绝（不变量 30/31）
        var retry = new ControlIntent("intent-retry", ControlIntentKind.Act, "tap", "screen.home", "rev-1");
        var retryJudgment = assurance.Judge(
            retry, new CandidateBinding("screen.home", "idle", "rev-1"), run.View!, world.Current!);
        Assert.False(retryJudgment.IsAdmissible);
        Assert.Equal("no-blind-retry", retryJudgment.RejectionReason);
        Assert.Single(effects.ReceiptLog);   // 没有第二个 dispatch 发生

        // 组合面同向封锁：伪造 retry intent 无路径进入 act pipeline（P5），
        // Control Loop 在新 revision 前也只签发 recovery——闭环内不存在 blind retry 通道
        Assert.Throws<InvalidOperationException>(
            () => kernel.Act(retry, new CandidateBinding("screen.home", "idle", "rev-1")));

        // 必经 re-observe → reconcile 产生新 WorldBelief revision 后才可再 act
        kernel.Process(Observation("screen.home", "idle", T1,
            producer: "effect.boundary.observer",
            lineage: new[] { $"dispatch:{act1.Receipt.ReceiptId}" }));    // rev-2
        Assert.Equal(2, world.Current!.RevisionNumber);

        var intent2 = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
        Assert.Equal(ControlIntentKind.Act, intent2.Kind);   // 恢复后回到正常控制
        Assert.Equal("rev-2", intent2.BasisRevisionId);

        var act2 = kernel.Act(intent2, new CandidateBinding("screen.home", "idle", "rev-2"));
        Assert.True(act2.Judgment.IsAdmissible);
        Assert.Equal(DispatchOutcome.Delivered, act2.Receipt!.Outcome);
        Assert.Equal(2, effects.ReceiptLog.Count);
        Assert.Equal(2, control.IntentLog.Count(i => i.Kind == ControlIntentKind.Act));
    }
}
