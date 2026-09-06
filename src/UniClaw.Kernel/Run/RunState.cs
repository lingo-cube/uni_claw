namespace UniClaw.Kernel.Run;

/// <summary>
/// Objective State：Primary Run 对 contract objective 的执行侧状态
/// （Target §13.2）。不是 UniAgent 的 Goal Evaluation。
/// </summary>
public sealed record ObjectiveState(string Objective, string Status);

/// <summary>
/// Proof Obligation State：只记录 contract-level / run-level obligations
/// （Target §13.2，不变量 36）。OUT-003 落地证明义务语义：每条 obligation
/// 为 contract author 声明的证明要求（objective / material effect /
/// completion / failure / safe-stop / escalation）；满足判定由 Assurance
/// 执行（D4），Run Model 只记录。action-local requirements 属 Assurance，
/// 永不进入此处。
/// </summary>
public sealed record ProofObligationState(IReadOnlyList<RunObligation> Obligations);

/// <summary>
/// Progress State：execution obligations 的推进记录（Target §13.2）。
/// 计数是推进留痕，不得单独成为 progress truth。
/// </summary>
public sealed record ProgressState(int Cycles, int Acts);

/// <summary>
/// Canonical Run State 聚合：Contract View + Objective + Proof Obligations +
/// Progress（Target §13.2）。只经 typed legal transition 更新（§17）；
/// 不含 action-local assurance state（§13 边界）。Outcome 非 null = terminal
/// （Terminal Outcome State 已记录，不可恢复 active）。
/// </summary>
public sealed record RunState(
    ExecutionContractView ContractView,
    ObjectiveState Objective,
    ProofObligationState ProofObligations,
    ProgressState Progress,
    OutcomeState? Outcome = null);
