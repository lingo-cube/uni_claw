namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动前后元素重叠状态分类。
/// 用于检测跳跃（jump）和正常滚动。
/// </summary>
public enum OverlapStatus
{
    /// <summary>有重叠：正常滚动，前后元素集合共享至少一个 ID</summary>
    HasOverlap,

    /// <summary>无重叠且前后都有元素：检测到跳跃（jump）</summary>
    NoOverlap_BothHaveElements,

    /// <summary>无重叠但前为空：安全的初始状态</summary>
    NoOverlap_BeforeEmpty,

    /// <summary>无重叠但后为空：可能到达列表末尾</summary>
    NoOverlap_AfterEmpty,

    /// <summary>前后都为空：空列表</summary>
    BothEmpty
}
