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
    private readonly UniClaw.Agent.Profile.UniAgentProfile _agentProfile;
    private readonly UniagentDshBindings _bindings;
    private readonly RuntimeRunStore _store;
    private readonly TaskInstanceStore _taskStore;
    private readonly ProductSessionStore _sessionStore;
    private readonly ActivationRecordStore _activationStore;

    private RuntimeHttpServer(WebApplication app, string runsRoot, string device,
        UniClaw.Agent.Profile.UniAgentProfile agentProfile, UniagentDshBindings bindings)
    {
        _app = app;
        _runsRoot = runsRoot;
        _device = device;
        _agentProfile = agentProfile;
        _bindings = bindings;
        _store = new RuntimeRunStore(Path.Combine(runsRoot, ".runtime-runs"));
        // RUNTIME-IDENTITY-001：身份铸造走独立 Store（ADR-0044/0045），
        // 不再混入 RuntimeRunStore 的一次性创建。
        var dataDir = Path.Combine(runsRoot, ".identity");
        _taskStore = new TaskInstanceStore(Path.Combine(dataDir, "task-instances.jsonl"));
        _sessionStore = new ProductSessionStore(Path.Combine(dataDir, "product-sessions.jsonl"));
        _activationStore = new ActivationRecordStore(Path.Combine(dataDir, "activation-records.jsonl"));
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
        // PRF-002（ADR-0041）：产品 profile 与 DSH 绑定分轨加载。
        var agentProfile = UniClaw.Agent.Profile.UniAgentProfileYaml.LoadDefault();
        var bindings = UniagentDshBindingsYaml.LoadDefault();
        var server = new RuntimeHttpServer(app, runsRoot, device, agentProfile, bindings);
        app.MapGet("/api/uniclaw-runtime/health", () => Results.Ok(new { ok = true, service = "uniclaw-runtime" }));
        // RUNTIME-IDENTITY-001（ADR-0045 分阶段创建）：
        // Step 1：POST /tasks → TaskInstanceStore 铸造 taskInstanceId
        // Step 2：POST /sessions → ProductSessionStore 铸造 productSessionId
        // Step 3：POST /runs（既有）→ 消费上面两步铸造的 ID
        app.MapPost("/api/uniclaw-runtime/tasks", server.CreateTaskInstanceAsync);
        app.MapPost("/api/uniclaw-runtime/sessions", server.CreateProductSessionAsync);
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

    /// <summary>
    /// RUNTIME-IDENTITY-001 · ADR-0045 Step 1：Runtime 铸造 taskInstanceId。
    /// 调用方不传 ID；同 idempotencyKey 重入返回已有实例。
    /// </summary>
    private Task<IResult> CreateTaskInstanceAsync(JsonElement? body, CancellationToken ct)
    {
        string taskRef, idempotencyKey;
        if (body is { ValueKind: JsonValueKind.Object } json)
        {
            taskRef = json.TryGetProperty("taskRef", out var tr) && tr.ValueKind == JsonValueKind.String ? tr.GetString() ?? "" : "";
            idempotencyKey = json.TryGetProperty("idempotencyKey", out var ik) && ik.ValueKind == JsonValueKind.String ? ik.GetString() ?? "" : "";
        }
        else { taskRef = ""; idempotencyKey = ""; }
        if (string.IsNullOrWhiteSpace(taskRef) || string.IsNullOrWhiteSpace(idempotencyKey))
            return Task.FromResult<IResult>(Error(400, "invalid-request", "taskRef and idempotencyKey are required", retryable: false));
        var record = _taskStore.Create(taskRef, idempotencyKey);
        return Task.FromResult<IResult>(Results.Ok(new { ok = true, taskInstanceId = record.TaskInstanceId, taskRef = record.TaskRef, productSessionId = record.ProductSessionId }));
    }

    /// <summary>
    /// RUNTIME-IDENTITY-001 · ADR-0045 Step 2：Runtime 铸造 productSessionId。
    /// 输入 taskInstanceId（来自 Step 1）；同 taskInstance 重入返回已有 session（1:1）。
    /// </summary>
    private Task<IResult> CreateProductSessionAsync(JsonElement? body, CancellationToken ct)
    {
        string taskInstanceId;
        if (body is { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty("taskInstanceId", out var ti) && ti.ValueKind == JsonValueKind.String)
            taskInstanceId = ti.GetString() ?? "";
        else taskInstanceId = "";
        if (string.IsNullOrWhiteSpace(taskInstanceId))
            return Task.FromResult<IResult>(Error(400, "invalid-request", "taskInstanceId is required", retryable: false));
        var taskRecord = _taskStore.Get(taskInstanceId);
        if (taskRecord is null)
            return Task.FromResult<IResult>(Error(404, "task-instance-not-found", $"TaskInstance {taskInstanceId} does not exist", retryable: false));
        var session = _sessionStore.Create(taskInstanceId);
        // 回写 binding（幂等）
        _taskStore.BindSession(taskInstanceId, session.ProductSessionId);
        return Task.FromResult<IResult>(Results.Ok(new { ok = true, productSessionId = session.ProductSessionId, taskInstanceId = taskInstanceId, runId = session.RunId }));
    }

    private async Task<IResult> StartRunAsync(LaunchRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
            return Error(StatusCodes.Status400BadRequest, "invalid-request", "request body is required", retryable: false);
        if (!HasReference(request.ProjectRef) || !HasReference(request.TestSetRef) || !HasReference(request.TaskRef))
            return Error(StatusCodes.Status400BadRequest, "invalid-request", "projectRef, testSetRef and taskRef are required", retryable: false);

        var taskInstanceId = Required(request.TaskInstanceId);
        var productSessionId = Required(request.ProductSessionId);
        var acceptedContractViewRef = Required(request.AcceptedContractViewRef);
        if (taskInstanceId is null || productSessionId is null || acceptedContractViewRef is null
            || request.ContractGeneration is null || request.ContractGeneration <= 0)
            return Error(StatusCodes.Status400BadRequest, "activation-binding-required",
                "taskInstanceId, productSessionId, acceptedContractViewRef and positive contractGeneration are required",
                retryable: false);

        var taskRecord = _taskStore.Get(taskInstanceId);
        var sessionRecord = _sessionStore.Get(productSessionId);
        if (taskRecord is null || sessionRecord is null)
            return Error(StatusCodes.Status404NotFound, "activation-binding-not-found",
                "taskInstanceId or productSessionId does not exist", retryable: false);
        if (!string.Equals(taskRecord.ProductSessionId, productSessionId, StringComparison.Ordinal)
            || !string.Equals(sessionRecord.TaskInstanceId, taskInstanceId, StringComparison.Ordinal))
            return Error(StatusCodes.Status409Conflict, "activation-binding-mismatch",
                "taskInstanceId and productSessionId do not belong to the same binding", retryable: false);

        // Runtime owns run identity. Caller-supplied RunId is never accepted as
        // canonical identity; launch/idempotency are lookup keys only.
        var launchId = Required(request.LaunchId) ?? $"launch-{Guid.NewGuid():N}";
        var idempotency = Required(request.IdempotencyKey) ?? $"idem-{Guid.NewGuid():N}";
        var correlation = Required(request.CorrelationId) ?? Guid.NewGuid().ToString("N");

        var activationKey = new ActivationKey(acceptedContractViewRef, request.ContractGeneration.Value);
        var reservation = _activationStore.Reserve(
            activationKey, taskInstanceId, productSessionId, launchId, idempotency, correlation);
        if (reservation.Conflict)
            return Error(StatusCodes.Status409Conflict, "activation-key-conflict",
                "launchId or idempotencyKey is already bound to a different activation key", retryable: false);
        var activation = reservation.Record;
        if (sessionRecord.RunId is not null && activation.Status != "finalized")
        {
            _activationStore.Fail(activationKey, "blocked", "product-session-already-bound");
            return Error(StatusCodes.Status409Conflict, "product-session-already-bound",
                "productSessionId is already bound to a different Run", retryable: false);
        }
        if (activation.Status is "failed" or "blocked")
            return Error(StatusCodes.Status409Conflict, "activation-closed", activation.Reason ?? activation.Status, retryable: false);
        if (activation.Status == "reserved" && activation.RunId is null && !reservation.Created && !request.Resume)
            return Error(StatusCodes.Status409Conflict, "activation-reserved",
                "activation is reserved and requires explicit resume", retryable: true);
        if (activation.Status == "finalized" && activation.RunId is not null)
        {
            var replay = _store.Get(activation.RunId);
            return replay is null
                ? Error(StatusCodes.Status409Conflict, "activation-record-incomplete",
                    "activation is finalized but its Run projection is unavailable", retryable: true)
                : Results.Accepted($"/api/uniclaw-runtime/runs/{replay.RunId}", Response(replay, idempotent: true));
        }

        var run = _store.Create(new RuntimeRunStore.CreateRequest(
            productSessionId,
            taskInstanceId,
            launchId,
            idempotency,
            correlation,
            request.ProjectRef,
            request.TestSetRef,
            request.TaskRef,
            request.EnvironmentIntent,
            acceptedContractViewRef,
            request.ContractGeneration));
        try
        {
            _sessionStore.BindRun(productSessionId, run.RunId);
            _activationStore.Finalize(activationKey, run.RunId);
        }
        catch (Exception ex)
        {
            _activationStore.Fail(activationKey, "blocked", $"activation-finalize-failed:{ex.Message}");
            return Error(StatusCodes.Status409Conflict, "activation-finalize-failed", ex.Message, retryable: true);
        }

        // CAP-006：runtime-http 面同样经产品 ModelManagement 缝 resolve
        //（缺省 realization 借用 DSH；不直连 yaml）。
        var model = DshModelManagement.ResolveDshModel(
            DshModelManagement.FromBindings(
                _bindings, _agentProfile,
                Environment.GetEnvironmentVariable("UNICLAW_UNIAGENT_PROD_MODEL")),
            LogicalProfileId.AgentDecision);
        // PRF-005（Q13）：会话首个 run 落初始化审计 envelope（装配可重建）。
        InitializationEnvelope.Write(_runsRoot, run.ProductSessionId, run.RunId,
            _agentProfile, _bindings, model);
        var device = RequestedDevice(request.EnvironmentIntent) ?? _device;
        var peer = new DshOpenedHttpPeer(_bindings.Service, model: model, expectedProfile: _agentProfile);
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
        var launch = new HostRunner.LaunchContext(running.RunId, running.ProductSessionId,
            launchId, idempotency, correlation,
            TaskInstanceId: taskInstanceId,
            ContractRef: $"contract-view:{acceptedContractViewRef}",
            AcceptedContractViewRef: acceptedContractViewRef,
            ContractGeneration: request.ContractGeneration);
        var agent = new DshAgentAdapter(channel, running.ProductSessionId, running.RunId, turnTimeout: TimeSpan.FromSeconds(110));
        // PNL-011：需求原文经 taskRef（web launch 流把 requirement 放在 taskRef.label）
        // 进入 run 目录 metadata 投影，报告①区从此有真相，不再依赖 --requirement。
        var requirement = JsonLabel(request.TaskRef, "requirement", "label");
        var title = JsonLabel(request.TaskRef, "title") ?? requirement;
        var taskSet = JsonLabel(request.TestSetRef, "label", "id");
        var modelRef = $"{model.Provider}/{model.Name}";
        _ = Task.Run(() => RunBackground(running.RunId, device, launch, agent, channel, peer, requirement, title, taskSet, modelRef), CancellationToken.None);
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

    private async Task RunBackground(string runId, string device, HostRunner.LaunchContext launch, DshAgentAdapter agent, DshOpenedDecisionChannel channel, DshOpenedHttpPeer peer, string? requirement, string? title, string? taskSet, string model)
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

            // PNL-011：finalize 附带产物（metadata 投影 / runtime 事件导出 / 全链路
            // 报告）。失败不改变 run 终态，只落 report-generation.log 到 run 目录。
            var dirName = Path.GetFileName(result.RunDir);
            var artifacts = BuildArtifacts(runId);
            var note = "Runtime execution finished";
            try
            {
                WriteRunMetadata(result.RunDir, runId, launch, device, model, requirement, title, taskSet, status, outcome, result);
                ExportRuntimeEvents(result.RunDir, runId);
                var report = await ToolHost().InvokeAsync("run-report", _runsRoot, dirName).ConfigureAwait(false);
                artifacts = BuildArtifacts(runId, dirName, report.ReportMd is not null, report.ReportJson is not null);
                note = $"Runtime execution finished; report exit={report.ExitCode}";
            }
            catch (Exception reportError)
            {
                try
                {
                    File.AppendAllText(Path.Combine(result.RunDir, "report-generation.log"),
                        $"{DateTimeOffset.UtcNow:O} finalize-artifact-failed: {reportError.Message}\n");
                }
                catch { }
            }

            _store.Transition(runId, status, "finalize", "run." + status, "runtime", "uniclaw-runtime", note, p => p with
            {
                Outcome = outcome,
                Reason = result.Reason,
                Artifacts = artifacts
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
            // PNL-010：产出路径配置来自本地 profile（product/tasks/tool-runtime.yaml）。
            _toolHost = RuntimeToolHost.Load(registry, RuntimeToolConfig.LoadDefault());
        }
        return _toolHost;
    }

    private Task<IResult> GetToolsAsync()
    {
        try
        {
            var host = ToolHost();
            var tools = host.ForSurface("workbench").Select(tool => new
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
                output = new { @base = host.Output.Base, subdir = host.Output.Subdir },
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
        var host = ToolHost();
        // PNL-012：model-procedure 与 deterministic-script 响应形状不同
        //（model/text/reportRef vs exitCode/stdout…）；错误 envelope 保持统一。
        if (host.Find(name) is { Invocation: "model-procedure" })
            return await InvokeProcedureToolAsync(host, name, request.RunDir.Trim());
        try
        {
            var result = await host.InvokeAsync(name, _runsRoot, request.RunDir.Trim());
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

    /// <summary>
    /// PNL-012：model-procedure 工具执行（run-diagnosis）。模型经产品缝解析
    /// （复用 agent.decision——诊断与执行决策同档语义能力；不新增 choice、
    /// 不硬编码模型名）；解析不到 → 显式 ROUTING_UNAVAILABLE，不静默降级。
    /// 传输缝复用 DshOpenedHttpPeer 的 slow 端点；诊断 prompt 大，有界等待
    /// 放宽到 120s（state.md 记录）。诊断文本只返回，不写任何 store。
    /// </summary>
    private async Task<IResult> InvokeProcedureToolAsync(RuntimeToolHost host, string name, string runDir)
    {
        ModelConfiguration model;
        try
        {
            model = DshModelManagement.ResolveDshModel(
                DshModelManagement.FromBindings(
                    _bindings, _agentProfile,
                    Environment.GetEnvironmentVariable("UNICLAW_UNIAGENT_PROD_MODEL")),
                LogicalProfileId.AgentDecision);
        }
        catch (InvalidOperationException ex)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "routing-unavailable", ex.Message, retryable: false);
        }
        try
        {
            var result = await host.InvokeProcedureAsync(
                name, _runsRoot, runDir,
                (requestId, prompt, model, cancellationToken) => TransportAsync(requestId, prompt, model, cancellationToken),
                model).ConfigureAwait(false);
            return Results.Ok(new
            {
                schemaVersion = "uniclaw.workspace.runtime-tool-invoke-response.v1",
                contractVersion = ContractVersion,
                ok = true,
                tool = name,
                result = new
                {
                    model = result.Model,
                    text = result.Text,
                    reportRef = result.ReportRef,
                },
            });
        }
        catch (InvalidOperationException ex)
        {
            var code = ex.Message.Split(':', 2)[0];
            var message = ex.Message.Contains(':') ? ex.Message[(ex.Message.IndexOf(':') + 1)..].Trim() : ex.Message;
            var status = code switch
            {
                "tool-not-found" => StatusCodes.Status404NotFound,
                "diagnosis-timeout" => StatusCodes.Status504GatewayTimeout,
                "diagnosis-transport-failed" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status400BadRequest,
            };
            return Error(status, code, message, retryable: status != StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>生产传输：slow 端点只取 assistant 文本；端点错误诚实上抛。</summary>
    private async Task<string> TransportAsync(string requestId, string prompt, ModelConfiguration model, CancellationToken cancellationToken)
    {
        await using var peer = new DshOpenedHttpPeer(_bindings.Service, model: model, expectedProfile: _agentProfile);
        var response = await peer.ExecuteSlowAsync(requestId, prompt, model, imagePng: null, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(response.Error))
            throw new InvalidOperationException($"slow-endpoint-error:{response.Error}:{response.Diagnostic}");
        return response.Text ?? string.Empty;
    }

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

    private static RuntimeRunStore.RuntimeArtifactRef[] BuildArtifacts(string runId, string? dirName = null, bool reportMd = false, bool reportJson = false)
    {
        var artifacts = new List<RuntimeRunStore.RuntimeArtifactRef>
        {
            new("facts", "facts", "present", $"runs/{runId}/artifacts/facts"),
            new("trace", "trace", "present", $"runs/{runId}/artifacts/trace"),
            new("evidence", "evidence", "partial", $"runs/{runId}/artifacts/evidence"),
            new("journal", "journal", "present", $"runs/{runId}/artifacts/journal"),
        };
        if (dirName is not null)
            artifacts.Add(new("report", "report", reportMd && reportJson ? "present" : "partial", $"runs/{dirName}/report/report.md"));
        return [.. artifacts];
    }

    private static string? JsonLabel(JsonElement? element, params string[] keys)
    {
        if (element is not { } value || value.ValueKind != JsonValueKind.Object) return null;
        foreach (var key in keys)
        {
            if (value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(property.GetString()))
                return property.GetString();
        }
        return null;
    }

    private static readonly JsonSerializerOptions ArtifactJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// PNL-011：run 目录的 metadata 投影（adapter 层观测：设备/模型/endpoint 是
    /// realization 关切，产品 HostRunner 不拥有）。历史 fixture 的 metadata.json
    /// 由采集流程手写；本方法使其成为 finalize 的正式产物。
    /// </summary>
    private void WriteRunMetadata(string runDir, string runId, HostRunner.LaunchContext launch, string device, string model,
        string? requirement, string? title, string? taskSet, string status, string outcome, HostRunner.HostRunResult result)
    {
        var dshSessionId = _store.Get(runId)?.HostSessionRef?.SessionId;
        var metadata = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "uniclaw.host-metadata.v1",
            ["runDir"] = runDir,
            ["runId"] = runId,
            ["productSessionId"] = launch.ProductSessionId,
            ["launchId"] = launch.LaunchId,
            ["correlationId"] = launch.CorrelationId,
            ["dshSessionId"] = dshSessionId,
            ["requirement"] = requirement,
            ["productSessionTitle"] = title,
            ["taskSet"] = taskSet,
            ["workspace"] = taskSet,
            ["device"] = device,
            ["productModel"] = model,
            ["dshEndpoint"] = _bindings.Service.BaseUri?.ToString(),
            ["real"] = true,
            ["status"] = status,
            ["outcome"] = outcome,
            ["deliveredEffects"] = result.DeliveredEffects,
            ["receipts"] = result.ReceiptOutcomes,
        };
        File.WriteAllText(Path.Combine(runDir, "metadata.json"), JsonSerializer.Serialize(metadata, ArtifactJson));
    }

    /// <summary>PNL-011：把 RuntimeRunStore 的生命周期事件导出进 run 目录，让报告
    /// 的 host 分区有真相，同时 run 目录成为自包含 evidence（store 仍是 authority）。</summary>
    private void ExportRuntimeEvents(string runDir, string runId)
    {
        var events = new List<RuntimeRunStore.RuntimeRunEvent>();
        string? cursor = null;
        do
        {
            var page = _store.ReadEvents(runId, cursor: cursor, limit: 100);
            events.AddRange(page.Events);
            cursor = page.NextCursor;
        }
        while (cursor is not null);
        File.WriteAllText(Path.Combine(runDir, "runtime-run-events.json"), JsonSerializer.Serialize(events, ArtifactJson));
    }

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
    public sealed record LaunchRequest(string? RunId, string? ProductSessionId, string? LaunchId, string? IdempotencyKey, string? CorrelationId, JsonElement? ProjectRef = null, JsonElement? TestSetRef = null, JsonElement? TaskRef = null, JsonElement? EnvironmentIntent = null, string? TaskInstanceId = null, string? AcceptedContractViewRef = null, int? ContractGeneration = null, bool Resume = false);
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
