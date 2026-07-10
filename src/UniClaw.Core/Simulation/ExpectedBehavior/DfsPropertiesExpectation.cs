namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// DFS 遍历顺序属性验证 (D-E4: dfs_properties 维度)。
/// 对照 TraversalResult.VisitedPages 检查 DFS 顺序属性。
/// </summary>
/// <param name="RootFirst">根节点是否最先访问</param>
/// <param name="ParentBeforeChild">父节点是否在子节点之前访问</param>
/// <param name="BackAfterForward">forward 之后是否有对应的 back</param>
public sealed record class DfsPropertiesExpectation(
    bool RootFirst,
    bool ParentBeforeChild,
    bool BackAfterForward);
