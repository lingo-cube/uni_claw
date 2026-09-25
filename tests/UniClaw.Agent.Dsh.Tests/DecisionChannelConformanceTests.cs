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
    [MemberData(nameof(DelayedRealizations))]
    public async Task Timeout_Drops_Late_Response_Across_Realizations(string _, Func<IDecisionChannel> factory)
    {
        await using var channel = factory();
        await using var adapter = new DshAgentAdapter(channel, "session-1", "run-1", TimeSpan.FromMilliseconds(10));
        var result = await adapter.ConsultAsync(Context("decision-timeout"));
        await Task.Delay(100);
        Assert.Null(result);
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
        await Task.Delay(5);
        adapter.AbortCurrentTurn();
        Assert.Null(await task);
        await Task.Delay(100);
        Assert.Contains(adapter.Diagnostics, diagnostic => diagnostic.Code == "turn-aborted");
        Assert.Contains(adapter.Diagnostics, diagnostic => diagnostic.Code == "late-response");
    }

    private static string ReplayFile() { var p = Path.Combine(Path.GetTempPath(), "agt002-test-replay.json"); File.WriteAllText(p, "{\"decision-1\":{\"kind\":\"noAction\",\"decisionId\":\"decision-1\",\"proposal\":{\"decisionId\":\"decision-1\",\"justification\":\"ok\"}}}"); return p; }
    private static DshProcessOptions StdioOptions() => new("node", Path.Combine(FindRoot(), "tests", "UniClaw.Agent.Dsh.Tests", "Fixtures", "product-sidecar.mjs"), FindRoot(), new Dictionary<string,string> { ["UNICLAW_SCHEMA_HASH"] = ProductProtocolSchema.Current.SchemaHash, ["DSH_REPLAY_FILE"] = ReplayFile() });
    private static string FindRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) d = d.Parent; return d!.FullName; }
    private static DecisionChannelResponse Ok(DecisionRequest r) => new(r.RequestId, r.Generation, new AgentDecision.NoAction(new AgentNoActionProposal(r.Context.DecisionId, "ok")));
    private static IReadOnlyDictionary<string, string> ReplayPayload() => new Dictionary<string, string>
    {
        ["decision-1"] = "{\"kind\":\"noAction\",\"decisionId\":\"decision-1\",\"proposal\":{\"decisionId\":\"decision-1\",\"justification\":\"ok\"}}",
        ["decision-2"] = "{\"kind\":\"noAction\",\"decisionId\":\"decision-2\",\"proposal\":{\"decisionId\":\"decision-2\",\"justification\":\"ok\"}}",
        ["decision-3"] = "{\"kind\":\"noAction\",\"decisionId\":\"decision-3\",\"proposal\":{\"decisionId\":\"decision-3\",\"justification\":\"ok\"}}",
    };
    private static AgentDecisionContext Context(string id) => new(id, "run-1", "v0", "objective", new HashSet<string> { "tap" }, new Dictionary<string, ClaimSummary>(), Array.Empty<AgentObligationView>(), AgentDecisionPhase.InitialPlanning);
}
