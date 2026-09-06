namespace UniClaw.Kernel.Run;

/// <summary>
/// Terminal Outcome State（Target §13.2）：记录 terminal classification、
/// fulfilled / unfulfilled obligations、Outcome Proof reference、unresolved
/// uncertainty 与 material effect references。只记录 Assurance judgment 的
/// 结果，不重复判断 Outcome Proof（不变量 38）。immutable 快照，纯数据，
/// 无判断逻辑。
/// </summary>
public sealed record OutcomeState(
    string OutcomeProofId,
    TerminalClassification Classification,
    IReadOnlyList<ObligationStatus> Obligations,
    IReadOnlySet<string> BasisEvidenceIds,
    IReadOnlySet<string> EffectEvidenceIds,
    IReadOnlySet<string> SituationEvidenceIds,
    int UnresolvedUncertainty,
    string Reason);

/// <summary>
/// Typed terminal transition 的结论（Run Model 唯一写入路径的结果）。
/// Accepted=false 表示 exact-prior 竞争失败或已 terminal（single-winner）。
/// </summary>
public sealed record OutcomeTransition(bool Accepted, string? Reason, RunState? TerminalState);
