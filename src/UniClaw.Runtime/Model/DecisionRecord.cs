namespace UniClaw.Runtime.Model;

/// <summary>
/// Agent 语义决策日志条目（§28 / 裁决 5）：不可变值，只追加不改写（I-2——唯一可变 owner 是 Agent，
/// Phase 1 由 Agent 持有 List&lt;DecisionRecord&gt;）。
///
/// 命名澄清（observability-emission-expansion）：本类型是「决策记录」，不是 OTel trace event，
/// 也不挂在观测 Trace 上。词汇约定：
///   - Trace = OTel 式因果链/协议载体（TraceRun + Activity spans，轨道 A）；
///   - Event = 挂在 Trace 上的点事件（投影层 RuntimeEventEnvelope / span 上的 OTel event）；
///   - DecisionRecord = Agent 内部语义决策 journal（轨道 B），经 RuntimeEventProjector 投影
///     为 RuntimeEventEnvelope 后成为 Event-on-Trace，自身从不直接进入观测系统
///     （persistence / export / metrics / spans DEFER — 裁决 5）。
/// 更名 TraceEvent → DecisionRecord 是纯词汇澄清，零行为变更；wired/持久化 schema 不变。
///
/// ContainerId / StepId / ActionId / Action / Reason / RunState 可空——生命周期事件在
/// Container / Traversal 存在之前即可产生（SC-P1-002 断言 6：无 Container / Step 事件）。
/// </summary>
public sealed record DecisionRecord
{
    /// <summary>Run 标识（必填）。</summary>
    public string RunId { get; }

    /// <summary>容器标识（生命周期早期条目可为 null）。</summary>
    public string? ContainerId { get; init; }

    /// <summary>步骤标识（生命周期早期条目可为 null）。</summary>
    public string? StepId { get; init; }

    /// <summary>动作标识（生命周期早期条目可为 null）。</summary>
    public string? ActionId { get; init; }

    /// <summary>动作载荷（SC-P1-005 断言 1：从决策记录证明动作作用于哪个元素）。</summary>
    public DeviceAction? Action { get; init; }

    /// <summary>显式原因（SC-P1-001/002/003/004「显式原因」断言的可观察面）。</summary>
    public string? Reason { get; init; }

    /// <summary>Run 生命周期转移（SC-P1-001 断言 1 / SC-P1-002 断言 1：从未进入 Running）。</summary>
    public RunState? RunState { get; init; }

    /// <summary>Trap 种类（A4；null = 该条目不是 Trap 发射记录 — Phase 1 条目不受影响）。</summary>
    public TrapKind? TrapKind { get; init; }

    /// <summary>Trap 来源层级（A4；null = 该条目不是 Trap 发射记录 — Phase 1 条目不受影响）。</summary>
    public TrapScope? TrapScope { get; init; }

    /// <summary>恢复动作标识（A4；null = 该条目未关联恢复动作 — Phase 1 条目不受影响）。</summary>
    public string? RecoveryId { get; init; }

    /// <summary>创建决策记录；RunId 必须非空。</summary>
    /// <exception cref="ArgumentException">runId 为空或空白。</exception>
    public DecisionRecord(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        RunId = runId;
    }

    /// <summary>
    /// DecisionRecord-derived run-outcome predicate for reconciliation (I-2: RunState's sole
    /// owner is the Agent; RunState member access is confined to the Model/Agent
    /// boundary). The Agent records a terminal <see cref="RunState.Failed"/> record on
    /// failure (never on completion), so the journal recording that terminal state signals
    /// a failed run outcome. Keeps the stateless HypothesisReconciler free of RunState
    /// member access (Planning/ never touches RunState).
    /// </summary>
    internal static bool IndicatesFailedRun(IReadOnlyList<DecisionRecord> trace)
        => trace.Any(entry => entry.RunState == UniClaw.Runtime.Model.RunState.Failed);
}
