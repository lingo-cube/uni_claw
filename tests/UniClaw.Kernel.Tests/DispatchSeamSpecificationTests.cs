using System.Reflection;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// DSE-001 验收（S1–S7）—— P14 DispatchRequest 收窄 + P15 三态 outcome 的
/// 规格测试。纯内存确定性（level: DETERMINISTIC）。最重验收 S3/S4：
/// UnknownOutcome 必须真实接回 recovery / re-observe 闭环，而不是枚举改名。
/// </summary>
public sealed class DispatchSeamSpecificationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 9, 10, 5, 0, TimeSpan.Zero);

    // ---- scripted 替身 ---------------------------------------------------

    private static ObservationProposal Observation(string subject, string value, DateTimeOffset captureTime) =>
        new(new ObservationClaim(subject, value), IngressKind.Observation, ObservationContext.External,
            new Provenance("provider.scripts", captureTime, $"scope:{subject}",
                new[] { "raw://capture", "encode:v1" }));

    private static ExecutionContract Contract() => new(
        Version: "c1",
        Objective: "verify-home-screen",
        Scope: new HashSet<string> { "screen.home" },
        AllowedEffects: new HashSet<string> { "tap" },
        ForbiddenEffects: new HashSet<string> { "swipe" },
        ProofCriteria: new[] { "home-screen-observed" });

    private sealed class ScriptedPolicy : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, "tap", "screen.home");
    }

    /// <summary>确定性 driver：outcome(+reason) 队列，机械投递，无 retry。</summary>
    private sealed class ScriptedDriver(params (DispatchOutcome Outcome, string? Reason)[] outcomes) : IEffectDriver
    {
        private readonly Queue<DispatchResult> _results =
            new(outcomes.Select((o, i) => new DispatchResult(o.Outcome, $"scripted:{o.Outcome}", T1.AddMinutes(i), o.Reason)));

        public DispatchResult Deliver(DispatchRequest request) => _results.Dequeue();
    }

    private static ScriptedDriver Driver(params DispatchOutcome[] outcomes) =>
        new(outcomes.Select(o => (o, (string?)null)).ToArray());

    private static (UniKernel Kernel, WorldModel World, ControlLoop Control, EffectBoundary Effects)
        NewKernel(IEffectDriver driver)
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home" }, new SeedContainerAssociationStrategy());
        var run = new RunModel();
        var control = new ControlLoop(new ScriptedPolicy());
        var assurance = new RuntimeAssurance(new FreshnessDoubles.Satisfying());
        var effects = new EffectBoundary(driver);
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, run, control, assurance, effects);
        kernel.AdmitContract(Contract());
        kernel.Process(Observation("screen.home", "idle", T0));
        return (kernel, world, control, effects);
    }

    private static ControlIntent ActOnce(UniKernel kernel) =>
        kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));

    // ---- S1 结构断言：binding 不穿透、字段白名单恰四 -------------------

    [Fact]
    public void S1_DispatchRequestCarriesExactlyFourFields_AndDriverSeamNeverSeesCanonicalBinding()
    {
        // 字段白名单恰为 P14 minimal payload 四字段——无 IntentId/BindingId/
        // RevisionNumber/judgment/spatial
        var propertyNames = typeof(DispatchRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();
        Assert.Equal(new HashSet<string> { "Target", "EffectClass", "Parameters", "RevisionId" }, propertyNames);

        // IEffectDriver.Deliver 参数类型 = DispatchRequest（编译级隔离之外的
        // 结构复核：接口签名不含 CanonicalBinding）
        var parameter = typeof(IEffectDriver).GetMethod(nameof(IEffectDriver.Deliver))!.GetParameters().Single();
        Assert.Equal(typeof(DispatchRequest), parameter.ParameterType);
    }

    // ---- S2 三态 outcome × reason 透传 -----------------------------------

    [Theory]
    [InlineData(DispatchOutcome.DeliveryCompleted, "deliverycompleted")]
    [InlineData(DispatchOutcome.DeliveryFailed, "deliveryfailed")]
    [InlineData(DispatchOutcome.UnknownOutcome, "unknownoutcome")]
    public void S2_ThreeOutcomeStatesFlowToReceiptAndAttemptEvidenceClaim(
        DispatchOutcome outcome, string expectedClaimValue)
    {
        var (kernel, _, _, effects) = NewKernel(new ScriptedDriver((outcome, "timeout-killed")));

        var act = kernel.Act(ActOnce(kernel), new CandidateBinding("screen.home", "idle", "rev-1"));

        var receipt = Assert.IsType<EffectReceipt>(act.Receipt);
        Assert.Equal(outcome, receipt.Outcome);
        Assert.Equal("timeout-killed", receipt.Reason);

        // AttemptReport claim value 随三态（描述性 provenance，语义安全）
        var proposal = effects.ExportAttemptEvidence(receipt);
        Assert.Equal(expectedClaimValue, proposal.Claim.Value);
    }

    [Fact]
    public void S2b_UninterpretableResultIsExpressedAsUnknownOutcome_FailClosed()
    {
        // 协议 Deferred ⑬ 闭合：「结果缺失/不可解读」不再不可表达——
        // UnknownOutcome + reason，如实记录、不猜测成功
        var (kernel, _, _, _) = NewKernel(new ScriptedDriver(
            (DispatchOutcome.UnknownOutcome, "result-uninterpretable")));

        var act = kernel.Act(ActOnce(kernel), new CandidateBinding("screen.home", "idle", "rev-1"));

        Assert.Equal(DispatchOutcome.UnknownOutcome, act.Receipt!.Outcome);
        Assert.Equal("result-uninterpretable", act.Receipt.Reason);
    }

    // ---- S3 最重验收：Unknown → re-observe recovery 真闭环 ---------------

    [Fact]
    public void S3_UnknownOutcomeTriggersForcedRecoveryIntent_NotAct_AndNeverBlindRedispatch()
    {
        var (kernel, world, _, effects) = NewKernel(new ScriptedDriver(
            (DispatchOutcome.UnknownOutcome, "timeout-killed"),
            (DispatchOutcome.DeliveryCompleted, (string?)null)));

        // 第一次 act：driver 报 Unknown（effect 可能已发生也可能未发生）
        var intent1 = ActOnce(kernel);
        var act1 = kernel.Act(intent1, new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.Equal(DispatchOutcome.UnknownOutcome, act1.Receipt!.Outcome);
        var binding1 = act1.Binding!.Canonical!;

        // 同一 binding 二次 dispatch：EB 侧执法拒绝（never blind redispatch）
        var (gate2, receipt2) = effects.Dispatch(
            binding1, act1.Judgment!, world.DeriveBindingView("screen.home", null));
        Assert.False(gate2.Allowed);
        Assert.Equal("binding-already-dispatched", gate2.Reason);
        Assert.Null(receipt2);

        // 下轮 control cycle：revision 未推进 → 强制 Recovery intent（re-observe），
        // 不是 Act；零新 dispatch
        var intent2 = kernel.SelectIntent(kernel.DeriveSlice(SliceSeed.RootOf(kernel)));
        Assert.Equal(ControlIntentKind.Recovery, intent2.Kind);
        Assert.Single(effects.ReceiptLog);
    }

    [Fact]
    public void S3b_AfterFreshRevisionDispatchIsAdmissibleAgain_FullClosedLoop()
    {
        // Unknown → re-observe（新 revision）→ 才允许再次 dispatch：
        // no-blind-retry（Assurance）与 forced-recovery（Control）双执法面
        var (kernel, _, _, effects) = NewKernel(new ScriptedDriver(
            (DispatchOutcome.UnknownOutcome, "timeout-killed"),
            (DispatchOutcome.DeliveryCompleted, (string?)null)));

        var act1 = kernel.Act(ActOnce(kernel), new CandidateBinding("screen.home", "idle", "rev-1"));
        Assert.Equal(DispatchOutcome.UnknownOutcome, act1.Receipt!.Outcome);

        // re-observe：新证据 → 新 revision（rev-2）
        kernel.Process(Observation("screen.home", "idle", T1));

        // revision 已推进 → policy 恢复 Act；no-blind-retry check 通过
        // （belief.RevisionNumber > failedAt）；第二次 dispatch 完成
        var intent2 = ActOnce(kernel);
        Assert.Equal(ControlIntentKind.Act, intent2.Kind);
        var act2 = kernel.Act(intent2, new CandidateBinding("screen.home", "idle", "rev-2"));

        Assert.True(act2.Gate!.Allowed);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, act2.Receipt!.Outcome);
        Assert.Equal(2, effects.ReceiptLog.Count);
    }

    // ---- S4 对照：DeliveryCompleted 不触发 recovery ----------------------

    [Fact]
    public void S4_DeliveryCompletedDoesNotTriggerForcedRecovery()
    {
        var (kernel, _, _, _) = NewKernel(Driver(DispatchOutcome.DeliveryCompleted));

        kernel.Act(ActOnce(kernel), new CandidateBinding("screen.home", "idle", "rev-1"));

        // 下轮不是强制 Recovery——policy 正常决策（Act）
        var intent2 = ActOnce(kernel);
        Assert.Equal(ControlIntentKind.Act, intent2.Kind);
    }
}
