namespace UniClaw.Runtime.Model;

/// <summary>
/// 恢复执行的结构化结果（HG-5 冻结，恰好 2 变体）：Verified | Failed(非空原因)。
/// Phase 2 恢复语义购买的最小结果表面：恢复成功（Verified — 无字段，RecoveryId 属 DecisionRecord — A4）
/// 或恢复失败（Failed(显式原因)）。不引入 Incomplete / Retryable 等其它变体（HG-5）。
/// 纯数据定义：无行为方法、无 RunState、无恢复逻辑。
/// </summary>
public abstract record RecoveryResult
{
    /// <summary>恢复执行成功（无字段 — RecoveryId 由 DecisionRecord 携带 — A4）。</summary>
    public sealed record Verified : RecoveryResult;

    /// <summary>恢复执行失败，携带非空原因。</summary>
    public sealed record Failed : RecoveryResult
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

    private RecoveryResult() { }
}
