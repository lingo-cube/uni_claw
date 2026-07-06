using System.Collections.Immutable;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// 引擎执行结果（统一 SimulationResult + TraversalResult）。
/// sealed record class, ImmutableArray 集合 (P-5)。
/// </summary>
/// <param name="Success">是否成功完成</param>
/// <param name="CompletionReason">完成原因（使用 Reasons 常量）</param>
/// <param name="TotalSteps">总执行步数</param>
/// <param name="ElapsedSeconds">耗时（秒）</param>
/// <param name="ActionHistory">操作历史记录</param>
/// <param name="VisitedPages">已访问页面 ID 序列</param>
/// <param name="Trace">每步 trace 记录</param>
/// <param name="TraceId">追踪 ID</param>
/// <param name="FinalState">FSM 终态</param>
/// <param name="Error">错误（如果有）</param>
public sealed record class TraversalResult(
    bool Success,
    string CompletionReason,
    int TotalSteps,
    double ElapsedSeconds,
    ImmutableArray<ActionRecord> ActionHistory,
    ImmutableArray<string> VisitedPages,
    ImmutableArray<TraceRecord> Trace,
    string? TraceId,
    TraversalState FinalState,
    Exception? Error = null)
{
    /// <summary>预定义完成原因</summary>
    public static class Reasons
    {
        public const string AllVisited = "all_visited";
        public const string MaxSteps = "max_steps";
        public const string Error = "error";
        public const string AntiLoop = "anti_loop";
        public const string Cancelled = "cancelled";
    }
}
