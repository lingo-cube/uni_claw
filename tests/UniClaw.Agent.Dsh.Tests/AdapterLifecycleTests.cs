using UniClaw.Agent.Dsh;
using UniClaw.Agent.Dsh.Tests.Fixtures;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class AdapterLifecycleTests
{
    [Fact]
    public async Task One_Run_Allows_One_Active_Consultation()
    {
        var transport = DecisionChannelFixture.Open(
            request => new DecisionChannelResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "done"))),
            delay: TimeSpan.FromMilliseconds(50));
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1",
            TimeSpan.FromSeconds(1));
        var firstTask = adapter.ConsultAsync(Context("decision-1"));
        await EventuallyAsync(() => adapter.SemanticConsultationsStarted == 1, ShortTestWindow);
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
        var transport = DecisionChannelFixture.Open(
            request => new DecisionChannelResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "late"))),
            delay: TimeSpan.FromMilliseconds(80), ignoreCancellation: true);
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1",
            TimeSpan.FromMilliseconds(10));

        var result = await adapter.ConsultAsync(Context("decision-1"));
        await EventuallyAsync(() => adapter.Diagnostics.Any(d => d.Code == "late-response"),
            ShortTestWindow);

        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "timeout");
        Assert.Contains(adapter.Diagnostics, d => d.Code == "late-response");
        Assert.Equal(1, adapter.SemanticConsultationsStarted);
    }

    [Fact]
    public async Task AbortCurrentTurn_IsMechanical_And_Late_Result_IsDropped()
    {
        var transport = DecisionChannelFixture.Open(
            request => new DecisionChannelResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "late"))),
            delay: TimeSpan.FromMilliseconds(50), ignoreCancellation: true);
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1",
            TimeSpan.FromSeconds(1));
        var task = adapter.ConsultAsync(Context("decision-1"));
        await EventuallyAsync(() => adapter.SemanticConsultationsStarted == 1, ShortTestWindow);
        adapter.AbortCurrentTurn();
        var result = await task;
        await EventuallyAsync(() => adapter.Diagnostics.Any(d => d.Code == "late-response"),
            ShortTestWindow);

        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "turn-aborted");
        Assert.Contains(adapter.Diagnostics, d => d.Code == "late-response");
    }

    [Fact]
    public async Task AbortCurrentTurnAsync_Awaits_Channel_And_Drops_Late_Response_When_Peer_Ignores_Cancellation()
    {
        var responseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var abortGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new DeterministicDshPeer(
            request => new DecisionChannelResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "late"))),
            ignoreCancellation: true,
            decisionResponseGate: responseGate,
            abortGate: abortGate);
        await using var channel = new DshOpenedDecisionChannel(peer);
        await using var adapter = new DshAgentAdapter(channel, "session-1", "run-1",
            TimeSpan.FromSeconds(30));

        var consultation = adapter.ConsultAsync(Context("decision-abort-observed"));
        await peer.DecisionRequestStarted.WaitAsync(ShortTestWindow);

        var abort = adapter.AbortCurrentTurnAsync();
        await peer.AbortStarted.WaitAsync(ShortTestWindow);
        Assert.False(abort.IsCompleted);

        peer.ReleaseAbort();
        await abort.WaitAsync(ShortTestWindow);
        Assert.Null(await consultation.WaitAsync(ShortTestWindow));
        Assert.Equal(1, peer.AbortCount);

        peer.ReleaseDecisionResponse();
        await peer.DecisionResponseReturned.WaitAsync(ShortTestWindow);
        await EventuallyAsync(() => adapter.Diagnostics.Any(d => d.Code == "late-response"),
            ShortTestWindow);

        Assert.Equal(1, adapter.SemanticConsultationsStarted);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "turn-aborted");
    }

    [Fact]
    public async Task External_Cancellation_Exits_Promptly_And_Drops_Late_Response()
    {
        var responseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new DeterministicDshPeer(
            request => new DecisionChannelResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "late"))),
            ignoreCancellation: true,
            decisionResponseGate: responseGate);
        await using var channel = new DshOpenedDecisionChannel(peer);
        await using var adapter = new DshAgentAdapter(channel, "session-1", "run-1",
            TimeSpan.FromSeconds(30));
        using var externalCancellation = new CancellationTokenSource();

        var consultation = adapter.ConsultAsync(Context("decision-external-cancel"), externalCancellation.Token);
        await peer.DecisionRequestStarted.WaitAsync(ShortTestWindow);
        externalCancellation.Cancel();

        Assert.Null(await consultation.WaitAsync(ShortTestWindow));
        Assert.Equal(1, peer.AbortCount);

        peer.ReleaseDecisionResponse();
        await peer.DecisionResponseReturned.WaitAsync(ShortTestWindow);
        await EventuallyAsync(() => adapter.Diagnostics.Any(d => d.Code == "late-response"),
            ShortTestWindow);
        Assert.Equal(1, adapter.SemanticConsultationsStarted);
    }

    [Fact]
    public async Task DecisionId_Mismatch_Fails_Closed()
    {
        var transport = DecisionChannelFixture.Open(
            request => new DecisionChannelResponse(request.RequestId, request.Generation,
                new AgentDecision.NoAction(new AgentNoActionProposal("wrong", "bad"))));
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1");

        var result = await adapter.ConsultAsync(Context("decision-1"));

        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "decision-correlation-mismatch");
    }

    [Fact]
    public async Task Stale_Generation_Response_Fails_Closed()
    {
        var transport = DecisionChannelFixture.Open(
            request => new DecisionChannelResponse(request.RequestId, request.Generation - 1,
                new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "stale"))));
        await using var adapter = new DshAgentAdapter(transport, "session-1", "run-1");

        var result = await adapter.ConsultAsync(Context("decision-stale"));

        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, d => d.Code == "stale-response");
    }

    [Fact]
    public async Task Revoke_Serializes_With_An_In_Flight_Attachment()
    {
        var peer = new DeterministicDshPeer(request => new DecisionChannelResponse(
            request.RequestId, request.Generation,
            new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "ok"))));
        var inner = new DshOpenedDecisionChannel(peer);
        var channel = new GatedAttachChannel(inner);
        await using var adapter = new DshAgentAdapter(channel, "session-1", "run-1",
            TimeSpan.FromSeconds(1));

        var consultation = adapter.ConsultAsync(Context("decision-revoked-attach"));
        await channel.InnerAttachmentReturned.WaitAsync(ShortTestWindow);
        var revoke = adapter.RevokeAttachmentAsync();
        await Task.Yield();
        Assert.False(revoke.IsCompleted);

        channel.ReleaseAttachResult();
        await Task.WhenAll(consultation.WaitAsync(ShortTestWindow), revoke.WaitAsync(ShortTestWindow));
        Assert.Null(adapter.DshSessionId);
    }

    private sealed class GatedAttachChannel : IDecisionChannel
    {
        private readonly IDecisionChannel _inner;
        private readonly TaskCompletionSource<bool> _release = NewSignal();
        private readonly TaskCompletionSource<bool> _innerAttachmentReturned = NewSignal();

        public GatedAttachChannel(IDecisionChannel inner) => _inner = inner;
        public Task InnerAttachmentReturned => _innerAttachmentReturned.Task;

        public void ReleaseAttachResult() => _release.TrySetResult(true);

        public async Task<DecisionChannelAttachment> AttachAsync(
            HandshakeRequest request, CancellationToken cancellationToken)
        {
            var attachment = await _inner.AttachAsync(request, cancellationToken).ConfigureAwait(false);
            _innerAttachmentReturned.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return attachment;
        }

        public Task<DecisionChannelResponse> PushAsync(
            DecisionRequest request, CancellationToken cancellationToken) =>
            _inner.PushAsync(request, cancellationToken);

        public Task AbortCurrentTurnAsync(CancellationToken cancellationToken) =>
            _inner.AbortCurrentTurnAsync(cancellationToken);

        public Task RevokeAsync(CancellationToken cancellationToken) =>
            _inner.RevokeAsync(cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();

        private static TaskCompletionSource<bool> NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static readonly TimeSpan ShortTestWindow = TimeSpan.FromSeconds(2);

    private static async Task EventuallyAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (!condition())
        {
            if (Environment.TickCount64 >= deadline)
                throw new TimeoutException("condition did not become true within the test window");
            await Task.Delay(10);
        }
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
