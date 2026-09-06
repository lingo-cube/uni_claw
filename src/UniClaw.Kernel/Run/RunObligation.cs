namespace UniClaw.Kernel.Run;

/// <summary>
/// Run-level Proof Obligation 种类（Target §13.2 六类证明义务，不变量 36）。
/// action-local requirements（freshness / admissibility / precondition /
/// grounding validity）属 Assurance 短生命周期 judgment，永不进入此处。
/// </summary>
public enum RunObligationKind
{
    /// <summary>objective 被判明的证明义务。</summary>
    Objective,

    /// <summary>material effect（外部环境发生预期改变）的证明义务。</summary>
    MaterialEffect,

    /// <summary>显式 completion 判据的证明义务。</summary>
    Completion,

    /// <summary>失败终局的证明义务（evidence-backed failure signal）。</summary>
    Failure,

    /// <summary>safe-stop / blocked 终局的证明义务。</summary>
    SafeStop,

    /// <summary>escalation 终局的证明义务。</summary>
    Escalation,
}

/// <summary>
/// Run-level Proof Obligation：contract author 声明的 contract/run-level 证明
/// 要求（Target §13.2；本片由测试脚本构造）。Subject + RequiredValue 构成对
/// accepted Evidence 支持的 claim 的期望；满足判定由 Assurance 执行（D4），
/// Run Model 只记录。
/// </summary>
public sealed record RunObligation(
    string ObligationId,
    RunObligationKind Kind,
    string Subject,
    string RequiredValue,
    bool Mandatory);

/// <summary>
/// 单条 obligation 的满足状态（Assurance 判定产出；Run Model 只记录）。
/// BackingEvidenceId = 支撑该 claim 的 accepted Evidence Record 引用。
/// </summary>
public sealed record ObligationStatus(
    string ObligationId,
    RunObligationKind Kind,
    bool Mandatory,
    bool Satisfied,
    string? BackingEvidenceId);

/// <summary>
/// Terminal classification（Target §13.2「terminal classification」词汇）。
/// 四语义分类：Completion / Failure / SafeStop / Escalation（任务 四；
/// baseline §15.2 的 Safe-Stop / Escalation 展开为两个枚举值为实现选择，
/// enum 名属 L4 开放项 §22——未锁死，但四类语义必须可区分）。
/// 「未知 / 证据不足」不是分类成员：证据不足由 proof absence 表达，
/// 不得伪装成成功或失败（任务 九.E）。
/// </summary>
public enum TerminalClassification
{
    /// <summary>全部 mandatory run obligations 已被足够 accepted Evidence 支撑。</summary>
    Completion,

    /// <summary>失败 evidence 充分；terminal non-success。</summary>
    Failure,

    /// <summary>无法安全继续（blocked / safe-stop）；terminal non-success。</summary>
    SafeStop,

    /// <summary>需升级（escalation）；terminal non-success。</summary>
    Escalation,
}
