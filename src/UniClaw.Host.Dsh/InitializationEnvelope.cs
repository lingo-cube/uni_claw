using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;

namespace UniClaw.Host.Dsh;

/// <summary>
/// PRF-005（ADR-0041 / Q13）— Product Run 的首个可审计初始化 envelope：
/// 一次 run 能完整重建「当时装配了什么」。落
/// <c>runs/&lt;productSessionId&gt;/initialization.json</c>（运行产物，
/// gitignore；评审副本走 evidence select，两纪律不混）。每会话首个 run
/// 落盘一次；后续 run 沿用首 envelope（会话内装配钉扎，Q9 进程级语义的
/// 会话投影）。
/// </summary>
public static class InitializationEnvelope
{
    /// <summary>Writes the session's initialization envelope (first run wins;
    /// later runs of the same session keep the original). Returns the written
    /// path, or null when the session envelope already exists.</summary>
    public static string? Write(
        string runsRoot,
        string productSessionId,
        string runId,
        UniAgentProfile agentProfile,
        UniagentDshBindings bindings,
        ModelConfiguration modelRoute)
    {
        ArgumentNullException.ThrowIfNull(agentProfile);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(modelRoute);

        var sessionDirectory = Path.Combine(runsRoot, Sanitize(productSessionId));
        Directory.CreateDirectory(sessionDirectory);
        var envelopePath = Path.Combine(sessionDirectory, "initialization.json");
        if (File.Exists(envelopePath))
            return null;

        var schemaHash = ProductProtocolSchema.Current.SchemaHash;
        var node = new JsonObject
        {
            ["productSessionId"] = productSessionId,
            ["firstRunId"] = runId,
            ["recordedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["runtimeProfileRevision"] = agentProfile.Revision,
            ["protocolSchemaHash"] = schemaHash,
            ["productPromptRevision"] = ReadArtifactRevision(agentProfile.Assembly?.PromptManifest, "promptRevision"),
            ["safetyPolicyRevision"] = ReadArtifactRevision(agentProfile.Assembly?.SafetyPolicy, "safetyPolicyRevision"),
            ["modelRoute"] = new JsonObject
            {
                ["provider"] = modelRoute.Provider,
                ["model"] = modelRoute.Name,
            },
            ["dshEndpoint"] = bindings.Service.BaseUri.ToString(),
        };
        File.WriteAllText(envelopePath, node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
        return envelopePath;
    }

    /// <summary>Reads the revision field of a referenced product artifact
    /// (prompt manifest.json / policy json). The assembly hash was already
    /// enforced at profile load; here we only surface the revision.</summary>
    private static int ReadArtifactRevision(AssemblyReference? reference, string fieldName)
    {
        if (reference is null)
            return 0;
        var root = RepoRoot();
        var path = Path.GetFullPath(Path.Combine(root,
            reference.Path.EndsWith(".json", StringComparison.Ordinal)
                ? reference.Path
                : Path.Combine(reference.Path, "manifest.json")));
        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"initialization-envelope: assembly artifact missing: {reference.Path}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty(fieldName, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var revision))
            throw new InvalidOperationException(
                $"initialization-envelope: {fieldName} missing in {reference.Path}");
        return revision;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("repo root not found");
    }

    private static string Sanitize(string value) =>
        string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
}
