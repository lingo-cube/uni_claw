using System.Net;
using System.Text;
using UniClaw.Host.Perception;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class JevSemanticPerceptionTests
{
    [Theory]
    [InlineData("http")]
    [InlineData("schema")]
    [InlineData("model")]
    [InlineData("choice")]
    [InlineData("confidence")]
    public async Task ProviderFailuresProduceNoObservationOrProjection(string variant)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(variant switch
        {
            "http" => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            "schema" => Json("{}"),
            "model" => Json("""{"model":"other/model","answers":{}}"""),
            "choice" => Json("""{"model":"typesafe/jev","answers":{"page":{"type":"choice","choice":"invented"}}}"""),
            _ => Json("""{"model":"typesafe/jev","answers":{"page":{"type":"choice","choice":"notifications","confidence":2}}}"""),
        })));
        await using var adapter = new JevSemanticPerceptionAdapter(Models(), new(new("https://example.test/systemone"), "private-test-key"), http);
        var result = await adapter.CreateCapability().ObserveSemanticsAsync(Request());
        Assert.Null(result.Observation);
        Assert.Empty(SemanticObservationProjector.CreateProposals(Request(), result));
        Assert.NotEqual(CapabilityStatus.Completed, result.Status);
        Assert.Equal(HealthStatus.Degraded, adapter.CheckHealth().Status);
        Assert.DoesNotContain("private-test-key", result.Assessment.Diagnostic);
    }

    [Fact]
    public async Task MissingConfigurationAndDisposedAdapterDoNotCallTransport()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => { calls++; throw new InvalidOperationException("must not call"); }));
        await using var missing = new JevSemanticPerceptionAdapter(Models(), new(new("https://example.test/systemone"), null), http);
        Assert.Equal(HealthStatus.Unhealthy, missing.CheckHealth().Status);
        Assert.Equal(CapabilityStatus.Unavailable, (await missing.CreateCapability().ObserveSemanticsAsync(Request())).Status);
        await using var disposed = new JevSemanticPerceptionAdapter(Models(), new(new("https://example.test/systemone"), "key"), http);
        await disposed.DisposeAsync();
        Assert.Equal(CapabilityStatus.Unavailable, (await disposed.CreateCapability().ObserveSemanticsAsync(Request())).Status);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task JevAndDeterministicSupplierUseTheSameRegisteredBuyerWithoutProviderBranches()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json("""{"model":"typesafe/jev","answers":{"page":{"type":"choice","choice":"notifications"},"title":{"type":"choice","choice":"title"}}}"""))));
        await using var adapter = new JevSemanticPerceptionAdapter(Models(), new(new("https://example.test/systemone"), "key"), http);
        var alternate = new UniPerceptionCapability().WithSemanticExecutor((request, _) =>
            ValueTask.FromResult(SemanticPerceptionResult.Observed(request, "notifications", "App notifications")));
        async Task<string> Buyer(UniPerceptionCapability supplied)
        {
            var registry = PerceptionCapabilityComposition.RegisterProductPerception(uniPerception: supplied);
            var capability = (ISemanticPerception)registry.Resolve("uni.perception")!;
            var result = await capability.ObserveSemanticsAsync(Request());
            return Assert.Single(SemanticObservationProjector.CreateProposals(Request(), result)).Claim.Value;
        }
        Assert.Equal(await Buyer(alternate), await Buyer(adapter.CreateCapability()));
    }

    [Fact]
    public async Task PreviousObservationRecordedActionAndExpectedIntentAreDistinctInputFields()
    {
        string? body = null;
        using var http = new HttpClient(new Handler(async message =>
        {
            body = await message.Content!.ReadAsStringAsync();
            return Json("""{"model":"typesafe/jev","answers":{"page":{"type":"choice","choice":"notifications"},"title":{"type":"choice","choice":"title"},"relation":{"type":"choice","choice":"different_page"},"expected":{"type":"choice","choice":"match"}}}""");
        }));
        await using var adapter = new JevSemanticPerceptionAdapter(Models(), new(new("https://example.test/systemone"), "key"), http);
        var request = Request() with
        {
            Previous = Request("previous", "previous-cycle").Current with { PageName = "Notifications" },
            Action = new("tap", "navigation-row", "App notifications", "previous", "recorded-ocr-and-task-description", X: 100, Y: 500),
            Intent = new("notifications", "查看应用通知列表"),
        };
        var result = await adapter.CreateCapability().ObserveSemanticsAsync(request);
        Assert.Equal(SemanticPageRelation.DifferentPage, result.Relation);
        Assert.Equal(SemanticExpectedMatch.Match, result.ExpectedMatch);
        using var json = System.Text.Json.JsonDocument.Parse(body!);
        var state = json.RootElement.GetProperty("state");
        Assert.Equal("Notifications", state.GetProperty("previousPageDescription").GetProperty("pageName").GetString());
        Assert.False(state.GetProperty("current").TryGetProperty("pageName", out _));
        Assert.False(state.GetProperty("current").TryGetProperty("pagePurpose", out _));
        Assert.False(state.GetProperty("current").TryGetProperty("pageKey", out _));
        Assert.Equal("navigation-row", state.GetProperty("lastAction").GetProperty("elementType").GetString());
        Assert.Equal("caller-intent-not-observation", state.GetProperty("expectedPage").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task RegisteredCapabilityUsesJevAndRetainsObservedPageWhenIntentContradictsIt()
    {
        string? requestBody = null;
        using var http = new HttpClient(new Handler(async message =>
        {
            Assert.Equal("Bearer", message.Headers.Authorization?.Scheme);
            requestBody = await message.Content!.ReadAsStringAsync();
            return Json("""{"model":"typesafe/jev","answers":{"page":{"type":"choice","choice":"notifications","confidence":0.83},"title":{"type":"choice","choice":"title"},"expected":{"type":"choice","choice":"match"}}}""");
        }));
        await using var adapter = new JevSemanticPerceptionAdapter(Models(),
            new(new Uri("https://example.test/systemone"), "private-test-key"), http);
        var registry = PerceptionCapabilityComposition.RegisterProductPerception(uniPerception: adapter.CreateCapability());
        var capability = Assert.IsAssignableFrom<ISemanticPerception>(registry.Resolve("uni.perception"));
        var result = await capability.ObserveSemanticsAsync(Request() with { Intent = new("unused", "查看闲置应用列表") });
        Assert.Equal("notifications", result.Observation!.PageKey);
        Assert.Equal("App notifications", result.Observation.ObservedTitle);
        Assert.Equal(SemanticExpectedMatch.Conflicted, result.ExpectedMatch);
        Assert.Contains("expectedPage", requestBody);
        Assert.DoesNotContain("private-test-key", requestBody);
        Assert.Equal(HealthStatus.Healthy, adapter.CheckHealth().Status);
    }

    internal static ModelManagement Models() => new(new[]
    {
        new ModelBindingSnapshot(LogicalProfileId.Text, "commandcode", "typesafe/jev", Available: true, Health: true)
    });

    internal static SemanticPerceptionRequest Request(string capture = "capture-a", string cycle = "cycle-a") =>
        new("request-" + capture, new CapabilityCorrelation("run-a", captureId: capture, observationCycleId: cycle),
            new SemanticObservationContext(capture, cycle, "run-a", DateTimeOffset.Parse("2026-10-21T00:00:00Z"),
                CoordinateSpace.DeviceViewport(1080, 1920, captureId: capture),
                new[] { new SemanticLayoutItem("title", "text", "App notifications", new UiBounds(60, 418, 776, 519)) },
                new[] { "ev-source" }),
            new[] { new SemanticPageCandidate("notifications", "App notifications", "按应用查看通知列表"),
                    new SemanticPageCandidate("unused", "Unused apps", "未使用应用列表") });

    internal static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    internal sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request);
    }
}
