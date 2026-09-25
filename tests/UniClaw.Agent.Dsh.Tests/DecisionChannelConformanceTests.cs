using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Dsh.Tests.Fixtures;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class DecisionChannelConformanceTests
{
    public static IEnumerable<object[]> ConfigurableRealizations()
    {
        yield return new object[] { "dsh-opened", (Func<Func<DecisionRequest, DecisionChannelResponse>, IDecisionChannel>)(response => DecisionChannelFixture.Open(response)) };
        yield return new object[] { "stdio", (Func<Func<DecisionRequest, DecisionChannelResponse>, IDecisionChannel>)(response => new StdioDecisionChannel(new DeterministicDshPeer(response))) };
        yield return new object[] { "fake", (Func<Func<DecisionRequest, DecisionChannelResponse>, IDecisionChannel>)(response => new FakeDecisionChannel(response)) };
        yield return new object[] { "replay", (Func<Func<DecisionRequest, DecisionChannelResponse>, IDecisionChannel>)(response => new ReplayDecisionChannel(response)) };
    }

    public static IEnumerable<object[]> DelayedRealizations()
    {
        yield return new object[] { "dsh-opened", (Func<IDecisionChannel>)(() => DecisionChannelFixture.Open(Ok, TimeSpan.FromMilliseconds(80), true)) };
        yield return new object[] { "stdio", (Func<IDecisionChannel>)(() => new StdioDecisionChannel(new DeterministicDshPeer(Ok, TimeSpan.FromMilliseconds(80), true))) };
        yield return new object[] { "fake", (Func<IDecisionChannel>)(() => new FakeDecisionChannel(new DeterministicDshPeer(Ok, TimeSpan.FromMilliseconds(80), true))) };
        yield return new object[] { "replay", (Func<IDecisionChannel>)(() => new ReplayDecisionChannel(new DeterministicDshPeer(Ok, TimeSpan.FromMilliseconds(80), true))) };
    }

    public static IEnumerable<object[]> CancellationIgnoringRealizations()
    {
        yield return new object[] { "dsh-opened", (Func<(IDecisionChannel, DeterministicDshPeer)>)(() =>
        {
            var peer = new DeterministicDshPeer(Ok, ignoreCancellation: true,
                decisionResponseGate: new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            return (new DshOpenedDecisionChannel(peer), peer);
        }) };
        yield return new object[] { "stdio", (Func<(IDecisionChannel, DeterministicDshPeer)>)(() =>
        {
            var peer = new DeterministicDshPeer(Ok, ignoreCancellation: true,
                decisionResponseGate: new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            return (new StdioDecisionChannel(peer), peer);
        }) };
        yield return new object[] { "fake", (Func<(IDecisionChannel, DeterministicDshPeer)>)(() =>
        {
            var peer = new DeterministicDshPeer(Ok, ignoreCancellation: true,
                decisionResponseGate: new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            return (new FakeDecisionChannel(peer), peer);
        }) };
        yield return new object[] { "replay", (Func<(IDecisionChannel, DeterministicDshPeer)>)(() =>
        {
            var peer = new DeterministicDshPeer(Ok, ignoreCancellation: true,
                decisionResponseGate: new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            return (new ReplayDecisionChannel(peer), peer);
        }) };
    }

    public static IEnumerable<object[]> GatedRevokeRealizations()
    {
        yield return new object[] { "dsh-opened", (Func<(IDecisionChannel, DeterministicDshPeer)>)(() =>
        {
            var peer = new DeterministicDshPeer(Ok, ignoreCancellation: true,
                decisionResponseGate: new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            return (new DshOpenedDecisionChannel(peer), peer);
        }) };
        yield return new object[] { "stdio", (Func<(IDecisionChannel, DeterministicDshPeer)>)(() =>
        {
            var peer = new DeterministicDshPeer(Ok, ignoreCancellation: true,
                decisionResponseGate: new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            return (new StdioDecisionChannel(peer), peer);
        }) };
        yield return new object[] { "fake", (Func<(IDecisionChannel, DeterministicDshPeer)>)(() =>
        {
            var peer = new DeterministicDshPeer(Ok, ignoreCancellation: true,
                decisionResponseGate: new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            return (new FakeDecisionChannel(peer), peer);
        }) };
        yield return new object[] { "replay", (Func<(IDecisionChannel, DeterministicDshPeer)>)(() =>
        {
            var peer = new DeterministicDshPeer(Ok, ignoreCancellation: true,
                decisionResponseGate: new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            return (new ReplayDecisionChannel(peer), peer);
        }) };
    }

    public static IEnumerable<object[]> MismatchedHandshakeRealizations()
    {
        static HandshakeResponse Bad(HandshakeRequest request) => new(
            true,
            request.Protocol with { SchemaHash = "mismatch" },
            request.ExpectedCapabilities,
            "dsh-test");
        yield return new object[] { "dsh-opened", (Func<IDecisionChannel>)(() => new DshOpenedDecisionChannel(new DeterministicDshPeer(Ok, handshake: Bad))) };
        yield return new object[] { "stdio", (Func<IDecisionChannel>)(() => new StdioDecisionChannel(new DeterministicDshPeer(Ok, handshake: Bad))) };
        yield return new object[] { "fake", (Func<IDecisionChannel>)(() => new FakeDecisionChannel(new DeterministicDshPeer(Ok, handshake: Bad))) };
        yield return new object[] { "replay", (Func<IDecisionChannel>)(() => new ReplayDecisionChannel(new DeterministicDshPeer(Ok, handshake: Bad))) };
    }

    public static IEnumerable<object[]> RetryingHandshakeRealizations()
    {
        yield return new object[] { "dsh-opened", (Func<IDecisionChannel>)(() => new DshOpenedDecisionChannel(RetryingPeer())) };
        yield return new object[] { "stdio", (Func<IDecisionChannel>)(() => new StdioDecisionChannel(RetryingPeer())) };
        yield return new object[] { "fake", (Func<IDecisionChannel>)(() => new FakeDecisionChannel(RetryingPeer())) };
        yield return new object[] { "replay", (Func<IDecisionChannel>)(() => new ReplayDecisionChannel(RetryingPeer())) };
    }

    public static IEnumerable<object[]> Realizations()
    {
        yield return new object[] { "dsh-opened", (Func<IDecisionChannel>)(() => DecisionChannelFixture.Open(Ok)) };
        yield return new object[] { "stdio", (Func<IDecisionChannel>)(() => new StdioDecisionChannel(StdioOptions())) };
        yield return new object[] { "fake", (Func<IDecisionChannel>)(() => new FakeDecisionChannel(Ok)) };
        yield return new object[] { "replay", (Func<IDecisionChannel>)(() => new ReplayDecisionChannel(ReplayPayload())) };
    }

    [Theory]
    [MemberData(nameof(Realizations))]
    public async Task Attach_And_Decision_Correlation_Conform(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        var request = ProductHandshake.CreateRequest("session-1", "run-1");
        var attachment = await channel.AttachAsync(request, CancellationToken.None);
        Assert.True(attachment.Accepted);
        var decision = await channel.PushAsync(new DecisionRequest("req-1", 1, "session-1", "run-1", Context("decision-1")), CancellationToken.None);
        Assert.Equal("req-1", decision.RequestId);
        Assert.Equal(1, decision.Generation);
        Assert.Null(decision.Error);
        Assert.Equal("decision-1", AgentDecisionCorrelation.TryGetDecisionId(decision.Decision!));
    }

    [Theory]
    [MemberData(nameof(MismatchedHandshakeRealizations))]
    public async Task Schema_Mismatch_Rejects_Attachment(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        var attachment = await channel.AttachAsync(ProductHandshake.CreateRequest("session-1", "run-1"), CancellationToken.None);
        Assert.False(attachment.Accepted);
        Assert.Equal("schema-hash-mismatch", attachment.FailureReason);
    }

    [Theory]
    [MemberData(nameof(RetryingHandshakeRealizations))]
    public async Task Failed_Attach_Can_Retry_After_Rejection(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        var request = ProductHandshake.CreateRequest("session-1", "run-1");

        var rejected = await channel.AttachAsync(request, CancellationToken.None);
        var retried = await channel.AttachAsync(request, CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal("schema-hash-mismatch", rejected.FailureReason);
        Assert.True(retried.Accepted);
    }

    [Fact]
    public async Task Concurrent_Attach_Creates_Exactly_One_Physical_Attachment()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new DeterministicDshPeer(Ok, handshakeGate: gate);
        await using var channel = new DshOpenedDecisionChannel(peer);
        var request = ProductHandshake.CreateRequest("session-1", "run-1");

        var first = channel.AttachAsync(request, CancellationToken.None);
        await peer.HandshakeStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var waiters = Enumerable.Range(0, 7)
            .Select(_ => channel.AttachAsync(request, CancellationToken.None))
            .ToArray();
        peer.ReleaseHandshake();
        var attachments = await Task.WhenAll(new[] { first }.Concat(waiters));

        Assert.All(attachments, attachment => Assert.True(attachment.Accepted));
        Assert.Single(attachments.Select(attachment => attachment.AttachmentId).Distinct());
        Assert.Equal(1, peer.HandshakeCount);
    }

    [Fact]
    public async Task Concurrent_Failed_Attach_Callers_Share_One_Attempt()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new DeterministicDshPeer(
            Ok,
            handshake: request => new HandshakeResponse(true,
                request.Protocol with { SchemaHash = "mismatch" },
                request.ExpectedCapabilities, "dsh-test"),
            handshakeGate: gate);
        await using var channel = new DshOpenedDecisionChannel(peer);
        var request = ProductHandshake.CreateRequest("session-1", "run-1");

        var first = channel.AttachAsync(request, CancellationToken.None);
        await peer.HandshakeStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var waiters = Enumerable.Range(0, 7)
            .Select(_ => channel.AttachAsync(request, CancellationToken.None))
            .ToArray();
        peer.ReleaseHandshake();
        var results = await Task.WhenAll(new[] { first }.Concat(waiters));

        Assert.All(results, attachment => Assert.False(attachment.Accepted));
        Assert.Equal(1, peer.HandshakeCount);
    }

    [Fact]
    public async Task Pending_Attach_Does_Not_Share_Attachment_Across_Product_Mappings()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new DeterministicDshPeer(Ok, handshakeGate: gate);
        await using var channel = new DshOpenedDecisionChannel(peer);
        var request = ProductHandshake.CreateRequest("session-1", "run-1");

        var original = channel.AttachAsync(request, CancellationToken.None);
        await peer.HandshakeStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var mismatched = await channel.AttachAsync(
            ProductHandshake.CreateRequest("session-1", "other-run"), CancellationToken.None);
        peer.ReleaseHandshake();
        var attachment = await original;

        Assert.False(mismatched.Accepted);
        Assert.Equal("product-mapping-mismatch", mismatched.FailureReason);
        Assert.True(attachment.Accepted);
        Assert.Equal(1, peer.HandshakeCount);
    }

    [Fact]
    public async Task Repeated_Synchronous_Attach_Failures_Retry_Without_Retaining_Failed_Task()
    {
        var peer = new DeterministicDshPeer(
            Ok,
            handshake: request => new HandshakeResponse(false, request.Protocol,
                request.ExpectedCapabilities, "dsh-test"));
        await using var channel = new DshOpenedDecisionChannel(peer);
        var request = ProductHandshake.CreateRequest("session-1", "run-1");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await channel.AttachAsync(request, CancellationToken.None);
            Assert.False(result.Accepted);
        }

        Assert.Equal(3, peer.HandshakeCount);
    }

    [Fact]
    public async Task Canceled_Attach_Waiter_Does_Not_Split_Physical_Handshake()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new DeterministicDshPeer(Ok, handshakeGate: gate);
        await using var channel = new DshOpenedDecisionChannel(peer);
        var request = ProductHandshake.CreateRequest("session-1", "run-1");
        using var cancellation = new CancellationTokenSource();

        var canceledWaiter = channel.AttachAsync(request, cancellation.Token);
        await peer.HandshakeStarted.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledWaiter);

        var attachmentTask = channel.AttachAsync(request, CancellationToken.None);
        peer.ReleaseHandshake();
        var attachment = await attachmentTask;

        Assert.True(attachment.Accepted);
        Assert.Equal(1, peer.HandshakeCount);
    }

    [Fact]
    public async Task Revoke_During_InFlight_Attach_Does_Not_Reuse_Old_Attach_Task()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new DeterministicDshPeer(Ok, handshakeGate: gate);
        await using var channel = new DshOpenedDecisionChannel(peer);
        var request = ProductHandshake.CreateRequest("session-1", "run-1");

        var oldAttach = channel.AttachAsync(request, CancellationToken.None);
        await peer.HandshakeStarted.WaitAsync(TimeSpan.FromSeconds(2));
        await channel.RevokeAsync(CancellationToken.None);
        var newAttach = channel.AttachAsync(request, CancellationToken.None);
        peer.ReleaseHandshake();

        var oldAttachment = await oldAttach;
        var newAttachment = await newAttach;

        Assert.Equal("attachment-revoked", oldAttachment.FailureReason);
        Assert.True(newAttachment.Accepted);
        Assert.Equal(2, peer.HandshakeCount);
    }

    [Theory]
    [MemberData(nameof(Realizations))]
    public async Task Revoke_Rejects_Future_Request_Without_Run_Semantics(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        await channel.AttachAsync(ProductHandshake.CreateRequest("session-1", "run-1"), CancellationToken.None);
        await channel.RevokeAsync(CancellationToken.None);
        var result = await channel.PushAsync(new DecisionRequest("req-2", 2, "session-1", "run-1", Context("decision-2")), CancellationToken.None);
        Assert.Null(result.Decision);
        Assert.Equal("channel-not-attached", result.Error);
    }

    [Theory]
    [MemberData(nameof(GatedRevokeRealizations))]
    public async Task Revoke_During_InFlight_Drops_Response(
        string _, Func<(IDecisionChannel Channel, DeterministicDshPeer Peer)> factory)
    {
        var (channel, peer) = factory();
        await using (channel)
        {
            await channel.AttachAsync(ProductHandshake.CreateRequest("session-1", "run-1"), CancellationToken.None);
            var push = channel.PushAsync(
                new DecisionRequest("revoked-in-flight", 1, "session-1", "run-1", Context("decision-revoked")),
                CancellationToken.None);
            await peer.DecisionRequestStarted.WaitAsync(TimeSpan.FromSeconds(2));
            await channel.RevokeAsync(CancellationToken.None);

            var response = await push.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Null(response.Decision);
            Assert.Equal("attachment-revoked", response.Error);
            peer.ReleaseDecisionResponse();
            await peer.DecisionResponseReturned.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Theory]
    [MemberData(nameof(Realizations))]
    public async Task Mapping_Mismatch_Fails_Closed(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        await channel.AttachAsync(ProductHandshake.CreateRequest("session-1", "run-1"), CancellationToken.None);
        var result = await channel.PushAsync(new DecisionRequest("req-3", 3, "other", "run-1", Context("decision-3")), CancellationToken.None);
        Assert.Null(result.Decision);
        Assert.Equal("product-mapping-mismatch", result.Error);
    }

    [Theory]
    [MemberData(nameof(ConfigurableRealizations))]
    public async Task DecisionId_Mismatch_Fails_Closed_Across_Realizations(
        string _, Func<Func<DecisionRequest, DecisionChannelResponse>, IDecisionChannel> factory)
    {
        await using var channel = factory(request => new(request.RequestId, request.Generation,
            new AgentDecision.NoAction(new AgentNoActionProposal("wrong", "mismatch"))));
        await using var adapter = new DshAgentAdapter(channel, "session-1", "run-1");
        var result = await adapter.ConsultAsync(Context("decision-4"));
        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, diagnostic => diagnostic.Code == "decision-correlation-mismatch");
    }

    [Theory]
    [MemberData(nameof(ConfigurableRealizations))]
    public async Task Generation_Mismatch_Is_Returned_For_Adapter_To_Drop(
        string _, Func<Func<DecisionRequest, DecisionChannelResponse>, IDecisionChannel> factory)
    {
        await using var channel = factory(request => new(request.RequestId, request.Generation - 1,
            new AgentDecision.NoAction(new AgentNoActionProposal(request.Context.DecisionId, "stale"))));
        await using var adapter = new DshAgentAdapter(channel, "session-1", "run-1");
        var result = await adapter.ConsultAsync(Context("decision-stale"));
        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, diagnostic => diagnostic.Code == "stale-response");
    }

    [Theory]
    [MemberData(nameof(ConfigurableRealizations))]
    public async Task Malformed_Response_Fails_Closed_Across_Realizations(
        string _, Func<Func<DecisionRequest, DecisionChannelResponse>, IDecisionChannel> factory)
    {
        await using var channel = factory(request => new(request.RequestId, request.Generation, null,
            "malformed-decision"));
        await using var adapter = new DshAgentAdapter(channel, "session-1", "run-1");
        var result = await adapter.ConsultAsync(Context("decision-malformed"));
        Assert.Null(result);
        Assert.Contains(adapter.Diagnostics, diagnostic => diagnostic.Code == "no-decision");
    }

    [Theory]
    [MemberData(nameof(Realizations))]
    public async Task Duplicate_Request_Is_Dropped(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        await channel.AttachAsync(ProductHandshake.CreateRequest("session-1", "run-1"), CancellationToken.None);
        var request = new DecisionRequest("duplicate-request", 1, "session-1", "run-1", Context("decision-1"));
        var first = await channel.PushAsync(request, CancellationToken.None);
        var second = await channel.PushAsync(request, CancellationToken.None);
        Assert.NotNull(first.Decision);
        Assert.Null(second.Decision);
        Assert.Equal("duplicate-request", second.Error);
    }

    [Theory]
    [MemberData(nameof(DelayedRealizations))]
    public async Task One_In_Flight_Is_Enforced(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        await channel.AttachAsync(ProductHandshake.CreateRequest("session-1", "run-1"), CancellationToken.None);
        var first = channel.PushAsync(new DecisionRequest("in-flight-1", 1, "session-1", "run-1", Context("decision-1")), CancellationToken.None);
        await Task.Delay(5);
        var second = await channel.PushAsync(new DecisionRequest("in-flight-2", 2, "session-1", "run-1", Context("decision-2")), CancellationToken.None);
        Assert.Null(second.Decision);
        Assert.Equal("one-in-flight", second.Error);
        Assert.NotNull((await first).Decision);
    }

    [Theory]
    [MemberData(nameof(CancellationIgnoringRealizations))]
    public async Task Push_Cancellation_Is_Prompt_When_Peer_Ignores_Cancellation(
        string _, Func<(IDecisionChannel Channel, DeterministicDshPeer Peer)> factory)
    {
        var (channel, peer) = factory();
        await using (channel)
        {
            await channel.AttachAsync(ProductHandshake.CreateRequest("session-1", "run-1"), CancellationToken.None);
            using var cancellation = new CancellationTokenSource();
            var request = new DecisionRequest("cancel-in-flight", 1, "session-1", "run-1", Context("decision-cancel"));

            var push = channel.PushAsync(request, cancellation.Token);
            await peer.DecisionRequestStarted.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            var response = await push.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Null(response.Decision);
            Assert.Equal("turn-aborted", response.Error);
            peer.ReleaseDecisionResponse();
            await peer.DecisionResponseReturned.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Theory]
    [MemberData(nameof(DelayedRealizations))]
    public async Task Timeout_Drops_Late_Response_Across_Realizations(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        await using var adapter = new DshAgentAdapter(channel, "session-1", "run-1", TimeSpan.FromMilliseconds(10));
        var result = await adapter.ConsultAsync(Context("decision-timeout"));
        Assert.Null(result);
        await EventuallyAsync(
            () => adapter.Diagnostics.Any(diagnostic => diagnostic.Code == "late-response"),
            TimeSpan.FromSeconds(2));
        Assert.Contains(adapter.Diagnostics, diagnostic => diagnostic.Code == "timeout");
        Assert.Contains(adapter.Diagnostics, diagnostic => diagnostic.Code == "late-response");
    }

    [Theory]
    [MemberData(nameof(DelayedRealizations))]
    public async Task Abort_Is_Mechanical_Across_Realizations(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        await using var adapter = new DshAgentAdapter(channel, "session-1", "run-1", TimeSpan.FromSeconds(1));
        var task = adapter.ConsultAsync(Context("decision-abort"));
        await EventuallyAsync(() => adapter.SemanticConsultationsStarted == 1, TimeSpan.FromSeconds(2));
        adapter.AbortCurrentTurn();
        Assert.Null(await task);
        await EventuallyAsync(
            () => adapter.Diagnostics.Any(diagnostic => diagnostic.Code == "late-response"),
            TimeSpan.FromSeconds(2));
        Assert.Contains(adapter.Diagnostics, diagnostic => diagnostic.Code == "turn-aborted");
        Assert.Contains(adapter.Diagnostics, diagnostic => diagnostic.Code == "late-response");
    }

    private static string ReplayFile() { var p = Path.Combine(Path.GetTempPath(), "agt002-test-replay.json"); File.WriteAllText(p, "{\"decision-1\":{\"kind\":\"noAction\",\"decisionId\":\"decision-1\",\"proposal\":{\"decisionId\":\"decision-1\",\"justification\":\"ok\"}}}"); return p; }
    private static DshProcessOptions StdioOptions() => new("node", Path.Combine(FindRoot(), "tests", "UniClaw.Agent.Dsh.Tests", "Fixtures", "product-sidecar.mjs"), FindRoot(), new Dictionary<string,string> { ["UNICLAW_SCHEMA_HASH"] = ProductProtocolSchema.Current.SchemaHash, ["DSH_REPLAY_FILE"] = ReplayFile() });
    private static string FindRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) d = d.Parent; return d!.FullName; }
    private static DeterministicDshPeer RetryingPeer()
    {
        var attempt = 0;
        return new DeterministicDshPeer(Ok, handshake: request => ++attempt == 1
            ? new HandshakeResponse(true, request.Protocol with { SchemaHash = "mismatch" },
                request.ExpectedCapabilities, "dsh-test")
            : new HandshakeResponse(true, request.Protocol, request.ExpectedCapabilities, "dsh-test"));
    }

    private static async Task EventuallyAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("condition was not reached before the test window");
            await Task.Delay(10);
        }
    }

    private static DecisionChannelResponse Ok(DecisionRequest r) => new(r.RequestId, r.Generation, new AgentDecision.NoAction(new AgentNoActionProposal(r.Context.DecisionId, "ok")));
    private static IReadOnlyDictionary<string, string> ReplayPayload() => new Dictionary<string, string>
    {
        ["decision-1"] = "{\"kind\":\"noAction\",\"decisionId\":\"decision-1\",\"proposal\":{\"decisionId\":\"decision-1\",\"justification\":\"ok\"}}",
        ["decision-2"] = "{\"kind\":\"noAction\",\"decisionId\":\"decision-2\",\"proposal\":{\"decisionId\":\"decision-2\",\"justification\":\"ok\"}}",
        ["decision-3"] = "{\"kind\":\"noAction\",\"decisionId\":\"decision-3\",\"proposal\":{\"decisionId\":\"decision-3\",\"justification\":\"ok\"}}",
    };
    private static AgentDecisionContext Context(string id) => new(id, "run-1", "v0", "objective", new HashSet<string> { "tap" }, new Dictionary<string, ClaimSummary>(), Array.Empty<AgentObligationView>(), AgentDecisionPhase.InitialPlanning);
}
