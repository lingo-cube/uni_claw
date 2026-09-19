using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UniClaw.Kernel.World;

namespace UniClaw.Simulation.Tests;

internal enum WorldCoverageKind
{
    ContentOnly,
}

internal sealed record NormalizedRegion(double X1, double Y1, double X2, double Y2)
{
    public bool Contains(NormalizedRegion candidate) =>
        candidate.X1 >= X1 && candidate.Y1 >= Y1
        && candidate.X2 <= X2 && candidate.Y2 <= Y2;
}

/// <summary>Human-reviewed expectation；只属于 test Oracle，不是 Evidence。</summary>
internal sealed record HumanGroundTruthElement(
    string GroundTruthId,
    string Role,
    string SemanticDescriptor,
    NormalizedRegion Bounds);

internal sealed record HumanGroundTruthSet(
    string CaptureId,
    string SourceRevisionId,
    string SpatialFrameId,
    IReadOnlyList<HumanGroundTruthElement> Elements);

internal sealed record ReconstructionAssetRef(
    string Kind,
    string RelativePath,
    string Sha256,
    string AuthorityRole,
    string CaptureId,
    string SourceRevisionId,
    string SpatialFrameId);

internal sealed record ReconstructionAssetInput(
    ReconstructionAssetRef Reference,
    byte[] Content);

/// <summary>
/// 请求只容纳一个 Slice，因此构造上不能把多个 WorldBelief revision 融成
/// current truth。
/// </summary>
internal sealed record WorldReconstructionRequest(
    string CaptureId,
    Slice SourceSlice,
    string SpatialFrameId,
    NormalizedRegion ContentRegion,
    HumanGroundTruthSet HumanGroundTruth,
    IReadOnlyList<ReconstructionAssetInput> Assets);

internal sealed record WorldCoverageManifest(
    WorldCoverageKind Kind,
    string SourceRevisionId,
    string RootContainerId,
    string SpatialFrameId,
    NormalizedRegion Region,
    int PresentedElements,
    IReadOnlyList<string> ExcludedRegions);

internal sealed record WorldPresentationElement(
    string OccurrenceId,
    string OwningContainerId,
    string Role,
    string SemanticDescriptor,
    string? State,
    SpatialLocator? Locator);

internal sealed record WorldPresentation(
    string SourceRevisionId,
    string RootContainerId,
    bool IsAuthoritative,
    IReadOnlyList<WorldPresentationElement> Elements);

internal sealed record ReconstructionLineage(
    string SourceRevisionId,
    string RootContainerId,
    IReadOnlyList<ReconstructionAssetRef> Assets);

internal enum WorldDivergenceKind
{
    Missing,
    Duplicate,
    WrongParent,
    FrameMismatch,
    OutOfCoverage,
}

internal sealed record WorldDivergence(
    WorldDivergenceKind Kind,
    int Position,
    string Key,
    string Expected,
    string Actual);

internal sealed record WorldOracleComparison(
    int IncludedGroundTruthElements,
    int ExcludedGroundTruthElements,
    WorldDivergence? FirstDivergence);

internal sealed record WorldReconstructionResult(
    WorldPresentation Presentation,
    WorldCoverageManifest Coverage,
    string SemanticDigest,
    ReconstructionLineage Lineage,
    WorldOracleComparison OracleComparison);

/// <summary>
/// WRC-001 concrete test module。它只把 owner 已表达的 Slice 做非权威呈现和
/// semantic comparison；不建立第二份 WorldBelief，也不写任何产品状态。
/// </summary>
internal sealed class WorldReconstruction
{
    public WorldReconstructionResult Reconstruct(WorldReconstructionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceSlice.InScopeContainerIds.Count != 1
            || request.SourceSlice.InScopeContainerIds[0] != request.SourceSlice.RootContainerId)
            throw new ArgumentException("ContentOnly tracer requires exactly one root container scope", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SourceSlice.SourceRevisionId))
            throw new ArgumentException("Source revision is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SpatialFrameId))
            throw new ArgumentException("Spatial frame is required", nameof(request));
        ValidateCorrelation(request);

        var expected = request.HumanGroundTruth.Elements
            .Where(item => request.ContentRegion.Contains(item.Bounds))
            .ToArray();
        var presentationElements = request.SourceSlice.Occurrences
            .Where(occurrence => IsWithinContentCoverage(occurrence, request))
            .Select(ToPresentationElement)
            .OrderBy(element => element.Locator!.Y1)
            .ThenBy(element => element.Locator!.X1)
            .ThenBy(element => element.Role, StringComparer.Ordinal)
            .ThenBy(element => element.SemanticDescriptor, StringComparer.Ordinal)
            .ThenBy(element => element.OccurrenceId, StringComparer.Ordinal)
            .ToArray();
        var coverage = new WorldCoverageManifest(
            WorldCoverageKind.ContentOnly,
            request.SourceSlice.SourceRevisionId,
            request.SourceSlice.RootContainerId,
            request.SpatialFrameId,
            request.ContentRegion,
            presentationElements.Length,
            new[] { "system-bars", "app-bar-and-search" });
        var presentation = new WorldPresentation(
            request.SourceSlice.SourceRevisionId,
            request.SourceSlice.RootContainerId,
            IsAuthoritative: false,
            presentationElements);
        var lineage = new ReconstructionLineage(
            request.SourceSlice.SourceRevisionId,
            request.SourceSlice.RootContainerId,
            request.Assets.Select(asset => asset.Reference).ToArray());
        var divergence = FindFirstDivergence(request, expected);
        var comparison = new WorldOracleComparison(
            expected.Length,
            request.HumanGroundTruth.Elements.Count - expected.Length,
            divergence);
        var digest = ComputeDigest(presentation, coverage, lineage);
        return new WorldReconstructionResult(presentation, coverage, digest, lineage, comparison);
    }

    private static void ValidateCorrelation(WorldReconstructionRequest request)
    {
        var groundTruth = request.HumanGroundTruth;
        if (groundTruth.CaptureId != request.CaptureId
            || groundTruth.SourceRevisionId != request.SourceSlice.SourceRevisionId
            || groundTruth.SpatialFrameId != request.SpatialFrameId)
            throw new ArgumentException(
                "Human GT capture/revision/frame correlation does not match the reconstruction request",
                nameof(request));

        foreach (var input in request.Assets)
        {
            var asset = input.Reference;
            if (asset.CaptureId != request.CaptureId
                || asset.SourceRevisionId != request.SourceSlice.SourceRevisionId
                || asset.SpatialFrameId != request.SpatialFrameId)
                throw new ArgumentException(
                    $"Asset correlation mismatch: {asset.Kind}",
                    nameof(request));
            var actualHash = Convert.ToHexString(SHA256.HashData(input.Content)).ToLowerInvariant();
            if (actualHash != asset.Sha256)
                throw new ArgumentException(
                    $"Asset integrity mismatch: {asset.Kind}",
                    nameof(request));
        }
    }

    private static bool IsWithinContentCoverage(
        OccurrenceFact occurrence,
        WorldReconstructionRequest request)
    {
        var locator = occurrence.Locator;
        return locator is not null
            && locator.SpatialFrameId == request.SpatialFrameId
            && request.ContentRegion.Contains(new NormalizedRegion(
                locator.X1, locator.Y1, locator.X2, locator.Y2));
    }

    private static WorldPresentationElement ToPresentationElement(OccurrenceFact occurrence)
        => new(
            occurrence.OccurrenceId,
            occurrence.OwningContainerId ?? "<missing-parent>",
            occurrence.Role,
            occurrence.SemanticDescriptor ?? "<missing-descriptor>",
            occurrence.State,
            occurrence.Locator);

    private static WorldDivergence? FindFirstDivergence(
        WorldReconstructionRequest request,
        IReadOnlyList<HumanGroundTruthElement> expected)
    {
        var occurrences = request.SourceSlice.Occurrences;
        for (var index = 0; index < occurrences.Count; index++)
        {
            var occurrence = occurrences[index];
            if (occurrence.OwningContainerId != request.SourceSlice.RootContainerId)
                return new WorldDivergence(
                    WorldDivergenceKind.WrongParent,
                    index,
                    occurrence.OccurrenceId,
                    request.SourceSlice.RootContainerId,
                    occurrence.OwningContainerId ?? "missing");
        }

        for (var index = 0; index < occurrences.Count; index++)
        {
            var occurrence = occurrences[index];
            if (occurrence.Locator?.SpatialFrameId != request.SpatialFrameId)
                return new WorldDivergence(
                    WorldDivergenceKind.FrameMismatch,
                    index,
                    occurrence.OccurrenceId,
                    request.SpatialFrameId,
                    occurrence.Locator?.SpatialFrameId ?? "missing");
        }


        for (var index = 0; index < occurrences.Count; index++)
        {
            var locator = occurrences[index].Locator!;
            var bounds = new NormalizedRegion(locator.X1, locator.Y1, locator.X2, locator.Y2);
            if (!request.ContentRegion.Contains(bounds))
                return new WorldDivergence(
                    WorldDivergenceKind.OutOfCoverage,
                    index,
                    occurrences[index].OccurrenceId,
                    "ContentOnly",
                    $"{Number(locator.X1)},{Number(locator.Y1)},{Number(locator.X2)},{Number(locator.Y2)}");
        }

        var firstPositions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < occurrences.Count; index++)
        {
            var semanticKey = SemanticKey(occurrences[index].Role, occurrences[index].SemanticDescriptor);
            var identityKey = OccurrenceIdentityKey(occurrences[index]);
            if (!firstPositions.TryAdd(identityKey, index))
                return new WorldDivergence(
                    WorldDivergenceKind.Duplicate,
                    index,
                    semanticKey,
                    "unique",
                    "duplicate");
        }

        var actualKeys = occurrences
            .Select(item => SemanticKey(item.Role, item.SemanticDescriptor))
            .ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < expected.Count; index++)
        {
            var key = SemanticKey(expected[index].Role, expected[index].SemanticDescriptor);
            if (!actualKeys.Contains(key))
                return new WorldDivergence(
                    WorldDivergenceKind.Missing,
                    index,
                    key,
                    "present",
                    "missing");
        }

        return null;
    }

    private static string SemanticKey(string role, string? descriptor) =>
        $"{role}|{descriptor ?? "<missing-descriptor>"}";

    private static string OccurrenceIdentityKey(OccurrenceFact occurrence)
    {
        var locator = occurrence.Locator!;
        return string.Join('|',
            SemanticKey(occurrence.Role, occurrence.SemanticDescriptor),
            occurrence.OwningContainerId,
            locator.SpatialFrameId,
            Number(locator.X1),
            Number(locator.Y1),
            Number(locator.X2),
            Number(locator.Y2));
    }

    private static string ComputeDigest(
        WorldPresentation presentation,
        WorldCoverageManifest coverage,
        ReconstructionLineage lineage)
    {
        var canonical = new StringBuilder();
        Add(canonical, presentation.SourceRevisionId);
        Add(canonical, presentation.RootContainerId);
        Add(canonical, presentation.IsAuthoritative ? "authoritative" : "non-authoritative");
        Add(canonical, coverage.Kind.ToString());
        Add(canonical, coverage.SpatialFrameId);
        Add(canonical, Number(coverage.Region.X1));
        Add(canonical, Number(coverage.Region.Y1));
        Add(canonical, Number(coverage.Region.X2));
        Add(canonical, Number(coverage.Region.Y2));
        Add(canonical, coverage.PresentedElements.ToString(CultureInfo.InvariantCulture));
        foreach (var excludedRegion in coverage.ExcludedRegions.Order(StringComparer.Ordinal))
            Add(canonical, excludedRegion);

        foreach (var element in presentation.Elements)
        {
            Add(canonical, element.OccurrenceId);
            Add(canonical, element.OwningContainerId);
            Add(canonical, element.Role);
            Add(canonical, element.SemanticDescriptor);
            Add(canonical, element.State ?? "<unknown>");
            Add(canonical, element.Locator is null ? "<missing>" : Number(element.Locator.X1));
            Add(canonical, element.Locator is null ? "<missing>" : Number(element.Locator.Y1));
            Add(canonical, element.Locator is null ? "<missing>" : Number(element.Locator.X2));
            Add(canonical, element.Locator is null ? "<missing>" : Number(element.Locator.Y2));
            Add(canonical, element.Locator?.SpatialFrameId ?? "<missing>");
        }

        // Human GT 是 comparison oracle，不是 actual reconstruction 语义。其
        // metadata 仍留在 lineage，但绝不进入 presentation digest。
        foreach (var asset in lineage.Assets
            .Where(asset => asset.AuthorityRole != "test-oracle-only")
            .OrderBy(asset => asset.Kind, StringComparer.Ordinal)
            .ThenBy(asset => asset.Sha256, StringComparer.Ordinal))
        {
            Add(canonical, asset.Kind);
            Add(canonical, asset.Sha256);
            Add(canonical, asset.AuthorityRole);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Add(StringBuilder destination, string value) =>
        destination.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
