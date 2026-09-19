namespace UniClaw.Kernel.World.UiRealization;

/// <summary>
/// container 维度匹配语义的显式参数化（RVR-002 F1）：两种合法模式的
/// 语义差异在此显式化，取代各消费点散落的内联谓词。
/// </summary>
internal enum ContainerMatchMode
{
    /// <summary>spec-anchored 相等匹配（ResolveCurrent / DeriveEntityObligation
    /// FactKind 的 WorldModel owner-side 语义）：target 未给 container →
    /// 任意 occurrence；给出 → occurrence container 相等才匹配。</summary>
    TargetEquality,

    /// <summary>scope membership（DescriptorTargetPolicy 的 Slice traversal
    /// 语义）：occurrence container 为 null → 通过；非 null 须 ∈ scope 集合
    ///（scope 缺省 = 空 scope，非 null 一律不通过——fail-closed）。</summary>
    ScopeMembership,
}

/// <summary>
/// TargetDescriptor → occurrence 的机械确定性匹配唯一实现（RVR-002 F1：
/// 三处消费点 ResolveCurrent / DeriveEntityObligationFactKind /
/// DescriptorTargetPolicy 共用，消除谓词副本漂移；GroundingSeam 的
/// 「Role 相等 ∧ descriptor 相等(若给)」语义在此单点持有）。纯函数、
/// 零状态；不做任何 container 存在性校验（那是 DeriveSlice 的职责）。
/// </summary>
internal static class OccurrenceDescriptorMatcher
{
    /// <summary>Role 相等 ∧ SemanticDescriptor 相等(若给) ∧ container 按
    /// <paramref name="containerMode"/> 判定。</summary>
    public static bool Matches(
        string occurrenceRole,
        string? occurrenceSemanticDescriptor,
        string? occurrenceOwningContainerId,
        string targetRole,
        string? targetSemanticDescriptor,
        string? targetOwningContainerId,
        ContainerMatchMode containerMode,
        IReadOnlySet<string>? scopeContainerIds = null)
    {
        if (occurrenceRole != targetRole)
            return false;
        if (targetSemanticDescriptor is not null && occurrenceSemanticDescriptor != targetSemanticDescriptor)
            return false;
        return containerMode == ContainerMatchMode.TargetEquality
            ? targetOwningContainerId is null || occurrenceOwningContainerId == targetOwningContainerId
            : occurrenceOwningContainerId is null
                || (scopeContainerIds is not null && scopeContainerIds.Contains(occurrenceOwningContainerId));
    }
}
