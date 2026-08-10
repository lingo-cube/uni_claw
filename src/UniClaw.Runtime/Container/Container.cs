using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Container;

/// <summary>
/// 语义页面范围的局部运行状态域（宪章 §6；specs/container-traversal SHALL）。
/// 拥有其全部局部可变状态（当前 Observation / candidates / visited / local progress / 完成判断）——
/// 唯一 owner（I-2），跨 owner 只暴露不可变快照。
/// 回答两个问题：IsStillMine（当前观测是否仍属于本语义页面）、IsLocalComplete（局部执行是否完成）。
/// 单步执行委托给注入的 step executor（B6 Traversal 实现；B5 只留注入点——§49：不为 executor 建接口）；
/// 步骤失败结果原样转交 Agent（I-8 / SC-P1-004：不重写、不吞没、不重试、不恢复、不触碰 RunState）。
/// Semantic Identity 由显式规则注入（切片 1 = 页面名/元素匹配；Phase 5 算法 DEFER；
/// Fingerprint 字段与机制 DEFER — 裁决 2，I-6 原则保留）。
/// 不硬编码场景字符串（裁决 11）：页面名 / identity 规则 / 步骤数据全部由调用侧注入。
/// 依赖：仅 Model + BCL（I-1 — 不引用 Agent/Startup/World/Environment/Traversal）。
/// </summary>
public sealed class Container
{
    private readonly string _semanticPageName;
    private readonly Func<Observation, bool> _identityRule;
    private readonly Func<PlanStep, Observation, ImmutableArray<ObservedElement>, TraversalStepResult> _stepExecutor;
    private readonly Func<PlanStep, Observation, ImmutableArray<ObservedElement>, ImmutableDictionary<int, CandidateAuthorizationEvidence>, TraversalStepResult>? _groundedStepExecutor;
    private Observation? _observation;
    private ImmutableArray<PlanStep> _executedSteps = [];
    private ImmutableArray<Observation> _viewportExplorationObservations = [];
    private bool _isLocalComplete;

    /// <summary>构造语义页面容器。</summary>
    /// <param name="semanticPageName">语义页面名（来自 RecoveryAnchor.ExpectedSemanticEntry 等注入数据）。</param>
    /// <param name="identityRule">still-mine 规则：Observation → bool（切片 1 显式规则注入；Phase 5 语义解析算法 DEFER）。</param>
    /// <param name="stepExecutor">单步执行器：PlanStep + 当前观测 + candidates → TraversalStepResult（B6 实现；
    /// 失败时须返回 Failed(非空原因) 供原样转交——非异常、非静默 — §45）。</param>
    /// <exception cref="ArgumentException">semanticPageName 为空或空白。</exception>
    /// <exception cref="ArgumentNullException">identityRule 或 stepExecutor 为 null。</exception>
    public Container(
        string semanticPageName,
        Func<Observation, bool> identityRule,
        Func<PlanStep, Observation, ImmutableArray<ObservedElement>, TraversalStepResult> stepExecutor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticPageName);
        ArgumentNullException.ThrowIfNull(identityRule);
        ArgumentNullException.ThrowIfNull(stepExecutor);
        _semanticPageName = semanticPageName;
        _identityRule = identityRule;
        _stepExecutor = stepExecutor;
    }

    /// <summary>CP12 forwarding constructor. The Container forwards immutable Agent receipts without interpreting them.</summary>
    public Container(
        string semanticPageName,
        Func<Observation, bool> identityRule,
        Func<PlanStep, Observation, ImmutableArray<ObservedElement>, ImmutableDictionary<int, CandidateAuthorizationEvidence>, TraversalStepResult> stepExecutor,
        bool forwardsAuthorizationReceipts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticPageName);
        ArgumentNullException.ThrowIfNull(identityRule);
        ArgumentNullException.ThrowIfNull(stepExecutor);
        if (!forwardsAuthorizationReceipts)
            throw new ArgumentException("CP12 forwarding constructor requires explicit forwarding=true.", nameof(forwardsAuthorizationReceipts));
        _semanticPageName = semanticPageName;
        _identityRule = identityRule;
        _stepExecutor = (step, observation, candidates) => stepExecutor(step, observation, candidates, ImmutableDictionary<int, CandidateAuthorizationEvidence>.Empty);
        _groundedStepExecutor = stepExecutor;
    }

    /// <summary>语义页面名（只读快照；RecoveryAnchor.ExpectedSemanticEntry 的数据来源）。</summary>
    public string SemanticPageName => _semanticPageName;

    /// <summary>当前观测（只读快照）；null = 尚未 Bind。</summary>
    public Observation? CurrentObservation => _observation;

    /// <summary>候选元素 = 当前观测的全部元素（只读快照；Container 提供候选，Traversal.Select 选择与消歧 — 裁决 3）。</summary>
    public ImmutableArray<ObservedElement> Candidates => _observation?.Elements ?? ImmutableArray<ObservedElement>.Empty;

    /// <summary>已执行（尝试过）的步骤（追加式只读快照；含失败步骤 — visited tracking / local progress）。</summary>
    public ImmutableArray<PlanStep> ExecutedSteps => _executedSteps;

    /// <summary>
    /// SC-P3-CAND-007 bounded same-Container accepted Observation evidence.
    /// Sequence is freshness/order evidence only; callers receive an immutable snapshot.
    /// </summary>
    public ImmutableArray<Observation> ViewportExplorationObservations => _viewportExplorationObservations;

    /// <summary>局部执行是否完成：最近一次 ExecuteStep 返回 Succeeded 即为 true；Bind 后重置为 false。</summary>
    public bool IsLocalComplete => _isLocalComplete;

    /// <summary>显式绑定初始观测（Agent 在 Navigate / Rebind 时调用 — §6 / design.md §5）；重置局部进度。</summary>
    /// <exception cref="ArgumentNullException">observation 为 null。</exception>
    public void Bind(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _observation = observation;
        _executedSteps = [];
        _viewportExplorationObservations = [observation];
        _isLocalComplete = false;
    }

    /// <summary>当前观测是否仍属于本语义页面（§6：注入的 identity rule 判定；本容器不做全局/身份算法判定 — I-3）。</summary>
    /// <exception cref="ArgumentNullException">observation 为 null。</exception>
    public bool IsStillMine(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return _identityRule(observation);
    }

    /// <summary>
    /// 判断现有证据是否构成 SC-P3-002 支持的 Container-scope 局部 obstruction 假设。
    /// 同一前台应用 + 语义页面 Unknown + 当前 identity rule 不接受该观测，只允许进入局部证明流程，
    /// 不证明 Popup 已识别、底层页面已改变或 Container 连续。
    /// </summary>
    public bool IsLocalObstructionHypothesis(
        Observation observation,
        string? reconciledSemanticPage,
        string expectedForegroundApplication)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedForegroundApplication);
        return reconciledSemanticPage is null
            && string.Equals(
                observation.ForegroundApplication,
                expectedForegroundApplication,
                StringComparison.Ordinal)
            && !IsStillMine(observation);
    }

    /// <summary>局部 obstruction hypothesis 是否可由计划中已批准的 handling step 在当前候选上 grounding。</summary>
    public bool CanHandleLocalObstruction(
        Observation observation,
        string? reconciledSemanticPage,
        string expectedForegroundApplication,
        PlanStep localHandlingStep)
    {
        ArgumentNullException.ThrowIfNull(localHandlingStep);
        return IsLocalObstructionHypothesis(
                observation,
                reconciledSemanticPage,
                expectedForegroundApplication)
            && observation.Elements.Any(element => string.Equals(
                element.Text,
                localHandlingStep.TargetDescription,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// 将 fresh obstruction Observation 接入同一 Container，供计划中已批准的下一步执行有界 local handling。
    /// 仅当下一步目标可由当前候选 grounding 时接受；更新当前 Observation，但不调用 Bind，因而不清空 local progress。
    /// </summary>
    public bool TryAcceptLocalObstruction(
        Observation observation,
        string? reconciledSemanticPage,
        string expectedForegroundApplication,
        PlanStep localHandlingStep)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(localHandlingStep);
        if (_observation is null)
            throw new InvalidOperationException("Container 尚未绑定观测：Bind 必须先于局部 obstruction 分类。");
        if (observation.SequenceNumber <= _observation.SequenceNumber
            || !CanHandleLocalObstruction(
                observation,
                reconciledSemanticPage,
                expectedForegroundApplication,
                localHandlingStep))
        {
            return false;
        }

        _observation = observation;
        return true;
    }

    /// <summary>
    /// 用 handling 后的 fresh evidence 验证同一 Container 连续性。只有序号严格推进、前台兼容、
    /// 既有 identity rule 接受且 reconciled semantic page 与本 Container 一致时才更新当前 Observation；
    /// 失败不修改绑定或 local progress。
    /// </summary>
    public bool TryVerifyLocalContinuity(
        Observation observation,
        string? reconciledSemanticPage,
        string expectedForegroundApplication)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedForegroundApplication);
        if (_observation is null)
            throw new InvalidOperationException("Container 尚未绑定观测：Bind 必须先于局部连续性验证。");
        if (observation.SequenceNumber <= _observation.SequenceNumber
            || !string.Equals(
                observation.ForegroundApplication,
                expectedForegroundApplication,
                StringComparison.Ordinal)
            || !IsStillMine(observation)
            || !string.Equals(reconciledSemanticPage, _semanticPageName, StringComparison.Ordinal))
        {
            return false;
        }

        _observation = observation;
        return true;
    }

    /// <summary>
    /// 用 fresh post-viewport evidence 验证同一 Container 连续性。snapshot 元素变化本身不参与 identity
    /// 裁决；只有严格更新的序号、兼容前台、既有 identity rule 与相同 reconciled semantic page
    /// 共同成立时才推进当前 Observation。成功不调用 Bind，因此保留既有 local progress。
    /// </summary>
    public bool TryVerifyViewportContinuity(
        Observation observation,
        string? reconciledSemanticPage,
        string expectedForegroundApplication)
    {
        if (!TryVerifyLocalContinuity(
                observation,
                reconciledSemanticPage,
                expectedForegroundApplication))
        {
            return false;
        }

        _viewportExplorationObservations = _viewportExplorationObservations.Add(observation);
        return true;
    }

    /// <summary>
    /// 使用既有 Trap vocabulary 构造 Container-scope 局部证明不足证据。
    /// 只表达 Container continuity 未获证明；Agent 仍决定 rebind、Agent Recovery 或 Run failure。
    /// </summary>
    public Trap CreateLocalObstructionEscalation(
        Observation? observed,
        DeviceAction? lastAction,
        string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        if (_observation is null)
            throw new InvalidOperationException("Container 尚未绑定观测：Bind 必须先于局部 obstruction 升级。");
        return new Trap(
            TrapKind.ContainerMismatch,
            TrapScope.Container,
            _observation.SequenceNumber,
            observed?.SequenceNumber,
            "Container.VerifyLocalContinuity",
            evidence,
            lastAction);
    }

    /// <summary>
    /// 使用既有 Trap vocabulary 构造 viewport continuity 未获证明的 Container-scope evidence。
    /// Agent 仍独占 rebind、Recovery、GoalEvidence 与最终 RunState authority。
    /// </summary>
    public Trap CreateViewportContinuityEscalation(
        Observation? observed,
        DeviceAction? lastAction,
        string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        if (_observation is null)
            throw new InvalidOperationException("Container 尚未绑定观测：Bind 必须先于 viewport continuity 升级。");
        return new Trap(
            TrapKind.ContainerMismatch,
            TrapScope.Container,
            _observation.SequenceNumber,
            observed?.SequenceNumber,
            "Container.VerifyViewportContinuity",
            evidence,
            lastAction);
    }

    /// <summary>
    /// 执行一步：把 PlanStep + 当前观测 + candidates 交给注入的 step executor。
    /// 结果原样返回：Succeeded → 局部完成；Failed → 只读转交 Agent（I-8 / SC-P1-004：
    /// 不重写、不吞没、不重试、不恢复、不触碰 RunState — Run 终止 authority 在 Agent）。
    /// </summary>
    /// <exception cref="ArgumentNullException">step 为 null。</exception>
    /// <exception cref="InvalidOperationException">尚未 Bind；或 executor 返回 null（协议违约 — §45）。</exception>
    public TraversalStepResult ExecuteStep(PlanStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (_observation is null)
            throw new InvalidOperationException("Container 尚未绑定观测：Bind 必须先于 ExecuteStep 调用。");
        var result = _stepExecutor(step, _observation, _observation.Elements)
            ?? throw new InvalidOperationException("step executor 返回 null：executor 必须返回 TraversalStepResult（非异常、非静默 — §45）。");
        _executedSteps = _executedSteps.Add(step);
        if (result is TraversalStepResult.Succeeded)
            _isLocalComplete = true;
        return result;
    }

    /// <summary>Forwards CP12 immutable authorization receipts unchanged; this Container makes no grounding decision.</summary>
    public TraversalStepResult ExecuteStep(
        PlanStep step,
        ImmutableDictionary<int, CandidateAuthorizationEvidence> authorizationReceipts)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(authorizationReceipts);
        if (_observation is null)
            throw new InvalidOperationException("Container 尚未绑定观测：Bind 必须先于 ExecuteStep 调用。");
        if (_groundedStepExecutor is null)
            throw new InvalidOperationException("Container 未装配 CP12 immutable receipt forwarding executor。");
        var result = _groundedStepExecutor(step, _observation, _observation.Elements, authorizationReceipts)
            ?? throw new InvalidOperationException("step executor 返回 null：executor 必须返回 TraversalStepResult（非异常、非静默 — §45）。");
        _executedSteps = _executedSteps.Add(step);
        if (result is TraversalStepResult.Succeeded)
            _isLocalComplete = true;
        return result;
    }
}
