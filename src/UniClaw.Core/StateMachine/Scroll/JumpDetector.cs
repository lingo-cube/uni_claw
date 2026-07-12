using System.Collections.Immutable;
using UniClaw.Core.Simulation.Scroll;

namespace UniClaw.Core.StateMachine.Scroll;

/// <summary>
/// 步骤 5：跳跃检测
/// 通过比较滚动前后元素 ID 集合检测元素不连续性（跳跃）。
/// </summary>
public static class JumpDetector
{
    /// <summary>
    /// 检测跳跃
    /// </summary>
    /// <param name="beforeElementIds">滚动前元素 ID 集合</param>
    /// <param name="afterElementIds">滚动后元素 ID 集合</param>
    /// <returns>滚动验证结果</returns>
    public static ScrollVerifyResult Detect(
        ImmutableArray<string> beforeElementIds,
        ImmutableArray<string> afterElementIds)
    {
        return ScrollVerifyResult.Compute(beforeElementIds, afterElementIds);
    }

    /// <summary>
    /// 检测跳跃（完整版本，带可自定义的比较器）
    /// </summary>
    /// <param name="beforeElementIds">滚动前元素 ID 集合</param>
    /// <param name="afterElementIds">滚动后元素 ID 集合</param>
    /// <param name="stringComparer">字符串比较器（可选）</param>
    /// <returns>滚动验证结果</returns>
    public static ScrollVerifyResult DetectWithComparer(
        ImmutableArray<string> beforeElementIds,
        ImmutableArray<string> afterElementIds,
        IEqualityComparer<string>? stringComparer)
    {
        if (stringComparer == null)
            return Detect(beforeElementIds, afterElementIds);

        var beforeSet = beforeElementIds.ToHashSet(stringComparer);
        var afterSet = afterElementIds.ToHashSet(stringComparer);

        var status = ClassifyOverlap(beforeSet, afterSet);
        var overlapCount = beforeSet.Intersect(afterSet).Count();
        var newCount = afterSet.Except(beforeSet).Count();
        var duplicateCount = overlapCount;
        var duplicateRatio = afterSet.Count > 0 ? (double)duplicateCount / afterSet.Count : 0.0;

        return new ScrollVerifyResult(
            Status: status,
            BeforeElementIds: beforeElementIds,
            AfterElementIds: afterElementIds,
            OverlapCount: overlapCount,
            NewElementCount: newCount,
            DuplicateElementCount: duplicateCount,
            DuplicateRatio: duplicateRatio);
    }

    /// <summary>
    /// 检查是否检测到跳跃
    /// </summary>
    public static bool IsJumpDetected(ScrollVerifyResult result) =>
        result.Status == OverlapStatus.NoOverlap_BothHaveElements;

    /// <summary>
    /// 检查是否为安全初始状态（前为空）
    /// </summary>
    public static bool IsSafeInitialState(ScrollVerifyResult result) =>
        result.Status == OverlapStatus.NoOverlap_BeforeEmpty;

    /// <summary>
    /// 检查是否为可能末尾状态（后为空）
    /// </summary>
    public static bool IsPossibleEndOfList(ScrollVerifyResult result) =>
        result.Status == OverlapStatus.NoOverlap_AfterEmpty;

    /// <summary>
    /// 检查是否为空列表（前后都为空）
    /// </summary>
    public static bool IsEmptyList(ScrollVerifyResult result) =>
        result.Status == OverlapStatus.BothEmpty;

    /// <summary>
    /// 分类重叠状态
    /// </summary>
    private static OverlapStatus ClassifyOverlap(HashSet<string> before, HashSet<string> after)
    {
        var hasOverlap = before.Overlaps(after);

        if (hasOverlap)
            return OverlapStatus.HasOverlap;

        var beforeEmpty = before.Count == 0;
        var afterEmpty = after.Count == 0;

        if (beforeEmpty && afterEmpty)
            return OverlapStatus.BothEmpty;

        if (beforeEmpty)
            return OverlapStatus.NoOverlap_BeforeEmpty;

        if (afterEmpty)
            return OverlapStatus.NoOverlap_AfterEmpty;

        return OverlapStatus.NoOverlap_BothHaveElements;
    }
}
