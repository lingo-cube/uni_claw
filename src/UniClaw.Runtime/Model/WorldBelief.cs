namespace UniClaw.Runtime.Model;

/// <summary>
/// Agent 对现实世界的当前最佳判断（宪章 §10）：由 Observation + 语义推断生成，可被新的 Observation 修正。
/// 允许 Unknown / Uncertain / Conflicting（§10：SemanticPage 为 null 即 Unknown；证据不足时不得假装确定）。
/// 与 Runtime State 严格分离（§11）：belief 记录「认为现实是什么」，不记录「程序内部执行状态」。
/// 不复制场景特定语义字段（如 WiFi 开关状态）——Goal 完成判定直接基于 Observation evidence（裁决 2）。
/// </summary>
/// <param name="SemanticPage">当前最佳判断的语义页面名；null = Unknown（§10）。</param>
/// <param name="Confidence">置信度，范围 [0, 1]。</param>
/// <param name="Evidence">支撑该判断的证据描述（可读）。</param>
/// <param name="SourceObservationSequence">本 belief 依据的观测序号（对支撑观测序列的引用 — 裁决 2）。</param>
public sealed record WorldBelief(
    string? SemanticPage,
    float Confidence,
    string? Evidence,
    long? SourceObservationSequence);
