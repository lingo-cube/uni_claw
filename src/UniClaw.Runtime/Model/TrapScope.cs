namespace UniClaw.Runtime.Model;

/// <summary>
/// Trap 发射 scope（HG-1 冻结）：Trap 的来源层级。
/// Step / Container 是宪章 §21 词汇的保留成员（Phase 2 当前不发射）；
/// Phase 2 仅 Agent 发射 Trap（Agent 是唯一 Run 终止 authority — I-2 / I-8）。
/// </summary>
public enum TrapScope
{
    /// <summary>步骤级来源（宪章 §21 词汇预留 — Phase 2 不发射）。</summary>
    Step = 0,

    /// <summary>容器级来源（宪章 §21 词汇预留 — Phase 2 不发射）。</summary>
    Container = 1,

    /// <summary>Agent 级来源（Phase 2 唯一发射 scope — Agent 是 Run 级判定 authority）。</summary>
    Agent = 2,
}
