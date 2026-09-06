using UniClaw.Kernel.Run;

namespace UniClaw.Kernel.Outcome;

/// <summary>
/// Runtime Outcome — Primary Run 的 immutable terminal 结论 envelope
/// （Target §3.8 / §7，不变量 39）。Uni Kernel 唯一产出：terminal Outcome
/// State 成立后 exactly once emission。携带 Run identity、terminal
/// classification、fulfilled / unfulfilled obligations、Outcome Proof
/// reference、必要 Evidence references，以及 material effect / failure /
/// escalation references（如适用）。只从 canonical Outcome State 投影；
/// Kernel 不重判 completion、不解析 Evidence、不覆盖 Outcome State、
/// 不根据 Provider 自述修改 classification（任务 七）。
/// </summary>
public sealed record RuntimeOutcome(
    string RunId,
    string OutcomeProofId,
    TerminalClassification Classification,
    IReadOnlyList<ObligationStatus> Obligations,
    IReadOnlySet<string> BasisEvidenceIds,
    IReadOnlySet<string> EffectEvidenceIds,
    IReadOnlySet<string> SituationEvidenceIds,
    int UnresolvedUncertainty,
    string Reason);
