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
    private Observation? _observation;
    private ImmutableArray<PlanStep> _executedSteps = [];
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

    /// <summary>语义页面名（只读快照；RecoveryAnchor.ExpectedSemanticEntry 的数据来源）。</summary>
    public string SemanticPageName => _semanticPageName;

    /// <summary>当前观测（只读快照）；null = 尚未 Bind。</summary>
    public Observation? CurrentObservation => _observation;

    /// <summary>候选元素 = 当前观测的全部元素（只读快照；Container 提供候选，Traversal.Select 选择与消歧 — 裁决 3）。</summary>
    public ImmutableArray<ObservedElement> Candidates => _observation?.Elements ?? ImmutableArray<ObservedElement>.Empty;

    /// <summary>已执行（尝试过）的步骤（追加式只读快照；含失败步骤 — visited tracking / local progress）。</summary>
    public ImmutableArray<PlanStep> ExecutedSteps => _executedSteps;

    /// <summary>局部执行是否完成：最近一次 ExecuteStep 返回 Succeeded 即为 true；Bind 后重置为 false。</summary>
    public bool IsLocalComplete => _isLocalComplete;

    /// <summary>显式绑定初始观测（Agent 在 Navigate / Rebind 时调用 — §6 / design.md §5）；重置局部进度。</summary>
    /// <exception cref="ArgumentNullException">observation 为 null。</exception>
    public void Bind(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _observation = observation;
        _executedSteps = [];
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
}
