using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 预期页面访问覆盖率 (D-E4: page_coverage 维度)。
/// 对照 TraversalResult.VisitedPages 进行 Contains 语义匹配。
/// </summary>
/// <param name="Required">必须访问的页面名列表 (支持 "auto_derive" sentinel)</param>
/// <param name="Forbidden">禁止访问的页面名列表</param>
public sealed record class PageCoverageExpectation(
    ImmutableArray<string> Required,
    ImmutableArray<string> Forbidden);
