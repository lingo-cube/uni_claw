using System.Reflection;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;
using Xunit;

using UniClaw.Kernel.Trace;

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

    /// <summary>provenance 完整的观察（正路径输入；ING-006 迁移：+kind/context 参数，post-action 可覆写）。</summary>
    private static ObservationProposal Observation(
        string subject,
        string value,
        DateTimeOffset captureTime,
        string producer = "provider.scripts",
        IReadOnlyList<string>? lineage = null,
        IngressKind kind = IngressKind.Observation,
        ObservationContext context = ObservationContext.External) =>
        new(new ObservationClaim(subject, value), kind, context,
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
        var world = new WorldModel(new HashSet<string> { "screen.home" }, new SeedContainerAssociationStrategy());
        var run = new RunModel();
        var control = new ControlLoop(policy ?? new ScriptedPolicy());
        var assurance = new RuntimeAssurance(new FreshnessDoubles.Satisfying());
        var effects = new EffectBoundary(driver ?? new ScriptedDriver(DispatchOutcome.Delivered));
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, run, control, assurance, effects);
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

        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var candidate = new CandidateBinding("screen.home", "idle", "rev-1");
        var act = kernel.Act(intent, candidate);

        // 四类产出分别在四个 Owner 的 append-only log，IntentId 关联
        Assert.Equal(ControlIntentKind.Act, control.IntentLog.Single(i => i.IntentId == intent.IntentId).Kind);
        var judgment = assurance.JudgmentLog.Single(j => j.IntentId == intent.IntentId);
        Assert.True(judgment.IsAdmissible);
        Assert.NotNull(effects.BindingLog.Single(b => b.Canonical?.IntentId == intent.IntentId).Canonical);
        Assert.Equal(DispatchOutcome.Delivered, effects.ReceiptLog.Single(r => r.IntentId == intent.IntentId).Outcome);

        // 次序不可合并（短路证明）——新序（ADR-0009 / CBA-005）：canonical
        // binding 先认定；被 Assurance 拒绝的 act-intent 由 Gate 执行
        // enforcement 并拒绝（只执法不重判），零 dispatch；binding validity
        // 不因 judgment 拒绝消失（validity ≠ authorization）
        var (k2, _, w2, _, _, a2, e2) = NewKernel(policy: new ScriptedPolicy(effectClass: "swipe"));
        k2.AdmitContract(Contract());
        PrimeWorld(k2);
        var forbiddenIntent = k2.SelectIntent(k2.DeriveSlice(SliceSeed.RootOf(k2)));
        var act2 = k2.Act(forbiddenIntent, new CandidateBinding("screen.home", "idle", "rev-1"));

        Assert.False(act2.Judgment!.IsAdmissible);   // safety guard：forbidden effect 拒绝
        Assert.NotNull(act2.Binding!.Canonical);     // Bind 先行：binding 存在
        Assert.False(act2.Gate!.Allowed);            // Gate 执法并拒绝
        Assert.Equal("not-authorized", act2.Gate.Reason);
        Assert.Null(act2.Receipt);                   // 零 dispatch
        Assert.NotEmpty(a2.JudgmentLog);
        Assert.NotEmpty(e2.BindingLog);
        Assert.Empty(e2.ReceiptLog);
        Assert.True(e2.IsBindingValid(act2.Binding.Canonical!, w2.DeriveBindingView("screen.home")));   // validity 未消失

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

        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));

        // 四态拒绝（D8 三态 + CBA-005 D2 no-candidate）：拒绝即短路——
        // 无 canonical、无 judgment、无 gate、零 dispatch
        var stale = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-0"));
        Assert.Null(stale.Judgment);                 // 新序：Bind 拒绝短路，无 judgment
        Assert.Null(stale.Binding!.Canonical);
        Assert.Equal(BindingRejectionReason.StaleRevision, stale.Binding.RejectionReason);
        Assert.Null(stale.Receipt);

        var ambiguous = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1", IsAmbiguous: true));
        Assert.Null(ambiguous.Judgment);
        Assert.Equal(BindingRejectionReason.Ambiguous, ambiguous.Binding!.RejectionReason);
        Assert.Null(ambiguous.Receipt);

        var unknown = kernel.Act(intent, new CandidateBinding("screen.unknown", "?", "rev-1"));
        Assert.Null(unknown.Judgment);
        Assert.Equal(BindingRejectionReason.UnknownTarget, unknown.Binding!.RejectionReason);
        Assert.Null(unknown.Receipt);

        // candidate 缺失 = 第四态（显式 decision，非异常）
        var noCandidate = kernel.Act(intent, candidate: null);
        Assert.Null(noCandidate.Judgment);
        Assert.Equal(BindingRejectionReason.NoCandidate, noCandidate.Binding!.RejectionReason);
        Assert.Null(noCandidate.Receipt);

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
        Assert.False(effects.IsBindingValid(canonical, world.DeriveBindingView("screen.home")));   // 已失效

        // §17 失效源二：revision currency loss——新 revision 取代后 binding 失效
        // （FRS-007 词汇：这是 currentness 维度，不是 freshness loss——freshness
        //  不参与派生 validity，ADR-0010）
        kernel.Process(Observation("screen.home", "active", T1));   // rev-2
        Assert.False(effects.IsBindingValid(canonical, world.DeriveBindingView("screen.home")));
    }

    // ---- 验收 4：Gate 只执法 ---------------------------------------------

    [Fact]
    public void Accepted4_GateOnlyEnforcesAuthorizationAndNeverRejudges()
    {
        var (kernel, _, world, _, _, _, effects) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var candidate = new CandidateBinding("screen.home", "idle", "rev-1");
        var canonical = effects.Bind(intent, candidate, world.DeriveBindingView("screen.home")).Canonical!;

        // (a) 非 admissible judgment → 拒绝，无 receipt
        var rejected = new AssuranceJudgment(
            intent.IntentId, canonical.BindingId, canonical.RevisionId,
            false, Array.Empty<AssuranceCheck>(), "safety-guard", new FreshnessJudgment(FreshnessSufficiency.Sufficient, "scripted:sufficient"));
        var (gate1, receipt1) = effects.Dispatch(canonical, rejected, world.DeriveBindingView("screen.home"));
        Assert.False(gate1.Allowed);
        Assert.Null(receipt1);

        // (b) judgment 属于其他 intent → 拒绝（不改 target、不代为修复）
        var mismatched = new AssuranceJudgment(
            "intent-other", canonical.BindingId, canonical.RevisionId,
            true, Array.Empty<AssuranceCheck>(), null, new FreshnessJudgment(FreshnessSufficiency.Sufficient, "scripted:sufficient"));
        var (gate2, receipt2) = effects.Dispatch(canonical, mismatched, world.DeriveBindingView("screen.home"));
        Assert.False(gate2.Allowed);
        Assert.Null(receipt2);

        // (c) 匹配的 admissible judgment（三元组齐全）→ 执法通过 → 机械投递
        var authorized = new AssuranceJudgment(
            intent.IntentId, canonical.BindingId, canonical.RevisionId,
            true, Array.Empty<AssuranceCheck>(), null, new FreshnessJudgment(FreshnessSufficiency.Sufficient, "scripted:sufficient"));
        var (gate3, receipt3) = effects.Dispatch(canonical, authorized, world.DeriveBindingView("screen.home"));
        Assert.True(gate3.Allowed);
        Assert.NotNull(receipt3);

        // (d) binding 派生失效（stale）→ 拒绝（fail-closed；非重判）
        kernel.Process(Observation("screen.home", "active", T1));   // rev-2
        var (gate4, receipt4) = effects.Dispatch(canonical, authorized, world.DeriveBindingView("screen.home"));
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

        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
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
            context: ObservationContext.PostActionEffectFlow,
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
        var slice = kernel.DeriveSlice(SliceSeed.RootOf(kernel));

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

        // CBA-005 验收 2：CandidateBinding 退出 Assurance 全部 public 输入
        // 签名（反射负向，覆盖 DeclaredOnly 全部方法含泛型参数）
        foreach (var assuranceMethod in typeof(RuntimeAssurance).GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Assert.All(assuranceMethod.GetParameters(), p =>
            {
                Assert.NotEqual(typeof(CandidateBinding), p.ParameterType);
                Assert.All(p.ParameterType.GetGenericArguments(),
                    g => Assert.NotEqual(typeof(CandidateBinding), g));
            });
        }

        // 对照：hypothesis 由 Control Loop 内部拥有，act-intent 与其零附着（P1）
        var (kernel, _, _, _, control, _, _) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);
        kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));

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
        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var act = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));
        kernel.Process(Observation("screen.home", "active", T1,
            producer: "effect.boundary.observer",
            context: ObservationContext.PostActionEffectFlow,
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
        var intent1 = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var act1 = kernel.Act(intent1, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.Equal(DispatchOutcome.Failed, act1.Receipt!.Outcome);

        // dispatch 失败 → Control Loop 选择 recovery intent（re-observe；D10）
        var recovery = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        Assert.Equal(ControlIntentKind.Recovery, recovery.Kind);

        // 同 target 无新 revision 的直接重试被 Assurance 拒绝（不变量 30/31）
        // —— 新序（ADR-0009 / CBA-005）：先 Bind 出 canonical，再 Judge 该 binding
        var retry = new ControlIntent("intent-retry", ControlIntentKind.Act, "tap", "screen.home", "rev-1");
        var retryCanonical = effects.Bind(
            retry, new CandidateBinding("screen.home", "idle", "rev-1"), world.DeriveBindingView("screen.home")).Canonical!;
        var retryJudgment = assurance.Judge(retry, retryCanonical, run.View!, world.DeriveActionAssuranceView("screen.home"));
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
            context: ObservationContext.PostActionEffectFlow,
            lineage: new[] { $"dispatch:{act1.Receipt.ReceiptId}" }));    // rev-2
        Assert.Equal(2, world.Current!.RevisionNumber);

        var intent2 = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        Assert.Equal(ControlIntentKind.Act, intent2.Kind);   // 恢复后回到正常控制
        Assert.Equal("rev-2", intent2.BasisRevisionId);

        var act2 = kernel.Act(intent2, new CandidateBinding("screen.home", "idle", "rev-2"));
        Assert.True(act2.Judgment!.IsAdmissible);
        Assert.Equal(DispatchOutcome.Delivered, act2.Receipt!.Outcome);
        Assert.Equal(2, effects.ReceiptLog.Count);
        Assert.Equal(2, control.IntentLog.Count(i => i.Kind == ControlIntentKind.Act));
    }

    // ---- Pressure Scenario 13（协议基线 §4-13 / CBA-005 D7）--------------
    // Gate 三元组 correlation：证明 ADR-0009 锁的是 Intent + Binding +
    // Revision，不是只认 Intent。两 case 分别独立断言，不同时改两个 id。

    [Fact]
    public void PressureScenario13_SameIntentIdWithWrongBindingIdIsGateRejected()
    {
        var (kernel, _, world, run, _, assurance, effects) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var canonical = effects.Bind(
            intent, new CandidateBinding("screen.home", "idle", "rev-1"), world.DeriveBindingView("screen.home")).Canonical!;
        var authorized = assurance.Judge(intent, canonical, run.View!, world.DeriveActionAssuranceView("screen.home"));
        Assert.True(authorized.IsAdmissible);

        // case 1：同 IntentId、异 BindingId → Gate 必须拒绝（零 dispatch）
        var wrongBinding = new AssuranceJudgment(
            intent.IntentId, "bind-other", canonical.RevisionId,
            true, Array.Empty<AssuranceCheck>(), null, new FreshnessJudgment(FreshnessSufficiency.Sufficient, "scripted:sufficient"));
        var (gate, receipt) = effects.Dispatch(canonical, wrongBinding, world.DeriveBindingView("screen.home"));
        Assert.False(gate.Allowed);
        Assert.Equal("judgment-binding-id-mismatch", gate.Reason);
        Assert.Null(receipt);
        Assert.Empty(effects.ReceiptLog);
    }

    [Fact]
    public void PressureScenario13_SameIntentIdWithWrongRevisionIdIsGateRejected()
    {
        var (kernel, _, world, run, _, assurance, effects) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var canonical = effects.Bind(
            intent, new CandidateBinding("screen.home", "idle", "rev-1"), world.DeriveBindingView("screen.home")).Canonical!;
        var authorized = assurance.Judge(intent, canonical, run.View!, world.DeriveActionAssuranceView("screen.home"));
        Assert.True(authorized.IsAdmissible);

        // case 2：同 IntentId、同 BindingId、异 RevisionId → Gate 必须拒绝
        var wrongRevision = new AssuranceJudgment(
            intent.IntentId, canonical.BindingId, "rev-999",
            true, Array.Empty<AssuranceCheck>(), null, new FreshnessJudgment(FreshnessSufficiency.Sufficient, "scripted:sufficient"));
        var (gate, receipt) = effects.Dispatch(canonical, wrongRevision, world.DeriveBindingView("screen.home"));
        Assert.False(gate.Allowed);
        Assert.Equal("judgment-revision-mismatch", gate.Reason);
        Assert.Null(receipt);
        Assert.Empty(effects.ReceiptLog);
    }

    [Fact]
    public void Judge_RejectsCanonicalBindingOfDifferentIntent_FailsClosedAtProducerSide()
    {
        var (kernel, _, world, run, _, assurance, effects) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        // 产者侧 correlation（CBA-005 验收 3）：binding 属于 intentB，
        // 却配 intentA 来审 → Judge fail-closed 拒绝
        var intentA = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var intentB = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var canonicalForB = effects.Bind(
            intentB, new CandidateBinding("screen.home", "idle", "rev-1"), world.DeriveBindingView("screen.home")).Canonical!;

        var judgment = assurance.Judge(intentA, canonicalForB, run.View!, world.DeriveActionAssuranceView("screen.home"));
        Assert.False(judgment.IsAdmissible);
        Assert.Equal("binding-intent-correlation", judgment.RejectionReason);
        // 三元组记录的是被审 binding（D4）
        Assert.Equal(canonicalForB.BindingId, judgment.BindingId);
        Assert.Equal(canonicalForB.RevisionId, judgment.RevisionId);

        // null binding = API contract violation（验收 3）：不产生 judgment
        Assert.Throws<ArgumentNullException>(
            () => assurance.Judge(intentA, null!, run.View!, world.DeriveActionAssuranceView("screen.home")));
    }
}
