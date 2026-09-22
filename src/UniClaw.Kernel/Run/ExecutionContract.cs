namespace UniClaw.Kernel.Run;

/// <summary>单项 contract admission 检查结果（Run Model 自己的检查留痕词汇）。</summary>
public sealed record ContractCheck(string Name, bool Passed);

/// <summary>
/// Contract admission 结论：输入是否可建立 canonical Run State（Target §13.2）。
/// 拒绝时零 Run State 副作用（验收 1）。
/// </summary>
public sealed record ContractAdmission(bool Accepted, IReadOnlyList<ContractCheck> Checks, string? RejectionReason);

/// <summary>
/// Execution Contract 候选（UniAgent 侧产物，Target §3.3；本切片由测试脚本
/// 构造，D4）。任一字段缺失或不完整 → admission fail-closed。
/// Obligations：可选的 run-level 证明义务规格（OUT-003，D2）；为 null 时
/// Run Model 从 ProofCriteria 派生不可判定的占位 obligation（C2E 既有
/// contract 形状零改动）。
/// </summary>
public sealed record ExecutionContract(
    string Version,
    string Objective,
    IReadOnlySet<string>? Scope,
    IReadOnlySet<string>? AllowedEffects,
    IReadOnlySet<string>? ForbiddenEffects,
    IReadOnlyList<string>? ProofCriteria,
    IReadOnlyList<RunObligation>? Obligations = null,
    int? MaxConsultations = null,
    int? MaxTotalSteps = null);
