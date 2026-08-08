using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Traversal;

/// <summary>单步 journal 条目：步骤的不可变记录（TraceEvent.StepId 数据源；B10+ 断言的观察面）。</summary>
/// <param name="StepId">步标识（本 Traversal 实例内唯一、按执行顺序递增，如 "Step-1"）。</param>
/// <param name="SelectedElementIndex">Select 选中的候选 Index；Check 失败（无匹配）= null。</param>
/// <param name="DispatchedAction">分发给环境的动作；未分发（Check 失败 / 协议解析失败）= null。</param>
/// <param name="PostActionObservation">动作后重新观测的快照（§3）；未到达 Observe 阶段 = null。</param>
/// <param name="Result">本步结构化结果（Succeeded | Failed(原因)）。</param>
/// <param name="RetryCount">重试序号（A5；0 = 正常首次执行，&gt;0 = 第 N 次重试执行记录；
/// Phase 1 恒为 0 — 重试执行由 Phase 2 恢复机制引入，HG-2/HG-5 不创建恢复机制）。</param>
public sealed record TraversalJournalEntry(
    string StepId,
    int? SelectedElementIndex,
    DeviceAction? DispatchedAction,
    Observation? PostActionObservation,
    TraversalStepResult Result,
    int RetryCount = 0);

/// <summary>
/// 局部、确定性的执行 Kernel（宪章 §7；specs/container-traversal SHALL）：
/// Select → Check → Execute → Observe → Verify → Branch 单步协议。
/// 拥有单步执行状态（journal），不承担 Agent 级决策（不裁决 Container identity、不决定全局 Plan、
/// 不私自恢复 — I-8）；Run 终止 authority 在 Agent。
/// grounding 仅使用 Text + SwitchState? 证据（裁决 3）；同文本多候选时 SetSwitch 目标
/// state-bearing 优先（SC-P1-005）；无 coordinate / hierarchy 模型（裁决 3）。
/// 无法推进 → TraversalStepResult.Failed(非空原因)（结构化结果，非异常、非静默 — §45）。
/// 不硬编码场景字符串（裁决 11）：target / action 数据全部来自 PlanStep（调用侧注入）。
/// 协议 token（由本类定义，非场景数据）：
///   "Tap" → DeviceAction.Tap；"SetSwitch true" / "SetSwitch false" → DeviceAction.SetSwitch。
/// Phase 1 组合决策：IEnvironment 是异步端口（B2），但 B5 注入的 executor 形状是同步的；
/// Fake 环境同步完成（§33），故 ExecuteStep 同步阻塞等待（GetAwaiter().GetResult()）。
/// 裁决：Phase 4 接入真实 IO 时改为异步形状。
/// 无 FSM（I-7：protocol 用普通方法表达）。依赖：仅 IEnvironment + Model + BCL（I-1）。
/// </summary>
public sealed class Traversal
{
    private readonly IEnvironment _environment;
    private ImmutableList<TraversalJournalEntry> _journal = [];
    private int _stepCounter;

    /// <summary>构造 Traversal。</summary>
    /// <param name="environment">IEnvironment 端口（B2）——观察与动作能力边界。</param>
    /// <exception cref="ArgumentNullException">environment 为 null。</exception>
    public Traversal(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <summary>单步执行 journal（追加式只读快照；每步恰好一条记录）。</summary>
    public IReadOnlyList<TraversalJournalEntry> Journal => _journal;

    /// <summary>
    /// 执行单步（签名与 B5 Container 注入的 executor delegate 形状完全一致，方法组可直接注入）。
    /// Select（Text + SwitchState?，SetSwitch 多候选 state-bearing 优先 — SC-P1-005）→
    /// Check（无匹配 → Failed，零动作分发 — SC-P1-004）→ Execute（协议 token → DeviceAction，
    /// Rejected/TimedOut → Failed）→ Observe（动作后必须重新观察 — §3）→
    /// Verify（观测已获得且序号推进；不要求世界状态变化 — SC-P1-003 负向：switch-stuck 仍 Succeed，
    /// Run 失败是 Agent/evaluator 的判定 — I-10）→
    /// Branch（Succeeded | Failed(原因)；无恢复分支 — Trap Phase 2，裁决 4）。
    /// </summary>
    /// <exception cref="ArgumentNullException">step 或 observation 为 null。</exception>
    public TraversalStepResult ExecuteStep(PlanStep step, Observation observation, ImmutableArray<ObservedElement> candidates)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(observation);
        return ExecuteStepCoreAsync(step, observation, candidates).GetAwaiter().GetResult();
    }

    private async Task<TraversalStepResult> ExecuteStepCoreAsync(PlanStep step, Observation observation, ImmutableArray<ObservedElement> candidates)
    {
        var stepId = $"Step-{++_stepCounter}";

        // ── 1. Select：Text 匹配；SetSwitch 目标同文本多候选 → 非 null SwitchState 优先（SC-P1-005）──
        var selected = Select(step, candidates);

        // ── 2. Check：无匹配候选 → Failed(非空原因)，零动作分发（SC-P1-004）────────────────────────
        if (selected is null)
        {
            return AppendJournal(stepId, selectedIndex: null, dispatched: null, postObservation: null,
                new TraversalStepResult.Failed($"目标「{step.TargetDescription}」在当前观测中无匹配候选（Select 无结果）。"));
        }

        // ── 3. Execute：协议 token → DeviceAction（TargetElementIndex = 选中元素 Index）──────────────
        var action = BuildAction(step.ActionDescription, selected.Value);
        if (action is null)
        {
            return AppendJournal(stepId, selected, dispatched: null, postObservation: null,
                new TraversalStepResult.Failed($"动作描述「{step.ActionDescription}」不是受支持的协议 token（Tap | SetSwitch true|false）。"));
        }
        var actionResult = await _environment.ExecuteAsync(action, CancellationToken.None);
        if (actionResult.Outcome != ActionResultOutcome.Dispatched)
        {
            return AppendJournal(stepId, selected, action, postObservation: null,
                new TraversalStepResult.Failed($"动作分发未生效（{actionResult.Outcome}）：{actionResult.Info ?? "无附加信息"}。"));
        }

        // ── 4. Observe：动作后必须重新观察（§3）──────────────────────────────────────────────────────
        var postObservation = await _environment.ObserveAsync(CancellationToken.None);

        // ── 5. Verify：观测已获得且序号推进；不要求世界状态变化（dispatch ≠ world success — 裁决 10）─
        if (postObservation.SequenceNumber <= observation.SequenceNumber)
        {
            return AppendJournal(stepId, selected, action, postObservation,
                new TraversalStepResult.Failed("动作后观测序号未推进：环境未返回新的观测（违反 §3 动作后必须重新观察）。"));
        }

        // ── 6. Branch：Succeeded（post-action Observation 记录于 journal）；无恢复分支（裁决 4）──────
        return AppendJournal(stepId, selected, action, postObservation, new TraversalStepResult.Succeeded());
    }

    private TraversalStepResult AppendJournal(string stepId, int? selectedIndex, DeviceAction? dispatched, Observation? postObservation, TraversalStepResult result)
    {
        _journal = _journal.Add(new TraversalJournalEntry(stepId, selectedIndex, dispatched, postObservation, result));
        return result;
    }

    /// <summary>grounding 选择：仅 Text + SwitchState? 证据（裁决 3）；SetSwitch 多候选 state-bearing 优先（SC-P1-005）；多个 state-bearing 取首个（确定性）。</summary>
    private static int? Select(PlanStep step, ImmutableArray<ObservedElement> candidates)
    {
        var matches = candidates
            .Select((element, index) => (Element: element, Index: index))
            .Where(x => string.Equals(x.Element.Text, step.TargetDescription, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 0)
            return null;

        if (IsSetSwitchAction(step.ActionDescription) && matches.Count > 1)
        {
            var stateBearing = matches.Where(x => x.Element.SwitchState is not null).ToList();
            if (stateBearing.Count > 0)
                return stateBearing[0].Index;
        }

        return matches[0].Index; // 单候选 / Tap / 无 state-bearing：取首个（确定性）
    }

    private static bool IsSetSwitchAction(string actionDescription)
        => actionDescription.StartsWith("SetSwitch", StringComparison.Ordinal);

    /// <summary>协议 token 解析（本类定义，非场景数据 — 裁决 11）："Tap" → Tap；"SetSwitch true|false" → SetSwitch。</summary>
    private static DeviceAction? BuildAction(string actionDescription, int targetElementIndex)
    {
        if (string.Equals(actionDescription, "Tap", StringComparison.Ordinal))
            return new DeviceAction.Tap(targetElementIndex);

        const string setSwitchPrefix = "SetSwitch ";
        if (actionDescription.StartsWith(setSwitchPrefix, StringComparison.Ordinal))
        {
            var stateText = actionDescription[setSwitchPrefix.Length..];
            if (string.Equals(stateText, "true", StringComparison.Ordinal))
                return new DeviceAction.SetSwitch(targetElementIndex, true);
            if (string.Equals(stateText, "false", StringComparison.Ordinal))
                return new DeviceAction.SetSwitch(targetElementIndex, false);
        }

        return null;
    }
}
