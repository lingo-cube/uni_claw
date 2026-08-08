namespace UniClaw.Runtime.Model;

/// <summary>
/// Run 全局生命周期（宪章 §18）：只回答「这个 Run 当前处于什么生命周期」，
/// 不承担任何世界判断、页面恢复或 Agent Intelligence（§18）。
/// Phase 1 转移：Idle → Initializing → Running → Completed | Failed（SC-P1-001 / SC-P1-002 / SC-P1-003 / SC-P1-004）。
/// 完成只能由 Goal Evidence 证明（I-10）；终止只能由 Agent 判定（RunState 唯一 owner，I-2）。
/// </summary>
public enum RunState
{
    /// <summary>Run 尚未开始。</summary>
    Idle = 0,

    /// <summary>正在初始化：Startup 执行中（Attach → Launch → Observe → Verify → Ready）。</summary>
    Initializing = 1,

    /// <summary>正式执行中：仅当 Startup 报告 Ready 之后才允许进入（SC-P1-002：Ready 前不得进入 Running）。</summary>
    Running = 2,

    /// <summary>正常完成：仅当 Goal evidence evaluator 产出 Satisfied 的 GoalEvidence（I-10）。</summary>
    Completed = 3,

    /// <summary>失败终止：由 Agent 判定并记录显式原因（SC-P1-002/003/004 失败路径）。</summary>
    Failed = 4,

    /// <summary>预留：Pause / Shutdown 完整语义引入时启用；Phase 1 无语义、不发生转移（design §8）。</summary>
    Terminated = 5,
}
