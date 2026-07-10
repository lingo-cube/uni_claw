using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 预期元素交互覆盖率 (D-E4: element_coverage 维度)。
/// 对照 TraversalResult.ActionHistory 计算覆盖率比值。
/// </summary>
/// <param name="Required">必须交互的元素 ID 列表 (支持 "auto_derive" sentinel)</param>
/// <param name="RequiredRatio">覆盖率阈值 (默认 0.95)</param>
public sealed record class ElementCoverageExpectation(
    ImmutableArray<string> Required,
    double RequiredRatio = 0.95);
