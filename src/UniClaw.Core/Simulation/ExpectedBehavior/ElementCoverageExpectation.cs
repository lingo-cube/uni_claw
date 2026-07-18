using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 预期元素交互覆盖率 (D-E4: element_coverage 维度)。
/// 对照 TraversalResult.ActionHistory 校验「怎么证明做了」。
///
/// C-11 constitution schema 变更 (simulation-test-quality-hardening + elementcoverage-mode-cleanup):
/// - 旧 <c>RequiredRatio</c> (ratio 阈值) 是 masking 根因 —— 一个完全不滚动的引擎仍能通过; 已移除。
/// - <see cref="Mode"/> 驱动精确 set-diff (Exact/Subset); 缺省 Exact (JSON 缺省 mode 时回落, 非 ratio)。
/// - <see cref="AllowedMisses"/> 是 exact 模式下「显式、可审计的豁免」(每项带 Reason), 与 ratio 的隐式放宽语义对立。
/// </summary>
/// <param name="Required">必须交互的元素 ID 列表 (支持 "auto_derive" sentinel; 由 WithDerivation 派生为 fixture chrome ∪ scroll 全集)</param>
/// <param name="Mode">覆盖严格度 (Exact=精确集合差 / Subset=过游走 guard); 缺省 Exact</param>
/// <param name="AllowedMisses">exact 模式显式豁免 (每项 Id+Reason); pass iff missed ⊆ AllowedMisses.Ids</param>
/// <param name="TargetName">subset 模式目标元素名 (来自 CompletionPolicy.TargetName, 由 WithDerivation 捕获); subset guard 据此定位 target tap 位置</param>
public sealed record class ElementCoverageExpectation(
    ImmutableArray<string> Required,
    ElementCoverageMode Mode = ElementCoverageMode.Exact,
    ImmutableArray<ElementMiss> AllowedMisses = default,
    string? TargetName = null);

