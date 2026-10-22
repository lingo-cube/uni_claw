using Xunit;
using UniClaw.Simulation.Tests.ComponentReplay;

namespace UniClaw.Simulation.Tests;

public sealed class ComponentReplayGovernanceTests
{
    private const string KnownHash = "4bf5122f344554c53bde2ebb8cd2b7e3d1600ad631c385a5d7cce23c7785459a";
    private static readonly (string Kind, string Path)[] Categories =
    [
        ("product-contract", "contract.bin"), ("product-implementation", "implementation.bin"),
        ("simulation-adapter", "adapter.bin"), ("replay-input", "input.bin"), ("oracle", "oracle.bin")
    ];

    [Fact]
    public void UnchangedPinsAllowFactoryOnceAndExposeManifestHash()
    {
        var guard = ReplayCompositionGuard.FromJson(Manifest());
        var calls = 0;
        var result = guard.Create("/virtual/repo", () => ++calls, _ => [1]);
        Assert.Equal(1, result);
        Assert.Equal(1, calls);
        Assert.Equal(64, guard.ManifestSha256.Length);
        Assert.Equal(5, guard.Pins.Count);
        Assert.Equal("product-contract", guard.Pins[0].GetProperty("kind").GetString());
    }

    [Theory]
    [MemberData(nameof(MutatedKinds))]
    public void EveryContentCategoryRejectsBeforeFactory(string kind, string path)
    {
        var guard = ReplayCompositionGuard.FromJson(Manifest());
        var calls = 0;
        var ex = Assert.Throws<InvalidDataException>(() => guard.Create("/virtual/repo", () => ++calls,
            absolutePath => Path.GetFileName(absolutePath) == path ? [2] : [1]));
        Assert.Contains($"hash-mismatch:{path}", ex.Message);
        Assert.Equal(0, calls); // schema version is unchanged; pinned bytes are what trigger refusal.
    }

    public static IEnumerable<object[]> MutatedKinds() => Categories.Select(x => new object[] { x.Kind, x.Path });

    [Fact]
    public void UnrecognizedCompositionSchemaRejectsBeforeFactory()
    {
        var guard = ReplayCompositionGuard.FromJson(Manifest().Replace("composition.v1", "composition.v2"));
        var calls = 0;
        Assert.Throws<InvalidDataException>(() => guard.Create("/virtual/repo", () => ++calls, _ => [1]));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void CausalChangeIsRecordedWithoutHardCodingOneChangeIntoTheGuard()
    {
        var guard = ReplayCompositionGuard.FromJson(Manifest().Replace("SIM-014", "SIM-015"));
        Assert.Equal("new change", guard.Create("/virtual/repo", () => "new change", _ => [1]));
    }

    private static string Manifest() => System.Text.Json.JsonSerializer.Serialize(new
    {
        schemaVersion = "component-replay-composition.v1", causalChange = "SIM-014",
        pins = Categories.Select(x => new { kind = x.Kind, path = x.Path, sha256 = KnownHash })
    });
}
