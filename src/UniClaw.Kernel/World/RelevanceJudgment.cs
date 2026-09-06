namespace UniClaw.Kernel.World;

/// <summary>
/// Belief Relevance 判定留痕：World Model 对单条 accepted Evidence 的
/// 独立判定产物（与 Admission Record 是两个产出，Target 验收 1）。
/// </summary>
public sealed record RelevanceJudgment(string EvidenceId, bool IsRelevant, string Reason);
