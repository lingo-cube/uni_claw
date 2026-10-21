using System.Net;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;
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
         "artifacts":{"promptHash":"<prompt>",
                      "skill":{"name":"android-automotive-ui-testing","revision":4,
                               "sha256":"3271ed614ae6dc590b6f4c454fc1251b8976044c92a2a5556ce773fbca0aa21a"}},
         "dshSessionId":"dsh-9"}
        """;

    [Fact]
    public async Task Handshake_MapsStampAndCapabilities()
    {
        var expected = HandshakeRequest();
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            HandshakeBody
                .Replace("<hash>", expected.Protocol.SchemaHash)
                .Replace("<manifest>", expected.Protocol.CapabilityManifestHash)
                .Replace("<prompt>", UniAgentProfileYaml.LoadDefault().Assembly!.PromptManifest!.Hash))));
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

    // AGT-019/RUN-006 修复：slow 解析归一对象错误封包（fail() 的
    // {code,message} 形状此前被吞成 slow-endpoint-empty-text）。

    [Fact]
    public async Task Slow_HighIsOnRequest_AndCancellationAbortsOnlyItsRequestId()
    {
        using var cancelled = new CancellationTokenSource();
        var aborted = false;
        var handler = new ScriptedHandler(async (request, _) =>
        {
            using var body = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            if (request.RequestUri!.AbsolutePath.EndsWith("/slow"))
            {
                Assert.Equal("high", body.RootElement.GetProperty("reasoningEffort").GetString());
                cancelled.Cancel();
                throw new OperationCanceledException(cancelled.Token);
            }
            Assert.EndsWith("/abort", request.RequestUri.AbsolutePath);
            Assert.Equal("slow-high-cancel", body.RootElement.GetProperty("slowRequestId").GetString());
            aborted = true;
            return Json(HttpStatusCode.OK, "{\"aborted\":true}");
        });
        await using var peer = new DshOpenedHttpPeer(new(new Uri("http://127.0.0.1:3081/")),
            credential: Credential(), handler: handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => peer.ExecuteSlowAsync(
            "slow-high-cancel", "prompt", new("configured", "model"), cancellationToken: cancelled.Token,
            reasoningEffort: "high"));
        Assert.True(aborted);
    }

    [Fact]
    public async Task Slow_NormalizesObjectErrorEnvelope()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"requestId":"slow-1","text":null,"error":{"code":"dsh-session-create-failed","message":"agentPreset uniclaw-slow not found"},"diagnostics":{}}""")));
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var response = await peer.ExecuteSlowAsync(
            "slow-1", "prompt", new ModelConfiguration("zai-coding-cn", "glm-5.3-flash"),
            imagePng: null, cancellationToken: CancellationToken.None);

        Assert.Null(response.Text);
        Assert.Contains("dsh-session-create-failed", response.Error, StringComparison.Ordinal);
        Assert.Contains("uniclaw-slow", response.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Slow_KeepsStringErrorEnvelope()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"requestId":"slow-2","text":null,"error":"no-slow-result","diagnostics":{}}""")));
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var response = await peer.ExecuteSlowAsync(
            "slow-2", "prompt", new ModelConfiguration("zai-coding-cn", "glm-5.3-flash"),
            imagePng: null, cancellationToken: CancellationToken.None);

        Assert.Equal("no-slow-result", response.Error);
    }

    [Fact]
    public async Task Consult_DecodesObjectDecision_AndCarriesContext()
    {
        string? receivedBody = null;
        var handler = new ScriptedHandler(async (request, _) =>
        {
            receivedBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK,
                """{"requestId":"dsh-req-00000001","generation":3,"task":{"initialization":{},"payload":{"kind":"noAction","decisionId":"decision-1","proposal":{"decisionId":"decision-1","justification":"already on"}}},"diagnostics":{"source":"submit_decision"}}""");
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
    public async Task Consult_SerializesHeartbeatReferenceIndexAndCasePacket()
    {
        string? receivedBody = null;
        var handler = new ScriptedHandler(async (request, _) =>
        {
            receivedBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK,
                """{"requestId":"dsh-req-context","generation":1,"task":{"initialization":{},"payload":{"kind":"noAction","decisionId":"decision-context","proposal":{"decisionId":"decision-context","justification":"inspect"}}}}""");
        });
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var heartbeat = new Heartbeat(
            new HeartbeatWorldModel("rev-1", "settings", "settings", null, "wifi-dialog", 7, "capture-7"),
            new HeartbeatKernel(AgentDecisionPhase.StepVerified, "tap Wi-Fi", "receipt-1", "verified", Array.Empty<AgentObligationView>(), true),
            new HeartbeatTraversal("wifi-name", new[] { "wifi-name" }, 0, 0, 1, new[] { "opened Wi-Fi" }, new[] { "tap Wi-Fi" }),
            7, 8,
            new[] { new HeartbeatEvent(8, "PopupStateObserved", "Wi-Fi dialog observed", "wifi-name") }, 0);
        var reference = new ContextReferenceEntry(
            "event-8", "event", "CoverageLedger", 8, "capture-7", "fresh", "run-1/event-8",
            "Wi-Fi dialog observation", "CoverageLedger", "observed", "agent", "none", "read", 4096, "available");
        var packet = new CasePacket(
            "coverage-stagnant", new[] { "CoverageStagnantSignal" }, heartbeat,
            heartbeat.EventDelta, new[] { reference }, new[] { "replan", "skip" }, new[] { "zero-effect-until-verified" });
        var context = Context("decision-context") with
        {
            Heartbeat = heartbeat,
            ContextReferenceIndex = new[] { reference },
            CasePacket = packet,
        };

        var response = await peer.ReceiveDecisionRequestAsync(new DecisionRequest(
            "dsh-req-context", 1, "product-session-1", "run-1", context), CancellationToken.None);

        Assert.Null(response.Error);
        Assert.Contains("\"heartbeat\"", receivedBody, StringComparison.Ordinal);
        Assert.Contains("\"contextReferenceIndex\"", receivedBody, StringComparison.Ordinal);
        Assert.Contains("\"casePacket\"", receivedBody, StringComparison.Ordinal);
        Assert.Contains("Wi-Fi dialog observation", receivedBody, StringComparison.Ordinal);
        Assert.Contains("coverage-stagnant", receivedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consult_DecodesInitialCapabilitySelection_OutsideDecision()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
                """{"requestId":"dsh-req-00000001","generation":3,"task":{"initialization":{"capabilitySelection":{"capabilityId":"runtime.language-inspector","expectedLanguage":"en","ignoreRoutes":["route:ignored"]}},"payload":{"kind":"noAction","decisionId":"decision-1","proposal":{"decisionId":"decision-1","justification":"already on"}}}}""")));
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var response = await peer.ReceiveDecisionRequestAsync(new DecisionRequest(
            "dsh-req-00000001", 3, "product-session-1", "run-1",
            Context("decision-1")), CancellationToken.None);

        Assert.Equal("runtime.language-inspector", response.TaskInitialization?.CapabilitySelection?.CapabilityId);
        Assert.Equal("en", response.TaskInitialization?.CapabilitySelection?.ExpectedLanguage);
        Assert.Equal(new[] { "route:ignored" }, response.TaskInitialization?.CapabilitySelection?.IgnoreRoutes);
        Assert.IsType<AgentDecision.NoAction>(response.Decision);
    }

    [Fact]
    public async Task Consult_RejectsTopLevelCapabilitySelection()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"requestId":"dsh-req-00000001","generation":3,"capabilitySelection":{"capabilityId":"runtime.language-inspector"},"task":{"initialization":{},"payload":{"kind":"noAction","decisionId":"decision-1","proposal":{"decisionId":"decision-1","justification":"already on"}}}}""")));
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var response = await peer.ReceiveDecisionRequestAsync(new DecisionRequest(
            "dsh-req-00000001", 3, "product-session-1", "run-1", Context("decision-1")), CancellationToken.None);

        Assert.Null(response.Decision);
        Assert.Contains("top-level capabilitySelection", response.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consult_AfterHandshake_ReusesReturnedDshSession()
    {
        var expected = HandshakeRequest();
        string? consultBody = null;
        var handler = new ScriptedHandler(async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/handshake", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK,
                    HandshakeBody
                        .Replace("<hash>", expected.Protocol.SchemaHash)
                        .Replace("<manifest>", expected.Protocol.CapabilityManifestHash)
                .Replace("<prompt>", UniAgentProfileYaml.LoadDefault().Assembly!.PromptManifest!.Hash));
            }

            consultBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK,
                """{"requestId":"dsh-req-00000001","generation":1,"decision":{"kind":"noAction","decisionId":"decision-1","proposal":{"decisionId":"decision-1","justification":"already on"}}}""");
        });
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var attached = await peer.HandshakeAsync(expected, CancellationToken.None);
        Assert.True(attached.Accepted);
        await peer.ReceiveDecisionRequestAsync(new DecisionRequest(
            "dsh-req-00000001", 1, "product-session-1", "run-1",
            Context("decision-1")), CancellationToken.None);

        Assert.Contains("\"dshSessionId\":\"dsh-9\"", consultBody, StringComparison.Ordinal);
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

    [Fact]
    public async Task Consult_RetriesSelectedModelCapacity_OnSameRequest()
    {
        var attempts = 0;
        var handler = new ScriptedHandler((_, _) =>
        {
            attempts++;
            var body = attempts == 1
                ? "{\"requestId\":\"dsh-req-capacity\",\"generation\":1,\"decision\":null,\"error\":\"Selected model is at capacity. Please try a different model.\"}"
                : "{\"requestId\":\"dsh-req-capacity\",\"generation\":1,\"task\":{\"initialization\":{},\"payload\":{\"kind\":\"noAction\",\"decisionId\":\"decision-capacity\",\"proposal\":{\"decisionId\":\"decision-capacity\",\"justification\":\"done\"}}}}";
            return Task.FromResult(Json(HttpStatusCode.OK, body));
        });
        await using var peer = new DshOpenedHttpPeer(
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
            credential: Credential(), handler: handler);

        var response = await peer.ReceiveDecisionRequestAsync(new DecisionRequest(
            "dsh-req-capacity", 1, "product-session-1", "run-1",
            Context("decision-capacity")), CancellationToken.None);

        Assert.Null(response.Error);
        Assert.IsType<AgentDecision.NoAction>(response.Decision);
        Assert.Equal(2, attempts);
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
