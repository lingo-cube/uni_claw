using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// NodeId 碰撞分辨率验证 (D-E4: collision_proof 维度)。
/// 验证同 Text 在不同页面上的 NodeId 碰撞是否正确解决。
/// 对照 TraversalResult.VisitedPages 按 Text 分组统计 distinct count。
/// </summary>
/// <param name="Text">元素显示文本 (如 "ON")</param>
/// <param name="ExpectedDistinct">预期同 Text 的 distinct NodeId 数量</param>
/// <param name="ParentPages">限制检查的页面范围 (可选)</param>
public sealed record class CollisionProof(
    string Text,
    int ExpectedDistinct,
    ImmutableArray<string>? ParentPages = null);
