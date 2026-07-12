using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动验证结果：捕获前后元素集合的重叠状态和统计信息。
/// </summary>
public sealed record class ScrollVerifyResult
{
    /// <summary>重叠状态</summary>
    public OverlapStatus Status { get; init; }

    /// <summary>滚动前元素 ID 集合</summary>
    public ImmutableArray<string> BeforeElementIds { get; init; }

    /// <summary>滚动后元素 ID 集合</summary>
    public ImmutableArray<string> AfterElementIds { get; init; }

    /// <summary>重叠元素数量（ID 相同）</summary>
    public int OverlapCount { get; init; }

    /// <summary>新增元素数量（仅在后集合中）</summary>
    public int NewElementCount { get; init; }

    /// <summary>重复元素数量（同时在前后集合中）</summary>
    public int DuplicateElementCount { get; init; }

    /// <summary>重复元素比例（重复元素数 / 后集合元素数）</summary>
    public double DuplicateRatio { get; init; }

    /// <param name="Status">重叠状态</param>
    /// <param name="BeforeElementIds">滚动前元素 ID 集合</param>
    /// <param name="AfterElementIds">滚动后元素 ID 集合</param>
    /// <param name="OverlapCount">重叠元素数量</param>
    /// <param name="NewElementCount">新增元素数量</param>
    /// <param name="DuplicateElementCount">重复元素数量</param>
    /// <param name="DuplicateRatio">重复元素比例</param>
    public ScrollVerifyResult(
        OverlapStatus Status,
        ImmutableArray<string> BeforeElementIds,
        ImmutableArray<string> AfterElementIds,
        int OverlapCount = 0,
        int NewElementCount = 0,
        int DuplicateElementCount = 0,
        double DuplicateRatio = 0.0)
    {
        this.Status = Status;
        this.BeforeElementIds = BeforeElementIds;
        this.AfterElementIds = AfterElementIds;
        this.OverlapCount = OverlapCount;
        this.NewElementCount = NewElementCount;
        this.DuplicateElementCount = DuplicateElementCount;
        this.DuplicateRatio = DuplicateRatio;
    }

    /// <summary>是否检测到跳跃（无重叠且前后都有元素）</summary>
    public bool IsJumpDetected => Status == OverlapStatus.NoOverlap_BothHaveElements;

    /// <summary>是否为正常滚动（有重叠）</summary>
    public bool IsNormalScroll => Status == OverlapStatus.HasOverlap;

    /// <summary>从元素 ID 集合计算验证结果</summary>
    public static ScrollVerifyResult Compute(
        ImmutableArray<string> beforeIds,
        ImmutableArray<string> afterIds)
    {
        var beforeSet = beforeIds.ToHashSet();
        var afterSet = afterIds.ToHashSet();

        // 分类重叠状态
        var status = ClassifyOverlap(beforeSet, afterSet);

        // 计算统计
        var overlapCount = beforeSet.Intersect(afterSet).Count();
        var newCount = afterSet.Except(beforeSet).Count();
        var duplicateCount = overlapCount;

        // 计算重复比例（重复元素数 / 后集合元素数）
        var duplicateRatio = afterSet.Count > 0 ? (double)duplicateCount / afterSet.Count : 0.0;

        return new ScrollVerifyResult(
            Status: status,
            BeforeElementIds: beforeIds,
            AfterElementIds: afterIds,
            OverlapCount: overlapCount,
            NewElementCount: newCount,
            DuplicateElementCount: duplicateCount,
            DuplicateRatio: duplicateRatio);
    }

    private static OverlapStatus ClassifyOverlap(HashSet<string> before, HashSet<string> after)
    {
        var hasOverlap = before.Overlaps(after);

        if (hasOverlap)
            return OverlapStatus.HasOverlap;

        // 无重叠，检查边界情况
        var beforeEmpty = before.Count == 0;
        var afterEmpty = after.Count == 0;

        if (beforeEmpty && afterEmpty)
            return OverlapStatus.BothEmpty;

        if (beforeEmpty)
            return OverlapStatus.NoOverlap_BeforeEmpty;

        if (afterEmpty)
            return OverlapStatus.NoOverlap_AfterEmpty;

        // 都有元素但无重叠 = 跳跃
        return OverlapStatus.NoOverlap_BothHaveElements;
    }
}
