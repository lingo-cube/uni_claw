using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Kernel.Evidence;

/// <summary>
/// 谁产生、何时捕获、声明何 scope、经历了何种转换（Target §11.2 Provenance）。
/// 不可变；admission 完整性检查逐字段验证。
/// PER-013 Slice C：可选第 5 位 <paramref name="Hierarchy"/> 携带 hierarchy
/// capture 结构 metadata（typed descriptor，替代 lineage 字符串协议）；legacy
/// path 恒 null，EvidenceId 字节不变。Ledger 对其通用透传，不做新 admission
/// 判定（P2 authority 不变）。
/// </summary>
public sealed record Provenance(
    string Producer,
    DateTimeOffset CaptureTime,
    string Scope,
    IReadOnlyList<string> TransformationLineage,
    HierarchyCaptureDescriptor? Hierarchy = null);
