using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// CDS-001 验收（S1–S7）—— desired-state satisfaction（HD-4 / ADR-0017 /
/// I-3 事前保证）。纯内存确定性。核心断言：已满足 → 零 Act intent / 零
/// dispatch（U2 / destructive correctness 闭合）。container 种子与 owner
/// 预知照抄 ControlReferencePolicyTests ProbeContainerId 模式。
/// </summary>
public sealed class DesiredStateSatisfactionTests
{
    private const string SeedValue = "ds-satisfaction-seed";

    // ---- scripted 替身 ---------------------------------------------------

    /// <summary>固定 occurrence 景观（含可选 state）的 observation strategy。</summary>
    private sealed class FixedObservationStrategy(string owner, params (string Role, string? State)[] occurrences)
        : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            occurrences.Select(o => new ProposedOccurrence(owner, o.Role, "wifi-switch", o.State)).ToArray();
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:ok", UIWorldDoubles.T1);
    }

    /// <summary>SeedContainer 首条 evidence 探针（ControlReferencePolicyTests
    /// ProbeContainerId 同法）：确定性预知 minted root container id。</summary>
    private static string ProbeContainerId() =>
        "ctr-" + new EvidenceLedger()
            .Admit(UIWorldDoubles.Observation(SeedValue, UIWorldDoubles.T0))
            .Admission.EvidenceId![3..15];

    private static (UniKernel Kernel, ControlLoop Control, EffectBoundary Effects) NewKernel(
        IUiObservationStrategy observation, IControlPolicy policy)
    {
        var (kernel, control, effects, _) = NewKernelWithAssurance(observation, policy);
        return (kernel, control, effects);
    }

    // S6 词汇隔离（RVR-002 H1 / CDS-001 S6）需要观察 Assurance 留痕与
    // freshness evaluator 调用面，故拆出返回 assurance 的组装变体。
    private static (UniKernel Kernel, ControlLoop Control, EffectBoundary Effects, RuntimeAssurance Assurance)
        NewKernelWithAssurance(
            IUiObservationStrategy observation, IControlPolicy policy,
            IFreshnessEvaluator? freshness = null)
    {
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            new SeedContainerAssociationStrategy(), observation);
        var control = new ControlLoop(policy);
        var effects = new EffectBoundary(new OkDriver());
        var assurance = new RuntimeAssurance(freshness ?? new FreshnessDoubles.Satisfying());
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), control,
            assurance, effects);
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: "set-the-toggle",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "set-switch" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "toggle-set" }));
        return (kernel, control, effects, assurance);
    }

    private static void Prime(UniKernel kernel) =>
        kernel.Process(UIWorldDoubles.Observation(SeedValue, UIWorldDoubles.T0));

    private static TargetSpec ToggleSpec(string? desiredState) =>
        new("toggle", "wifi-switch", "set-switch", desiredState);

    // ---- S1 载荷：state 全链可见（strategy → belief → Slice） -----------

    [Fact]
    public void S1_OccurrenceStateFlowsFromStrategyThroughBeliefToSlice()
    {
        var owner = ProbeContainerId();
        var (kernel, _, _) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", "true")),
            new DescriptorTargetPolicy(new[] { ToggleSpec(null) }));
        Prime(kernel);

        var occurrence = Assert.Single(kernel.DeriveSlice(owner).Occurrences);
        Assert.Equal("toggle", occurrence.Role);
        Assert.Equal("true", occurrence.State); // Known(value)
    }

    [Fact]
    public void S1b_NullStateIsUnknownNotFalse()
    {
        var owner = ProbeContainerId();
        var (kernel, _, _) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", null)),
            new DescriptorTargetPolicy(new[] { ToggleSpec(null) }));
        Prime(kernel);

        var occurrence = Assert.Single(kernel.DeriveSlice(owner).Occurrences);
        Assert.Null(occurrence.State); // Unknown ≠ false（二态 epistemic，ADR-0017）
    }

    // ---- S2 已满足 → Observe / 零 dispatch / 零 binding ------------------

    [Fact]
    public void S2_SatisfiedSpecYieldsObserveNoDispatchNoBinding()
    {
        var owner = ProbeContainerId();
        var policy = new DescriptorTargetPolicy(new[] { ToggleSpec("true") });
        var (kernel, _, effects) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", "true")), policy);
        Prime(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));

        // 已满足：policy 完成 spec（visited）→ Observe；零 Act intent
        Assert.Equal(ControlIntentKind.Observe, intent.Kind);
        Assert.Contains(("toggle", "wifi-switch"), policy.Visited);
        Assert.Empty(effects.BindingLog);   // 零 binding 认定
        Assert.Empty(effects.ReceiptLog);   // 零 dispatch
    }

    // ---- S3 未满足 → Act 全链不变 ---------------------------------------

    [Fact]
    public void S3_UnsatisfiedSpecActsThroughFullChain()
    {
        var owner = ProbeContainerId();
        var (kernel, _, effects) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", "false")),
            new DescriptorTargetPolicy(new[] { ToggleSpec("true") }));
        Prime(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));
        Assert.Equal(ControlIntentKind.Act, intent.Kind);

        var act = kernel.ActViaCurrentGrounding(intent,
            new TargetDescriptor("toggle", "wifi-switch"));
        Assert.True(act.Act!.Gate!.Allowed);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, act.Act.Receipt!.Outcome);
        Assert.Single(effects.ReceiptLog);
    }

    // ---- S4 Unknown → 跳过不 visited（fail-closed 不 dispatch） ----------

    [Fact]
    public void S4_UnknownStateSkipsSpecWithoutVisiting()
    {
        var owner = ProbeContainerId();
        var policy = new DescriptorTargetPolicy(new[] { ToggleSpec("true") });
        var (kernel, _, effects) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", null)), policy);
        Prime(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));

        Assert.Equal(ControlIntentKind.Observe, intent.Kind);
        Assert.Empty(policy.Visited);       // Unknown ≠ 完成——待新观察可判
        Assert.Empty(effects.ReceiptLog);   // fail-closed：零 dispatch
    }

    // ---- S5 端到端 I-3 / U2：SetSwitch(true) on already-true 零物理动作 --

    [Fact]
    public void S5_NonIdempotentSetOnAlreadySatisfiedTargetIsZeroPhysicalAction()
    {
        var owner = ProbeContainerId();
        var (kernel, _, effects) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", "true")),
            new DescriptorTargetPolicy(new[] { ToggleSpec("true") }));
        Prime(kernel);

        // 多轮 control cycle：已满足 → 恒 Observe → 恒零物理动作
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));
            Assert.Equal(ControlIntentKind.Observe, intent.Kind);
        }
        Assert.Empty(effects.ReceiptLog);
        Assert.Empty(effects.BindingLog);
    }

    // ---- S6 DesiredState=null（Click 型）不做检查 ------------------------

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(null)]
    public void S6_NullDesiredStateSkipsSatisfactionCheck(string? state)
    {
        var owner = ProbeContainerId();
        var (kernel, _, _) = NewKernel(
            new FixedObservationStrategy(owner, ("toggle", state)),
            new DescriptorTargetPolicy(new[] { ToggleSpec(null) }));
        Prime(kernel);

        // state 任意 → 照常 Act（Click 型 intent 无期望终态，不适用 satisfaction）
        var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));
        Assert.Equal(ControlIntentKind.Act, intent.Kind);
    }

    // ---- S6 词汇隔离（RVR-002 H1 / CDS-001 S6 真 S6）---------------------
    // 「satisfaction Unknown 不与 FreshnessJudgment Unknown 混用」：Control 轴
    // 的 satisfaction-Unknown 处置路径与 Assurance 轴的 freshness 判定互不
    // 消费对方的词汇。可观察面选择：FreshnessJudgment 是 Judge 内部产出
    // （运行时唯一产出点 = RuntimeAssurance.Judge 经 IFreshnessEvaluator），
    // 故 Control 轴以「evaluator 零调用 + JudgmentLog 零留痕」断言无任何
    // freshness 语义参与；Assurance 轴以「同一 revision 上两 view 各自判定、
    // 结果互不含对方词汇」断言（Judge checks 无 obligation 项；
    // ObligationStatus 无 freshness 维度）。

    /// <summary>计数 freshness 替身：记录 Evaluate 调用次数（freshness 词汇
    /// 是否被某条路径消费的最小可观察留痕）。</summary>
    private sealed class CountingFreshness : IFreshnessEvaluator
    {
        public int CallCount { get; private set; }

        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input)
        {
            CallCount++;
            return new(FreshnessSufficiency.Sufficient, "scripted:sufficient");
        }
    }

    [Fact]
    public void S6_VocabularyIsolation_ScenarioA_SatisfactionUnknownPathNeverTouchesFreshness()
    {
        // Control 轴：DesiredState="on" 而 State=null → satisfaction Unknown
        // → policy 跳过不签发（同 S4 场景），但本测试断言的是**隔离**：
        // 该处置路径全程零 FreshnessJudgment 参与。
        var owner = ProbeContainerId();
        var freshness = new CountingFreshness();
        var policy = new DescriptorTargetPolicy(new[] { ToggleSpec("true") });
        var (kernel, _, effects, assurance) = NewKernelWithAssurance(
            new FixedObservationStrategy(owner, ("toggle", null)), policy, freshness);
        Prime(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(owner));

        // satisfaction Unknown → 跳过不 visited、不签发（S4 语义）
        Assert.Equal(ControlIntentKind.Observe, intent.Kind);
        Assert.Empty(policy.Visited);
        Assert.Empty(effects.BindingLog);
        Assert.Empty(effects.ReceiptLog);

        // 隔离断言：零 freshness evaluator 调用、零 AssuranceJudgment 留痕
        // ——satisfaction-Unknown 的处置不产出也不消费 freshness 语义。
        Assert.Equal(0, freshness.CallCount);
        Assert.Empty(assurance.JudgmentLog);
    }

    [Fact]
    public void S6_VocabularyIsolation_ScenarioB_FreshnessSufficientAndEntityFactUnknownStayIndependent()
    {
        // Assurance 轴：同一 belief（rev-1）上 FreshnessBasis 充分
        // （FreshnessJudgment → Sufficient）与 entity obligation fact Unknown
        // （occurrence state 缺失）同时成立——两轴各自判定，词汇不互串。
        var freshness = new CountingFreshness();
        var assurance = new RuntimeAssurance(freshness);

        // freshness 侧（ActionAssuranceView：无 obligation/satisfaction 通道）
        var actionView = new ActionAssuranceView(
            "rev-1", 1, new FreshnessBasis(UIWorldDoubles.T0), HasConflictOnTarget: false);
        var intent = new ControlIntent("intent-s6b", ControlIntentKind.Act, "set-switch", "toggle", "rev-1");
        var binding = new CanonicalBinding(
            "bind-s6b", intent.IntentId, "set-switch", "toggle", "true", "rev-1", 1);
        var contractView = new ExecutionContractView(
            "c1", "set-the-toggle",
            new HashSet<string> { UIWorldDoubles.Observed },
            new HashSet<string> { "set-switch" },
            new HashSet<string>(),
            new[] { "toggle-set" });

        var judgment = assurance.Judge(intent, binding, contractView, actionView);

        // entity fact Unknown 不触发 freshness 拒绝：freshness 照常 Sufficient、
        // judgment admissible，且检查集不含任何 obligation/satisfaction 项
        //（freshness 判定不消费 satisfaction 词汇）。
        Assert.True(judgment.IsAdmissible);
        Assert.Null(judgment.RejectionReason);
        Assert.Equal(FreshnessSufficiency.Sufficient, judgment.Freshness.Sufficiency);
        Assert.DoesNotContain(judgment.Checks, c =>
            c.Name.Contains("oblig", StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains("satisf", StringComparison.OrdinalIgnoreCase));
        var freshnessCallsAfterJudge = freshness.CallCount;
        Assert.Equal(1, freshnessCallsAfterJudge);

        // satisfaction 侧（OutcomeAssuranceView：无 freshness 通道）——同一
        // rev-1 的 entity obligation fact Unknown：FreshnessJudgment Sufficient
        // 不使 obligation 满足（freshness 不能提升 satisfaction）。
        var outcomeView = new OutcomeAssuranceView(
            "rev-1", ConflictingClaimCount: 0,
            Claims: new Dictionary<string, ScopedClaim>(),
            Conflicts: Array.Empty<Conflict>(),
            BasisEvidenceIds: new HashSet<string>(),
            EntityFacts: new[] { new EntityObligationFact("obl-toggle-on", EntityObligationFactKind.Unknown) });
        var obligations = new ProofObligationState(new[]
        {
            new RunObligation(
                "obl-toggle-on", RunObligationKind.Objective,
                Subject: "toggle", RequiredValue: "on", Mandatory: true,
                EntityScope: new TargetDescriptor("toggle", "wifi-switch")),
        });

        var status = Assert.Single(assurance.EvaluateObligations(
            obligations, outcomeView, new Dictionary<string, EvidenceRecord>()));

        // Unknown fact 如实未满足（不伪装满足）；ObligationStatus 无 freshness
        // 维度——satisfaction 判定不消费 freshness 词汇，反之亦然（Evaluate
        // 路径零 evaluator 调用）。
        Assert.Equal("obl-toggle-on", status.ObligationId);
        Assert.False(status.Satisfied);
        Assert.Null(status.BackingEvidenceId);
        Assert.Equal(freshnessCallsAfterJudge, freshness.CallCount);
    }
}
