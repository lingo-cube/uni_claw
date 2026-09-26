using UniClaw.Agent.Dsh;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// B2 single-source enforcement: the DSH plugin package consumes the SAME
/// generated Product protocol artifact the .NET side generates — schema JSON
/// byte-identical (trimmed), and the plugin's locally-reported schemaHash
/// exactly <see cref="ProductProtocolSchema.Current"/>.SchemaHash. Drift in
/// either file fails here before anything reaches a DSH runtime.
/// </summary>
public sealed class PluginSchemaArtifactTests
{
    [Fact]
    public void Plugin_SchemaArtifact_IsTheGeneratedSingleSource()
    {
        var root = FindPluginPackage();
        var schemaPath = Path.Combine(root, "schema", "product-protocol.schema.json");
        var hashPath = Path.Combine(root, "schema", "schema-hash.txt");

        Assert.True(File.Exists(schemaPath), $"missing plugin schema artifact: {schemaPath}");
        Assert.True(File.Exists(hashPath), $"missing plugin schema hash artifact: {hashPath}");

        Assert.Equal(ProductProtocolSchema.Current.Json,
            File.ReadAllText(schemaPath).Trim());

        Assert.Equal(ProductProtocolSchema.Current.SchemaHash,
            File.ReadAllText(hashPath).Trim());
    }

    internal static string FindPluginPackage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName,
                "dsh", "uniclaw-decision-channel");
            if (Directory.Exists(candidate))
                return candidate;
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                break;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("dsh/uniclaw-decision-channel not found");
    }
}
