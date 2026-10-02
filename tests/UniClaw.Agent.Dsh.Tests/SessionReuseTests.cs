using UniClaw.Agent.Dsh;
using UniClaw.Agent.Dsh.Tests.Fixtures;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// AGT-005 补齐 — DSH 任务 session 复用的显式回归：同一 Product run 的
/// 顺序多轮咨询必须只 attach 一次、复用同一 DSH session id（容量重试同
/// request 的复用语义见 DshOpenedHttpPeerTests；此处证明跨咨询轮次的面）。
/// </summary>
public sealed class SessionReuseTests
{
    [Fact]
    public async Task SequentialConsults_AttachOnce_ReuseSameDshSession()
    {
        var peer = new DeterministicDshPeer(request => new DecisionChannelResponse(
            request.RequestId, request.Generation,
            new AgentDecision.NoAction(new AgentNoActionProposal(
                request.Context.DecisionId, "step-done"))));
        await using var transport = new DshOpenedDecisionChannel(peer);
        await using var adapter = new DshAgentAdapter(transport, "session-reuse-1", "run-reuse-1",
            TimeSpan.FromSeconds(5));

        var first = await adapter.ConsultAsync(Context("decision-1"));
        var sessionAfterFirst = adapter.DshSessionId;
        var second = await adapter.ConsultAsync(Context("decision-2"));
        var sessionAfterSecond = adapter.DshSessionId;

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(2, adapter.SemanticConsultationsStarted);
        // attach（handshake）只发生一次（首次咨询）；后续咨询复用同一 attachment。
        Assert.Equal(1, peer.HandshakeCount);
        Assert.Equal(2, peer.RequestCount);
        Assert.NotNull(sessionAfterFirst);
        Assert.Equal(sessionAfterFirst, sessionAfterSecond);
    }

    private static AgentDecisionContext Context(string decisionId) => new(
        decisionId,
        "run-reuse-1",
        "v0",
        Objective: "settings-coverage",
        AllowedEffects: new HashSet<string>(StringComparer.Ordinal) { "tap", "swipe-up" },
        CurrentWorldClaims: new Dictionary<string, ClaimSummary>(),
        PendingObligations: Array.Empty<AgentObligationView>(),
        AgentDecisionPhase.InitialPlanning);
}
