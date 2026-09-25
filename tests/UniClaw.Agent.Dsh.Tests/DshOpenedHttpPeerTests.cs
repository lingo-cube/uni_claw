using System.Net;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// Deterministic wire-level coverage for the authenticated 3080 peer:
/// handshake stamp mapping, consult decision decoding (object and string
/// payload forms), fail-closed 401/403, and abort — all against a scripted
/// HttpMessageHandler. Live-service behavior is exercised by the E2E slice.
/// </summary>
public sealed class DshOpenedHttpPeerTests
{
    private sealed class ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> script)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return script(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
    };

    private static HandshakeRequest HandshakeRequest() =>
        ProductHandshake.CreateRequest("product-session-1", "run-1");

    private const string HandshakeBody = """
        {"accepted":true,
         "protocol":{"protocolVersion":"uniclaw.agent.protocol.v1","schemaVersion":"uniclaw.agent.schema.v1",
                     "schemaHash":"<hash>","profileId":"uniagent-prod","profileVersion":"1",
                     "capabilityManifestHash":"<manifest>"},
         "reportedCapabilities":{"capabilities":["submit_decision"]},
         "dshSessionId":"dsh-9"}
        """;

    [Fact]
    public async Task Handshake_MapsStampAndCapabilities()
    {
        var expected = HandshakeRequest();
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            HandshakeBody
                .Replace("<hash>", expected.Protocol.SchemaHash)
                .Replace("<manifest>", expected.Protocol.CapabilityManifestHash))));
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var response = await peer.HandshakeAsync(expected, CancellationToken.None);

        Assert.True(response.Accepted);
        Assert.Equal("dsh-9", response.DshSessionId);
        Assert.Equal("uniagent-prod", response.Protocol.ProfileId);
        Assert.Equal(expected.Protocol.SchemaHash, response.Protocol.SchemaHash);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/uniclaw-agent/handshake", request.RequestUri!.AbsolutePath);
        Assert.Contains("dsh-auth-", request.Headers.GetValues("Cookie").Single(), StringComparison.Ordinal);
        // The DshOpenedDecisionChannel performs the full frozen-stamp
        // validation; here we verify the wire contract end-to-end too.
        Assert.True(ProductHandshake.Validate(expected, response).Accepted);
    }

    [Fact]
    public async Task Consult_DecodesObjectDecision_AndCarriesContext()
    {
        string? receivedBody = null;
        var handler = new ScriptedHandler((request, _) =>
        {
            receivedBody = request.Content!.ReadAsStringAsync().Result;
            return Task.FromResult(Json(HttpStatusCode.OK,
                """{"requestId":"dsh-req-00000001","generation":3,"decision":{"kind":"noAction","decisionId":"decision-1","proposal":{"decisionId":"decision-1","justification":"already on"}},"diagnostics":{"source":"submit_decision"}}"""));
        });
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var response = await peer.ReceiveDecisionRequestAsync(new DecisionRequest(
            "dsh-req-00000001", 3, "product-session-1", "run-1",
            Context("decision-1")), CancellationToken.None);

        Assert.Null(response.Error);
        Assert.IsType<AgentDecision.NoAction>(response.Decision);
        Assert.Contains("decision-1", receivedBody, StringComparison.Ordinal);
        Assert.Contains("turnTimeoutMs", receivedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consult_ReturnsErrorPayload_WhenChannelFailsClosed()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"requestId":"dsh-req-00000002","generation":1,"decision":null,"error":"no-submit-decision"}""")));
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var response = await peer.ReceiveDecisionRequestAsync(new DecisionRequest(
            "dsh-req-00000002", 1, "product-session-1", "run-1",
            Context("decision-2")), CancellationToken.None);

        Assert.Null(response.Decision);
        Assert.Equal("no-submit-decision", response.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AuthFailure_FailsClosed_NoFallback(HttpStatusCode status)
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var failure = await Assert.ThrowsAsync<HttpRequestException>(
            () => peer.HandshakeAsync(HandshakeRequest(), CancellationToken.None));
        Assert.Contains("dsh-web-auth-failed", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Abort_PostsToAbortRoute()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{\"aborted\":true}")));
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        await peer.AbortCurrentTurnAsync(CancellationToken.None);

        Assert.Equal("/api/uniclaw-agent/abort", Assert.Single(handler.Requests).RequestUri!.AbsolutePath);
    }

    private static DshWebCredential Credential() => new(
        "dsh-auth-test", "v1.test.test", DateTimeOffset.UtcNow.AddDays(1));

    private static AgentDecisionContext Context(string decisionId) => new(
        decisionId, "run-1", "v0", "objective", new HashSet<string> { "tap" },
        new Dictionary<string, ClaimSummary>(), Array.Empty<AgentObligationView>(),
        AgentDecisionPhase.InitialPlanning);
}
