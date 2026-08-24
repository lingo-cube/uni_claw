namespace UniClaw.Runtime.Model;

/// <summary>
/// Trace 因果链事件（§28 / 裁决 5）：不可变值，只追加不改写（I-2——唯一可变 owner 是 Agent，
/// Phase 1 由 Agent 持有 List&lt;TraceEvent&gt;）。不创建独立 Observability/ 组件
/// （persistence / export / metrics / spans DEFER — 裁决 5）。
/// ContainerId / StepId / ActionId / Action / Reason / RunState 可空——生命周期事件在
/// Container / Traversal 存在之前即可产生（SC-P1-002 断言 6：无 Container / Step 事件）。
/// </summary>
public sealed record TraceEvent
{
    /// <summary>Run 标识（必填）。</summary>
    public string RunId { get; }

    /// <summary>容器标识（生命周期早期事件可为 null）。</summary>
    public string? ContainerId { get; init; }

    /// <summary>步骤标识（生命周期早期事件可为 null）。</summary>
    public string? StepId { get; init; }

    /// <summary>动作标识（生命周期早期事件可为 null）。</summary>
    public string? ActionId { get; init; }

    /// <summary>动作载荷（SC-P1-005 断言 1：从 Trace 证明动作作用于哪个元素）。</summary>
    public DeviceAction? Action { get; init; }

    /// <summary>显式原因（SC-P1-001/002/003/004「显式原因」断言的可观察面）。</summary>
    public string? Reason { get; init; }

    /// <summary>Run 生命周期转移（SC-P1-001 断言 1 / SC-P1-002 断言 1：从未进入 Running）。</summary>
    public RunState? RunState { get; init; }

    /// <summary>Trap 种类（A4；null = 该事件不是 Trap 发射事件 — Phase 1 事件不受影响）。</summary>
    public TrapKind? TrapKind { get; init; }

    /// <summary>Trap 来源层级（A4；null = 该事件不是 Trap 发射事件 — Phase 1 事件不受影响）。</summary>
    public TrapScope? TrapScope { get; init; }

    /// <summary>恢复动作标识（A4；null = 该事件未关联恢复动作 — Phase 1 事件不受影响）。</summary>
    public string? RecoveryId { get; init; }

    /// <summary>创建 Trace 事件；RunId 必须非空。</summary>
    /// <exception cref="ArgumentException">runId 为空或空白。</exception>
    public TraceEvent(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        RunId = runId;
    }

    /// <summary>
    /// Trace-derived run-outcome predicate for reconciliation (I-2: RunState's sole
    /// owner is the Agent; RunState member access is confined to the Model/Agent
    /// boundary). The Agent records a terminal <see cref="RunState.Failed"/> event on
    /// failure (never on completion), so a trace recording that terminal state signals
    /// a failed run outcome. Keeps the stateless HypothesisReconciler free of RunState
    /// member access (Planning/ never touches RunState).
    /// </summary>
    internal static bool IndicatesFailedRun(IReadOnlyList<TraceEvent> trace)
        => trace.Any(entry => entry.RunState == UniClaw.Runtime.Model.RunState.Failed);
}
