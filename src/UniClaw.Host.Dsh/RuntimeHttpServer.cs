using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UniClaw.Agent.Dsh;
using UniClaw.Host;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Dsh;

/// Runtime HTTP boundary for external Product launchers. Identity is created
/// here; HostRunner only consumes the supplied launch context.
public sealed class RuntimeHttpServer
{
    private readonly WebApplication _app;
    private readonly string _runsRoot;
    private readonly string _device;
    private readonly UniagentProdConfiguration _config;
    private readonly ConcurrentDictionary<string, Task> _runs = new();
    private readonly ConcurrentDictionary<string, RuntimeRun> _records = new();

    private RuntimeHttpServer(WebApplication app, string runsRoot, string device, UniagentProdConfiguration config)
    { _app = app; _runsRoot = runsRoot; _device = device; _config = config; }

    public static RuntimeHttpServer Create(string[] args, string runsRoot, string device)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        var app = builder.Build();
        var config = UniagentProdYaml.LoadDefault();
        var server = new RuntimeHttpServer(app, runsRoot, device, config);
        app.MapGet("/api/uniclaw-runtime/health", () => Results.Ok(new { ok = true, service = "uniclaw-runtime" }));
        app.MapPost("/api/uniclaw-runtime/runs", server.StartRunAsync);
        app.MapPost("/api/uniclaw-runtime/runs/recover", server.RecoverAsync);
        return server;
    }

    public Task RunAsync(int port, CancellationToken cancellationToken = default)
    { _app.Urls.Add($"http://127.0.0.1:{port}"); return RunCoreAsync(cancellationToken); }

    private async Task RunCoreAsync(CancellationToken cancellationToken)
    { await _app.StartAsync(cancellationToken); await _app.WaitForShutdownAsync(cancellationToken); }

    private async Task<IResult> StartRunAsync(LaunchRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return Results.BadRequest(new { ok = false, code = "invalid-request", message = "request body is required" });
        if (!HasReference(request.ProjectRef) || !HasReference(request.TestSetRef)
            || !HasReference(request.TaskRef))
            return Results.BadRequest(new { ok = false, code = "invalid-request", message = "projectRef, testSetRef and taskRef are required" });
        var runId = Required(request.RunId, "runId") ?? $"run-{Guid.NewGuid():N}";
        var productSessionId = Required(request.ProductSessionId, "productSessionId") ?? $"product-session-{Guid.NewGuid():N}";
        var launchId = Required(request.LaunchId, "launchId") ?? $"launch-{Guid.NewGuid():N}";
        var idempotency = Required(request.IdempotencyKey, "idempotencyKey") ?? $"idem-{Guid.NewGuid():N}";
        var correlation = Required(request.CorrelationId, "correlationId") ?? Guid.NewGuid().ToString("N");
        var existing = _records.Values.FirstOrDefault(candidate =>
            candidate.IdempotencyKey == idempotency || candidate.LaunchId == launchId);
        if (existing is not null)
        {
            return Results.Accepted($"/api/uniclaw-runtime/runs/{existing.RunId}",
                new { ok = true, idempotent = true, run = existing });
        }
        var modelName = Environment.GetEnvironmentVariable("UNICLAW_UNIAGENT_PROD_MODEL") ?? _config.Model.Name;
        var model = new ModelConfiguration(_config.Model.Provider, modelName);
        var device = RequestedDevice(request.EnvironmentIntent) ?? _device;
        var peer = new DshOpenedHttpPeer(_config.Service, model: model);
        var channel = new DshOpenedDecisionChannel(peer, attachTimeout: TimeSpan.FromSeconds(30));
        var attachment = await channel.AttachAsync(ProductHandshake.CreateRequest(productSessionId, runId), cancellationToken);
        if (!attachment.Accepted || string.IsNullOrWhiteSpace(attachment.DshSessionId))
        {
            await channel.DisposeAsync(); await peer.DisposeAsync();
            return Results.Problem($"DSH handshake failed: {attachment.FailureReason ?? "rejected"}", statusCode: StatusCodes.Status502BadGateway);
        }
        var dshSessionId = attachment.DshSessionId;
        var launch = new HostRunner.LaunchContext(runId, productSessionId, launchId, idempotency, correlation);
        var agent = new DshAgentAdapter(channel, productSessionId, runId, turnTimeout: TimeSpan.FromSeconds(110));
        var task = Task.Run(() => RunBackground(runId, device, launch, agent, channel, peer), CancellationToken.None);
        _runs[runId] = task;
        _records[runId] = new RuntimeRun(runId, productSessionId, dshSessionId, launchId, idempotency, correlation);
        return Results.Accepted($"/api/uniclaw-runtime/runs/{runId}", new { ok = true, run = new { runId, productSessionId, dshSessionId, launchId, idempotencyKey = idempotency, correlationId = correlation } });
    }

    private async Task RunBackground(string runId, string device, HostRunner.LaunchContext launch, DshAgentAdapter agent, DshOpenedDecisionChannel channel, DshOpenedHttpPeer peer)
    {
        try
        {
            // The DSH Host owns device preparation for this realization. Start
            // from the registered Wi-Fi Settings surface so the first live
            // observation is scoped to the task objective.
            RunAdb(device, "shell", "svc", "wifi", "enable");
            RunAdb(device, "shell", "am", "start", "-S", "-a", "android.settings.WIFI_SETTINGS");
            await Task.Delay(1500).ConfigureAwait(false);
            AgentDecision? Consult(AgentDecisionContext context) => agent.Consult(context);
            HostRunner.RunOnce(_runsRoot, new HostRunner.HostOptions { DeviceId = device, ConsultAgent = Consult, SettingsTraversal = true, Launch = launch, Live = new LivePerception.LiveAssets(device, "wifi-settings", Path.Combine(RepoRoot(), "platforms", "perception"), Path.Combine(RepoRoot(), ".perception", "venv", "bin", "python"), Path.Combine(RepoRoot(), ".perception", "cache")) });
        }
        catch (Exception ex) { Console.Error.WriteLine($"runtime run {runId} failed: {ex.Message}"); }
        finally
        {
            try { await agent.RevokeAttachmentAsync(); } catch { }
            await agent.DisposeAsync(); await channel.DisposeAsync(); await peer.DisposeAsync();
        }
    }

    private Task<IResult> RecoverAsync(RecoverRequest request, CancellationToken cancellationToken)
    {
        var run = _records.Values.FirstOrDefault(candidate =>
            (request?.LaunchId is null || candidate.LaunchId == request.LaunchId)
            && (request?.IdempotencyKey is null || candidate.IdempotencyKey == request.IdempotencyKey));
        return Task.FromResult<IResult>(run is not null
            ? Results.Ok(new { ok = true, run })
            : Results.NotFound(new { ok = false, code = "run-not-found", message = "no Runtime run matches launchId/idempotencyKey" }));
    }

    private static string? Required(string? value, string name) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static bool HasReference(JsonElement? value) => value is { } element
        && element.ValueKind is JsonValueKind.Object or JsonValueKind.String;
    private static string? RequestedDevice(JsonElement? environmentIntent)
    {
        if (environmentIntent is not { } intent || intent.ValueKind != JsonValueKind.Object
            || !intent.TryGetProperty("device", out var device)) return null;
        if (device.ValueKind == JsonValueKind.String) return device.GetString();
        if (device.ValueKind == JsonValueKind.Object
            && device.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.String)
            return string.IsNullOrWhiteSpace(id.GetString()) ? null : id.GetString();
        return null;
    }
    private static string RepoRoot() => Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName ?? Environment.CurrentDirectory;
    public sealed record LaunchRequest(string? RunId, string? ProductSessionId, string? LaunchId, string? IdempotencyKey, string? CorrelationId, JsonElement? ProjectRef = null, JsonElement? TestSetRef = null, JsonElement? TaskRef = null, JsonElement? EnvironmentIntent = null);
    public sealed record RecoverRequest(string? LaunchId = null, string? IdempotencyKey = null);
    private sealed record RuntimeRun(string RunId, string ProductSessionId, string DshSessionId, string LaunchId, string IdempotencyKey, string CorrelationId);

    private static void RunAdb(string device, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("adb")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("-s");
        info.ArgumentList.Add(device);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)
            ?? throw new InvalidOperationException("ENVIRONMENT_UNAVAILABLE: adb did not start");
        process.WaitForExit(10000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ENVIRONMENT_UNAVAILABLE: adb failed: {process.StandardError.ReadToEnd()}");
    }
}
