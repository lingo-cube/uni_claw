using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Recovery;

/// <summary>
/// 恢复机制组件（HG-4 Option B：机制归组件、决策归 Agent）。
/// 持有恢复机制状态（配方动作列表 / 分发游标），通过注入的委托完成配方解析、恢复动作解析、验证判据检查；
/// 动作分发与恢复观测仅经 IEnvironment 端口（B2）——本组件不引用 Agent 侧类型，
/// 也不引用 Container / Traversal 命名空间（I-1 依赖方向：Agent → Recovery → Environment；Guard 7）。
/// 不持有 RunState、不做恢复决策（何时恢复 / 恢复到哪 / 何时续跑由 Agent 决定 — I-8 / HG-4）；
/// 不创建 RecoveryRequest / Planner / Runtime 类型（HG-5）；无恢复重试 / 恢复策略（HG-2）。
/// 机制输入全部来自注入数据（裁决 8 / 11：不硬编码场景字符串）。
/// </summary>
public sealed class Recovery
{
    private readonly IEnvironment _environment;
    private readonly Func<string, ImmutableArray<DeviceAction>> _parseRestoreRecipe;
    private readonly Func<PlanStep, Observation, DeviceAction?> _resolveRecoveryAction;
    private readonly Func<Observation, string, bool> _verifyCriteria;
    private ImmutableArray<DeviceAction> _recipeActions = [];
    private int _recipeIndex;

    /// <summary>构造恢复机制组件。</summary>
    /// <param name="environment">IEnvironment 端口——恢复动作分发与恢复观测的能力边界（B2）。</param>
    /// <param name="parseRestoreRecipe">恢复配方解析：RecoveryAnchor.RestoreRecipe 字符串 → 动作列表
    /// （空 / null 配方 → 空列表；动作经 ExecuteNextAsync 依次分发）。</param>
    /// <param name="resolveRecoveryAction">位置恢复动作解析：PlanStep + 当前观测 → DeviceAction（无法解析 = null，由 Agent 显式失败）。</param>
    /// <param name="verifyCriteria">恢复验证判据检查：Observation + VerificationCriteria 字符串 → 是否满足。</param>
    /// <exception cref="ArgumentNullException">任一参数为 null。</exception>
    public Recovery(
        IEnvironment environment,
        Func<string, ImmutableArray<DeviceAction>> parseRestoreRecipe,
        Func<PlanStep, Observation, DeviceAction?> resolveRecoveryAction,
        Func<Observation, string, bool> verifyCriteria)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(parseRestoreRecipe);
        ArgumentNullException.ThrowIfNull(resolveRecoveryAction);
        ArgumentNullException.ThrowIfNull(verifyCriteria);
        _environment = environment;
        _parseRestoreRecipe = parseRestoreRecipe;
        _resolveRecoveryAction = resolveRecoveryAction;
        _verifyCriteria = verifyCriteria;
    }

    /// <summary>开始一次恢复会话：消费恢复锚点的配方（解析 → 动作列表，分发游标置零）。</summary>
    /// <param name="anchor">恢复锚点（RecoveryAnchor — §20）。</param>
    /// <exception cref="ArgumentNullException">anchor 为 null。</exception>
    public void Begin(RecoveryAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        _recipeActions = _parseRestoreRecipe(anchor.RestoreRecipe ?? string.Empty);
        _recipeIndex = 0;
    }

    /// <summary>配方中是否还有未分发的动作（Begin 后有效）。</summary>
    public bool HasRemainingActions => _recipeIndex < _recipeActions.Length;

    /// <summary>分发下一个配方动作（经 IEnvironment；dispatch 结果不构成恢复成功证据 — 裁决 10，验证由 Agent 驱动）。</summary>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>已分发的动作（供 Trace 记录）。</returns>
    /// <exception cref="InvalidOperationException">配方已耗尽（调用方必须先检查 HasRemainingActions）。</exception>
    public async Task<DeviceAction> ExecuteNextAsync(CancellationToken cancellationToken)
    {
        if (!HasRemainingActions)
            throw new InvalidOperationException("恢复配方已耗尽：ExecuteNextAsync 前必须先检查 HasRemainingActions。");
        var action = _recipeActions[_recipeIndex];
        _recipeIndex++;
        await _environment.ExecuteAsync(action, cancellationToken);
        return action;
    }

    /// <summary>分发单个恢复动作（位置恢复用 — 动作由 ResolveRecoveryAction 解析；经 IEnvironment）。</summary>
    /// <param name="action">待分发动作。</param>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>已分发的动作（供 Trace 记录）。</returns>
    /// <exception cref="ArgumentNullException">action 为 null。</exception>
    public async Task<DeviceAction> ExecuteActionAsync(DeviceAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _environment.ExecuteAsync(action, cancellationToken);
        return action;
    }

    /// <summary>恢复后重新观测（§3：动作后必须重新观察；恢复结果只能经观测重新确认）。</summary>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>恢复观测。</returns>
    public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        => await _environment.ObserveAsync(cancellationToken);

    /// <summary>恢复验证：按注入判据检查观测是否满足验证标准 → Verified | Failed(非空原因)。
    /// 失败原因显式携带期望（VerificationCriteria 文本）与实际（观测事实：前台 / 页面 / 序号）——
    /// SC-P2-003 Evidence 1（I-9 负向：恢复动作成功 ≠ 恢复完成；裁决 10 在恢复语境延续）。</summary>
    /// <param name="observation">恢复后观测。</param>
    /// <param name="verificationCriteria">验证标准（RecoveryAnchor.VerificationCriteria）——B5：
    /// 判据被语义消费（驱动 pass/fail 判定 + 作为失败证据的期望侧），非原样透传。</param>
    /// <returns>Verified（满足）或 Failed(非空原因)（不满足）。</returns>
    /// <exception cref="ArgumentNullException">observation 为 null。</exception>
    /// <exception cref="ArgumentException">verificationCriteria 为空或空白。</exception>
    public RecoveryResult Verify(Observation observation, string verificationCriteria)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationCriteria);
        return _verifyCriteria(observation, verificationCriteria)
            ? new RecoveryResult.Verified()
            : new RecoveryResult.Failed(BuildVerifyFailureReason(observation, verificationCriteria));
    }

    /// <summary>验证失败原因：期望 = VerificationCriteria 文本；实际 = 恢复观测的可观测事实
    /// （前台应用 / 页面 / 观测序号）。本阶段页面身份 = 前台应用（Startup.ExpectedSemanticEntry 同粒度）。</summary>
    private static string BuildVerifyFailureReason(Observation observation, string verificationCriteria)
    {
        var actual = observation.ForegroundApplication ?? "null";
        return $"恢复验证失败：期望 [{verificationCriteria}]，实际 Foreground=[{actual}], page=[{actual}]（seq={observation.SequenceNumber}）";
    }

    /// <summary>位置恢复动作解析：按注入规则把 PlanStep 解析为 DeviceAction（无法解析 = null — Agent 显式失败）。</summary>
    /// <param name="step">待恢复位置的 PlanStep。</param>
    /// <param name="observation">当前位置观测。</param>
    /// <returns>解析出的动作；null = 无法解析。</returns>
    public DeviceAction? ResolveRecoveryAction(PlanStep step, Observation observation)
        => _resolveRecoveryAction(step, observation);
}
