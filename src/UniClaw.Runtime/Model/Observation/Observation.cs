using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// 某一时刻从现实世界采集到的证据快照（宪章 §9）：Observation 是 World Belief 的输入，
/// 不是 Semantic Truth（I-4）——它可能不完整、有识别错误、有延迟。
/// 本阶段 Observation 不含 Fingerprint（裁决 2：Fingerprint 字段与机制 DEFER 到 Scroll Identity Scenario；
/// I-6 原则「Fingerprint 是 evidence，不是 identity」保留在宪章）。
/// </summary>
/// <param name="Elements">观测到的元素集合（不可变）。</param>
/// <param name="ForegroundApplication">前台应用标识；Startup 早期（尚未 Attach 完成）可能为 null。</param>
/// <param name="SequenceNumber">确定性、单调递增的观测序号（裁决 6：不依赖真实时间，不用 Timestamp 当序列号）。</param>
public sealed record Observation(
    ImmutableArray<ObservedElement> Elements,
    string? ForegroundApplication,
    long SequenceNumber);
