namespace UniClaw.Kernel.Evidence;

/// <summary>
/// 谁产生、何时捕获、声明何 scope、经历了何种转换（Target §11.2 Provenance）。
/// 不可变；admission 完整性检查逐字段验证。
/// </summary>
public sealed record Provenance(
    string Producer,
    DateTimeOffset CaptureTime,
    string Scope,
    IReadOnlyList<string> TransformationLineage);
