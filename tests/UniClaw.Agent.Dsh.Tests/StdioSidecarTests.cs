using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class StdioSidecarTests
{
    [Fact]
    public async Task Product_Sidecar_Handshake_And_Replay_Use_Only_SubmitDecision()
    {
        var repo = FindRepoRoot();
        var replayPath = Path.Combine(Path.GetTempPath(), $"agt002-replay-{Guid.NewGuid():N}.json");
        var options = ProductProtocolJson.CreateOptions();
        var decision = new AgentDecision.NoAction(new AgentNoActionProposal("decision-1", "replayed"));
        await File.WriteAllTextAsync(replayPath, JsonSerializer.Serialize(
            new Dictionary<string, AgentDecision> { ["decision-1"] = decision }, options));
        try
        {
            var process = new DshProcessOptions(
                "node",
                Path.Combine(repo, "platforms", "dsh", "product-sidecar.mjs"),
                repo,
                new Dictionary<string, string>
                {
                    ["UNICLAW_SCHEMA_HASH"] = ProductProtocolSchema.Current.SchemaHash,
                    ["DSH_REPLAY_FILE"] = replayPath,
                });
            await using var transport = new JsonRpcStdioTransport(process);
            await using var adapter = new DshAgentAdapter(transport, "product-session", "run-1",
                TimeSpan.FromSeconds(2));

            var result = await adapter.ConsultAsync(Context("decision-1"));

            Assert.IsType<AgentDecision.NoAction>(result);
            Assert.Equal("submit_decision", CapabilityManifest.ProductHeadless.Capabilities.Single());
            Assert.Empty(adapter.Diagnostics);
            Assert.NotNull(adapter.DshSessionId);
        }
        finally
        {
            File.Delete(replayPath);
        }
    }

    private static AgentDecisionContext Context(string decisionId) => new(
        decisionId, "run-1", "v0", "objective", new HashSet<string> { "tap" },
        new Dictionary<string, ClaimSummary>(), Array.Empty<AgentObligationView>(),
        AgentDecisionPhase.InitialPlanning);

    private static string FindRepoRoot()
    {
        for (var current = new DirectoryInfo(Environment.CurrentDirectory); current is not null;
            current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        throw new InvalidOperationException("repository root not found");
    }
}
