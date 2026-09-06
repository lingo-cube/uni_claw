namespace UniClaw.Kernel.Run;

/// <summary>
/// Objective State：Primary Run 对 contract objective 的执行侧状态
/// （Target §13.2）。不是 UniAgent 的 Goal Evaluation。
/// </summary>
public sealed record ObjectiveState(string Objective, string Status);

/// <summary>
/// Proof Obligation State：只记录 contract-level / run-level obligations
/// （Target §13.2）。本切片占位——记录存在，不实现 discharge（D3）；
/// action-local requirements 属 Assurance，永不进入此处（不变量 36）。
/// </summary>
public sealed record ProofObligationState(IReadOnlyList<string> Obligations);

/// <summary>
/// Progress State：execution obligations 的推进记录（Target §13.2）。
/// 计数是推进留痕，不得单独成为 progress truth。
/// </summary>
public sealed record ProgressState(int Cycles, int Acts);

/// <summary>
/// Canonical Run State 聚合：Contract View + Objective + Proof Obligations +
/// Progress（Target §13.2）。只经 typed legal transition 更新（§17）；
/// 不含 action-local assurance state（验收 6，§13 边界）。
/// </summary>
public sealed record RunState(
    ExecutionContractView ContractView,
    ObjectiveState Objective,
    ProofObligationState ProofObligations,
    ProgressState Progress);
