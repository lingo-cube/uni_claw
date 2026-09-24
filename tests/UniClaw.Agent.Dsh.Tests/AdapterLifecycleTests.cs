using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class AdapterLifecycleTests
{
    [Fact]
    public async Task One_Run_Allows_One_Active_Consultation()
    {
        var transport = new FakeDshTransport(
            request => new DshTransportResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "done"))),
            delay: TimeSpan.FromMilliseconds(50));
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1",
            TimeSpan.FromSeconds(1));
        var firstTask = adapter.ConsultAsync(Context("decision-1"));
        await Task.Delay(5);
        var second = await adapter.ConsultAsync(Context("decision-2"));
        var first = await firstTask;

        Assert.Null(second);
        Assert.NotNull(first);
        Assert.Equal(1, adapter.SemanticConsultationsStarted);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "active-consultation-exists");
    }

    [Fact]
    public async Task Timeout_Drops_Late_Response_Without_Reentering_Product()
    {
        var transport = new FakeDshTransport(
            request => new DshTransportResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "late"))),
            delay: TimeSpan.FromMilliseconds(80), ignoreCancellation: true);
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1",
            TimeSpan.FromMilliseconds(10));

        var result = await adapter.ConsultAsync(Context("decision-1"));
        await Task.Delay(100);

        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "timeout");
        Assert.Contains(adapter.Diagnostics, d => d.Code == "late-response");
        Assert.Equal(1, adapter.SemanticConsultationsStarted);
        Assert.Equal(1, transport.AbortCount);
    }

    [Fact]
    public async Task AbortCurrentTurn_IsMechanical_And_Late_Result_IsDropped()
    {
        var transport = new FakeDshTransport(
            request => new DshTransportResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "late"))),
            delay: TimeSpan.FromMilliseconds(50), ignoreCancellation: true);
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1",
            TimeSpan.FromSeconds(1));
        var task = adapter.ConsultAsync(Context("decision-1"));
        await Task.Delay(5);
        adapter.AbortCurrentTurn();
        var result = await task;
        await Task.Delay(60);

        Assert.Null(result);
        Assert.Equal(1, transport.AbortCount);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "turn-aborted");
        Assert.Contains(adapter.Diagnostics, d => d.Code == "late-response");
    }

    [Fact]
    public async Task DecisionId_Mismatch_Fails_Closed()
    {
        var transport = new FakeDshTransport(
            request => new DshTransportResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal("wrong", "bad"))));
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1");

        var result = await adapter.ConsultAsync(Context("decision-1"));

        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "decision-correlation-mismatch");
    }

    [Fact]
    public async Task Stale_Generation_Response_Fails_Closed()
    {
        var transport = new FakeDshTransport(
            request => new DshTransportResponse(request.RequestId, request.Generation - 1,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "stale"))));
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1");

        var result = await adapter.ConsultAsync(Context("decision-stale"));

        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "stale-response");
    }

    private static AgentDecisionContext Context(string decisionId) => new(
        decisionId,
        "run-1",
        "v0",
        "objective",
        new HashSet<string> { "tap" },
        new Dictionary<string, ClaimSummary>(),
        Array.Empty<AgentObligationView>(),
        AgentDecisionPhase.InitialPlanning);
}
