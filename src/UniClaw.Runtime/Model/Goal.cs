namespace UniClaw.Runtime.Model;

/// <summary>
/// Run 的目标：仅承载证据评估注入点（裁决 3——最小注入点）。
/// evaluator 对每次 post-action Observation 评估并产出 GoalEvidence（SC-P1-003）；
/// 「是否 Completed」的判定 authority 在 Agent（I-10）。
/// 不创建 GoalGraph / GoalEngine / GoalEvidenceSpec 层级（裁决 3 / I-12）。
/// </summary>
/// <param name="EvidenceEvaluator">证据评估器：对 Observation 评估产生 GoalEvidence（调用侧注入）。</param>
public sealed record Goal(Func<Observation, GoalEvidence> EvidenceEvaluator);
