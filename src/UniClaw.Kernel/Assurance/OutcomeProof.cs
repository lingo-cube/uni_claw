using UniClaw.Kernel.Run;

namespace UniClaw.Kernel.Assurance;

/// <summary>
/// Outcome Proof — Assurance 唯一产出的 terminal 判断（Target §15.2，
/// 不变量 22/37）。统一覆盖 Completion / Failure / SafeStop / Escalation：
/// 判断 Run/Contract-level Proof Obligation State 是否具备足够 accepted
/// Evidence 支持具体 terminal claim。不可变；写入 Outcome State 后冻结
/// 引用（§17）。obligation/classification 词汇属 Run Model 领域（Run
/// State 记录面），本类型不脱离 Assurance 判断 Authority。
/// </summary>
public sealed record OutcomeProof(
    string ProofId,
    TerminalClassification Classification,
    IReadOnlyList<ObligationStatus> Obligations,
    IReadOnlySet<string> BasisEvidenceIds,
    IReadOnlySet<string> EffectEvidenceIds,
    IReadOnlySet<string> SituationEvidenceIds,
    int UnresolvedUncertainty,
    string Reason);
