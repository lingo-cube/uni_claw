namespace UniClaw.Kernel.Run;

/// <summary>
/// PROTOCOL-FIX-003（ADR-0050 Q1:B）— 契约预算来源留痕：configured = 作者
/// 显式声明（初始额度承诺）；default = 缺省解析（null≡默认）。View 沿载，
/// admission 时确定性派生，不参与幂等签名比较（由两预算解析值决定）。
/// </summary>
public enum BudgetValueOrigin
{
    /// <summary>作者显式声明的初始额度。</summary>
    Configured,

    /// <summary>null → 缺省解析（16 / 256）。</summary>
    Default,

    /// <summary>兼容聚合读取：两个预算维度来源不同。</summary>
    Mixed,
}

/// <summary>
/// 已接受 contract version 的 immutable canonical view（Target §13.2）。
/// 对当前 Run immutable；同 version 重复 admit 复用同一实例，不产生新 View
/// （验收 9）。
/// </summary>
public sealed record ExecutionContractView(
    string Version,
    string Objective,
    IReadOnlySet<string> Scope,
    IReadOnlySet<string> AllowedEffects,
    IReadOnlySet<string> ForbiddenEffects,
    IReadOnlyList<string> ProofCriteria,
    int MaxConsultations = 16,
    int MaxTotalSteps = 256,
    BudgetValueOrigin BudgetValueOrigin = BudgetValueOrigin.Default,
    string? TaskInstanceId = null,
    string? ProductSessionId = null,
    string? ContractRef = null,
    string? AcceptedContractViewRef = null,
    int? ContractGeneration = null)
{
    /// <summary>
    /// maxConsultations 的逐维度来源。保留在 View 中，避免用聚合的
    /// BudgetValueOrigin 掩盖部分缺省合同。
    /// </summary>
    public BudgetValueOrigin MaxConsultationsOrigin { get; init; } = BudgetValueOrigin.Default;

    /// <summary>maxTotalSteps 的逐维度来源。</summary>
    public BudgetValueOrigin MaxTotalStepsOrigin { get; init; } = BudgetValueOrigin.Default;
}
