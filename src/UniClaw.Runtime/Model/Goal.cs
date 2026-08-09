using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Run 的目标：承载 Goal evidence、bounded candidate authorization 与 viewport exploration 注入点（裁决 3——最小注入点）。
/// evaluator 对每次 post-action Observation 评估并产出 GoalEvidence（SC-P1-003）；
/// 「是否 Completed」的判定 authority 在 Agent（I-10）。
/// SC-P3-CAND-006 evaluator 只对 fresh Observation 中的 candidate 产出三值 authorization evidence；
/// authorization 不是 dispatch、world effect、required work 或 completion truth。
/// SC-P3-CAND-007 evaluator 只解释同一 Container 内 bounded accepted Observation evidence；
/// exploration outcome 不是 viewport identity、local completion 或 Goal completion truth。
/// SC-P3-CAND-008 evaluator 只从 bounded accepted Observation evidence 与 Agent-derived semantic depth
/// 产出 complete required-branch inventory evidence；它不授权、选择、dispatch 或完成 branch/Goal。
/// 不创建 GoalGraph / GoalEngine / GoalEvidenceSpec 层级（裁决 3 / I-12）。
/// </summary>
/// <param name="EvidenceEvaluator">证据评估器：对 Observation 评估产生 GoalEvidence（调用侧注入）。</param>
/// <param name="CandidateAuthorizationEvaluator">SC-P3-CAND-006 optional bounded read-only candidate criterion。
/// true/false/null 分别表示 authorized/rejected/unresolved；缺席保持 fixed-Plan behavior。</param>
/// <param name="ViewportExplorationEvaluator">SC-P3-CAND-007 optional bounded same-Container exploration criterion。
/// true/false/null 分别表示 continue/exhausted/unresolved；缺席保持 fixed-Plan behavior。</param>
/// <param name="BranchInventoryEvaluator">SC-P3-CAND-008 optional bounded required-branch inventory criterion。
/// non-null/empty/null map 分别表示 complete inventory/positive leaf/unresolved；缺席保持 fixed-Plan behavior。</param>
public sealed record Goal(
    Func<Observation, GoalEvidence> EvidenceEvaluator,
    Func<Observation, ObservedElement, CandidateAuthorizationEvidence>? CandidateAuthorizationEvaluator = null,
    Func<ImmutableArray<Observation>, ViewportExplorationEvidence>? ViewportExplorationEvaluator = null,
    Func<ImmutableArray<Observation>, int, BranchInventoryEvidence>? BranchInventoryEvaluator = null);
