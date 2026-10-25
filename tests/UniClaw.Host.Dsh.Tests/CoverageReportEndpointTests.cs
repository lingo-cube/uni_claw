using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

public sealed class CoverageReportEndpointTests
{
    private sealed record RunningServer(WebApplication App, HttpClient Client);

    [Fact]
    public async Task CoverageEndpoints_UseCustomStoreExposeLogicalProtectionAndServeOriginalBytes()
    {
        var root = Directory.CreateTempSubdirectory("storage002-coverage-http-");
        var storage = new ReportDeliveryStorageOptions { StoreRelativePath = "product/nested/report-store" };
        RunningServer? server = null;
        try
        {
            const string runName = "coverage-run-http-001";
            Directory.CreateDirectory(Path.Combine(root.FullName, runName));
            var material = CoverageReportToolTests.Material();
            ReportDeliveryRecord accepted;
            await using (var delivery = new CoverageReportDelivery(root.FullName, new() { Storage = storage }))
            {
                accepted = await delivery.RequireAsync(runName, "http-coverage");
                var ready = await delivery.CaptureAsync(accepted.DeliveryId, material);
                Assert.Equal(ReportDeliveryStatus.Ready, ready.Record.Status);
            }

            Directory.Delete(Path.Combine(root.FullName, runName), recursive: true);
            var tools = RuntimeToolHost.LoadWithReportStorage(
                Path.Combine(RuntimeToolHostTests.RepoRoot(), "tool-registry.yaml"), storage);
            server = await StartServer(tools, root.FullName);

            var retry = await server.Client.PostAsync(
                $"/api/uniclaw-runtime/report-deliveries/{accepted.DeliveryId}/retry", content: null);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

            var inspect = await server.Client.GetAsync($"/api/uniclaw-runtime/report-deliveries/{accepted.DeliveryId}");
            Assert.Equal(HttpStatusCode.OK, inspect.StatusCode);
            var inspectionBody = await inspect.Content.ReadAsStringAsync();
            Assert.DoesNotContain(root.FullName, inspectionBody, StringComparison.Ordinal);
            using (var document = JsonDocument.Parse(inspectionBody))
            {
                Assert.Equal("coverage-report", document.RootElement.GetProperty("reportKind").GetString());
                Assert.True(document.RootElement.GetProperty("readable").GetBoolean());
            }

            var sources = await server.Client.GetAsync($"/api/uniclaw-runtime/report-deliveries/{accepted.DeliveryId}/sources");
            Assert.Equal(HttpStatusCode.OK, sources.StatusCode);
            var sourcesBody = await sources.Content.ReadAsStringAsync();
            Assert.DoesNotContain(root.FullName, sourcesBody, StringComparison.Ordinal);
            using (var document = JsonDocument.Parse(sourcesBody))
            {
                Assert.Contains(document.RootElement.GetProperty("protectedSources").EnumerateArray(), item =>
                    item.GetProperty("relativePath").GetString() == CoverageReportDelivery.SourceFileName);
                Assert.All(document.RootElement.GetProperty("protectedSources").EnumerateArray().ToArray(), item =>
                    Assert.False(Path.IsPathRooted(item.GetProperty("relativePath").GetString()!)));
            }

            var artifact = await server.Client.GetAsync(
                $"/api/uniclaw-runtime/report-deliveries/{accepted.DeliveryId}/artifacts/{CoverageReportDelivery.ReportFileName}");
            Assert.Equal(HttpStatusCode.OK, artifact.StatusCode);
            var bytes = await artifact.Content.ReadAsByteArrayAsync();
            Assert.Equal(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material.ReportJson)), SHA256.HashData(bytes));
            Assert.False(Directory.Exists(Path.Combine(root.FullName, ".required-reports")));
        }
        finally
        {
            if (server is not null)
            {
                server.Client.Dispose();
                await server.App.DisposeAsync();
            }
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task CoverageArtifactTampering_MakesDeliveryUnreadableAndArtifactUnavailable()
    {
        var root = Directory.CreateTempSubdirectory("storage002-coverage-tamper-");
        var storage = new ReportDeliveryStorageOptions { StoreRelativePath = "product/nested/report-store" };
        RunningServer? server = null;
        try
        {
            const string runName = "coverage-run-tamper-001";
            Directory.CreateDirectory(Path.Combine(root.FullName, runName));
            var material = CoverageReportToolTests.Material();
            ReportDeliveryRecord accepted;
            await using (var delivery = new CoverageReportDelivery(root.FullName, new() { Storage = storage }))
            {
                accepted = await delivery.RequireAsync(runName);
                await delivery.CaptureAsync(accepted.DeliveryId, material);
            }

            var tools = RuntimeToolHost.LoadWithReportStorage(
                Path.Combine(RuntimeToolHostTests.RepoRoot(), "tool-registry.yaml"), storage);
            var delivered = await tools.RetryRequiredReportAsync(root.FullName, accepted.DeliveryId);
            Assert.True(delivered.Ok, delivered.Result.StderrTail);
            await File.WriteAllTextAsync(Path.Combine(root.FullName, delivered.Result.ReportJson!), "tampered");

            server = await StartServer(RuntimeToolHost.LoadWithReportStorage(
                Path.Combine(RuntimeToolHostTests.RepoRoot(), "tool-registry.yaml"), storage), root.FullName);
            var inspect = await server.Client.GetAsync($"/api/uniclaw-runtime/report-deliveries/{accepted.DeliveryId}");
            Assert.Equal(HttpStatusCode.OK, inspect.StatusCode);
            using (var document = JsonDocument.Parse(await inspect.Content.ReadAsStringAsync()))
                Assert.False(document.RootElement.GetProperty("readable").GetBoolean());

            var artifact = await server.Client.GetAsync(
                $"/api/uniclaw-runtime/report-deliveries/{accepted.DeliveryId}/artifacts/{CoverageReportDelivery.ReportFileName}");
            Assert.Equal(HttpStatusCode.NotFound, artifact.StatusCode);
        }
        finally
        {
            if (server is not null)
            {
                server.Client.Dispose();
                await server.App.DisposeAsync();
            }
            root.Delete(recursive: true);
        }
    }

    private static async Task<RunningServer> StartServer(RuntimeToolHost tools, string root)
    {
        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(CoverageReportEndpointTests).Assembly.GetName().Name,
            EnvironmentName = "Development",
        });
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();
        ReportDeliveryEndpoints.Map(app, tools, root);
        await app.StartAsync();
        return new(app, new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") });
    }
}
