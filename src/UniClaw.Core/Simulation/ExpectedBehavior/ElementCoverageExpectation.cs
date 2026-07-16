using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 预期元素交互覆盖率 (D-E4: element_coverage 维度)。
/// 对照 TraversalResult.ActionHistory 校验「怎么证明做了」。
///
/// C-11 constitution schema 变更 (simulation-test-quality-hardening):
/// - 旧 <c>RequiredRatio</c> (ratio 阈值) 是 masking 根因 —— 一个完全不滚动的引擎仍能通过。
/// - 改为 <see cref="Mode"/> 驱动的精确 set-diff (Exact/Subset) 或过渡 LegacyRatio。
/// - <see cref="AllowedMisses"/> 是 exact 模式下「显式、可审计的豁免」(每项带 Reason), 与 ratio 的隐式放宽语义对立。
/// </summary>
/// <param name="Required">必须交互的元素 ID 列表 (支持 "auto_derive" sentinel; 由 WithDerivation 派生为 fixture chrome ∪ scroll 全集)</param>
/// <param name="Mode">覆盖严格度 (Exact=精确集合差 / Subset=过游走 guard / LegacyRatio=过渡 ratio)。缺省 LegacyRatio 以兼容未迁移 JSON</param>
/// <param name="AllowedMisses">exact 模式显式豁免 (每项 Id+Reason); pass iff missed ⊆ AllowedMisses.Ids</param>
/// <param name="TargetName">subset 模式目标元素名 (来自 CompletionPolicy.TargetName); subset guard 据此定位 target tap 位置</param>
/// <param name="RequiredRatio">[过渡] legacy_ratio 模式阈值; 全量迁移后删除 (task 8.1)</param>
public sealed record class ElementCoverageExpectation(
    ImmutableArray<string> Required,
    ElementCoverageMode Mode = ElementCoverageMode.LegacyRatio,
    ImmutableArray<ElementMiss> AllowedMisses = default,
    string? TargetName = null,
    double RequiredRatio = 0.95);
