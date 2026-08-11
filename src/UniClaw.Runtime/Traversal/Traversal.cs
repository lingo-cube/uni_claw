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
/// B4（SC-P2-002 / specs/step-retry.md）：Select 失败可在 Step-scope 内有界重试 —— re-observe + re-resolve
/// （仅 Select；零动作派发 —— 派发后重试归 Phase 3 Uncertain Action，裁决 10）；耗尽 → Failed(原因) escalate
/// （无 Trap、无恢复路径 —— step-retry.md 禁止；I-8 对偶：能本地处理不升级，但不得 steal 上层 recovery authority）；
/// maxRetries = 0（默认）保持 Phase 1 行为字节级一致（SC-P1-004 不回归）。
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
    private readonly int _maxRetries;
    private ImmutableList<TraversalJournalEntry> _journal = [];
    private int _stepCounter;

    /// <summary>构造 Traversal。</summary>
    /// <param name="environment">IEnvironment 端口（B2）——观察与动作能力边界。</param>
    /// <param name="maxRetries">Step-scope Select 失败重试上限（B4 / SC-P2-002）；0（默认）= Phase 1 行为
    /// 字节级不变：Select 失败直接 Failed 上报。重试仅 re-observe + re-resolve，零动作派发。</param>
    /// <exception cref="ArgumentNullException">environment 为 null。</exception>
    public Traversal(IEnvironment environment, int maxRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
        _maxRetries = maxRetries;
    }

    /// <summary>单步执行 journal（追加式只读快照；每步恰好一条记录）。</summary>
    public IReadOnlyList<TraversalJournalEntry> Journal => _journal;

    /// <summary>
    /// 执行单步（签名与 B5 Container 注入的 executor delegate 形状完全一致，方法组可直接注入）。
    /// Select（Text + SwitchState?，SetSwitch 多候选 state-bearing 优先 — SC-P1-005）→
    /// Check（无匹配 → Step-scope retry：有界 re-observe + re-resolve（B4 / SC-P2-002，仅 Select、
    /// 零动作派发）；耗尽或 maxRetries=0 → Failed，零动作分发 — SC-P1-004）→
    /// Execute（协议 token → DeviceAction；Rejected → Failed；TimedOut → Observe，由世界证据继续判断 — SC-P3-001）→
    /// Observe（动作后必须重新观察 — §3）→
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

    /// <summary>CP12 local execution path: accepts immutable Agent safety receipts but retains all target selection and verification authority.</summary>
    public TraversalStepResult ExecuteStep(
        PlanStep step,
        Observation observation,
        ImmutableArray<ObservedElement> candidates,
        ImmutableDictionary<int, CandidateAuthorizationEvidence> authorizationReceipts)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(authorizationReceipts);
        return ExecuteStepCoreAsync(step, observation, candidates, authorizationReceipts).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes an already grounded action for the semantic Agent loop. Traversal
    /// retains dispatch, fresh observation, sequence verification, and journal
    /// ownership; the caller cannot treat dispatch as a world effect.
    /// </summary>
    internal TraversalStepResult ExecuteLoweredAction(DeviceAction action, Observation observation)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(observation);
        return ExecuteLoweredActionAsync(action, observation).GetAwaiter().GetResult();
    }

    private async Task<TraversalStepResult> ExecuteLoweredActionAsync(DeviceAction action, Observation observation)
    {
        var stepId = $"Step-{++_stepCounter}";
        var result = await _environment.ExecuteAsync(action, CancellationToken.None);
        if (result.Outcome == ActionResultOutcome.Rejected)
            return AppendJournal(stepId, null, action, null,
                new TraversalStepResult.Failed($"Semantic action rejected: {result.Info ?? "no detail"}."));

        var fresh = await _environment.ObserveAsync(CancellationToken.None);
        if (fresh.SequenceNumber <= observation.SequenceNumber)
            return AppendJournal(stepId, null, action, fresh,
                new TraversalStepResult.Failed("Semantic action post-observation sequence did not advance."));

        return AppendJournal(stepId, null, action, fresh, new TraversalStepResult.Succeeded());
    }

    private async Task<TraversalStepResult> ExecuteStepCoreAsync(
        PlanStep step,
        Observation observation,
        ImmutableArray<ObservedElement> candidates,
        ImmutableDictionary<int, CandidateAuthorizationEvidence>? authorizationReceipts = null)
    {
        var stepId = $"Step-{++_stepCounter}";

        // ── 1. Select：ScrollForward 是 targetless protocol token；其余动作执行元素选择 ──────────────
        var isTargetlessViewportAction = IsScrollForwardAction(step.ActionDescription);
        var criterion = step.TargetGroundingCriterion;
        int? selected;
        if (criterion is not null)
        {
            // CP-12 action-scoped grounding: Tap + SetSwitch true|false are supported protocol tokens.
            // Candidate evaluation and post-action verification are caller-provided per action type.
            if (!IsTapOrSetSwitchAction(step.ActionDescription))
            {
                return AppendJournal(stepId, null, null, null,
                    new TraversalStepResult.Failed($"Target grounding does not support the '{step.ActionDescription}' action token. Supported: Tap, SetSwitch true|false."));
            }
            if (authorizationReceipts is null)
            {
                return AppendJournal(stepId, null, null, null,
                    new TraversalStepResult.Failed("Target grounding safety authorization is absent or not authorized."));
            }
            selected = SelectGrounded(step, observation, candidates, criterion, authorizationReceipts!, out var groundingFailure);
            // RC2-01 falsifier: criterion failure was already fail-closed before the
            // legacy retry block. Preserve that behavior so a retry budget can never
            // weaken criterion grounding or its authorization receipt requirement.
            if (selected is null)
                return AppendJournal(stepId, null, null, null, new TraversalStepResult.Failed(groundingFailure!));
        }
        else
        {
            selected = isTargetlessViewportAction ? null : Select(step, candidates);
        }

        // ── 2. Check：无匹配候选 → Step-scope retry（B4 / SC-P2-002：flicker-target 临时缺失）────────
        //    重试 = 有界 re-observe + re-resolve（仅 Select；零动作派发 — step-retry.md SHALL NOT）；
        //    耗尽 → Phase 1 Failed 路径（无 Trap / 无恢复 — step-retry.md 禁止）；
        //    maxRetries = 0 → 跳过重试块，行为与 Phase 1 字节级一致（SC-P1-004 不回归）。
        var retryCount = 0;
        if (!isTargetlessViewportAction && selected is null && _maxRetries > 0)
        {
            // 首次 Select 失败记录（RetryCount 0 — 正常首次执行尝试；SC-P2-002 Evidence 1）
            AppendJournal(stepId, selectedIndex: null, dispatched: null, postObservation: null,
                new TraversalStepResult.Failed($"目标「{step.TargetDescription}」在当前观测中无匹配候选（Select 无结果）。"));

            for (var retry = 1; retry <= _maxRetries; retry++)
            {
                // re-observe（仅观测，不派发任何 DeviceAction — step-retry.md SHALL NOT）
                var retryObs = await _environment.ObserveAsync(CancellationToken.None);
                selected = Select(step, retryObs.Elements);
                if (selected is not null)
                {
                    // 重试成功：re-observe 命中条目（RetryCount = retry；未派发动作）；
                    // 重试观测成为本步 grounding 上下文（后续 Execute / Verify 使用）
                    retryCount = retry;
                    observation = retryObs;
                    AppendJournal(stepId, selectedIndex: null, dispatched: null, postObservation: retryObs,
                        new TraversalStepResult.Failed($"目标「{step.TargetDescription}」第 {retry} 次重试 re-observe 命中，继续执行。"), retryCount);
                    break;
                }
                if (retry == _maxRetries)
                {
                    // 重试耗尽：escalate — Phase 1 Failed 路径（不产生 Trap — step-retry.md 禁止）
                    return AppendJournal(stepId, selectedIndex: null, dispatched: null, postObservation: null,
                        new TraversalStepResult.Failed($"目标「{step.TargetDescription}」在当前观测中无匹配候选（Select 无结果。已重试 {_maxRetries} 次。）"), _maxRetries);
                }
                AppendJournal(stepId, selectedIndex: null, dispatched: null, postObservation: retryObs,
                    new TraversalStepResult.Failed($"目标「{step.TargetDescription}」在当前观测中无匹配候选（重试 {retry}/{_maxRetries}）。"), retry);
            }
        }
        if (!isTargetlessViewportAction && selected is null)
        {
            // maxRetries = 0：Phase 1 原路径（SC-P1-004 missing-target 不回归）
            return AppendJournal(stepId, selectedIndex: null, dispatched: null, postObservation: null,
                new TraversalStepResult.Failed($"目标「{step.TargetDescription}」在当前观测中无匹配候选（Select 无结果）。"));
        }

        // ── 3. Execute：协议 token → DeviceAction；viewport action 不制造 element target ───────────
        // 从已选元素的 Bounds 提取空间证据——Environment 用于归一化→物理坐标映射。
        // null bounds 向后兼容（Index-based 路径继续工作）。
        ElementBounds? targetBounds = null;
        if (selected is not null)
        {
            var selectedElement = observation.Elements.FirstOrDefault(
                e => e.Index == selected.Value);
            targetBounds = selectedElement?.Bounds;
        }

        var action = isTargetlessViewportAction
            ? new DeviceAction.ScrollForward()
            : BuildAction(step.ActionDescription, selected!.Value, targetBounds);
        if (action is null)
        {
            return AppendJournal(stepId, selected, dispatched: null, postObservation: null,
                new TraversalStepResult.Failed($"动作描述「{step.ActionDescription}」不是受支持的协议 token（Tap | SetSwitch true|false | ScrollForward）。"), retryCount);
        }
        var actionResult = await _environment.ExecuteAsync(action, CancellationToken.None);
        if (actionResult.Outcome == ActionResultOutcome.Rejected)
        {
            return AppendJournal(stepId, selected, action, postObservation: null,
                new TraversalStepResult.Failed($"动作分发未生效（{actionResult.Outcome}）：{actionResult.Info ?? "无附加信息"}。"), retryCount);
        }

        // TimedOut 只说明 dispatch outcome 不确定，不证明 world success 或 confirmed failure（SC-P3-001）。
        // 与 Dispatched 一样先取得 fresh Observation；不重新进入 pre-dispatch Select retry，也不重复派发动作。

        // ── 4. Observe：动作后必须重新观察（§3）──────────────────────────────────────────────────────
        var postObservation = await _environment.ObserveAsync(CancellationToken.None);

        // ── 5. Verify：观测已获得且序号推进；不要求世界状态变化（dispatch ≠ world success — 裁决 10）─
        if (postObservation.SequenceNumber <= observation.SequenceNumber)
        {
            return AppendJournal(stepId, selected, action, postObservation,
                new TraversalStepResult.Failed("动作后观测序号未推进：环境未返回新的观测（违反 §3 动作后必须重新观察）。"), retryCount);
        }

        if (criterion is not null)
        {
            var confirmation = criterion.PostActionEvaluator(postObservation)
                ?? throw new InvalidOperationException("TargetGroundingCriterion.PostActionEvaluator 返回 null evidence。");
            if (confirmation.Supported is not true)
            {
                var outcome = confirmation.Supported is false ? "rejected" : "unconfirmed";
                return AppendJournal(stepId, selected, action, postObservation,
                    new TraversalStepResult.Failed($"Target grounding {outcome}: {confirmation.Reason}"), retryCount);
            }
        }

        // ── 6. Branch：Succeeded（post-action Observation 记录于 journal）；无恢复分支（裁决 4）──────
        return AppendJournal(stepId, selected, action, postObservation, new TraversalStepResult.Succeeded(), retryCount);
    }

    private TraversalStepResult AppendJournal(string stepId, int? selectedIndex, DeviceAction? dispatched, Observation? postObservation, TraversalStepResult result, int retryCount = 0)
    {
        _journal = _journal.Add(new TraversalJournalEntry(stepId, selectedIndex, dispatched, postObservation, result, retryCount));
        return result;
    }

    /// <summary>grounding 选择（委托给 TargetGrounder — 纯函数、无状态、无重试策略）。</summary>
    private static int? Select(PlanStep step, ImmutableArray<ObservedElement> candidates)
        => TargetGrounder.Ground(step.TargetDescription, step.ActionDescription, candidates);

    private static int? SelectGrounded(
        PlanStep step,
        Observation observation,
        ImmutableArray<ObservedElement> candidates,
        TargetGroundingCriterion criterion,
        ImmutableDictionary<int, CandidateAuthorizationEvidence> authorizationReceipts,
        out string? failure)
        => TargetGrounder.GroundCriterion(
            step.TargetDescription, observation, candidates, criterion, authorizationReceipts, out failure);

    private static bool IsSetSwitchAction(string actionDescription)
        => actionDescription.StartsWith("SetSwitch", StringComparison.Ordinal);

    private static bool IsTapOrSetSwitchAction(string actionDescription)
        => string.Equals(actionDescription, "Tap", StringComparison.Ordinal)
           || actionDescription.StartsWith("SetSwitch", StringComparison.Ordinal);

    private static bool IsScrollForwardAction(string actionDescription)
        => string.Equals(actionDescription, "ScrollForward", StringComparison.Ordinal);

    /// <summary>协议 token 解析（本类定义，非场景数据 — 裁决 11）："Tap" → Tap；"SetSwitch true|false" → SetSwitch。
    /// TargetBounds 从已选元素的 ObservedElement.Bounds 传入——Environment 用于归一化→物理坐标映射。</summary>
    private static DeviceAction? BuildAction(string actionDescription, int targetElementIndex, ElementBounds? targetBounds = null)
    {
        if (string.Equals(actionDescription, "Tap", StringComparison.Ordinal))
            return new DeviceAction.Tap(targetElementIndex, targetBounds);

        const string setSwitchPrefix = "SetSwitch ";
        if (actionDescription.StartsWith(setSwitchPrefix, StringComparison.Ordinal))
        {
            var stateText = actionDescription[setSwitchPrefix.Length..];
            if (string.Equals(stateText, "true", StringComparison.Ordinal))
                return new DeviceAction.SetSwitch(targetElementIndex, true, targetBounds);
            if (string.Equals(stateText, "false", StringComparison.Ordinal))
                return new DeviceAction.SetSwitch(targetElementIndex, false, targetBounds);
        }

        return null;
    }

    // ── Phase 4: Semantic Action Lowering (delegated to SemanticActionLowerer) ──

    /// <summary>
    /// Lowers an authorized SemanticAction using the current binding and observation.
    /// Delegates to <see cref="SemanticActionLowerer.Lower"/> (stateless pure function).
    /// Traversal remains protocol owner: lower → dispatch → observe → verify → journal.
    /// </summary>
    public static SemanticActionResult LowerAction(
        SemanticAction action,
        ObjectBinding binding,
        Observation observation)
        => SemanticActionLowerer.Lower(action, binding, observation);
}
