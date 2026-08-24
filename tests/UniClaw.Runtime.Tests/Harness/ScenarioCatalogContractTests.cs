using System.Text;
using System.Security.Cryptography;
using UniClaw.Runtime.Harness.Catalog;
using UniClaw.Runtime.Harness.Replay;
using Xunit;

namespace UniClaw.Runtime.Tests.Harness;

public sealed class ScenarioCatalogContractTests
{
    [Fact]
    public void SC_CAT_001_DeepLoad_RejectsDuplicateIds()
    {
        using var stream = Json("{\"schemaVersion\":1,\"catalogId\":\"c\",\"scenarios\":[{\"scenarioId\":\"x\"},{\"scenarioId\":\"x\"}]}");
        var (catalog, errors) = ScenarioCatalog.Load(stream, AppContext.BaseDirectory);
        Assert.Null(catalog);
        Assert.Contains(errors, x => x.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeepLoad_RejectsEscapingPath()
    {
        using var stream = Json("{\"schemaVersion\":1,\"catalogId\":\"c\",\"scenarios\":[{\"scenarioId\":\"x\",\"provenance\":\"Synthetic\",\"manifestPath\":\"../outside.json\",\"manifestSha256\":\"sha256:" + new string('0', 64) + "\"}]}");
        var (catalog, errors) = ScenarioCatalog.Load(stream, AppContext.BaseDirectory);
        Assert.Null(catalog);
        Assert.Contains(errors, x => x.Contains("escapes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeepLoad_RequiresExplicitManifestAndHash()
    {
        using var stream = Json("{\"schemaVersion\":1,\"catalogId\":\"c\",\"scenarios\":[{\"scenarioId\":\"x\",\"provenance\":\"Synthetic\"}]}");
        var (catalog, errors) = ScenarioCatalog.Load(stream, AppContext.BaseDirectory);
        Assert.Null(catalog);
        Assert.Contains(errors, x => x.Contains("manifest path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LegacyLoad_RemainsCompatibleAndLookupIsExplicit()
    {
        using var stream = Json("{\"catalogId\":\"c\",\"scenarios\":[{\"scenarioId\":\"x\"}]}");
        var (catalog, errors) = ScenarioCatalog.Load(stream);
        Assert.Empty(errors);
        Assert.Equal("x", catalog!.GetRequired("x").ScenarioId);
        Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired("other"));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public void DeepLoad_RejectsUnsupportedVersion(int catalogVersion, int entryVersion)
    {
        using var root = new TempRoot();
        var (path, hash) = WriteAsset(root.Path, "x", AssetMaturity.Synthetic);
        using var stream = Json($"{{\"schemaVersion\":{catalogVersion},\"catalogId\":\"c\",\"scenarios\":[{{\"schemaVersion\":{entryVersion},\"scenarioId\":\"x\",\"provenance\":\"Synthetic\",\"manifestPath\":\"{Path.GetFileName(path)}\",\"manifestSha256\":\"{hash}\"}}]}}");
        var (catalog, errors) = ScenarioCatalog.Load(stream, root.Path);
        Assert.Null(catalog);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void DeepLoad_RejectsHashMismatch()
    {
        using var root = new TempRoot();
        var (path, _) = WriteAsset(root.Path, "x", AssetMaturity.Synthetic);
        using var stream = CatalogJson("x", Path.GetFileName(path), "sha256:" + new string('0', 64), "Synthetic");
        var (catalog, errors) = ScenarioCatalog.Load(stream, root.Path);
        Assert.Null(catalog);
        Assert.Contains(errors, x => x.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("RecordedReality")]
    [InlineData("LiveCapture")]
    public void DeepLoad_RejectsProvenanceMismatchOrUnreviewed(string provenance)
    {
        using var root = new TempRoot();
        var (path, hash) = WriteAsset(root.Path, "x", AssetMaturity.Synthetic);
        using var stream = CatalogJson("x", Path.GetFileName(path), hash, provenance);
        var (catalog, errors) = ScenarioCatalog.Load(stream, root.Path);
        Assert.Null(catalog);
        Assert.Contains(errors, x => x.Contains("provenance", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("missing-replay", "replay-missing")]
    [InlineData("missing-frame", "frame-missing")]
    public void DeepLoad_RejectsDanglingReferences(string field, string id)
    {
        using var root = new TempRoot();
        var (path, hash) = WriteAsset(root.Path, "x", AssetMaturity.Synthetic);
        var reference = field.StartsWith("replay", StringComparison.Ordinal) ? $"\"replayIds\":[\"{id}\"]," : $"\"frameIds\":[\"{id}\"],";
        using var stream = Json($"{{\"catalogId\":\"c\",\"scenarios\":[{{\"scenarioId\":\"x\",\"provenance\":\"Synthetic\",{reference}\"manifestPath\":\"{Path.GetFileName(path)}\",\"manifestSha256\":\"{hash}\"}}]}}");
        var (catalog, errors) = ScenarioCatalog.Load(stream, root.Path);
        Assert.Null(catalog);
        Assert.Contains(errors, x => x.Contains(id, StringComparison.Ordinal));
    }

    [Fact]
    public void DeepLoad_ResolvesImmutableManifestScenarioAndReferences()
    {
        using var root = new TempRoot();
        var (path, hash) = WriteAsset(root.Path, "x", AssetMaturity.Synthetic);
        using var stream = CatalogJson("x", Path.GetFileName(path), hash, "Synthetic", "replay-x", "frame-x");
        var (catalog, errors) = ScenarioCatalog.Load(stream, root.Path);
        Assert.Empty(errors);
        var resolution = catalog!.ResolveRequired("x");
        Assert.Equal("x", resolution.Scenario.ScenarioId);
        Assert.Equal("replay-x", resolution.Replays.Single().ReplayId);
        Assert.Equal("frame-x", resolution.Frames.Single().FrameId);
    }

    [Fact]
    public void DeepLoad_RejectsReferencedArtifactPathEscape()
    {
        using var root = new TempRoot();
        var (path, hash) = WriteAssetWithArtifact(root.Path, "../outside.bin", null);
        using var stream = CatalogJson("x", Path.GetFileName(path), hash, "Synthetic", "replay-x", "frame-x");
        var (catalog, errors) = ScenarioCatalog.Load(stream, root.Path);
        Assert.Null(catalog);
        Assert.Contains(errors, x => x.Contains("artifact", StringComparison.OrdinalIgnoreCase) && x.Contains("escapes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeepLoad_RejectsReferencedArtifactHashMismatch()
    {
        using var root = new TempRoot();
        var (path, hash) = WriteAssetWithArtifact(root.Path, "evidence.bin", "sha256:" + new string('0', 64));
        using var stream = CatalogJson("x", Path.GetFileName(path), hash, "Synthetic", "replay-x", "frame-x");
        var (catalog, errors) = ScenarioCatalog.Load(stream, root.Path);
        Assert.Null(catalog);
        Assert.Contains(errors, x => x.Contains("artifact", StringComparison.OrdinalIgnoreCase) && x.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeepLoad_AcceptsReferencedArtifactWithVerifiedHash()
    {
        using var root = new TempRoot();
        var (path, hash) = WriteAssetWithArtifact(root.Path, "evidence.bin", null);
        using var stream = CatalogJson("x", Path.GetFileName(path), hash, "Synthetic", "replay-x", "frame-x");
        var (catalog, errors) = ScenarioCatalog.Load(stream, root.Path);
        Assert.Empty(errors);
        Assert.Equal("artifact-x", catalog!.ResolveRequired("x").Manifest.Artifacts.Single().ArtifactId);
    }

    private static MemoryStream CatalogJson(string id, string path, string hash, string provenance, string replay = "", string frame = "")
        => Json($"{{\"catalogId\":\"c\",\"scenarios\":[{{\"scenarioId\":\"{id}\",\"provenance\":\"{provenance}\",\"replayIds\":[\"{replay}\"],\"frameIds\":[\"{frame}\"],\"manifestPath\":\"{path}\",\"manifestSha256\":\"{hash}\"}}]}}");

    private static (string Path, string Hash) WriteAsset(string root, string scenarioId, AssetMaturity provenance)
    {
        var manifest = new HarnessAssetManifest { ManifestId = "m", Provenance = provenance,
            Frames = [new FrameAsset { FrameId = "frame-x" }], Replays = [new ReplayAsset { ReplayId = "replay-x" }],
            Scenarios = [new ScenarioAsset { ScenarioId = scenarioId, Provenance = provenance }] };
        var bytes = Encoding.UTF8.GetBytes(HarnessAssetManifestJson.Serialize(manifest));
        var path = System.IO.Path.Combine(root, "asset.json"); File.WriteAllBytes(path, bytes);
        return (path, "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static (string Path, string Hash) WriteAssetWithArtifact(string root, string relativePath, string? artifactHashOverride)
    {
        var content = Encoding.UTF8.GetBytes("artifact-evidence");
        var artifactPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relativePath));
        if (artifactPath.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(artifactPath)!);
            File.WriteAllBytes(artifactPath, content);
        }
        var artifact = new Artifact { ArtifactId = "artifact-x", FrameId = "frame-x", Type = ArtifactType.RawScreenshot,
            RelativePath = relativePath, ContentHash = artifactHashOverride ?? ("sha256:" + Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()) };
        var manifest = new HarnessAssetManifest { ManifestId = "m", Provenance = AssetMaturity.Synthetic,
            Frames = [new FrameAsset { FrameId = "frame-x", ArtifactIds = ["artifact-x"] }],
            Artifacts = [artifact], Replays = [new ReplayAsset { ReplayId = "replay-x" }],
            Scenarios = [new ScenarioAsset { ScenarioId = "x", Provenance = AssetMaturity.Synthetic }] };
        var bytes = Encoding.UTF8.GetBytes(HarnessAssetManifestJson.Serialize(manifest));
        var path = System.IO.Path.Combine(root, "asset.json"); File.WriteAllBytes(path, bytes);
        return (path, "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private sealed class TempRoot : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("catalog-contract-").FullName;
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }

    private static MemoryStream Json(string value) => new(Encoding.UTF8.GetBytes(value));
}
