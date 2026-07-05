using System.Collections.Immutable;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation;

/// <summary>仿真运行结果</summary>
public sealed record class SimulationResult(
    bool Success,
    string CompletionReason,
    int TotalSteps,
    double ElapsedSeconds,
    ImmutableArray<ActionRecord> ActionHistory,
    ImmutableArray<string> VisitedPages,
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
    }
}
