using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Effects;

/// <summary>
/// DSE-004 — Effect Driver R5 可替换性执法：同一消费闭包
/// （EffectBoundary gate → Deliver → receipt 链）喂两个结构不同的
/// IEffectDriver realization，契约互换成立——driver 替换只动组合根构造
/// 参数，闭包零改动（AGENTS.md 1.5.3「仿真 ⇄ 真件替换不改核心」的
/// effect 面确定性证明）。真件 driver（AdbLive/EgoBrowser）自身契约由
/// 各 realization 测试覆盖；ExecutorId 入 journal 的溯源由
/// EffectBoundaryExecutionSourceTests 覆盖；本测试证明缝的可互换律。
/// </summary>
public sealed class EffectDriverReplaceabilityTests
{
    private interface ICountingDriver : IEffectDriver
    {
        int DeliverCount { get; }
    }

    /// <summary>无状态脚本型 double：按脚本依次返回，耗尽后重复末项。</summary>
    private sealed class ScriptedDriver : ICountingDriver
    {
        private readonly Queue<DispatchResult> _script;
        private DispatchResult _last = new(DispatchOutcome.DeliveryCompleted, "default", DateTimeOffset.UnixEpoch);

        public int DeliverCount { get; private set; }

        public ScriptedDriver(params DispatchResult[] script) => _script = new Queue<DispatchResult>(script);

        public DispatchResult Deliver(DispatchRequest request)
        {
            DeliverCount++;
            if (_script.Count > 0)
                _last = _script.Dequeue();
            return _last;
        }
    }

    /// <summary>有状态首败后成型 double：第一次失败、之后成功——内部结构
    /// 与脚本型刻意不同，证明替换不要求复制实现形状。</summary>
    private sealed class FlakyThenOkDriver : ICountingDriver
    {
        private bool _failedOnce;

        public int DeliverCount { get; private set; }

        public DispatchResult Deliver(DispatchRequest request)
        {
            DeliverCount++;
            if (_failedOnce)
                return new(DispatchOutcome.DeliveryCompleted, "flaky-recovered",
                    DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(1));
            _failedOnce = true;
            return new(DispatchOutcome.DeliveryFailed, "flaky-report",
                DateTimeOffset.UnixEpoch, "device-busy");
        }
    }

    private static (CanonicalBinding Binding, AssuranceJudgment Judgment, BindingView View) Grounded(
        string bindingId = "bind-1", string intentId = "intent-1") =>
        (new CanonicalBinding(bindingId, intentId, "tap", "switch.wifi", "true", "rev-1", 1),
            new AssuranceJudgment(intentId, bindingId, "rev-1", IsAdmissible: true,
                Checks: Array.Empty<AssuranceCheck>(), RejectionReason: null,
                Freshness: new FreshnessJudgment(FreshnessSufficiency.Sufficient, "test:sufficient")),
            new BindingView("rev-1", 1, HasTargetSubjectClaim: true));

    /// <summary>
    /// 消费闭包：只依赖 EffectBoundary 公开面。可互换律（与 driver 是谁
    /// 无关）：① gate 判定不受 driver 身份影响；② receipt 忠实映射
    /// driver 三态 outcome（不伪造成功）；③ 每次 Dispatch 恰一次 Deliver
    /// （boundary 永不代 driver retry，不变量 27）；④ receipt 逐次留痕、
    /// id 唯一。
    /// </summary>
    private static void AssertInterchangeableLaw(
        EffectBoundary boundary, ICountingDriver driver,
        params DispatchOutcome[] expectedPerDispatch)
    {
        for (var i = 0; i < expectedPerDispatch.Length; i++)
        {
            var (binding, judgment, view) = Grounded($"bind-{i + 1}", $"intent-{i + 1}");
            var callsBefore = driver.DeliverCount;

            var (gate, receipt) = boundary.Dispatch(binding, judgment, view);

            Assert.True(gate.Allowed);                                    // ①
            Assert.NotNull(receipt);                                      // ②
            Assert.Equal(expectedPerDispatch[i], receipt!.Outcome);       // ②
            Assert.Equal(binding.IntentId, receipt.IntentId);             // ②
            Assert.Equal(driver.DeliverCount, callsBefore + 1);           // ③
        }
        var ids = boundary.ReceiptLog.Select(r => r.ReceiptId).ToArray(); // ④
        Assert.Equal(expectedPerDispatch.Length, ids.Length);
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void SameClosure_ScriptedDriver_HoldsContract()
    {
        var driver = new ScriptedDriver(
            new DispatchResult(DispatchOutcome.DeliveryCompleted, "ok-1", DateTimeOffset.UnixEpoch),
            new DispatchResult(DispatchOutcome.DeliveryCompleted, "ok-2", DateTimeOffset.UnixEpoch));

        AssertInterchangeableLaw(new EffectBoundary(driver),
            driver, DispatchOutcome.DeliveryCompleted, DispatchOutcome.DeliveryCompleted);
    }

    [Fact]
    public void SameClosure_FlakyDriver_HonestFailure_NoAutoRetry_RecoveryTracked()
    {
        var driver = new FlakyThenOkDriver();
        var boundary = new EffectBoundary(driver);

        AssertInterchangeableLaw(boundary, driver,
            DispatchOutcome.DeliveryFailed, DispatchOutcome.DeliveryCompleted);

        // 失败轨迹诚实：首个 receipt 保留 DeliveryFailed + 成因（attempt
        // evidence，不追溯改写）；两次投递两次留痕并存。
        Assert.Equal(DispatchOutcome.DeliveryFailed, boundary.ReceiptLog[0].Outcome);
        Assert.Equal("device-busy", boundary.ReceiptLog[0].Reason);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, boundary.ReceiptLog[1].Outcome);
        Assert.Equal(2, driver.DeliverCount);
    }

    [Fact]
    public void DriverSwap_OnlyChangesCompositionRoot_ClosureUnchanged()
    {
        // 同一闭包委托依次消费两个 driver：law 对两者同构成立，闭包代码
        // 零改动（换 driver = 换 EffectBoundary 构造参数）。
        var scripted = new ScriptedDriver(
            new DispatchResult(DispatchOutcome.DeliveryCompleted, "swap-ok", DateTimeOffset.UnixEpoch));
        var flaky = new FlakyThenOkDriver();

        AssertInterchangeableLaw(new EffectBoundary(scripted), scripted, DispatchOutcome.DeliveryCompleted);
        AssertInterchangeableLaw(new EffectBoundary(flaky), flaky, DispatchOutcome.DeliveryFailed);
    }
}
