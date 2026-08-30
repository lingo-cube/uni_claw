using System.Diagnostics;
using System.Net;
using System.Text;
using SkiaSharp;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Perception;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Observability;
using Xunit;

namespace UniClaw.Runtime.Tests.Vision;

[Collection("ObservabilityTraceEmitters")]
public sealed class PerceptionOperationalDiagnosticTests
{
    [Fact]
    public async Task FAIL01_OK_EMPTY_IsDistinguishableFromFailure()
    {
        var (result, activity) = await Analyze("{\"candidates\":[]}");
        Assert.Empty(result);
        Assert.Equal("OK_EMPTY", activity.GetTagItem("perception.outcome"));
        Assert.Null(activity.GetTagItem("perception.failure_class"));
    }

    [Fact]
    public async Task FAIL02_TransportFailure_IsInfrastructureFailure()
    {
        var (result, activity) = await Analyze("", HttpStatusCode.ServiceUnavailable);
        Assert.Empty(result);
        Assert.Equal("INFRASTRUCTURE_FAILURE", activity.GetTagItem("perception.failure_class"));
    }

    [Fact]
    public async Task FAIL03_MalformedJson_IsClassified()
    {
        var (result, activity) = await Analyze("not-json");
        Assert.Empty(result);
        Assert.Equal("MALFORMED_RESPONSE", activity.GetTagItem("perception.failure_class"));
    }

    [Fact]
    public async Task FAIL04_MissingCandidates_IsSchemaFailure()
    {
        var (result, activity) = await Analyze("{}");
        Assert.Empty(result);
        Assert.Equal("SCHEMA_FAILURE", activity.GetTagItem("perception.failure_class"));
    }

    [Fact]
    public async Task FAIL05_InvalidGeometry_IsRemovedAndDoesNotBecomeEvidence()
    {
        const string body = """
            {"candidates":[
              {"type":"toggle","text":"bad","bounds":{"x1":0.5,"y1":0.1,"x2":0.4,"y2":0.2}},
              {"type":"text","text":"good","bounds":{"x1":0.1,"y1":0.1,"x2":0.2,"y2":0.2}}
            ],"diagnostics":[{"code":"INVALID_GEOMETRY"}]}
            """;
        var (result, activity) = await Analyze(body);
        Assert.Single(result);
        Assert.Equal("good", result[0].Text);
        Assert.Equal("INVALID_GEOMETRY", activity.GetTagItem("perception.failure_class"));
    }

    [Fact]
    public async Task FAIL06_OlderResponseWithoutDiagnostics_RemainsCompatible()
    {
        var (result, activity) = await Analyze(
            "{\"candidates\":[{\"type\":\"text\",\"text\":\"ok\"}]}");
        Assert.Single(result);
        Assert.Equal("OK", activity.GetTagItem("perception.outcome"));
    }

    [Fact]
    public async Task STAGE01_DiagnosticOptIn_ForwardsHeaderAndRetainsStageViewsOnly()
    {
        HttpRequestMessage? seen = null;
        var source = new LocalVisionPerceptionSource(new HttpClient(
            new DelegateHandler((request, _) =>
            {
                seen = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"candidates\":[{\"type\":\"icon\",\"text\":\"\"}],\"stageViews\":{\"rawModelDetections\":[{\"rawLabel\":\"icon\",\"rawClassId\":7}]}}",
                        Encoding.UTF8, "application/json"),
                });
            })) { BaseAddress = new Uri("http://localhost") })
        {
            CaptureStageViews = true,
        };
        using var bitmap = new SKBitmap(1, 1);

        var result = await source.AnalyzeAsync(bitmap, 1, 1, CancellationToken.None);

        Assert.Single(result);
        Assert.NotNull(seen);
        Assert.True(seen!.Headers.TryGetValues("X-Capture-Stage-Views", out var values));
        Assert.Equal("true", Assert.Single(values));
        Assert.True(source.LastStageViews.HasValue);
        Assert.Equal("icon", source.LastStageViews!.Value
            .GetProperty("rawModelDetections")[0].GetProperty("rawLabel").GetString());
    }

    [Fact]
    public async Task STAGE02_DiagnosticOptIn_IsAbsentByDefaultAndDoesNotChangeCandidates()
    {
        HttpRequestMessage? seen = null;
        var source = new LocalVisionPerceptionSource(new HttpClient(
            new DelegateHandler((request, _) =>
            {
                seen = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"candidates\":[{\"type\":\"icon\",\"text\":\"\"}]}",
                        Encoding.UTF8, "application/json"),
                });
            })) { BaseAddress = new Uri("http://localhost") });
        using var bitmap = new SKBitmap(1, 1);

        var result = await source.AnalyzeAsync(bitmap, 1, 1, CancellationToken.None);

        Assert.Single(result);
        Assert.NotNull(seen);
        Assert.False(seen!.Headers.Contains("X-Capture-Stage-Views"));
        Assert.False(source.LastStageViews.HasValue);
    }

    [Fact]
    public async Task FAIL07_CallerCancellation_Rethrows()
    {
        var source = new LocalVisionPerceptionSource(new HttpClient(
            new DelegateHandler((_, ct) => Task.FromException<HttpResponseMessage>(
                new OperationCanceledException(ct))))
        { BaseAddress = new Uri("http://localhost") });
        using var bitmap = new SKBitmap(1, 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.AnalyzeAsync(bitmap, 1, 1, cancellation.Token));
    }

    private static async Task<(System.Collections.Immutable.ImmutableArray<PerceptionCandidate>, Activity)> Analyze(
        string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        using var recorder = new RuntimeTraceRecorder("perception-test", Guid.NewGuid().ToString("N"));
        using var activity = RuntimeObservability.StartSpan(
            "observe", ObservabilityLayer.Environment, ObservabilityComponent.EnvironmentObserve)!;
        var source = new LocalVisionPerceptionSource(new HttpClient(
            new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            }))) { BaseAddress = new Uri("http://localhost") });
        using var bitmap = new SKBitmap(1, 1);
        var result = await source.AnalyzeAsync(bitmap, 1, 1, CancellationToken.None);
        return (result, activity);
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
