using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>
/// Observation → WorldBelief 的纯 reconciliation 函数（宪章 §10；run-lifecycle SHALL）。
/// 无状态、无决策 authority：语义页面解析规则由调用侧注入；belief 实例由 Agent 持有（B7 — I-2）。
/// 证据不足时不得假装确定（§10）：规则返回 null → SemanticPage=null（Unknown）、Confidence=0。
/// </summary>
public static class Reconcile
{
    /// <summary>
    /// 由观测生成 WorldBelief：携带 SemanticPage / Confidence / Evidence / SourceObservationSequence
    /// （对支撑观测序列的引用 — 裁决 2）。不复制场景特定语义字段（裁决 2 — 由 Model 契约保证）。
    /// </summary>
    /// <param name="observation">观测证据（I-4：evidence，不是 semantic truth）。</param>
    /// <param name="resolveSemanticPage">注入的语义解析规则：Observation → 语义页面名；返回 null = Unknown（§10）。</param>
    /// <returns>生成的 WorldBelief。</returns>
    /// <exception cref="ArgumentNullException">observation 或 resolveSemanticPage 为 null。</exception>
    public static WorldBelief FromObservation(Observation observation, Func<Observation, string?> resolveSemanticPage)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(resolveSemanticPage);

        var semanticPage = resolveSemanticPage(observation);
        return semanticPage is null
            ? new WorldBelief(
                null,
                0f,
                $"语义页面 Unknown：观测（seq={observation.SequenceNumber}）无匹配的语义解析规则（§10 证据不足不得假装确定）。",
                observation.SequenceNumber)
            : new WorldBelief(
                semanticPage,
                1f,
                $"语义页面解析为「{semanticPage}」（观测 seq={observation.SequenceNumber}）。",
                observation.SequenceNumber);
    }
}
