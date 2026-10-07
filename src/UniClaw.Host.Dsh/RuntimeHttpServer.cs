using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;
using UniClaw.Agent.Dsh;
using UniClaw.Host;
using UniClaw.Host.Capability;
using UniClaw.Host.Runtime;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Dsh;

/// <summary>
/// Runtime HTTP composition root for the DSH Host realization. RuntimeRunStore
/// owns the product run projection; this adapter only translates HTTP requests
/// into HostRunner and DSH lifecycle calls.
/// </summary>
public sealed class RuntimeHttpServer
{
    private const string ResponseSchema = "uniclaw.workspace.runtime-run-response.v1";
    private const string ListResponseSchema = "uniclaw.workspace.runtime-run-list-response.v1";
    private const string EventsResponseSchema = "uniclaw.workspace.runtime-run-events-response.v1";
    private const string ContractVersion = "uniclaw.workspace.contract.v1";
    private readonly WebApplication _app;
    private readonly string _runsRoot;
    private readonly string _device;
    private readonly UniagentProdConfiguration _config;
    private readonly RuntimeRunStore _store;

    private RuntimeHttpServer(WebApplication app, string runsRoot, string device, UniagentProdConfiguration config)
    {
        _app = app;
        _runsRoot = runsRoot;
        _device = device;
        _config = config;
        _store = new RuntimeRunStore(Path.Combine(runsRoot, ".runtime-runs"));
    }

    public static RuntimeHttpServer Create(string[] args, string runsRoot, string device)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        var app = builder.Build();
        var config = UniagentProdYaml.LoadDefault();
        var server = new RuntimeHttpServer(app, runsRoot, device, config);
        app.MapGet("/api/uniclaw-runtime/health", () => Results.Ok(new { ok = true, service = "uniclaw-runtime" }));
        app.MapPost("/api/uniclaw-runtime/runs", server.StartRunAsync);
        app.MapGet("/api/uniclaw-runtime/runs", server.ListRunsAsync);
        app.MapGet("/api/uniclaw-runtime/runs/{runId}", server.GetRunAsync);
        app.MapGet("/api/uniclaw-runtime/runs/{runId}/events", server.GetEventsAsync);
        // Kept for PNL-004 compatibility. Recovery semantics are intentionally
        // not part of PNL-005 and will be replaced by a later Change.
        app.MapPost("/api/uniclaw-runtime/runs/recover", server.RecoverAsync);
        // PNL-008 / ADR-0039：Harness 工具暴露面（只读投影 + adapter 执行）。
        app.MapGet("/api/uniclaw-runtime/tools", server.GetToolsAsync);
        app.MapPost("/api/uniclaw-runtime/tools/{name}/invoke", server.InvokeToolAsync);
        return server;
    }

    public Task RunAsync(int port, CancellationToken cancellationToken = default)
    {
        _app.Urls.Add($"http://127.0.0.1:{port}");
        return RunCoreAsync(cancellationToken);
    }

    private async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        await _app.StartAsync(cancellationToken);
        await _app.WaitForShutdownAsync(cancellationToken);
    }

    private async Task<IResult> StartRunAsync(LaunchRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
            return Error(StatusCodes.Status400BadRequest, "invalid-request", "request body is required", retryable: false);
        if (!HasReference(request.ProjectRef) || !HasReference(request.TestSetRef) || !HasReference(request.TaskRef))
            return Error(StatusCodes.Status400BadRequest, "invalid-request", "projectRef, testSetRef and taskRef are required", retryable: false);

        // Runtime owns run identity. The legacy request RunId is accepted for
        // wire compatibility but is never used as the persisted identity.
        var launchId = Required(request.LaunchId) ?? $"launch-{Guid.NewGuid():N}";
        var idempotency = Required(request.IdempotencyKey) ?? $"idem-{Guid.NewGuid():N}";
        var correlation = Required(request.CorrelationId) ?? Guid.NewGuid().ToString("N");
        var existing = _store.FindByIdempotencyKey(idempotency) ?? _store.FindByLaunchId(launchId);
        if (existing is not null)
            return Results.Accepted($"/api/uniclaw-runtime/runs/{existing.RunId}", Response(existing, idempotent: true));

        var run = _store.Create(new RuntimeRunStore.CreateRequest(
            request.ProductSessionId,
            request.TaskInstanceId,
            launchId,
            idempotency,
            correlation,
            request.ProjectRef,
            request.TestSetRef,
            request.TaskRef,
            request.EnvironmentIntent));

        // CAP-006：runtime-http 面同样经产品 ModelManagement 缝 resolve
        //（缺省 realization 借用 DSH；不直连 yaml）。
        var model = DshModelManagement.ResolveDshModel(
            DshModelManagement.FromProfile(
                _config, Environment.GetEnvironmentVariable("UNICLAW_UNIAGENT_PROD_MODEL")),
            LogicalProfileId.AgentDecision);
        var device = RequestedDevice(request.EnvironmentIntent) ?? _device;
        var peer = new DshOpenedHttpPeer(_config.Service, model: model);
        var channel = new DshOpenedDecisionChannel(peer, attachTimeout: TimeSpan.FromSeconds(30));
        DecisionChannelAttachment attachment;
        try
        {
            attachment = await channel.AttachAsync(ProductHandshake.CreateRequest(run.ProductSessionId, run.RunId), cancellationToken);
        }
        catch (Exception ex)
        {
            _store.Transition(run.RunId, "failed", "attach", "run.failed", "runtime", "uniclaw-runtime", "DSH handshake threw an exception", p => p with { Reason = ex.Message });
            await channel.DisposeAsync();
            await peer.DisposeAsync();
            return Error(StatusCodes.Status503ServiceUnavailable, "dsh-handshake-failed", ex.Message, retryable: true);
        }

        if (!attachment.Accepted || string.IsNullOrWhiteSpace(attachment.DshSessionId))
        {
            var reason = attachment.FailureReason ?? "rejected";
            _store.Transition(run.RunId, "failed", "attach", "run.failed", "dsh", "dsh-host", "DSH handshake failed", p => p with { Reason = reason });
            await channel.DisposeAsync();
            await peer.DisposeAsync();
            return Error(StatusCodes.Status503ServiceUnavailable, "dsh-handshake-failed", reason, retryable: true);
        }

        var running = _store.Transition(
            run.RunId,
            "running",
            "execute",
            "run.started",
            "dsh",
            "dsh-host",
            "DSH session attached",
            p => p with { HostSessionRef = new RuntimeRunStore.HostSessionRef("dsh", attachment.DshSessionId) });
        var launch = new HostRunner.LaunchContext(running.RunId, running.ProductSessionId, launchId, idempotency, correlation);
        var agent = new DshAgentAdapter(channel, running.ProductSessionId, running.RunId, turnTimeout: TimeSpan.FromSeconds(110));
        _ = Task.Run(() => RunBackground(running.RunId, device, launch, agent, channel, peer), CancellationToken.None);
        return Results.Accepted($"/api/uniclaw-runtime/runs/{running.RunId}", Response(running));
    }

    private Task<IResult> GetRunAsync(string runId)
    {
        var run = _store.Get(runId);
        return Task.FromResult<IResult>(run is null
            ? Error(StatusCodes.Status404NotFound, "runtime-run-not-found", "Run does not exist", retryable: false)
            : Results.Ok(Response(run)));
    }

    private Task<IResult> ListRunsAsync(string? status, string? productSessionId, string? cursor, int? limit)
    {
        try
        {
            var page = _store.List(status, productSessionId, cursor, limit ?? 50);
            var observedAt = page.Runs.Count == 0 ? DateTimeOffset.UtcNow : page.Runs.Max(run => run.ObservedAt);
            var revision = page.Runs.Count == 0 ? 0 : page.Runs.Max(run => run.Revision);
            return Task.FromResult<IResult>(Results.Ok(new
            {
                schemaVersion = "uniclaw.workspace.runtime-run-list-response.v1",
                contractVersion = ContractVersion,
                ok = true,
                runs = page.Runs,
                nextCursor = page.NextCursor,
                revision,
                observedAt
            }));
        }
        catch (FormatException ex)
        {
            return Task.FromResult<IResult>(Error(StatusCodes.Status400BadRequest, "invalid-cursor", ex.Message, retryable: false, schema: ListResponseSchema));
        }
    }

    private Task<IResult> GetEventsAsync(string runId, string? source, string? cursor, int? limit)
    {
        try
        {
            var run = _store.Get(runId);
            if (run is null)
                return Task.FromResult<IResult>(Error(StatusCodes.Status404NotFound, "runtime-run-not-found", "Run does not exist", retryable: false));
            var page = _store.ReadEvents(runId, source, cursor, limit ?? 50);
            return Task.FromResult<IResult>(Results.Ok(new
            {
                schemaVersion = EventsResponseSchema,
                contractVersion = ContractVersion,
                ok = true,
                runId,
                events = page.Events,
                nextCursor = page.NextCursor,
                revision = run.Revision,
                observedAt = run.ObservedAt
            }));
        }
        catch (FormatException ex)
        {
            return Task.FromResult<IResult>(Error(StatusCodes.Status400BadRequest, "invalid-cursor", ex.Message, retryable: false));
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult<IResult>(Error(StatusCodes.Status404NotFound, "runtime-run-not-found", "Run does not exist", retryable: false));
        }
    }

    private async Task RunBackground(string runId, string device, HostRunner.LaunchContext launch, DshAgentAdapter agent, DshOpenedDecisionChannel channel, DshOpenedHttpPeer peer)
    {
        try
        {
            RunAdb(device, "shell", "am", "start", "-S", "-a", "android.settings.WIFI_SETTINGS");
            await Task.Delay(1500).ConfigureAwait(false);
            LanguageInspectionSelection? agentLanguageInspectionSelection = null;
            AgentDecision? ConsultWithCapabilitySelection(AgentDecisionContext context)
            {
                var decision = agent.Consult(context);
                if (agentLanguageInspectionSelection is null
                    && agent.TakeInitialTaskInitialization()?.CapabilitySelection is { } selection)
                {
                    agentLanguageInspectionSelection = new LanguageInspectionSelection(
                        selection.CapabilityId,
                        selection.ExpectedLanguage ?? string.Empty,
                        selection.IgnoreRoutes);
                }
                return decision;
            }
            var settingsPolicyConfig = SettingsCoverageConfig.LoadDefault();
            var runtimeIntegrationRegistry = RuntimeIntegrationCapabilityComposition
                .RegisterLanguageInspector();
            var result = HostRunner.RunOnce(_runsRoot, new HostRunner.HostOptions
            {
                DeviceId = device,
                ConsultAgent = ConsultWithCapabilitySelection,
                SettingsTraversal = true,
                SettingsActionPolicy = settingsPolicyConfig.ActionPolicy,
                Launch = launch,
                LanguageInspection = settingsPolicyConfig.LanguageInspectionRequest,
                LanguageInspectionSelectionProvider = () => agentLanguageInspectionSelection,
                RuntimeIntegrationCapabilities = runtimeIntegrationRegistry,
                Live = new LivePerception.LiveAssets(
                    device,
                    "wifi-settings",
                    Path.Combine(RepoRoot(), "platforms", "perception"),
                    Path.Combine(RepoRoot(), ".perception", "venv", "bin", "python"),
                    Path.Combine(RepoRoot(), ".perception", "cache"))
            });
            var status = result.Status == RunDriveStatus.Completed
                ? "completed"
                : result.Status == RunDriveStatus.WaitingForInput
                    ? "interrupted"
                    : "failed";
            var outcome = status == "completed" ? "completion" : status == "interrupted" ? "unknown" : "failure";
            _store.Transition(runId, status, "finalize", "run." + status, "runtime", "uniclaw-runtime", "Runtime execution finished", p => p with
            {
                Outcome = outcome,
                Reason = result.Reason,
                Artifacts = BuildArtifacts(runId)
            });
        }
        catch (OperationCanceledException ex)
        {
            _store.Transition(runId, "interrupted", "stopped", "run.interrupted", "runtime", "uniclaw-runtime", "Runtime execution interrupted", p => p with { Outcome = "unknown", Reason = ex.Message });
        }
        catch (Exception ex)
        {
            _store.Transition(runId, "failed", "failed", "run.failed", "runtime", "uniclaw-runtime", "Runtime execution failed", p => p with { Outcome = "failure", Reason = ex.Message });
        }
        finally
        {
            try { await agent.RevokeAttachmentAsync(); } catch { }
            await agent.DisposeAsync();
            await channel.DisposeAsync();
            await peer.DisposeAsync();
        }
    }

    private Task<IResult> RecoverAsync(RecoverRequest? request, CancellationToken cancellationToken)
    {
        var run = request is null ? null : _store.FindByIdempotencyKey(request.IdempotencyKey ?? string.Empty) ?? _store.FindByLaunchId(request.LaunchId ?? string.Empty);
        return Task.FromResult<IResult>(run is not null
            ? Results.Ok(Response(run))
            : Error(StatusCodes.Status404NotFound, "runtime-run-not-found", "no Runtime run matches launchId/idempotencyKey", retryable: false));
    }

    private RuntimeToolHost? _toolHost;
    private RuntimeToolHost ToolHost()
    {
        if (_toolHost is null)
        {
            var registry = Path.Combine(RepoRoot(), "tool-registry.yaml");
            if (!File.Exists(registry))
                throw new InvalidOperationException($"tool-registry-missing: {registry}");
            _toolHost = RuntimeToolHost.Load(registry);
        }
        return _toolHost;
    }

    private Task<IResult> GetToolsAsync()
    {
        try
        {
            var tools = ToolHost().ForSurface("workbench").Select(tool => new
            {
                name = tool.Name,
                summary = tool.Summary,
                invocation = tool.Invocation,
                posture = tool.Posture,
                status = tool.Status,
                outputSchema = tool.OutputSchema,
                consumes = tool.Consumes,
            });
            return Task.FromResult<IResult>(Results.Ok(new
            {
                schemaVersion = "uniclaw.workspace.runtime-tools-response.v1",
                contractVersion = ContractVersion,
                ok = true,
                tools,
            }));
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("tool-registry-missing"))
        {
            return Task.FromResult<IResult>(Error(StatusCodes.Status503ServiceUnavailable, "tool-registry-missing", ex.Message["tool-registry-missing".Length..].Trim(), retryable: false));
        }
    }

    private async Task<IResult> InvokeToolAsync(string name, ToolInvokeRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RunDir))
            return Error(StatusCodes.Status400BadRequest, "invalid-request", "body with runDir is required", retryable: false);
        try
        {
            var result = await ToolHost().InvokeAsync(name, _runsRoot, request.RunDir.Trim());
            return Results.Ok(new
            {
                schemaVersion = "uniclaw.workspace.runtime-tool-invoke-response.v1",
                contractVersion = ContractVersion,
                ok = result.Ok,
                tool = name,
                result = new
                {
                    exitCode = result.ExitCode,
                    stdoutTail = result.StdoutTail,
                    stderrTail = result.StderrTail,
                    reportJson = result.ReportJson,
                    reportMd = result.ReportMd,
                },
            });
        }
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.StartsWith("tool-not-found", StringComparison.Ordinal)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            var code = ex.Message.Split(':', 2)[0];
            var message = ex.Message.Contains(':') ? ex.Message[(ex.Message.IndexOf(':') + 1)..].Trim() : ex.Message;
            return Error(status, code, message, retryable: false);
        }
    }

    public sealed record ToolInvokeRequest(string? RunDir = null);

    private static object Response(RuntimeRunStore.RuntimeRunProjection run, bool idempotent = false) => new
    {
        schemaVersion = ResponseSchema,
        contractVersion = ContractVersion,
        ok = true,
        idempotent,
        run,
        revision = run.Revision,
        observedAt = run.ObservedAt,
        consistency = run.Consistency
    };

    private static IResult Error(int statusCode, string code, string message, bool retryable, object? details = null, string schema = ResponseSchema) =>
        Results.Json(new
        {
            schemaVersion = schema,
            contractVersion = ContractVersion,
            ok = false,
            error = new { code, message, retryable, details = details ?? new { } }
        }, statusCode: statusCode);

    private static RuntimeRunStore.RuntimeArtifactRef[] BuildArtifacts(string runId) =>
    [
        new("facts", "facts", "present", $"runs/{runId}/artifacts/facts"),
        new("trace", "trace", "present", $"runs/{runId}/artifacts/trace"),
        new("evidence", "evidence", "partial", $"runs/{runId}/artifacts/evidence"),
        new("journal", "journal", "present", $"runs/{runId}/artifacts/journal")
    ];

    private static string? Required(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool HasReference(JsonElement? value) => value is { } element && element.ValueKind is JsonValueKind.Object or JsonValueKind.String;
    private static string? RequestedDevice(JsonElement? environmentIntent)
    {
        if (environmentIntent is not { } intent || intent.ValueKind != JsonValueKind.Object || !intent.TryGetProperty("device", out var device)) return null;
        if (device.ValueKind == JsonValueKind.String) return device.GetString();
        if (device.ValueKind == JsonValueKind.Object && device.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            return string.IsNullOrWhiteSpace(id.GetString()) ? null : id.GetString();
        return null;
    }

    private static string RepoRoot() => Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName ?? Environment.CurrentDirectory;
    public sealed record LaunchRequest(string? RunId, string? ProductSessionId, string? LaunchId, string? IdempotencyKey, string? CorrelationId, JsonElement? ProjectRef = null, JsonElement? TestSetRef = null, JsonElement? TaskRef = null, JsonElement? EnvironmentIntent = null, string? TaskInstanceId = null);
    public sealed record RecoverRequest(string? LaunchId = null, string? IdempotencyKey = null);

    private static void RunAdb(string device, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("adb") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("-s");
        info.ArgumentList.Add(device);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("ENVIRONMENT_UNAVAILABLE: adb did not start");
        process.WaitForExit(10000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ENVIRONMENT_UNAVAILABLE: adb failed: {process.StandardError.ReadToEnd()}");
    }
}
