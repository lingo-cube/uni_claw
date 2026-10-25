using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

public sealed class ReportDeliveryEndpointTests
{
    private sealed record RunningServer(WebApplication App, HttpClient Client);

    private static async Task<RunningServer> StartServer(RuntimeToolHost tools, string runsRoot)
    {
        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ReportDeliveryEndpointTests).Assembly.GetName().Name,
            EnvironmentName = "Development",
        });
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();
        ReportDeliveryEndpoints.Map(app, tools, runsRoot);
        await app.StartAsync();
        return new(app, new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") });
    }

    [Fact]
    public async Task ReportDeliveryRoutes_ExposeEmptyCollectionsAndNotFoundForUnknownDelivery()
    {
        var repoRoot = RuntimeToolHostTests.RepoRoot();
        var registry = Path.Combine(repoRoot, "tool-registry.yaml");
        var runsRoot = Directory.CreateTempSubdirectory("storage002-report-endpoints-");
        RunningServer? server = null;
        try
        {
            server = await StartServer(RuntimeToolHost.Load(registry), runsRoot.FullName);
            var client = server.Client;

            var collection = await client.GetAsync("/api/uniclaw-runtime/report-deliveries?ownerRef=runtime-run:run-1");
            Assert.Equal(HttpStatusCode.OK, collection.StatusCode);
            Assert.Equal("[]", await collection.Content.ReadAsStringAsync());

            var runCollection = await client.GetAsync("/api/uniclaw-runtime/runs/run-1/report-deliveries");
            Assert.Equal(HttpStatusCode.OK, runCollection.StatusCode);
            Assert.Equal("[]", await runCollection.Content.ReadAsStringAsync());

            var missing = await client.GetAsync("/api/uniclaw-runtime/report-deliveries/report-missing");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

            var retry = await client.PostAsync("/api/uniclaw-runtime/report-deliveries/report-missing/retry",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NotFound, retry.StatusCode);

            var artifact = await client.GetAsync(
                "/api/uniclaw-runtime/report-deliveries/report-missing/artifacts/report.json");
            Assert.Equal(HttpStatusCode.NotFound, artifact.StatusCode);
        }
        finally
        {
            if (server is not null)
            {
                server.Client.Dispose();
                await server.App.DisposeAsync();
            }
            runsRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DeliveredReport_CanBeQueriedReadRetriedAndRetriedAfterSourceRemoval()
    {
        var repoRoot = RuntimeToolHostTests.RepoRoot();
        var registry = Path.Combine(repoRoot, "tool-registry.yaml");
        var runsRoot = Directory.CreateTempSubdirectory("storage002-report-delivered-");
        RunningServer? server = null;
        try
        {
            const string runId = "run-report-endpoint-001";
            const string runDirName = "run-report-endpoint-001";
            var sourceDir = Path.Combine(runsRoot.FullName, runDirName);
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "facts.json"),
                """{"runId":"run-report-endpoint-001","status":"completed","terminal":"finalized"}""");

            var tools = RuntimeToolHost.Load(registry);
            var requirement = await tools.RequireReportAsync(runsRoot.FullName, "runtime-run:" + runId, "http-test");
            var generated = await tools.RetryRequiredReportAsync(runsRoot.FullName, requirement.DeliveryId, runDirName);
            Assert.True(generated.Ok, generated.Result.StderrTail);
            var initialJson = await File.ReadAllBytesAsync(Path.Combine(runsRoot.FullName, generated.Result.ReportJson!));

            server = await StartServer(RuntimeToolHost.Load(registry), runsRoot.FullName);
            var client = server.Client;
            var list = await client.GetAsync($"/api/uniclaw-runtime/report-deliveries?ownerRef=runtime-run:{runId}");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using (var listed = System.Text.Json.JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
                Assert.Equal(requirement.DeliveryId, listed.RootElement[0].GetProperty("deliveryId").GetString());

            var runList = await client.GetAsync($"/api/uniclaw-runtime/runs/{runId}/report-deliveries");
            Assert.Equal(HttpStatusCode.OK, runList.StatusCode);
            using (var listed = System.Text.Json.JsonDocument.Parse(await runList.Content.ReadAsStringAsync()))
                Assert.Single(listed.RootElement.EnumerateArray());

            var inspect = await client.GetAsync($"/api/uniclaw-runtime/report-deliveries/{requirement.DeliveryId}");
            Assert.Equal(HttpStatusCode.OK, inspect.StatusCode);
            var inspectionBody = await inspect.Content.ReadAsStringAsync();
            Assert.DoesNotContain(runsRoot.FullName, inspectionBody, StringComparison.Ordinal);
            using (var inspected = System.Text.Json.JsonDocument.Parse(inspectionBody))
            {
                var row = inspected.RootElement;
                Assert.Equal("Delivered", row.GetProperty("status").GetString());
                Assert.True(row.GetProperty("readable").GetBoolean());
                Assert.Equal("runtime-run:" + runId, row.GetProperty("ownerRef").GetString());
                Assert.Equal("http-test", row.GetProperty("requestId").GetString());
                Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("sourceFingerprint").GetString()));
                Assert.Contains(row.GetProperty("artifacts").EnumerateArray(), artifact =>
                    artifact.GetProperty("fileName").GetString() == "report.json");
            }

            var artifactPath = $"/api/uniclaw-runtime/report-deliveries/{requirement.DeliveryId}/artifacts/report.json";
            var artifact = await client.GetAsync(artifactPath);
            Assert.Equal(HttpStatusCode.OK, artifact.StatusCode);
            Assert.Contains(runId, await artifact.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var undeclared = await client.GetAsync(
                $"/api/uniclaw-runtime/report-deliveries/{requirement.DeliveryId}/artifacts/facts.json");
            Assert.Equal(HttpStatusCode.NotFound, undeclared.StatusCode);

            var retryPath = $"/api/uniclaw-runtime/report-deliveries/{requirement.DeliveryId}/retry";
            var repeat = await client.PostAsync(retryPath,
                new StringContent($"{{\"runDir\":\"{runDirName}\"}}", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
            var repeatArtifact = await client.GetAsync(artifactPath);
            Assert.Equal(initialJson, await repeatArtifact.Content.ReadAsByteArrayAsync());

            Directory.Delete(sourceDir, recursive: true);
            var afterSourceRemoval = await client.PostAsync(retryPath, content: null);
            Assert.Equal(HttpStatusCode.OK, afterSourceRemoval.StatusCode);
            var finalArtifact = await client.GetAsync(artifactPath);
            Assert.Equal(initialJson, await finalArtifact.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            if (server is not null)
            {
                server.Client.Dispose();
                await server.App.DisposeAsync();
            }
            runsRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task AwaitingInputs_IsInspectableAndRetryWithoutSourceReturnsBoundedConflict()
    {
        var repoRoot = RuntimeToolHostTests.RepoRoot();
        var registry = Path.Combine(repoRoot, "tool-registry.yaml");
        var runsRoot = Directory.CreateTempSubdirectory("storage002-report-awaiting-");
        RunningServer? server = null;
        try
        {
            var tools = RuntimeToolHost.Load(registry);
            var row = await tools.RequireReportAsync(runsRoot.FullName, "runtime-run:run-awaiting-001", "awaiting-test");
            server = await StartServer(RuntimeToolHost.Load(registry), runsRoot.FullName);
            var client = server.Client;

            var inspect = await client.GetAsync($"/api/uniclaw-runtime/report-deliveries/{row.DeliveryId}");
            Assert.Equal(HttpStatusCode.OK, inspect.StatusCode);
            using (var inspected = System.Text.Json.JsonDocument.Parse(await inspect.Content.ReadAsStringAsync()))
            {
                Assert.Equal("AwaitingInputs", inspected.RootElement.GetProperty("status").GetString());
                Assert.False(inspected.RootElement.GetProperty("readable").GetBoolean());
            }

            var retry = await client.PostAsync($"/api/uniclaw-runtime/report-deliveries/{row.DeliveryId}/retry", content: null);
            Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
            var body = await retry.Content.ReadAsStringAsync();
            Assert.Contains("AwaitingInputs", body, StringComparison.Ordinal);
            Assert.DoesNotContain(runsRoot.FullName, body, StringComparison.Ordinal);
        }
        finally
        {
            if (server is not null)
            {
                server.Client.Dispose();
                await server.App.DisposeAsync();
            }
            runsRoot.Delete(recursive: true);
        }
    }
}
