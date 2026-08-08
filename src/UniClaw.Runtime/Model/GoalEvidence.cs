namespace UniClaw.Runtime.Model;

/// <summary>
/// Goal 完成判定的证据值（宪章 §43 / I-10：Completion 必须由 Goal Evidence 证明）：
/// evidence evaluator 对 Observation 的判定结果（满足 / 不满足 + 原因 + 证据来源观测序号）。
/// 是值类型，不是 GoalEvidenceSpec 层级（该层级仍 DEFER — 裁决 3）。
/// evaluator 只有报告证据的 authority；「是否 Completed」的判定 authority 在 Agent（SC-P1-003）。
/// </summary>
/// <param name="Satisfied">证据是否满足目标条件。</param>
/// <param name="Reason">判定原因（完成 / 失败路径均记录于 Trace — SC-P1-001 断言 4 / SC-P1-003 断言 3）。</param>
/// <param name="SourceObservationSequence">证据依据的观测序号（post-action Observation — SC-P1-003 断言 2）。</param>
public sealed record GoalEvidence(bool Satisfied, string Reason, long? SourceObservationSequence);
