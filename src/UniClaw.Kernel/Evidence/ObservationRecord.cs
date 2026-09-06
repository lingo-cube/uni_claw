namespace UniClaw.Kernel.Evidence;

/// <summary>
/// 观察声明：某 producer 断言某 subject 具有某 value。
/// 这是 admission 的输入候选，不是 canonical Evidence Record。
/// </summary>
public sealed record ObservationClaim(string Subject, string Value);

/// <summary>
/// 一次观察输入（scripted Provider 在测试中直接构造）。
/// Provenance 为 null 或字段不完整 → admission fail-closed。
/// </summary>
public sealed record ObservationRecord(ObservationClaim Claim, Provenance? Provenance);
