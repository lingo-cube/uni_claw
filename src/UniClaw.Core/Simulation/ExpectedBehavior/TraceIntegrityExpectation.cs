using System.Collections.Immutable;
using UniClaw.Core.Observability;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// Trace 完整性验证预期 (D-E4: trace_integrity 维度)。
/// 对照 TraversalResult.Trace 验证遍历过程中 trace 数据的完整性。
/// </summary>
/// <param name="RequiredSpanTypes">Trace 中必须出现的 SpanType 集合（空=跳过检查）</param>
/// <param name="MinPageTransitions">最少页面跳转记录数（0=跳过检查）</param>
public sealed record class TraceIntegrityExpectation(
    ImmutableArray<SpanType> RequiredSpanTypes = default,
    int MinPageTransitions = 0);
