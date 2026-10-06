using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.Fusion;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;
using UniClaw.Kernel.Capability;

namespace UniClaw.Kernel.Tests.Perception;

public sealed class OpenCodeSlowRealizationTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TextProbe_UsesExactBinding_AndEmitsStructuredProposal()
    {
        var request = Request(LogicalProfileId.Text);
        var handler = new SequenceHandler(_ => JsonResponse(200, ProviderEnvelope(
            "succeeded", new[] { new { subject = "wifi", value = "associated" } })));
        using var client = new HttpClient(handler);
        using var realization = NewRealization(client);

        var result = await realization.ExecuteAsync(request);

        Assert.Equal(SlowExecutionStatus.Succeeded, result.Status);
        var proposal = Assert.Single(result.Proposals);
        Assert.Equal("wifi", proposal.Claim.Subject);
        Assert.Contains("provider:zai-coding-cn", proposal.Provenance!.TransformationLineage);
        Assert.Contains("model:glm-5.3-flash", proposal.Provenance.TransformationLineage);
        Assert.Contains("glm-5.3-flash", handler.LastBody);
        Assert.Equal("zai-coding-cn", result.Binding!.ProviderId);
    }

    [Fact]
    public async Task RealTextProbe_UsesExistingP2Ingress()
    {
        var request = Request(LogicalProfileId.Text);
        var handler = new SequenceHandler(_ => JsonResponse(200, ProviderEnvelope(
            "succeeded", new[] { new { subject = "wifi", value = "associated" } })));
        using var client = new HttpClient(handler);
        using var realization = NewRealization(client);

        var result = await realization.ExecuteAsync(request);
        var ledger = new EvidenceLedger();
        var kernel = new UniKernel(ledger, new WorldModel(new HashSet<string> { "wifi" }),
            DisabledRunTrace.Instance);
        var admission = SlowResultProjector.Project(result, kernel);

        Assert.True(admission.Accepted);
        Assert.Single(ledger.CanonicalRecords);
    }

    [Fact]
    public async Task VisualProbe_ResolvesArtifactById_AndPreservesCaptureIdentity()
    {
        var request = Request(LogicalProfileId.Visual) with { RawArtifact = new RawArtifactRef("artifact-9") };
        string? resolved = null;
        var handler = new SequenceHandler(_ => JsonResponse(200, ProviderEnvelope(
            "succeeded", new[] { new { subject = "wifi", value = "visible" } })));
        using var client = new HttpClient(handler);
        using var realization = new OpenCodeSlowRealization(
            Models(), OpenCodeSlowRealizationOptions.Local(new Uri("http://127.0.0.1/")), client,
            (id, _) => { resolved = id; return ValueTask.FromResult<byte[]?>(new byte[] { 137, 80, 78, 71 }); });

        var result = await realization.ExecuteAsync(request);

        Assert.Equal("artifact-9", resolved);
        Assert.Equal("capture-1", result.Capture.CaptureId);
        Assert.NotEqual(result.Capture.CaptureId, request.RawArtifact!.ArtifactId);
        Assert.Contains("image_url", handler.LastBody);
        Assert.Contains("deepseek-v4-flash-vision-exp", handler.LastBody);
    }

    [Fact]
    public async Task RealVisionProbe_UsesExistingP2Ingress()
    {
        var request = Request(LogicalProfileId.Visual);
        var handler = new SequenceHandler(_ => JsonResponse(200, ProviderEnvelope(
            "succeeded", new[] { new { subject = "wifi", value = "visible" } })));
        using var client = new HttpClient(handler);
        using var realization = new OpenCodeSlowRealization(
            Models(), OpenCodeSlowRealizationOptions.Local(new Uri("http://127.0.0.1/")), client,
            (_, _) => ValueTask.FromResult<byte[]?>(new byte[] { 137, 80, 78, 71 }));

        var result = await realization.ExecuteAsync(request);
        var ledger = new EvidenceLedger();
        var kernel = new UniKernel(ledger, new WorldModel(new HashSet<string> { "wifi" }),
            DisabledRunTrace.Instance);

        var admission = SlowResultProjector.Project(result, kernel);

        Assert.True(admission.Accepted);
        Assert.Single(ledger.CanonicalRecords);
    }

    [Fact]
    public async Task CapacityResponse_IsRetriedWithDelay_ThenSucceeds()
    {
        var request = Request(LogicalProfileId.Text);
        var calls = 0;
        var handler = new SequenceHandler(_ => ++calls == 1
            ? JsonResponse(429, "capacity")
            : JsonResponse(200, ProviderEnvelope("succeeded", Array.Empty<object>())));
        using var client = new HttpClient(handler);
        using var realization = new OpenCodeSlowRealization(
            Models(), OpenCodeSlowRealizationOptions.Local(new Uri("http://127.0.0.1/")) with {
                CapacityRetryDelay = TimeSpan.Zero, MaxCapacityRetries = 1 }, client);

        var result = await realization.ExecuteAsync(request);

        Assert.Equal(SlowExecutionStatus.Succeeded, result.Status);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(1, realization.Invocations.Single().CapacityRetries);
    }

    [Fact]
    public async Task CapacityExhaustion_StopsWithZeroProposal()
    {
        var handler = new SequenceHandler(_ => JsonResponse(429, "capacity"));
        using var client = new HttpClient(handler);
        using var realization = new OpenCodeSlowRealization(
            Models(), OpenCodeSlowRealizationOptions.Local(new Uri("http://127.0.0.1/")) with {
                CapacityRetryDelay = TimeSpan.Zero, MaxCapacityRetries = 2 }, client);

        var result = await realization.ExecuteAsync(Request(LogicalProfileId.Text));

        Assert.Equal(SlowExecutionStatus.InfrastructureFailure, result.Status);
        Assert.Empty(result.Proposals);
        Assert.Equal(3, handler.CallCount);
        Assert.Contains("capacity exhausted", result.Diagnostic);
    }

    [Fact]
    public async Task FreeProse_IsMalformed_AndProducesZeroProposals()
    {
        var request = Request(LogicalProfileId.Text);
        var handler = new SequenceHandler(_ => JsonResponse(200, "I think Wi-Fi is associated."));
        using var client = new HttpClient(handler);
        using var realization = NewRealization(client);

        var result = await realization.ExecuteAsync(request);

        Assert.Equal(SlowExecutionStatus.MalformedResponse, result.Status);
        Assert.Empty(result.Proposals);
    }

    [Fact]
    public async Task UnavailableBinding_FailsClosedBeforeProviderCall()
    {
        var profile = LogicalProfileId.Text;
        var models = new ModelManagement(new[] {
            new ModelBindingSnapshot(profile, "zai-coding-cn", "glm-5.3-flash", Available: false)
        });
        var handler = new SequenceHandler(_ => JsonResponse(200, "{}"));
        using var client = new HttpClient(handler);
        using var realization = new OpenCodeSlowRealization(
            models, OpenCodeSlowRealizationOptions.Local(new Uri("http://127.0.0.1/")), client);

        var result = await realization.ExecuteAsync(Request(profile));

        Assert.Equal(SlowExecutionStatus.ModelUnavailable, result.Status);
        Assert.Equal(0, handler.CallCount);
        Assert.Contains("ROUTING_UNAVAILABLE", result.Diagnostic);
    }

    [Fact]
    public async Task ModelAudit_RequiresProviderCatalogueEvidence()
    {
        var handler = new SequenceHandler(_ => JsonResponse(200,
            "{\"data\":[{\"id\":\"glm-5.3-flash\"},{\"id\":\"deepseek-v4-flash-vision-exp\"}]}"));
        using var client = new HttpClient(handler);
        using var realization = NewRealization(client);

        var audit = await realization.AuditBindingsAsync(new[] {
            (LogicalProfileId.Text, "zai-coding-cn", "glm-5.3-flash"),
            (LogicalProfileId.Visual, "opencode", "deepseek-v4-flash-vision-exp"),
            (new LogicalProfileId("slow.unknown"), "opencode", "missing"),
        });

        Assert.True(audit[0].Available);
        Assert.True(audit[1].Available);
        Assert.False(audit[2].Available);
    }

    private static OpenCodeSlowRealization NewRealization(HttpClient client) =>
        new(Models(), OpenCodeSlowRealizationOptions.Local(new Uri("http://127.0.0.1/")), client);

    private static ModelManagement Models() => new(new[] {
        new ModelBindingSnapshot(LogicalProfileId.Text, "zai-coding-cn", "glm-5.3-flash", Available: true),
        new ModelBindingSnapshot(LogicalProfileId.Visual, "opencode", "deepseek-v4-flash-vision-exp", Available: true),
    });

    private static SlowPerceptionRequest Request(LogicalProfileId profile)
    {
        var claim = new RequiredClaim("wifi", "association");
        var capture = new FusionCapture("capture-1", "session-1", "cycle-1", CaptureTime);
        var context = EvidenceContextBuilder.Build(claim,
            semantic: new SemanticReasoningContext(claim, new[] { "ocr-1", "layout-1" }), budget: 4);
        return new SlowPerceptionRequest("request-1",
            new SlowAttemptKey("target-1", "cycle-1", claim, profile), claim, profile,
            "control", "association for Wi-Fi", capture, context,
            profile == LogicalProfileId.Visual ? new RawArtifactRef("artifact-1") : null,
            Models().Bindings.Single(x => x.LogicalProfile == profile));
    }

    private static string ProviderEnvelope(string status, IReadOnlyList<object> proposals) =>
        JsonSerializer.Serialize(new {
            id = "chatcmpl-test",
            choices = new[] { new { message = new {
                role = "assistant",
                content = JsonSerializer.Serialize(new { status, proposals })
            } } }
        });

    private static HttpResponseMessage JsonResponse(int status, string body)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        return response;
    }

    private sealed class SequenceHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;
        public int CallCount { get; private set; }
        public string LastBody { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "";
            return Task.FromResult(_responder(request));
        }
    }
}
