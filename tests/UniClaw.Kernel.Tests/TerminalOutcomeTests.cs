using System.Reflection;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Outcome;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// OUT-003 验收 1..10 + 核心反例 A-J —— 终局链确定性用例。
/// 纯内存 fake world（level: DETERMINISTIC）；scripted driver / observation
/// provider / policy 均为确定性测试替身。测试验证行为，不验证实现细节。
/// 终局链：Assurance Outcome Proof → Run Model terminal transition →
/// Uni Kernel Runtime Outcome (exactly once) → effect channel closed。
/// </summary>
public sealed class TerminalOutcomeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 8, 10, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 9, 8, 10, 10, 0, TimeSpan.Zero);

    /// <summary>World Model relevance scope：覆盖本片全部 obligation 的 subject。</summary>
    private static readonly IReadOnlySet<string> RelevantSubjects = new HashSet<string>
    {
        "screen.home", "screen.home.failure", "screen.home.blocked",
        "screen.home.subsequent", "screen.header", "screen.completion",
        "screen.escalate",
    };

    // ---- scripted 替身 ---------------------------------------------------

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

    /// <summary>合法完整的最小 Execution Contract；obligations 为本片显式 run-level 规格。</summary>
    private static ExecutionContract ContractWith(IReadOnlyList<RunObligation>? obligations = null) => new(
        Version: "c1",
        Objective: "verify-home-screen",
        Scope: new HashSet<string> { "screen.home" },
        AllowedEffects: new HashSet<string> { "tap" },
        ForbiddenEffects: new HashSet<string> { "swipe" },
        ProofCriteria: obligations?.Select(o => o.ObligationId).ToArray() ?? new[] { "home-screen-observed" },
        Obligations: obligations);

    /// <summary>成功路径 obligations：objective + material effect 双双 mandatory。</summary>
    private static IReadOnlyList<RunObligation> CompletionObligations() => new[]
    {
        new RunObligation("objective-home-active", RunObligationKind.Objective, "screen.home", "active", Mandatory: true),
        new RunObligation("effect-home-active", RunObligationKind.MaterialEffect, "screen.home", "active", Mandatory: true),
    };

    private sealed class ScriptedPolicy(string effectClass = "tap") : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, effectClass, "screen.home");
    }

    /// <summary>遍历耗尽型策略：只观察，不再 act（反例 J）。</summary>
    private sealed class ObserveOnlyPolicy : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Observe, EffectClass: null, TargetSubject: null);
    }

    private sealed class ScriptedDriver(params DispatchOutcome[] outcomes) : IEffectDriver
    {
        private readonly Queue<DispatchResult> _results =
            new(outcomes.Select((o, i) => new DispatchResult(o, $"scripted:{o}", T1.AddMinutes(i))));

        public DispatchResult Deliver(CanonicalBinding binding) => _results.Dequeue();
    }

    private static (UniKernel Kernel, EvidenceLedger Ledger, WorldModel World, RunModel Run,
        ControlLoop Control, RuntimeAssurance Assurance, EffectBoundary Effects) NewKernel(
        IEffectDriver? driver = null, IControlPolicy? policy = null)
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(RelevantSubjects);
        var run = new RunModel();
        var control = new ControlLoop(policy ?? new ScriptedPolicy());
        var assurance = new RuntimeAssurance(new FreshnessDoubles.Satisfying());
        var effects = new EffectBoundary(driver ?? new ScriptedDriver(DispatchOutcome.Delivered));
        var kernel = new UniKernel(ledger, world, run, control, assurance, effects);
        return (kernel, ledger, world, run, control, assurance, effects);
    }

    /// <summary>最小前置：contract 已接受 + rev-1（screen.home=idle）已确立。</summary>
    private static void PrimeWorld(UniKernel kernel) => kernel.Process(Observation("screen.home", "idle", T0));

    /// <summary>一次 tap act（dispatch Delivered；产生 attempt evidence 回流，零 revision）。</summary>
    private static ActResult ActOnce(UniKernel kernel)
    {
        var intent = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
        return kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));
    }

    /// <summary>post-action 效果观察（ING-006：context 显式声明 PostActionEffectFlow；producer/lineage 为 provenance）。</summary>
    private static void ObserveEffect(UniKernel kernel, ActResult act)
    {
        kernel.Process(Observation("screen.home", "active", T1,
            producer: "effect.boundary.observer",
            context: ObservationContext.PostActionEffectFlow,
            lineage: new[] { $"dispatch:{act.Receipt!.ReceiptId}" }));
    }

    // ---- 验收 3/8 正路径：Completion + emission --------------------------

    [Fact]
    public void Outcome1_CompletionRequiresSatisfiedMandatoryObligationsAndEmitsOutcome()
    {
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);

        var act = ActOnce(kernel);
        Assert.Equal(DispatchOutcome.Delivered, act.Receipt!.Outcome);

        // receipt 成功但无 post-action accepted Evidence → 不得 completion（反例 A 的一部分）
        var before = kernel.EvaluateTerminal();
        Assert.Null(before.Proof);
        Assert.False(before.Transition.Accepted);
        Assert.Null(before.Outcome);
        Assert.False(run.IsTerminal);

        // post-action 效果观察进入 belief（E2B 冲突词汇承载）→ mandatory 全满足
        ObserveEffect(kernel, act);

        var eval = kernel.EvaluateTerminal();
        Assert.NotNull(eval.Proof);
        Assert.Equal(TerminalClassification.Completion, eval.Proof!.Classification);
        Assert.True(eval.Transition.Accepted);
        Assert.True(run.IsTerminal);
        Assert.NotNull(run.State!.Outcome);
        Assert.Equal(TerminalClassification.Completion, run.State.Outcome!.Classification);

        // 全部 mandatory obligation 满足（验收 3）
        var mandatory = eval.Proof.Obligations.Where(o => o.Mandatory).ToList();
        Assert.NotEmpty(mandatory);
        Assert.All(mandatory, o => Assert.True(o.Satisfied));

        // emission 携带 proof ref 与 classification
        Assert.NotNull(eval.Outcome);
        Assert.Equal(eval.Proof.ProofId, eval.Outcome!.OutcomeProofId);
        Assert.Equal(TerminalClassification.Completion, eval.Outcome.Classification);
        Assert.Equal("run-1", eval.Outcome.RunId);
    }

    // ---- 验收 4 / 反例 A：Receipt success ≠ Completion --------------------

    [Fact]
    public void Outcome2_ReceiptSuccessIsNotCompletion()
    {
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);

        var act = ActOnce(kernel);
        Assert.Equal(DispatchOutcome.Delivered, act.Receipt!.Outcome);   // 最后一个 receipt 成功

        var eval = kernel.EvaluateTerminal();
        Assert.Null(eval.Proof);                 // 无 post-action accepted Evidence → 不得 completion（不变量 35）
        Assert.False(eval.Transition.Accepted);
        Assert.False(run.IsTerminal);
        Assert.Null(run.State!.Outcome);
        Assert.Null(eval.Outcome);
    }

    // ---- 验收 5 / 反例 B：Verified local Effect ≠ Completion --------------

    [Fact]
    public void Outcome3_VerifiedLocalEffectIsNotCompletion()
    {
        var obligations = new[]
        {
            new RunObligation("effect-home-active", RunObligationKind.MaterialEffect, "screen.home", "active", Mandatory: true),
            new RunObligation("subsequent-visible", RunObligationKind.Objective, "screen.home.subsequent", "visible", Mandatory: true),
        };
        var (kernel, ledger, world, run, _, assurance, _) = NewKernel();
        kernel.AdmitContract(ContractWith(obligations));
        PrimeWorld(kernel);

        var act = ActOnce(kernel);
        ObserveEffect(kernel, act);   // 局部 effect 已验证（self-produced 观察）

        // 证据层面：material effect obligation 确实 Satisfied
        var statuses = assurance.EvaluateObligations(
            run.State!.ProofObligations, world.Current!, ledger.CanonicalRecords);
        Assert.True(statuses.Single(o => o.ObligationId == "effect-home-active").Satisfied);
        Assert.False(statuses.Single(o => o.ObligationId == "subsequent-visible").Satisfied);

        // 但 mandatory 未全满足 → 不得 terminal success（反例 B / 不变量 35、37）
        var eval = kernel.EvaluateTerminal();
        Assert.Null(eval.Proof);
        Assert.False(eval.Transition.Accepted);
        Assert.False(run.IsTerminal);
        Assert.Null(run.State!.Outcome);
        Assert.Null(eval.Outcome);
    }

    // ---- 反例 C：Mandatory unresolved obligation --------------------------

    [Fact]
    public void Outcome4_MandatoryUnresolvedObligationBlocksCompletion()
    {
        var obligations = new[]
        {
            new RunObligation("objective-home-active", RunObligationKind.Objective, "screen.home", "active", Mandatory: true),
            new RunObligation("objective-header-visible", RunObligationKind.Objective, "screen.header", "visible", Mandatory: true),
        };
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(obligations));
        PrimeWorld(kernel);

        var act = ActOnce(kernel);
        ObserveEffect(kernel, act);   // 部分 satisfied（screen.home=active）

        var eval = kernel.EvaluateTerminal();
        Assert.Null(eval.Proof);      // 至少一个 mandatory unresolved → 不得 completion
        Assert.False(eval.Transition.Accepted);
        Assert.False(run.IsTerminal);
        Assert.Null(run.State!.Outcome);
    }

    // ---- 验收 6 / 反例 D(a)：Failure terminal ------------------------------

    [Fact]
    public void Outcome5_FailureTerminalFromEvidenceBackedProof()
    {
        var obligations = new[]
        {
            new RunObligation("objective-home-active", RunObligationKind.Objective, "screen.home", "active", Mandatory: true),
            new RunObligation("failure-signal", RunObligationKind.Failure, "screen.home.failure", "failed", Mandatory: true),
        };
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(obligations));
        PrimeWorld(kernel);

        // 环境失败证据（accepted、relevant）
        var failureEvidence = kernel.Process(Observation("screen.home.failure", "failed", T1));

        var eval = kernel.EvaluateTerminal();
        Assert.NotNull(eval.Proof);
        Assert.Equal(TerminalClassification.Failure, eval.Proof!.Classification);
        Assert.True(eval.Transition.Accepted);
        Assert.True(run.IsTerminal);
        Assert.Equal(TerminalClassification.Failure, run.State!.Outcome!.Classification);
        Assert.Equal(TerminalClassification.Failure, eval.Outcome!.Classification);

        // failure proof 独立成立：objective 未满足（active 未观察）仍可 failure terminal
        Assert.False(eval.Proof.Obligations.Single(o => o.ObligationId == "objective-home-active").Satisfied);
        Assert.True(eval.Proof.Obligations.Single(o => o.ObligationId == "failure-signal").Satisfied);
        Assert.Contains(failureEvidence.Admission.EvidenceId!, eval.Outcome.SituationEvidenceIds);
    }

    // ---- 验收 6 / 反例 D(b)：Safe-stop terminal ----------------------------

    [Fact]
    public void Outcome6_SafeStopTerminalFromEvidenceBackedProof()
    {
        var obligations = new[]
        {
            new RunObligation("blocked-signal", RunObligationKind.SafeStop, "screen.home.blocked", "unreachable", Mandatory: true),
        };
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(obligations));
        PrimeWorld(kernel);

        kernel.Process(Observation("screen.home.blocked", "unreachable", T1));

        var eval = kernel.EvaluateTerminal();
        Assert.NotNull(eval.Proof);
        Assert.Equal(TerminalClassification.SafeStop, eval.Proof!.Classification);
        Assert.True(eval.Transition.Accepted);
        Assert.True(run.IsTerminal);
        Assert.Equal(TerminalClassification.SafeStop, run.State!.Outcome!.Classification);
        Assert.Equal(TerminalClassification.SafeStop, eval.Outcome!.Classification);
    }

    // ---- 反例 E：Evidence insufficient 不猜测分类 --------------------------

    [Fact]
    public void Outcome7_EvidenceInsufficientDoesNotGuessClassification()
    {
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);   // 只有 idle，无任何成功/失败/停顿证据

        var eval = kernel.EvaluateTerminal();
        Assert.Null(eval.Proof);          // 既不能证明 success 也不能证明 failure → 不猜测
        Assert.False(eval.Transition.Accepted);
        Assert.False(run.IsTerminal);
        Assert.Null(run.State!.Outcome);
        Assert.Null(eval.Outcome);        // zero emission
    }

    // ---- 验收 7 / 反例 F：Concurrent terminal proposals single-winner -----

    [Fact]
    public void Outcome8_ConcurrentTerminalProposalsSingleWinner()
    {
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);
        var act = ActOnce(kernel);
        ObserveEffect(kernel, act);

        var first = kernel.EvaluateTerminal();    // proposal 1 胜出
        Assert.True(first.Transition.Accepted);
        Assert.NotNull(first.Outcome);

        var second = kernel.EvaluateTerminal();   // proposal 2 被拒
        Assert.False(second.Transition.Accepted);
        Assert.Equal("already-terminal", second.Transition.Reason);
        Assert.Null(second.Outcome);

        // 绝不产生两个 Runtime Outcome：History 中 terminal 状态唯一
        Assert.Single(run.History.Where(s => s.Outcome is not null));
    }

    // ---- 验收 7 直接面：exact-prior CAS + terminal 后不可恢复 active -------

    [Fact]
    public void Outcome9_TerminalTransitionExactPriorAndNoReactivate()
    {
        var (kernel, ledger, world, run, _, assurance, _) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);
        var act = ActOnce(kernel);
        ObserveEffect(kernel, act);   // rev-2 非 terminal

        var prior = run.State!;
        var proof = assurance.JudgeOutcome(
            run.View!, prior.ProofObligations, world.Current!, ledger.CanonicalRecords)!;
        Assert.Equal(TerminalClassification.Completion, proof.Classification);

        var t1 = run.TransitionToTerminal(proof, prior);
        Assert.True(t1.Accepted);
        Assert.NotNull(t1.TerminalState);
        Assert.True(run.IsTerminal);

        // 同一 prior 再来一个 proposal → exact-prior 拒绝
        var t2 = run.TransitionToTerminal(proof, prior);
        Assert.False(t2.Accepted);
        Assert.Equal("concurrent-terminal-proposal", t2.Reason);

        // 已 terminal 再来 → rejected
        var t3 = run.TransitionToTerminal(proof, run.State!);
        Assert.False(t3.Accepted);
        Assert.Equal("already-terminal", t3.Reason);

        // terminal 后不得回到 active（任务 六.5）
        Assert.Throws<InvalidOperationException>(() => run.RecordCycle());
        Assert.Throws<InvalidOperationException>(() => run.RecordAction());
    }

    // ---- 验收 8：Runtime Outcome immutable + exactly once ------------------

    [Fact]
    public void Outcome10_RuntimeOutcomeImmutableExactlyOnce()
    {
        var obligations = new[]
        {
            new RunObligation("objective-home-active", RunObligationKind.Objective, "screen.home", "active", Mandatory: true),
            new RunObligation("optional-header-visible", RunObligationKind.Objective, "screen.header", "visible", Mandatory: false),
        };
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(obligations));
        PrimeWorld(kernel);
        var act = ActOnce(kernel);
        ObserveEffect(kernel, act);

        var eval = kernel.EvaluateTerminal();
        Assert.NotNull(eval.Outcome);
        var outcome = eval.Outcome!;

        // envelope 纯由 terminal OutcomeState 投影（Kernel 不重判、不解析、不改 classification）
        var state = run.State!.Outcome!;
        Assert.Equal(state.OutcomeProofId, outcome.OutcomeProofId);
        Assert.Equal(state.Classification, outcome.Classification);
        Assert.Equal(state.Obligations, outcome.Obligations);
        Assert.Equal(state.BasisEvidenceIds, outcome.BasisEvidenceIds);
        Assert.Equal(state.EffectEvidenceIds, outcome.EffectEvidenceIds);
        Assert.Equal(state.SituationEvidenceIds, outcome.SituationEvidenceIds);
        Assert.Equal(state.UnresolvedUncertainty, outcome.UnresolvedUncertainty);
        Assert.Equal(state.Reason, outcome.Reason);

        // 带 fulfilled + unfulfilled obligations（non-mandatory unresolved 如实记录）
        Assert.Contains(outcome.Obligations, o => o.Satisfied);
        Assert.Contains(outcome.Obligations, o => !o.Satisfied);
        Assert.Equal(1, outcome.UnresolvedUncertainty);   // idle/active 冲突

        // immutable：record 值语义——with 表达式产生新实例，原实例不被原地改写
        var rewritten = outcome with { Reason = "hacked" };
        Assert.NotSame(rewritten, outcome);
        Assert.NotEqual("hacked", outcome.Reason);

        // exactly once：第二次 EvaluateTerminal 不再 emit
        var again = kernel.EvaluateTerminal();
        Assert.False(again.Transition.Accepted);
        Assert.Null(again.Outcome);
        Assert.Single(run.History.Where(s => s.Outcome is not null));
    }

    // ---- 验收 9 / 任务八：terminal 后 effect channel closed ----------------

    [Fact]
    public void Outcome11_TerminalClosesEffectChannel()
    {
        var (kernel, _, world, run, control, _, effects) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);
        var act = ActOnce(kernel);
        ObserveEffect(kernel, act);

        kernel.EvaluateTerminal();
        Assert.True(run.IsTerminal);
        Assert.True(effects.IsDeliveryClosed);
        var receiptsBefore = effects.ReceiptLog.Count;

        // (1) terminal 后新的 Control Intent 即使存在，也不能变成 effect
        var lateIntent = control.SelectIntent(run.View!, kernel.DeriveSlice("screen.home"), run.State!);
        Assert.Equal(ControlIntentKind.Act, lateIntent.Kind);   // intent 存在
        Assert.Throws<InvalidOperationException>(
            () => kernel.Act(lateIntent, new CandidateBinding("screen.home", "active", "rev-2")));
        Assert.Equal(receiptsBefore, effects.ReceiptLog.Count);

        // (2) late candidate binding 不能 dispatch（直连 Dispatch 被 delivery latch 拒绝）
        var canonical = effects.Bind(lateIntent, new CandidateBinding("screen.home", "active", "rev-2"), world.Current!).Canonical!;
        Assert.NotNull(canonical);   // binding 可形成，但不能成为 effect
        var gate = effects.Dispatch(
            canonical, new AssuranceJudgment(
                lateIntent.IntentId, canonical.BindingId, canonical.RevisionId,
                true, Array.Empty<AssuranceCheck>(), null, new FreshnessJudgment(FreshnessSufficiency.Sufficient, "scripted:sufficient")),
            world.Current!);
        Assert.False(gate.Gate.Allowed);
        Assert.Equal("delivery-closed", gate.Gate.Reason);
        Assert.Null(gate.Receipt);
        Assert.Equal(receiptsBefore, effects.ReceiptLog.Count);

        // (3) late authorization 不能 dispatch（同一 latch，与 judgment 内容无关）
        var gate2 = effects.Dispatch(
            canonical, new AssuranceJudgment(
                lateIntent.IntentId, canonical.BindingId, canonical.RevisionId,
                false, Array.Empty<AssuranceCheck>(), "safety", new FreshnessJudgment(FreshnessSufficiency.Sufficient, "scripted:sufficient")),
            world.Current!);
        Assert.False(gate2.Gate.Allowed);
        Assert.Equal("delivery-closed", gate2.Gate.Reason);
        Assert.Null(gate2.Receipt);

        // (4) late driver callback 不得重新打开 Run（progress 转移 fail-closed）
        Assert.Throws<InvalidOperationException>(() => run.RecordAction());
        Assert.Throws<InvalidOperationException>(() => run.RecordCycle());

        // (5) kernel 组合面同步 fail-closed（intent 签发与 act 均关门）
        Assert.Throws<InvalidOperationException>(() => kernel.SelectIntent(kernel.DeriveSlice("screen.home")));
        Assert.Equal(receiptsBefore, effects.ReceiptLog.Count);
    }

    // ---- 验收 10 / 反例 G：Late receipt 可记录但不能恢复/改写 --------------

    [Fact]
    public void Outcome12_LateReceiptRecordedButCannotResumeOrRewrite()
    {
        var (kernel, _, _, run, _, _, effects) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);
        var act = ActOnce(kernel);          // dispatch 在 terminal 前，receipt 已留痕
        ObserveEffect(kernel, act);
        kernel.EvaluateTerminal();

        var terminal = run.State!.Outcome!;
        var receiptsBefore = effects.ReceiptLog.Count;

        // terminal 后收到该 dispatch 的 attempt evidence（回流迟到）→ 可记录为 evidence（E2B 路径不变）
        var late = kernel.Process(Observation("attempt.screen.home", "delivered", T2,
            producer: "effect.boundary",
            kind: IngressKind.AttemptReport,
            context: ObservationContext.PostActionEffectFlow,
            lineage: new[] { $"dispatch:{act.Receipt!.ReceiptId}" }));
        Assert.Equal(AdmissionDecision.Accepted, late.Admission.Decision);
        Assert.False(late.Relevance!.IsRelevant);   // attempt.* 世界无关，零 revision

        // 不得改 Outcome State、不得恢复 Run、不得产生新 receipt
        Assert.Same(terminal, run.State!.Outcome);
        Assert.True(run.IsTerminal);
        Assert.Equal(receiptsBefore, effects.ReceiptLog.Count);
        var again = kernel.EvaluateTerminal();
        Assert.Null(again.Outcome);
    }

    // ---- 验收 10 / 反例 H：Late evidence 可追加但不改写 terminal truth -----

    [Fact]
    public void Outcome13_LateEvidenceAppendedButTerminalTruthNotRewritten()
    {
        var (kernel, _, world, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);
        var act = ActOnce(kernel);
        ObserveEffect(kernel, act);
        kernel.EvaluateTerminal();

        var terminal = run.State!.Outcome!;
        Assert.Equal(2, world.Current!.RevisionNumber);

        // 历史 evidence 可追加：terminal 后仍走既有 E2B admission/reconciliation 路径
        var late = kernel.Process(Observation("screen.home.subsequent", "visible", T2));
        Assert.NotNull(late.ResultingRevision);
        Assert.Equal(3, world.Current.RevisionNumber);   // belief 继续演进（world-owned）

        // terminal Outcome 不可被静默改写；Run 不恢复
        Assert.Same(terminal, run.State!.Outcome);
        Assert.True(run.IsTerminal);
        Assert.Single(run.History.Where(s => s.Outcome is not null));
        Assert.Throws<InvalidOperationException>(() => run.RecordCycle());
    }

    // ---- 验收 1/10 / 反例 I：Provider claim 不能写 Outcome State -----------

    [Fact]
    public void Outcome14_ProviderCompletionClaimCannotWriteOutcomeState()
    {
        var (kernel, _, world, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);

        // provider 宣称 completed —— 只能作为 proposal / raw output 进入 belief
        kernel.Process(Observation("screen.completion", "true", T1));
        Assert.True(world.Current!.WorldState.ContainsKey("screen.completion"));   // 规范化进 belief

        var eval = kernel.EvaluateTerminal();
        Assert.Null(eval.Proof);              // 不满足 contract obligations → 不能形成 completion proof
        Assert.False(eval.Transition.Accepted);
        Assert.False(run.IsTerminal);
        Assert.Null(run.State!.Outcome);      // provider 声明永不直达 Outcome State
        Assert.Null(eval.Outcome);
    }

    // ---- 反例 J：Control Loop 声明无更多工作 ≠ completion ------------------

    [Fact]
    public void Outcome15_ControlLoopClaimsNoMoreWorkIsNotCompletion()
    {
        var (kernel, _, _, run, control, _, _) = NewKernel(policy: new ObserveOnlyPolicy());
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);

        for (var i = 0; i < 3; i++)
        {
            var intent = kernel.SelectIntent(kernel.DeriveSlice("screen.home"));
            Assert.Equal(ControlIntentKind.Observe, intent.Kind);   // traversal 只在观察
        }
        Assert.Equal(3, run.State!.Progress.Cycles);

        // traversal exhausted / 无 progress / stack empty —— 不等于 completion
        Assert.False(run.IsTerminal);
        Assert.Null(run.State!.Outcome);
        var eval = kernel.EvaluateTerminal();
        Assert.Null(eval.Proof);              // 必须经过 Outcome Proof；控制面声明不算
        Assert.False(run.IsTerminal);

        // 结构证据：Control Loop 没有任何产出 terminal 判断的 API（不变量 29）
        Assert.Empty(Enum.GetNames<ControlIntentKind>()
            .Where(n => n.Contains("Complete") || n.Contains("Success")
                     || n.Contains("Done") || n.Contains("Terminal")));
        Assert.Empty(typeof(ControlLoop).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.ReturnType == typeof(OutcomeProof)
                     || m.ReturnType == typeof(RuntimeOutcome)
                     || m.ReturnType == typeof(OutcomeState)));
        Assert.NotEmpty(control.IntentLog);
    }

    // ---- 反例 D(c)：Escalation terminal（四分类独立 proof 齐备）------------

    [Fact]
    public void Outcome17_EscalationTerminalFromEvidenceBackedProof()
    {
        var obligations = new[]
        {
            new RunObligation("escalation-signal", RunObligationKind.Escalation, "screen.escalate", "escalate", Mandatory: true),
        };
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(obligations));
        PrimeWorld(kernel);

        kernel.Process(Observation("screen.escalate", "escalate", T1));

        var eval = kernel.EvaluateTerminal();
        Assert.NotNull(eval.Proof);
        Assert.Equal(TerminalClassification.Escalation, eval.Proof!.Classification);
        Assert.True(eval.Transition.Accepted);
        Assert.True(run.IsTerminal);
        Assert.Equal(TerminalClassification.Escalation, run.State!.Outcome!.Classification);
        Assert.Equal(TerminalClassification.Escalation, eval.Outcome!.Classification);
    }

    // ---- Review F2 回归：空 mandatory 集不得 vacuous completion ------------

    [Fact]
    public void Outcome18_VacuousCompletionIsPreventedWithoutMandatoryObligations()
    {
        // 全部 obligation 均非 mandatory：即使全部满足，也不得伪装 completion
        var obligations = new[]
        {
            new RunObligation("objective-home-active", RunObligationKind.Objective, "screen.home", "active", Mandatory: false),
            new RunObligation("effect-home-active", RunObligationKind.MaterialEffect, "screen.home", "active", Mandatory: false),
        };
        var (kernel, _, _, run, _, _, _) = NewKernel();
        kernel.AdmitContract(ContractWith(obligations));
        PrimeWorld(kernel);
        var act = ActOnce(kernel);
        ObserveEffect(kernel, act);   // 全部 obligation 已满足

        var eval = kernel.EvaluateTerminal();
        Assert.Null(eval.Proof);      // 无 mandatory 证明义务 → 无 completion proof
        Assert.False(eval.Transition.Accepted);
        Assert.False(run.IsTerminal);
        Assert.Null(run.State!.Outcome);
    }

    // ---- 验收 1/2：唯一 Authority + Run 只记录 ------------------------------

    [Fact]
    public void Outcome16_AssuranceIsSoleOutcomeProofAuthorityAndRunOnlyRecords()
    {
        // (a) 全 kernel 程序集：唯一产出 OutcomeProof 的公共方法 = RuntimeAssurance.JudgeOutcome
        // （排除 record 编译器生成的 <Clone>$ 拷贝方法与被拒的访问器）
        var producers = typeof(OutcomeProof).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => !m.IsSpecialName
                && !m.Name.Contains("<Clone>$", StringComparison.Ordinal)
                && m.ReturnType == typeof(OutcomeProof))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        Assert.Equal(new[] { "RuntimeAssurance.JudgeOutcome" }, producers);

        // (b) Run State 对象图不含 Assurance 类型（Run Model 记录，不判断；不变量 38）
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

        // (c) Run-level obligation 词汇不含 action-local 状态（任务 五；不变量 36）
        var actionLocalFields = new[] { "Freshness", "Admissib", "Precondition", "Grounding", "Retry" };
        foreach (var type in new[] { typeof(RunObligation), typeof(ObligationStatus), typeof(ProofObligationState) })
            Assert.All(type.GetProperties(),
                p => Assert.DoesNotContain(actionLocalFields, banned => p.Name.Contains(banned, StringComparison.OrdinalIgnoreCase)));

        // (d) 行为闭环：Receipt（attempt evidence）永不确定 obligation 满足
        var (kernel, ledger, world, run, _, assurance, _) = NewKernel();
        kernel.AdmitContract(ContractWith(CompletionObligations()));
        PrimeWorld(kernel);
        ActOnce(kernel);   // receipt 回流 admitted，但 attempt.* 世界无关
        var statuses = assurance.EvaluateObligations(
            run.State!.ProofObligations, world.Current!, ledger.CanonicalRecords);
        Assert.All(statuses, s => Assert.False(s.Satisfied));   // receipt 满足不了任何 obligation
    }
}
