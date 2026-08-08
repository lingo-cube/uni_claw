namespace UniClaw.Runtime.Model;

/// <summary>
/// 单步执行的结构化结果（§45 / SC-P1-004）：Succeeded | Failed(非空原因)。
/// 是 Phase 1 购买的最小 escalate 表面：Traversal 无法推进时返回 Failed(原因)（结构化结果，
/// 非异常、非静默），经 Container 只读转交上报 Agent（I-8：lower scope 可 escalate，不得 steal
/// higher-scope authority）；Run 终止 authority 在 Agent。Trap 一等模型由 Phase 2 引入（裁决 4）；
/// Result 不携带 Expected / Observed 世界快照字段（裁决 4）。
/// </summary>
public abstract record TraversalStepResult
{
    /// <summary>单步执行成功。</summary>
    public sealed record Succeeded : TraversalStepResult;

    /// <summary>单步执行失败，携带非空原因。</summary>
    public sealed record Failed : TraversalStepResult
    {
        /// <summary>失败原因（非空）。</summary>
        public string Reason { get; }

        /// <summary>以指定原因创建失败结果。</summary>
        /// <exception cref="ArgumentException">reason 为空或空白。</exception>
        public Failed(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            Reason = reason;
        }
    }

    private TraversalStepResult() { }
}
