using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Dsh.Tests.Fixtures;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class StdioSidecarTests
{
    [Fact]
    public async Task Stdio_Test_Realization_Uses_Only_SubmitDecision()
    {
        var decision = new AgentDecision.NoAction(new AgentNoActionProposal("decision-1", "replayed"));
        var options = ProductProtocolJson.CreateOptions();
        var json = JsonSerializer.Serialize(decision, options);
        await using var channel = new StdioDecisionChannel(new DshProcessOptions("node", Path.Combine(FindRoot(), "tests", "UniClaw.Agent.Dsh.Tests", "Fixtures", "product-sidecar.mjs"), FindRoot(), new Dictionary<string,string> { ["UNICLAW_SCHEMA_HASH"] = ProductProtocolSchema.Current.SchemaHash, ["DSH_REPLAY_FILE"] = ReplayFile() }));
        await using var adapter = new DshAgentAdapter(channel, "product-session", "run-1", TimeSpan.FromSeconds(2));
        var result = await adapter.ConsultAsync(Context("decision-1"));
        Assert.IsType<AgentDecision.NoAction>(result);
        Assert.Equal("submit_decision", CapabilityManifest.ProductHeadless.Capabilities.Single());
        Assert.Empty(adapter.Diagnostics);
        Assert.NotNull(adapter.DshSessionId);
    }

    private static string ReplayFile() { var p = Path.Combine(Path.GetTempPath(), "agt002-stdio-replay.json"); File.WriteAllText(p, "{\"decision-1\":{\"kind\":\"noAction\",\"decisionId\":\"decision-1\",\"proposal\":{\"decisionId\":\"decision-1\",\"justification\":\"replayed\"}}}"); return p; }
    private static string FindRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) d = d.Parent; return d!.FullName; }
    private static AgentDecisionContext Context(string decisionId) => new(decisionId, "run-1", "v0", "objective", new HashSet<string> { "tap" }, new Dictionary<string, ClaimSummary>(), Array.Empty<AgentObligationView>(), AgentDecisionPhase.InitialPlanning);
}
