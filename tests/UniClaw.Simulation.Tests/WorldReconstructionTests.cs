using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// WRC-001：单 revision / ContentOnly 的 test-only semantic reconstruction
/// Oracle。Human-reviewed GT 只参与比较，不写回 Product authority。
/// </summary>
public sealed class WorldReconstructionTests
{
    private readonly WorldReconstruction _reconstruction = new();

    [Fact]
    public void CompatibleContentOnlyAssets_ReconstructDeterministically()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();

        var first = _reconstruction.Reconstruct(fixture.Request);
        var second = _reconstruction.Reconstruct(fixture.Request);

        Assert.Null(first.OracleComparison.FirstDivergence);
        Assert.False(first.Presentation.IsAuthoritative);
        Assert.Equal(7, first.Presentation.Elements.Count);
        Assert.Equal(WorldCoverageKind.ContentOnly, first.Coverage.Kind);
        Assert.Equal(fixture.Request.SourceSlice.SourceRevisionId, first.Coverage.SourceRevisionId);
        Assert.Equal(fixture.Request.SourceSlice.SourceRevisionId, first.Presentation.SourceRevisionId);
        Assert.Equal(fixture.Request.SourceSlice.SourceRevisionId, first.Lineage.SourceRevisionId);
        Assert.Equal(fixture.Request.SpatialFrameId, first.Coverage.SpatialFrameId);
        Assert.Equal(2, first.OracleComparison.ExcludedGroundTruthElements);
        Assert.Equal(7, first.Coverage.PresentedElements);
        Assert.DoesNotContain(first.Presentation.Elements,
            element => element.SemanticDescriptor is "Settings" or "Search settings");
        Assert.Equal(new[] { "screenshot", "uia-xml", "response-json", "human-gt" },
            first.Lineage.Assets.Select(asset => asset.Kind));
        Assert.All(first.Lineage.Assets, asset => Assert.Matches("^[0-9a-f]{64}$", asset.Sha256));
        Assert.Equal("test-oracle-only", first.Lineage.Assets.Single(asset => asset.Kind == "human-gt").AuthorityRole);
        Assert.Equal(
            "7083130907b024d75b8d7d204dc3255f230c6ba41420e641e00dfc83b34c81d5",
            first.SemanticDigest);
        Assert.Equal(first.SemanticDigest, second.SemanticDigest);
        Assert.Equal(first.OracleComparison, second.OracleComparison);
    }

    [Fact]
    public void MissingOccurrence_ReportsStableFirstDivergence()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var occurrences = fixture.Request.SourceSlice.Occurrences
            .Where(item => item.SemanticDescriptor != "Battery 100%")
            .ToArray();
        var request = fixture.Request with
        {
            SourceSlice = fixture.Request.SourceSlice with { Occurrences = occurrences },
        };

        var first = _reconstruction.Reconstruct(request);
        var second = _reconstruction.Reconstruct(request);

        Assert.Equal(new WorldDivergence(
            WorldDivergenceKind.Missing,
            Position: 4,
            Key: "list_item|Battery 100%",
            Expected: "present",
            Actual: "missing"), first.OracleComparison.FirstDivergence);
        Assert.Equal(first.OracleComparison, second.OracleComparison);
        Assert.Equal(first.SemanticDigest, second.SemanticDigest);
    }

    [Fact]
    public void DuplicateOccurrence_ReportsStableFirstDivergence()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var original = fixture.Request.SourceSlice.Occurrences[0];
        var occurrences = fixture.Request.SourceSlice.Occurrences
            .Append(original with { OccurrenceId = "occ-duplicate-network" })
            .ToArray();
        var request = fixture.Request with
        {
            SourceSlice = fixture.Request.SourceSlice with { Occurrences = occurrences },
        };

        var first = _reconstruction.Reconstruct(request);
        var second = _reconstruction.Reconstruct(request);

        Assert.Equal(new WorldDivergence(
            WorldDivergenceKind.Duplicate,
            Position: 7,
            Key: "list_item|Network & internet Mobile, Wi‑Fi, hotspot",
            Expected: "unique",
            Actual: "duplicate"), first.OracleComparison.FirstDivergence);
        Assert.Equal(first.OracleComparison, second.OracleComparison);
        Assert.Equal(first.SemanticDigest, second.SemanticDigest);
    }

    [Fact]
    public void WrongParent_ReportsStableFirstDivergence()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var occurrences = fixture.Request.SourceSlice.Occurrences.ToArray();
        occurrences[0] = occurrences[0] with { OwningContainerId = "container-other" };
        var request = fixture.Request with
        {
            SourceSlice = fixture.Request.SourceSlice with { Occurrences = occurrences },
        };

        var first = _reconstruction.Reconstruct(request);
        var second = _reconstruction.Reconstruct(request);

        Assert.Equal(new WorldDivergence(
            WorldDivergenceKind.WrongParent,
            Position: 0,
            Key: occurrences[0].OccurrenceId,
            Expected: fixture.Request.SourceSlice.RootContainerId,
            Actual: "container-other"), first.OracleComparison.FirstDivergence);
        Assert.Equal(first.OracleComparison, second.OracleComparison);
        Assert.Equal(first.SemanticDigest, second.SemanticDigest);
    }

    [Fact]
    public void FrameMismatch_ReportsStableFirstDivergence()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var occurrences = fixture.Request.SourceSlice.Occurrences.ToArray();
        var locator = Assert.IsType<SpatialLocator>(occurrences[0].Locator);
        occurrences[0] = occurrences[0] with
        {
            Locator = new SpatialLocator(locator.X1, locator.Y1, locator.X2, locator.Y2, "frame-other"),
        };
        var request = fixture.Request with
        {
            SourceSlice = fixture.Request.SourceSlice with { Occurrences = occurrences },
        };

        var first = _reconstruction.Reconstruct(request);
        var second = _reconstruction.Reconstruct(request);

        Assert.Equal(new WorldDivergence(
            WorldDivergenceKind.FrameMismatch,
            Position: 0,
            Key: occurrences[0].OccurrenceId,
            Expected: fixture.Request.SpatialFrameId,
            Actual: "frame-other"), first.OracleComparison.FirstDivergence);
        Assert.Equal(first.OracleComparison, second.OracleComparison);
        Assert.Equal(first.SemanticDigest, second.SemanticDigest);
    }

    [Fact]
    public void HumanOracleChanges_DoNotChangeActualSemanticDigest()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var baseline = _reconstruction.Reconstruct(fixture.Request);
        var expected = fixture.Request.HumanGroundTruth.Elements.ToArray();
        expected[6] = expected[6] with { SemanticDescriptor = "Battery 99%" };
        var request = fixture.Request with
        {
            HumanGroundTruth = fixture.Request.HumanGroundTruth with { Elements = expected },
        };

        var changedOracle = _reconstruction.Reconstruct(request);

        Assert.Equal(baseline.SemanticDigest, changedOracle.SemanticDigest);
        Assert.Equal(WorldDivergenceKind.Missing,
            changedOracle.OracleComparison.FirstDivergence?.Kind);
        Assert.Equal("list_item|Battery 99%",
            changedOracle.OracleComparison.FirstDivergence?.Key);
    }

    [Fact]
    public void AssetSetOrder_DoesNotChangeSemanticDigest()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var baseline = _reconstruction.Reconstruct(fixture.Request);
        var reordered = _reconstruction.Reconstruct(fixture.Request with
        {
            Assets = fixture.Request.Assets.Reverse().ToArray(),
        });

        Assert.Equal(baseline.SemanticDigest, reordered.SemanticDigest);
        Assert.Equal(baseline.OracleComparison, reordered.OracleComparison);
    }

    [Fact]
    public void CrossRevisionAssetCorrelation_FailsClosed()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var assets = fixture.Request.Assets.ToArray();
        assets[0] = assets[0] with
        {
            Reference = assets[0].Reference with { SourceRevisionId = "world-revision-other" },
        };

        var error = Assert.Throws<ArgumentException>(() =>
            _reconstruction.Reconstruct(fixture.Request with { Assets = assets }));

        Assert.Contains("Asset correlation mismatch: screenshot", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GroundTruthFrameCorrelation_FailsClosed()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var request = fixture.Request with
        {
            HumanGroundTruth = fixture.Request.HumanGroundTruth with
            {
                SpatialFrameId = "device-viewport:other-frame",
            },
        };

        var error = Assert.Throws<ArgumentException>(() => _reconstruction.Reconstruct(request));

        Assert.Contains("Human GT capture/revision/frame correlation", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssetContentTamper_FailsClosed()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var assets = fixture.Request.Assets.ToArray();
        assets[0] = assets[0] with { Content = new byte[] { 0x00, 0x01, 0x02 } };

        var error = Assert.Throws<ArgumentException>(() =>
            _reconstruction.Reconstruct(fixture.Request with { Assets = assets }));

        Assert.Contains("Asset integrity mismatch: screenshot", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OutOfContentOccurrence_IsExcludedAndReported()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var occurrences = fixture.Request.SourceSlice.Occurrences
            .Append(new OccurrenceFact(
                "occ-fixed-title",
                fixture.Request.SourceSlice.RootContainerId,
                "text_block",
                "Settings",
                Locator: new SpatialLocator(0.05, 0.17, 0.38, 0.23, fixture.Request.SpatialFrameId)))
            .ToArray();
        var request = fixture.Request with
        {
            SourceSlice = fixture.Request.SourceSlice with { Occurrences = occurrences },
        };

        var result = _reconstruction.Reconstruct(request);

        Assert.Equal(WorldDivergenceKind.OutOfCoverage,
            result.OracleComparison.FirstDivergence?.Kind);
        Assert.Equal("occ-fixed-title", result.OracleComparison.FirstDivergence?.Key);
        Assert.DoesNotContain(result.Presentation.Elements,
            element => element.OccurrenceId == "occ-fixed-title");
    }

    [Fact]
    public void EquivalentOccurrenceOrder_HasSamePresentationDigest()
    {
        var fixture = WorldReconstructionFixture.LoadSettingsFrame();
        var baseline = _reconstruction.Reconstruct(fixture.Request);
        var reordered = _reconstruction.Reconstruct(fixture.Request with
        {
            SourceSlice = fixture.Request.SourceSlice with
            {
                Occurrences = fixture.Request.SourceSlice.Occurrences.Reverse().ToArray(),
            },
        });

        Assert.Equal(baseline.SemanticDigest, reordered.SemanticDigest);
        Assert.Equal(
            baseline.Presentation.Elements.Select(element => element.OccurrenceId),
            reordered.Presentation.Elements.Select(element => element.OccurrenceId));
    }

    [Fact]
    public void ReconstructionOracle_RemainsOutsideProductSource()
    {
        var productRoot = Path.Combine(GoldenPaths.RepoRoot(), "src");
        var forbiddenNames = new[] { "WorldReconstruction", "WorldPresentation", "HumanGroundTruthElement" };

        var offenders = Directory.EnumerateFiles(productRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => forbiddenNames.Any(name => File.ReadAllText(path).Contains(name, StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(GoldenPaths.RepoRoot(), path))
            .ToArray();

        Assert.Empty(offenders);
        Assert.DoesNotContain(
            typeof(Slice).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == typeof(WorldReconstruction).Assembly.GetName().Name);
        Assert.True(typeof(WorldReconstruction).IsNotPublic);
        Assert.False(typeof(WorldReconstruction).IsInterface);
    }
}
