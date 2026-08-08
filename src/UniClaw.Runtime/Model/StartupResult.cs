namespace UniClaw.Runtime.Model;

/// <summary>
/// Startup 阶段的结构化结果（§19 / §45：Result 类型表达语义）：Ready(RecoveryAnchor) 或 NotReady(显式原因)。
/// Ready 之前 Run 不得进入 Running（SC-P1-001 / SC-P1-002）；
/// 报告 Ready / NotReady 的 authority 在 Startup 程序，判定 Run 去向的 authority 在 Agent（I-8）。
/// 不抛异常——NotReady(reason) 即失败表达。
/// </summary>
public abstract record StartupResult
{
    /// <summary>Startup 成功，携带已建立的 RecoveryAnchor。</summary>
    /// <param name="Anchor">Startup 建立的可信恢复入口（§20）。</param>
    public sealed record Ready(RecoveryAnchor Anchor) : StartupResult;

    /// <summary>Startup 失败，携带显式原因（SC-P1-002 断言 2：StartupResult == NotReady(显式原因)）。</summary>
    public sealed record NotReady : StartupResult
    {
        /// <summary>失败原因（非空——显式原因契约）。</summary>
        public string Reason { get; }

        /// <summary>以指定原因创建 NotReady。</summary>
        /// <exception cref="ArgumentException">reason 为空或空白。</exception>
        public NotReady(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            Reason = reason;
        }
    }

    private StartupResult() { }
}
