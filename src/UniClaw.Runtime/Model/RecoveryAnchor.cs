namespace UniClaw.Runtime.Model;

/// <summary>
/// Startup 建立的可信恢复入口（宪章 §20）：当 Agent 完全迷失时，至少可以恢复到这里重新建立可信世界。
/// 它不是 Traversal Root Node。EntryStrategy / RestoreRecipe 是恢复规划数据（裁决 8）——
/// 调用侧注入的字符串描述（Phase 2 恢复执行机制消费；Phase 1 Startup 不读取），
/// 可选字段（默认 null — 向后兼容既有 3 字段构造）。
/// 本模型仍不创建 recovery planning / execution / FSM。
/// </summary>
/// <param name="ApplicationIdentity">可信入口的应用标识。</param>
/// <param name="ExpectedSemanticEntry">入口期望到达的语义页面。</param>
/// <param name="VerificationCriteria">验证入口恢复成功的判据。</param>
/// <param name="RestoreRecipe">恢复动作描述（调用侧注入的字符串数据；null = 未提供）。</param>
/// <param name="EntryStrategy">入口策略描述（调用侧注入的字符串数据；null = 未提供）。</param>
public sealed record RecoveryAnchor(
    string ApplicationIdentity,
    string ExpectedSemanticEntry,
    string VerificationCriteria,
    string? RestoreRecipe = null,
    string? EntryStrategy = null);
