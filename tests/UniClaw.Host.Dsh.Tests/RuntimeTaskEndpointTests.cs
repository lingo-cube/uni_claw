using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using UniClaw.Host.Dsh;
using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

/// <summary>
/// RUNTIME-TASK-001 切片 1 — admission（POST /tasks + declaration）与只读
/// 查询（GET /tasks/{id}，定形 T4）HTTP 端点锚定测试。A6-②③④。
/// </summary>
public sealed class RuntimeTaskEndpointTests
{
    private const string DeclarationJson =
        """{"schemaVersion":"task-declaration.v1","taskRef":"task/android-settings/toggle-wifi-reuse","intentDigest":null,"testSet":{"id":"testset/android-settings","version":"default","sourceRevision":"repo-default"},"scenarios":[],"fixtures":[{"id":"fixture/android-settings/settings-root","digest":null}],"acceptanceRefs":[]}""";

    private sealed record RunningServer(WebApplication App, HttpClient Client, string StorePath, TaskInstanceStore Store);

    private static async Task<RunningServer> StartServer()
    {
        var root = Directory.CreateTempSubdirectory("runtime-task-endpoint-");
        var storePath = Path.Combine(root.FullName, ".identity", "task-instances.jsonl");
        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RuntimeTaskEndpointTests).Assembly.GetName().Name,
            EnvironmentName = "Development",
        });
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var store = new TaskInstanceStore(storePath);
        var app = builder.Build();
        RuntimeHttpServer.RuntimeTaskEndpoints.Map(app, store);
        await app.StartAsync();
        return new(app, new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") }, storePath, store);
    }

    private static JsonElement Body(JsonElement root) => root.Clone();

    /// <summary>append-only writer 持有写句柄时的 sharing-safe 行计数。</summary>
    private static int SharedLineCount(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var count = 0;
        while (reader.ReadLine() is not null) count += 1;
        return count;
    }

    [Fact]
    public async Task A6_4_CreateWithDeclaration_PersistsThenReturns200WithDeclarationAndFingerprint()
    {
        var server = await StartServer();
        try
        {
            var request = JsonDocument.Parse(
                $$"""{"taskRef":"task/android-settings/toggle-wifi-reuse","idempotencyKey":"idem-http-1","declaration":{{DeclarationJson}}}""");
            var response = await server.Client.PostAsJsonAsync("/api/uniclaw-runtime/tasks", request.RootElement);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(payload.GetProperty("ok").GetBoolean());
            var taskInstanceId = payload.GetProperty("taskInstanceId").GetString()!;
            Assert.StartsWith("ti-", taskInstanceId);
            Assert.Equal("task/android-settings/toggle-wifi-reuse", payload.GetProperty("declaration").GetProperty("taskRef").GetString());
            Assert.Equal(64, payload.GetProperty("declarationFingerprint").GetString()!.Length);

            // 先持久化后成功：JSONL 里已有该实例。
            string persisted;
            using (var stream = new FileStream(server.StorePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                persisted = reader.ReadToEnd();
            Assert.Contains(taskInstanceId, persisted);
            Assert.Contains("task-declaration.v1", persisted);

            // T4 查询端点：未产生 Run 的实例可查，逐字段投影。
            var get = await server.Client.GetAsync($"/api/uniclaw-runtime/tasks/{taskInstanceId}");
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var view = await get.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(view.GetProperty("ok").GetBoolean());
            Assert.Equal(taskInstanceId, view.GetProperty("taskInstanceId").GetString());
            Assert.Equal("task/android-settings/toggle-wifi-reuse", view.GetProperty("taskRef").GetString());
            Assert.Equal("task-declaration.v1", view.GetProperty("declaration").GetProperty("schemaVersion").GetString());
            Assert.Equal(payload.GetProperty("declarationFingerprint").GetString(), view.GetProperty("declarationFingerprint").GetString());
            Assert.Equal(JsonValueKind.Null, view.GetProperty("productSessionId").ValueKind);
            Assert.True(view.GetProperty("createdAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow);

            // 同 key 同声明重入 → 200 复用，不追加。
            var replay = await server.Client.PostAsJsonAsync("/api/uniclaw-runtime/tasks", request.RootElement);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            var replayPayload = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(taskInstanceId, replayPayload.GetProperty("taskInstanceId").GetString());
        }
        finally
        {
            server.Client.Dispose();
            await server.App.DisposeAsync();
            server.Store.Dispose();
            Directory.Delete(Path.GetDirectoryName(server.StorePath)!, recursive: true);
        }
    }

    [Fact]
    public async Task A6_2_SameKeyDifferentDeclaration_Returns4xxInputConflictAndKeepsOriginal()
    {
        var server = await StartServer();
        try
        {
            var first = JsonDocument.Parse(
                $$"""{"taskRef":"task/android-settings/toggle-wifi-reuse","idempotencyKey":"idem-http-conflict","declaration":{{DeclarationJson}}}""");
            var ok = await server.Client.PostAsJsonAsync("/api/uniclaw-runtime/tasks", first.RootElement);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var linesBefore = SharedLineCount(server.StorePath);

            var conflicting = JsonDocument.Parse(
                $$"""{"taskRef":"task/android-settings/toggle-wifi-reuse","idempotencyKey":"idem-http-conflict","declaration":{{DeclarationJson.Replace("toggle-wifi-reuse\"","toggle-wifi-reuse-v2\"")}}}""");
            var conflict = await server.Client.PostAsJsonAsync("/api/uniclaw-runtime/tasks", conflicting.RootElement);
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            var payload = await conflict.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("input-conflict", payload.GetProperty("error").GetProperty("code").GetString());
            Assert.Equal(linesBefore, SharedLineCount(server.StorePath));
        }
        finally
        {
            server.Client.Dispose();
            await server.App.DisposeAsync();
            server.Store.Dispose();
            Directory.Delete(Path.GetDirectoryName(server.StorePath)!, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidDeclaration_SchemaVersionOrShape_Returns400()
    {
        var server = await StartServer();
        try
        {
            var badVersion = await server.Client.PostAsJsonAsync("/api/uniclaw-runtime/tasks",
                JsonDocument.Parse("""{"taskRef":"t","idempotencyKey":"k1","declaration":{"schemaVersion":"other.v9"}}""").RootElement);
            Assert.Equal(HttpStatusCode.BadRequest, badVersion.StatusCode);

            var badShape = await server.Client.PostAsJsonAsync("/api/uniclaw-runtime/tasks",
                JsonDocument.Parse("""{"taskRef":"t","idempotencyKey":"k2","declaration":"not-an-object"}""").RootElement);
            Assert.Equal(HttpStatusCode.BadRequest, badShape.StatusCode);
        }
        finally
        {
            server.Client.Dispose();
            await server.App.DisposeAsync();
            server.Store.Dispose();
            Directory.Delete(Path.GetDirectoryName(server.StorePath)!, recursive: true);
        }
    }

    [Fact]
    public async Task MissingTaskRefOrIdempotencyKey_StillReturns400()
    {
        var server = await StartServer();
        try
        {
            var response = await server.Client.PostAsJsonAsync("/api/uniclaw-runtime/tasks",
                JsonDocument.Parse("""{"taskRef":"t"}""").RootElement);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid-request", payload.GetProperty("error").GetProperty("code").GetString());
        }
        finally
        {
            server.Client.Dispose();
            await server.App.DisposeAsync();
            server.Store.Dispose();
            Directory.Delete(Path.GetDirectoryName(server.StorePath)!, recursive: true);
        }
    }

    [Fact]
    public async Task A6_4_GetUnknownTaskInstance_Returns404TaskInstanceNotFound()
    {
        var server = await StartServer();
        try
        {
            var response = await server.Client.GetAsync("/api/uniclaw-runtime/tasks/ti-does-not-exist");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(payload.GetProperty("ok").GetBoolean());
            Assert.Equal("task-instance-not-found", payload.GetProperty("error").GetString());
        }
        finally
        {
            server.Client.Dispose();
            await server.App.DisposeAsync();
            server.Store.Dispose();
            Directory.Delete(Path.GetDirectoryName(server.StorePath)!, recursive: true);
        }
    }
}
