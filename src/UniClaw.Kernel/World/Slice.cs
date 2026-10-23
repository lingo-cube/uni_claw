namespace UniClaw.Kernel.World;

/// <summary>A read scope in one observed frame, never an entity or delivery target.</summary>
public sealed record SliceRegion
{
    public string CaptureId { get; }
    public UniClaw.Kernel.Perception.CoordinateSpace Space { get; }
    public SpatialLocator Bounds { get; }

    public SliceRegion(string captureId, UniClaw.Kernel.Perception.CoordinateSpace space, SpatialLocator bounds)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(bounds);
        if (string.IsNullOrWhiteSpace(captureId) || space.CaptureId != captureId
            || bounds.X1 >= bounds.X2 || bounds.Y1 >= bounds.Y2 || !FrameMatches(bounds, space))
            throw new ArgumentException("region requires a positive area and an exact capture/frame source");
        CaptureId = captureId; Space = space; Bounds = bounds;
    }

    internal static bool FrameMatches(SpatialLocator locator, UniClaw.Kernel.Perception.CoordinateSpace space) =>
        locator.SpatialFrameId == space.CoordinateSpaceId
        || locator.SpatialFrameId == UniClaw.Kernel.Perception.CoordinateSpace.DeviceViewportPrefix
            && space.CoordinateSpaceId.StartsWith(locator.SpatialFrameId + ":", StringComparison.Ordinal);

    internal bool Contains(SpatialLocator b) => b.X1 < b.X2 && b.Y1 < b.Y2
        && b.X1 >= Bounds.X1 && b.Y1 >= Bounds.Y1 && b.X2 <= Bounds.X2 && b.Y2 <= Bounds.Y2;
}

/// <summary>
/// OccurrenceFact — Slice 携带的 occurrence 景观 fact（revision-local
/// ObservationOccurrence 的协议侧表示；无 EvidenceBasis 溯源——slice 是
/// scoped projection，溯源留在 Owner 侧）。State（CDS-001）：可选
/// presentation state（随 occurrence 替换；null = Unknown 非 false）——
/// Control 侧 desired-state satisfaction 判定输入（ADR-0017）。
/// </summary>
public sealed record OccurrenceFact(
    string OccurrenceId,
    string? OwningContainerId,
    string Role,
    string? SemanticDescriptor,
    string? State = null,
    SpatialLocator? Locator = null,
    NativeLocator? Native = null);

/// <summary>
/// Slice = scoped immutable projection of a WorldBelief revision（Target §12.2，
/// 不变量 20；UIW-004：scope 锚定 RootContainerIdentity）。只声明 source
/// revision 与 container scope；不回写 WorldBelief。有效性由 source revision
/// currency 派生判定（World Model 侧，ADR-0010）；freshness 充分性属消费侧
/// Freshness Judgment，不是 Slice 自身有效性的组成。不存在显式 invalidation
/// event。
/// </summary>
/// <param name="RootContainerId">scope 根 container（必须存在于 source
/// revision 的 Containers，fail-closed）。</param>
/// <param name="InScopeContainerIds">In-scope container 集合（默认 {root}；
/// 须 ⊆ source revision containers）。</param>
/// <param name="Occurrences">OwningContainerId ∈ InScope 的 occurrence 景观。</param>
/// <param name="ScopedClaims">分区兼容通道：claim subject 以任一 InScope
/// containerId + "." 为前缀的条目。claim subject 约定
/// &lt;containerId&gt;.&lt;rest&gt; 属 realization 约定（非协议词汇）。</param>
public sealed record Slice(
    string SourceRevisionId,
    string RootContainerId,
    FreshnessBasis FreshnessBasis,
    IReadOnlyList<string> InScopeContainerIds,
    IReadOnlyList<OccurrenceFact> Occurrences,
    IReadOnlyDictionary<string, string> ScopedClaims,
    SliceRegion? Region = null)
{
    public IReadOnlyList<WorldSemanticKnowledge> SemanticKnowledge { get; init; } =
        System.Collections.Immutable.ImmutableArray<WorldSemanticKnowledge>.Empty;
}
