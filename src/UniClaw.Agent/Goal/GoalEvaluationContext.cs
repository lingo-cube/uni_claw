namespace UniClaw.Agent.Goal;

/// <summary>
/// Goal Evaluation Context — Target §3.9 第三输入（user/supervisory
/// context）的 opaque 契约（GEV-004 D7）：context 进入 canonical API 语义
/// 但不扩能力。当前仅允许 Empty、零成员；未来 context buyer 到来时扩展
/// 内部语义而不破坏 Evaluate 签名。
/// _Avoid_: user profile、session state、config。
/// </summary>
public sealed record GoalEvaluationContext
{
    private GoalEvaluationContext() { }

    /// <summary>唯一合法实例（当前唯一取值）。</summary>
    public static GoalEvaluationContext Empty { get; } = new();
}
