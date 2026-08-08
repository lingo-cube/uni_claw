namespace UniClaw.Runtime.Model;

/// <summary>
/// Trap 一等模型（HG-1 冻结，恰好 7 字段）：可信世界信念缺失的结构化表达（裁决 4 Phase 2 购买）。
/// Expected / Observed 是观测序号引用（long?，SequenceNumber），不是 Observation 快照（I-13）——
/// 世界状态只能通过观测序号在观测流中回溯，Trap 不携带世界快照。
/// 纯数据定义：无行为方法、无 RunState、无恢复逻辑；发射 authority 在 Agent（I-8），
/// 恢复语义（Recoverability）明确不在此模型内（HG-2 — 无 Recoverability 字段）。
/// </summary>
public sealed record Trap
{
    /// <summary>Trap 种类（分类原因 — TrapKind）。</summary>
    public TrapKind Kind { get; }

    /// <summary>来源层级（Phase 2 仅 Agent 发射 — TrapScope / 宪章 §21）。</summary>
    public TrapScope Scope { get; }

    /// <summary>期望状态的观测序号引用（long? — 非 Observation 快照，I-13）。</summary>
    public long? Expected { get; }

    /// <summary>观测到的状态的观测序号引用（long? — 非 Observation 快照，I-13）。</summary>
    public long? Observed { get; }

    /// <summary>来源标识（如组件 / 位置描述；非空）。</summary>
    public string Source { get; }

    /// <summary>证据描述（非空；观测到的证据事实）。</summary>
    public string Evidence { get; }

    /// <summary>最近的已分发动作（可为 null — 无动作或仅在观察阶段）。</summary>
    public DeviceAction? LastAction { get; }

    /// <summary>构造 Trap。</summary>
    /// <param name="kind">Trap 种类（分类原因）。</param>
    /// <param name="scope">来源层级。</param>
    /// <param name="expected">期望状态的观测序号引用（可为 null — 无期望序号）。</param>
    /// <param name="observed">观测到的状态的观测序号引用（可为 null — 无观测序号）。</param>
    /// <param name="source">来源标识（非空）。</param>
    /// <param name="evidence">证据描述（非空）。</param>
    /// <param name="lastAction">最近的已分发动作（可为 null）。</param>
    /// <exception cref="ArgumentException">source 或 evidence 为空或空白。</exception>
    public Trap(
        TrapKind kind,
        TrapScope scope,
        long? expected,
        long? observed,
        string source,
        string evidence,
        DeviceAction? lastAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        Kind = kind;
        Scope = scope;
        Expected = expected;
        Observed = observed;
        Source = source;
        Evidence = evidence;
        LastAction = lastAction;
    }
}
