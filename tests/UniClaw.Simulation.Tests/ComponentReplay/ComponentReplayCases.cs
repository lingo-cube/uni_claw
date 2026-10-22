using System.Security.Cryptography;
using System.Text.Json;

namespace UniClaw.Simulation.Tests.ComponentReplay;

internal sealed record SemanticOracle(string RejectionStatus, string RejectionDisposition, string SuccessStatus,
    string SuccessDisposition, string SuccessRelation, string SuccessExpectedMatch, string DuplicateStatus,
    string UnavailableStatus, string UnavailableDisposition, string ConflictedStatus, string ConflictedDisposition,
    string ConflictedMatch, string MisboundStatus, string MisboundDisposition, string CancelStatus);
internal sealed record WorldOracle(bool NeedsIdentityBefore, bool NeedsIdentityAfterWrong,
    bool NeedsIdentityAfterMatching, int ContainersBefore, int ContainersAfter, int FaultReceipts);
internal sealed record PageOracle(string Id, string PageKey, string Title, string[] Controls);
internal sealed record ComponentOracle(SemanticOracle Semantic, WorldOracle World, PageOracle[] Cases);

internal static class ComponentReplayCases
{
    internal static string Root => GoldenPaths.RepoRoot();
    internal static string Fixture(string name) => Path.Combine(Root, RecordedComponentInputs.FixtureRoot, name);
    internal static ComponentOracle Oracle() => JsonSerializer.Deserialize<ComponentOracle>(
        File.ReadAllText(Fixture("oracle.json")), RecordedComponentInputs.Json)!;
    internal static ReplayCompositionGuard Guard() => ReplayCompositionGuard.FromJson(File.ReadAllText(Fixture("composition.json")));

    // Adding a data row discovers a new test instance; neither component nor
    // the Runtime/Runner knows these source IDs.
    internal static IEnumerable<object[]> Rows(string supplierProperty)
    {
        using var suite = JsonDocument.Parse(File.ReadAllText(Fixture("suite.json")));
        foreach (var id in suite.RootElement.GetProperty("sourceIds").EnumerateArray())
        foreach (var supplier in suite.RootElement.GetProperty(supplierProperty).EnumerateArray())
            yield return [id.GetString()!, supplier.GetString()!];
    }

    internal static void Export(string name, ReplayCompositionGuard guard, object actual)
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_REGRESSION_REPLAY_OUTPUT") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        var kernel = typeof(UniClaw.Kernel.UniKernel).Assembly;
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new
        {
            scope = "real product component; recorded layouts + controlled suppliers; no device/model/whole-Run claim",
            composition = new { guard.ManifestSha256, pins = guard.Pins,
                kernelAssembly = kernel.GetName().Name,
                kernelAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(kernel.Location))).ToLowerInvariant() },
            actual,
        }, RecordedComponentInputs.Json));
    }
}
