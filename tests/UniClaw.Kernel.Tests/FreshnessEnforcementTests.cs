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
/// FRS-007 验收 1..8 —— Pressure Scenario 14 真实锚点（N1-N7）。
/// 纯内存（level: DETERMINISTIC）；freshness evaluator 为脚本化确定性
/// 替身（无时钟 / 无阈值——Deferred ④/⑪ 不偷解）。测试验证行为，
/// 不验证实现细节（N5/N7 的结构性断言对应验收 2/4 的保护面）。
/// ADR-0010：freshness = Freshness Basis × Consumption Requirement 的
/// 消费相对判断；revision currentness ≠ freshness。
/// </summary>
public sealed class FreshnessEnforcementTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 9, 10, 5, 0, TimeSpan.Zero);

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

    /// <summary>最小 contract（默认允许 tap、禁止 swipe；双消费用例覆写）。</summary>
    private static ExecutionContract Contract(
        IReadOnlySet<string>? allowed = null,
        IReadOnlySet<string>? forbidden = null) => new(
        Version: "c1",
        Objective: "verify-home-screen",
        Scope: new HashSet<string> { "screen.home" },
        AllowedEffects: allowed ?? new HashSet<string> { "tap" },
        ForbiddenEffects: forbidden ?? new HashSet<string> { "swipe" },
        ProofCriteria: new[] { "home-screen-observed" });

    /// <summary>默认申请对 screen.home 的 tap。</summary>
    private sealed class ScriptedPolicy(string effectClass = "tap") : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, effectClass, "screen.home");
    }

    /// <summary>序列策略：按队列依次产出指定 effect class 的 act。</summary>
    private sealed class SequencedPolicy(params string[] effectClasses) : IControlPolicy
    {
        private readonly Queue<string> _queue = new(effectClasses);
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, _queue.Dequeue(), "screen.home");
    }

    private sealed class ScriptedDriver(params DispatchOutcome[] outcomes) : IEffectDriver
    {
        private readonly Queue<DispatchResult> _results =
            new(outcomes.Select((o, i) => new DispatchResult(o, $"scripted:{o}", T1.AddMinutes(i))));
        public DispatchResult Deliver(CanonicalBinding binding) => _results.Dequeue();
    }

    private static (UniKernel Kernel, EvidenceLedger Ledger, WorldModel World, RunModel Run,
        ControlLoop Control, RuntimeAssurance Assurance, EffectBoundary Effects) NewKernel(
        IControlPolicy? policy = null,
        IFreshnessEvaluator? evaluator = null,
        IEffectDriver? driver = null)
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" }, new SeedContainerAssociationStrategy());
        var run = new RunModel();
        var control = new ControlLoop(policy ?? new ScriptedPolicy());
        var assurance = new RuntimeAssurance(evaluator ?? new FreshnessDoubles.Satisfying());
        var effects = new EffectBoundary(driver ?? new ScriptedDriver(DispatchOutcome.Delivered));
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, run, control, assurance, effects);
        return (kernel, ledger, world, run, control, assurance, effects);
    }

    private static void PrimeWorld(UniKernel kernel) => kernel.Process(Observation("screen.home", "idle", T0));

    // ---- N1：Scenario 14 主锚 ---------------------------------------------

    [Fact]
    public void N1_CurrentRevisionWithInsufficientFreshnessIsRejectedWithoutDispatch()
    {
        // evaluator 判 Insufficient；revision 仍 current（rev-1，无任何新 evidence）
        var (kernel, _, world, run, _, _, effects) = NewKernel(
            evaluator: new FreshnessDoubles.ByEffectClass(rules: [("tap", FreshnessSufficiency.Insufficient)]));
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var act = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));

        // Scenario 14：revision current ∧ freshness insufficient → 拒绝
        Assert.False(act.Judgment!.IsAdmissible);
        Assert.Equal("freshness-sufficiency", act.Judgment.RejectionReason);
        Assert.False(act.Judgment.Checks.Single(c => c.Name == "freshness-sufficiency").Passed);
        Assert.Equal(FreshnessSufficiency.Insufficient, act.Judgment.Freshness!.Sufficiency);

        // Gate 只执法既有 judgment：零 dispatch、零 receipt、零回流副作用
        Assert.False(act.Gate!.Allowed);
        Assert.Equal("not-authorized", act.Gate.Reason);
        Assert.Null(act.Receipt);
        Assert.Empty(effects.ReceiptLog);
        Assert.False(run.IsTerminal);

        // D7（条件化）：freshness 拒绝本身不使 binding invalid——revision 仍
        // current 且未消费，validity 仍成立（authorization denied ≠
        // binding invalidation）
        Assert.True(effects.IsBindingValid(act.Binding!.Canonical!, world.DeriveBindingView("screen.home")));
        Assert.Equal("rev-1", world.Current!.RevisionId);   // 无 revision 前提如实成立
    }

    // ---- N2：Unknown fail-closed 且与 Insufficient 不折叠 ------------------

    [Fact]
    public void N2_UnknownFreshnessFailsClosedWithDistinguishableOutcome()
    {
        var (kernel, _, _, _, _, _, _) = NewKernel(
            evaluator: new FreshnessDoubles.ByEffectClass(rules: [("tap", FreshnessSufficiency.Unknown)]));
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var act = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));

        // Unknown（判定输入不足）同样 fail-closed，但三态可区分、不折叠
        Assert.False(act.Judgment!.IsAdmissible);
        Assert.Equal("freshness-sufficiency", act.Judgment.RejectionReason);
        Assert.Equal(FreshnessSufficiency.Unknown, act.Judgment.Freshness!.Sufficiency);
        Assert.NotEqual(FreshnessSufficiency.Insufficient, act.Judgment.Freshness.Sufficiency);
        Assert.Null(act.Receipt);
    }

    // ---- N3：双消费对照（consumption-relative 证明，不偷解 ③） -------------

    [Fact]
    public void N3_SameRevisionDifferentRequirementsYieldDifferentFreshnessJudgments()
    {
        // evaluator：tap（低要求）→ Sufficient；swipe（高要求）→ Insufficient
        var (kernel, _, world, _, _, _, effects) = NewKernel(
            policy: new SequencedPolicy("tap", "swipe"),
            evaluator: new FreshnessDoubles.ByEffectClass(rules: [("swipe", FreshnessSufficiency.Insufficient)]));
        kernel.AdmitContract(Contract(allowed: new HashSet<string> { "tap", "swipe" },
                                      forbidden: new HashSet<string>()));
        PrimeWorld(kernel);

        // 消费 A（低 requirement）：同 revision → Sufficient → 放行 dispatch
        var intentA = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var actA = kernel.Act(intentA, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.True(actA.Judgment!.IsAdmissible);
        Assert.Equal(FreshnessSufficiency.Sufficient, actA.Judgment.Freshness!.Sufficiency);
        Assert.NotNull(actA.Receipt);

        // attempt 回流是 AttemptReport（非 world-relevant）→ 无新 revision，
        // 同一 FreshnessBasis 仍在消费
        Assert.Equal("rev-1", world.Current!.RevisionId);

        // 消费 B（高 requirement）：同 revision 同 basis → Insufficient → 拒绝
        var intentB = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var actB = kernel.Act(intentB, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.False(actB.Judgment!.IsAdmissible);
        Assert.Equal(FreshnessSufficiency.Insufficient, actB.Judgment.Freshness!.Sufficiency);
        Assert.Null(actB.Receipt);
        Assert.Single(effects.ReceiptLog);   // 只有消费 A 的真实投递
    }

    // ---- N4：evaluator 未配置 = composition error，fail-fast ----------------

    [Fact]
    public void N4_MissingEvaluatorIsCompositionErrorFailFast()
    {
        // 不是 runtime Unknown（D3：两类 absence 不混）
        Assert.Throws<ArgumentNullException>(() => new RuntimeAssurance(null!));
    }

    // ---- N5：belief/binding 面无全局 freshness 判定状态 ---------------------

    [Fact]
    public void N5_NoGlobalFreshnessStateOnBeliefOrBindingSurfaces()
    {
        foreach (var type in new[] { typeof(WorldBeliefRevision), typeof(Slice), typeof(CanonicalBinding) })
        {
            foreach (var property in type.GetProperties())
            {
                if (property.Name == nameof(WorldBeliefRevision.FreshnessBasis))
                {
                    // 唯一允许成员：FreshnessBasis 输入聚合（非判定状态）
                    Assert.Equal(typeof(FreshnessBasis), property.PropertyType);
                    continue;
                }
                Assert.False(
                    property.Name.Contains("Fresh", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("Stale", StringComparison.OrdinalIgnoreCase),
                    $"{type.Name} 不得携带 freshness 判定状态成员：{property.Name}");
            }
        }

        // 判定状态只存在于 Assurance judgment 载体（freshness ≠ belief 属性）
        Assert.Equal(typeof(FreshnessJudgment),
            typeof(AssuranceJudgment).GetProperty(nameof(AssuranceJudgment.Freshness))!.PropertyType);
    }

    // ---- N6：freshness 拒绝不触发 recovery / blind retry -------------------

    [Fact]
    public void N6_FreshnessRejectionDoesNotForceRecoveryIntent()
    {
        var (kernel, _, _, _, control, _, _) = NewKernel(
            evaluator: new FreshnessDoubles.ByEffectClass(rules: [("tap", FreshnessSufficiency.Insufficient)]));
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intentA = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var act = kernel.Act(intentA, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.False(act.Judgment!.IsAdmissible);   // 前提：确被 freshness 拒绝

        // 拒绝非 dispatch 失败：无 receipt → 无 pendingRecovery → 下一 intent
        // 由策略决定（Act），不是强制 Recovery（freshness failure ≠ blind
        // retry 触发器；重观察编排属 Observation Control，deferred）
        var intentB = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        Assert.Equal(ControlIntentKind.Act, intentB.Kind);
        Assert.All(control.IntentLog, i => Assert.Equal(ControlIntentKind.Act, i.Kind));
    }

    // ---- N7：检查集 = CBA-005 九检查零改动 + 恰新增一条 --------------------

    [Fact]
    public void N7_CheckSetIsCba005NinePlusExactlyOneFreshnessCheck()
    {
        var (kernel, _, _, _, _, _, _) = NewKernel();
        kernel.AdmitContract(Contract());
        PrimeWorld(kernel);

        var intent = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        var act = kernel.Act(intent, new CandidateBinding("screen.home", "idle", "rev-1"));

        // 顺序锁定 RejectionReason（首个失败项）的确定性；CBA-005 D3 检查
        // 集零改动 + 恰新增 freshness-sufficiency（insufficient/unknown 的
        // 区分在 FreshnessJudgment 自身，不搞动态 check 名）
        Assert.Equal(new[]
        {
            "intent-is-act", "effect-class-allowed", "effect-class-not-forbidden",
            "target-declared", "binding-intent-correlation", "binding-revision-currentness",
            "no-unresolved-conflict", "intent-basis-currentness",
            "freshness-sufficiency", "no-blind-retry",
        }, act.Judgment!.Checks.Select(c => c.Name).ToArray());
        Assert.True(act.Judgment.IsAdmissible);
        Assert.True(act.Judgment.Checks.Single(c => c.Name == "freshness-sufficiency").Passed);
    }
}
