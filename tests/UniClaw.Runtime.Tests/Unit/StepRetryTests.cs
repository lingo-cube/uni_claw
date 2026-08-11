using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario;
using UniClaw.Runtime.Traversal;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal; // CS0118：类名与命名空间同名 → 别名
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// B4 — Step-scope Retry（Traversal；SC-P2-002 / specs/step-retry.md）：
/// Select 失败 → 有界 re-observe + re-resolve（零动作派发）；耗尽 → Phase 1 Failed 路径（无 Trap / 无恢复）。
/// maxRetries = 0（默认）→ 行为与 Phase 1 字节级一致（SC-P1-004 missing-target 不回归）。
/// </summary>
public sealed class StepRetryTests
{
    // ── 测试 1：maxRetries = 0 → Phase 1 原路径（SC-P1-004 不回归：单条 Failed、原因字节级一致、零 re-observe、零派发）──
    [Fact]
    public void MaxRetriesZero_SelectFailure_ImmediateFailed_IdenticalToPhase1()
    {
        var environment = new ScriptedProbeEnvironment([Template("Network", El("Bluetooth"))]);
        var traversal = new RuntimeTraversal(environment, maxRetries: 0);
        var step = new PlanStep("WiFi", "Tap");
        var initial = new Observation([El("Bluetooth")], "Network", 1);

        var result = traversal.ExecuteStep(step, initial, initial.Elements);

        var failed = Assert.IsType<TraversalStepResult.Failed>(result);
        Assert.Equal("目标「WiFi」在当前观测中无匹配候选（Select 无结果）。", failed.Reason);
        var entry = Assert.Single(traversal.Journal);
        Assert.Equal(0, entry.RetryCount);
        Assert.Null(entry.DispatchedAction);
        Assert.Null(entry.PostActionObservation);
        Assert.Equal(0, environment.SequenceCount); // 零 re-observe
        Assert.Empty(environment.ExecutedActions);  // 零派发
    }

    // ── 测试 2：flicker-target — 首次 re-observe 后目标出现 → 重试成功继续执行（SC-P2-002 主路径）──
    [Fact]
    public void RetrySuccess_FlickerTarget_RecoversOnSecondObserve()
    {
        // flicker 状态在调用侧初始观测（seq1，仅 "Bluetooth"）；世界恢复后 re-observe（seq2）含 "WiFi"
        var environment = new ScriptedProbeEnvironment(
            [Template("Network", El("Bluetooth"), El("WiFi"))],
            firstSequence: 2);
        var traversal = new RuntimeTraversal(environment, maxRetries: 1);
        var step = new PlanStep("WiFi", "Tap");
        var initial = new Observation([El("Bluetooth")], "Network", 1);

        var result = traversal.ExecuteStep(step, initial, initial.Elements);

        Assert.IsType<TraversalStepResult.Succeeded>(result);
        // journal = 首次失败(0) + 重试命中(1) + Succeeded(1)（SC-P2-002 Evidence 1：重试条目可见）
        Assert.Equal(3, traversal.Journal.Count);

        var first = traversal.Journal[0];
        Assert.Equal(0, first.RetryCount);
        Assert.IsType<TraversalStepResult.Failed>(first.Result);
        Assert.Null(first.DispatchedAction);
        Assert.Null(first.PostActionObservation);

        var retry = traversal.Journal[1];
        Assert.Equal(1, retry.RetryCount);
        Assert.IsType<TraversalStepResult.Failed>(retry.Result);
        Assert.Null(retry.DispatchedAction);
        var retryObs = retry.PostActionObservation ?? throw new InvalidOperationException("重试条目缺少 re-observe 观测。");
        Assert.Equal(2, retryObs.SequenceNumber); // re-observe 序号可追溯

        var final = traversal.Journal[2];
        Assert.Equal(1, final.RetryCount); // 本步最终在第 1 次重试上成功
        Assert.IsType<TraversalStepResult.Succeeded>(final.Result);
        var tap = Assert.IsType<DeviceAction.Tap>(final.DispatchedAction);
        Assert.Equal(1, tap.TargetElementIndex); // Index 来自 re-resolve 后的重试观测（SC-P2-002 Evidence 7）
        var postObs = final.PostActionObservation ?? throw new InvalidOperationException("成功条目缺少动作后观测。");
        Assert.Equal(3, postObs.SequenceNumber);

        // 全程仅派发一次动作（重试期间零派发）
        var dispatched = Assert.Single(environment.ExecutedActions);
        Assert.Equal(1, Assert.IsType<DeviceAction.Tap>(dispatched).TargetElementIndex);
    }

    // ── 测试 3：目标永不出现 → 重试耗尽 → 1+N 条 Failed，最后一条为耗尽原因，零派发 ──
    [Fact]
    public void RetryExhausted_TargetNeverAppears_AllFailedEntries_ZeroDispatch()
    {
        var environment = new ScriptedProbeEnvironment([Template("Network", El("Bluetooth"))], firstSequence: 2);
        var traversal = new RuntimeTraversal(environment, maxRetries: 2);
        var step = new PlanStep("WiFi", "Tap");
        var initial = new Observation([El("Bluetooth")], "Network", 1);

        var result = traversal.ExecuteStep(step, initial, initial.Elements);

        var failed = Assert.IsType<TraversalStepResult.Failed>(result);
        Assert.Equal("目标「WiFi」在当前观测中无匹配候选（Select 无结果。已重试 2 次。）", failed.Reason);
        // 1 + N 条：首次失败 + (N-1) 次中间重试失败 + 耗尽（重试有界 — SC-P2-002 Evidence 5）
        Assert.Equal(3, traversal.Journal.Count);
        Assert.All(traversal.Journal, e => Assert.IsType<TraversalStepResult.Failed>(e.Result));
        Assert.All(traversal.Journal, e => Assert.Null(e.DispatchedAction)); // 全程零派发
        Assert.Equal(0, traversal.Journal[0].RetryCount);
        Assert.Equal(1, traversal.Journal[1].RetryCount);
        Assert.Equal(2, traversal.Journal[2].RetryCount);
        Assert.Equal("目标「WiFi」在当前观测中无匹配候选（重试 1/2）。",
            Assert.IsType<TraversalStepResult.Failed>(traversal.Journal[1].Result).Reason);
        Assert.Empty(environment.ExecutedActions);
    }

    // ── 测试 4：重试条目与正常条目可区分（RetryCount > 0；未派发动作；re-observe 条目携带观测快照）──
    [Fact]
    public void RetryEntries_Distinguishable_RetryCountGreaterThanZero()
    {
        var environment = new ScriptedProbeEnvironment([Template("Network", El("Bluetooth"))], firstSequence: 2);
        var traversal = new RuntimeTraversal(environment, maxRetries: 2);
        var step = new PlanStep("WiFi", "Tap");
        var initial = new Observation([El("Bluetooth")], "Network", 1);

        traversal.ExecuteStep(step, initial, initial.Elements);

        Assert.All(traversal.Journal.Where(e => e.RetryCount > 0), e =>
        {
            Assert.Null(e.DispatchedAction);                       // 重试 = 仅 re-observe，绝不派发动作
            Assert.IsType<TraversalStepResult.Failed>(e.Result);   // 重试/耗尽条目均为结构化 Failed
        });
        var retryObs = traversal.Journal[1].PostActionObservation
            ?? throw new InvalidOperationException("重试 re-observe 条目缺少观测快照。");
        Assert.Equal(2, retryObs.SequenceNumber); // 重试条目携带 re-observe 快照（SC-P2-002 Evidence 1）
        Assert.Null(traversal.Journal[2].PostActionObservation);   // 耗尽条目无观测快照
        Assert.Equal(0, traversal.Journal[0].RetryCount);          // 正常首次条目 RetryCount = 0
    }

    // ── 测试 5：确定性 — 同输入 + 同环境配置 → 同重试次数 + 同结果（SC-P1-001 断言 7 重放不回归）──
    [Fact]
    public void Deterministic_SameInputs_SameRetrySequence()
    {
        var (traversalA, envA) = RunFlickerScenario();
        var (traversalB, envB) = RunFlickerScenario();

        Assert.Equal(traversalA.Journal.Count, traversalB.Journal.Count);
        for (var i = 0; i < traversalA.Journal.Count; i++)
        {
            var a = traversalA.Journal[i];
            var b = traversalB.Journal[i];
            Assert.Equal(a.StepId, b.StepId);
            Assert.Equal(a.RetryCount, b.RetryCount);
            Assert.Equal(a.Result, b.Result);
            Assert.Equal(a.DispatchedAction, b.DispatchedAction);
            Assert.Equal(a.PostActionObservation?.SequenceNumber, b.PostActionObservation?.SequenceNumber);
        }
        Assert.Equal(envA.ExecutedActions, envB.ExecutedActions);
    }

    // ── 测试 6：重试路径不产生 Trap / Recovery 痕迹（step-retry.md：不升级、不触发恢复）──
    [Fact]
    public void NoTrap_NoRecoveryEvents()
    {
        // (a) 结果面：耗尽返回普通结构化 Failed（TraversalStepResult 无 Trap 字段 — 裁决 4）
        var environment = new ScriptedProbeEnvironment([Template("Network", El("Bluetooth"))], firstSequence: 2);
        var traversal = new RuntimeTraversal(environment, maxRetries: 2);
        var step = new PlanStep("WiFi", "Tap");
        var initial = new Observation([El("Bluetooth")], "Network", 1);
        var result = traversal.ExecuteStep(step, initial, initial.Elements);
        Assert.IsType<TraversalStepResult.Failed>(result);

        // (b) 源码面：Traversal 生产代码不含 Trap / Recovery 事件字段（TraceEvent.TrapKind / TrapScope / RecoveryId）
        var source = TestRepositoryPaths.RepoPath("src", "UniClaw.Runtime", "Traversal", "Traversal.cs");
        Assert.True(File.Exists(source), $"Traversal 源码缺失: {source}");
        var content = File.ReadAllText(source);
        foreach (var banned in new[] { "TrapKind", "TrapScope", "RecoveryId" })
        {
            Assert.False(
                content.Contains(banned, StringComparison.Ordinal),
                $"Traversal 源码包含「{banned}」：Step-scope retry 不得产生 Trap / Recovery 事件（step-retry.md 禁止）。");
        }
    }

    // ── RC2-01: Criterion-Grounded Retry Safety Falsifier ─────────────────────────────────────────────

    /// <summary>
    /// Challenge falsifier: criterion failure with a non-zero legacy retry budget
    /// was already fail-closed. It must not re-observe, fall back to text matching,
    /// or dispatch even when the scripted retry observation would be groundable.
    /// </summary>
    [Fact]
    public void CriterionFailure_WithRetryBudget_FailsClosedWithoutLegacyFallbackOrDispatch()
    {
        var criterion = BoundsCriterion("WiFi");
        var auth = Auth(0, true);
        var step = new PlanStep("WiFi", "Tap", TargetGroundingCriterion: criterion);
        var env = new ScriptedProbeEnvironment(
            [Template("Settings", ElIdx("WiFi", 0, Bounds(0.1f, 0.2f, 0.3f, 0.4f)))]);
        var traversal = new RuntimeTraversal(env, maxRetries: 2);
        var initial = new Observation([ElIdx("WiFi", 0, bounds: null)], "Settings", 1);

        var result = traversal.ExecuteStep(step, initial, initial.Elements, auth);

        var failed = Assert.IsType<TraversalStepResult.Failed>(result);
        Assert.Contains("Target grounding insufficient", failed.Reason);
        Assert.Single(traversal.Journal);
        Assert.Equal(0, traversal.Journal[0].RetryCount);
        Assert.Equal(0, env.SequenceCount);
        Assert.Empty(env.ExecutedActions);
    }

    /// <summary>Criterion target succeeds first try — normal dispatch remains unchanged.</summary>
    [Fact]
    public void CriterionTarget_SucceedsFirstTry_NormalDispatchUnchanged()
    {
        var criterion = BoundsCriterion("WiFi");
        var auth = Auth(0, true);
        var step = new PlanStep("WiFi", "Tap", TargetGroundingCriterion: criterion);

        var env = new ScriptedProbeEnvironment(
            [Template("Settings", ElIdx("WiFi", 0, Bounds(0.1f, 0.2f, 0.3f, 0.4f)))],
            firstSequence: 2);
        var traversal = new RuntimeTraversal(env, maxRetries: 0);

        // WiFi WITH bounds at index 0 → criterion passes immediately
        var initial = new Observation(
            [ElIdx("WiFi", 0, Bounds(0.1f, 0.2f, 0.3f, 0.4f))], "Settings", 1);

        var result = traversal.ExecuteStep(step, initial, initial.Elements, auth);

        Assert.IsType<TraversalStepResult.Succeeded>(result);
        Assert.Single(traversal.Journal); // no retry entries
        Assert.Equal(0, traversal.Journal[0].RetryCount);
        var dispatched = Assert.Single(env.ExecutedActions);
        Assert.Equal(0, Assert.IsType<DeviceAction.Tap>(dispatched).TargetElementIndex);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>执行一次 flicker-target 场景（重试 #1 命中）：初始观测缺目标 → 重试 re-observe 后目标出现。</summary>
    private static (RuntimeTraversal Traversal, ScriptedProbeEnvironment Environment) RunFlickerScenario()
    {
        var environment = new ScriptedProbeEnvironment(
            [Template("Network", El("Bluetooth"), El("WiFi"))],
            firstSequence: 2);
        var traversal = new RuntimeTraversal(environment, maxRetries: 2);
        var step = new PlanStep("WiFi", "Tap");
        var initial = new Observation([El("Bluetooth")], "Network", 1);
        traversal.ExecuteStep(step, initial, initial.Elements);
        return (traversal, environment);
    }

    /// <summary>脚本模板：(前台, 元素集)；seq 由环境自增分配。</summary>
    private static (string Foreground, ImmutableArray<ObservedElement> Elements) Template(string foreground, params ObservedElement[] elements)
        => (foreground, [.. elements]);

    /// <summary>构造元素（SwitchState 默认 null = 非开关承载元素）。</summary>
    private static ObservedElement El(string text, bool? switchState = null) => new(text, switchState, 0);

    /// <summary>
    /// 脚本化探测环境（Phase 2C 模式 — 直接构造 Observation）：
    /// 按脚本顺序产出观测，脚本耗尽后重复最后一个模板；记录已派发动作（B4 断言重试零派发 / Index 来自 re-resolve）。
    /// </summary>
    private sealed class ScriptedProbeEnvironment : IEnvironment
    {
        private readonly IReadOnlyList<(string Foreground, ImmutableArray<ObservedElement> Elements)> _script;
        private readonly List<DeviceAction> _executedActions = [];
        private int _index;
        private long _sequence;

        /// <param name="firstSequence">首次观测序号（默认 1；调用侧初始观测通常为 seq1，重试 re-observe 由此从 seq2 起）。</param>
        public ScriptedProbeEnvironment(IReadOnlyList<(string Foreground, ImmutableArray<ObservedElement> Elements)> script, long firstSequence = 1)
        {
            _script = script;
            _sequence = firstSequence - 1;
        }

        /// <summary>已产出的观测数量（测试用：re-observe 计数）。</summary>
        public long SequenceCount => _sequence;

        /// <summary>已派发动作（重试期间必须为空 — step-retry.md SHALL NOT 派发）。</summary>
        public IReadOnlyList<DeviceAction> ExecutedActions => _executedActions;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (foreground, elements) = _script[Math.Min(_index, _script.Count - 1)];
            _index++;
            _sequence++;
            return Task.FromResult(new Observation(elements, foreground, _sequence));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _executedActions.Add(action);
            return Task.FromResult(new ActionResult(
                ActionResultOutcome.Dispatched, action.ToString(), "probe: dispatched"));
        }
    }

    // ── RC2-01 helpers ──────────────────────────────────────────────────────────────────────────────

    private static TargetGroundingCriterion BoundsCriterion(string targetText)
        => new(
            (obs, el) => string.Equals(el.Text, targetText, StringComparison.Ordinal) && el.Bounds is not null
                ? new TargetGroundingEvidence(true, $"text matches '{targetText}' with bounds")
                : new TargetGroundingEvidence(false, $"no bounds for '{targetText}'"),
            obs => new TargetGroundingEvidence(true, "post-action ok"));

    private static ImmutableDictionary<int, CandidateAuthorizationEvidence> Auth(int index, bool authorized)
        => new Dictionary<int, CandidateAuthorizationEvidence>
        {
            [index] = new CandidateAuthorizationEvidence(authorized, authorized ? "ok" : "rejected"),
        }.ToImmutableDictionary();

    private static ElementBounds Bounds(float x1, float y1, float x2, float y2)
        => new(x1, y1, x2, y2);

    private static ObservedElement ElIdx(string text, int index, ElementBounds? bounds = null, string? perceptionType = null, bool? switchState = null)
        => new(text, switchState, index, bounds, perceptionType);
}
