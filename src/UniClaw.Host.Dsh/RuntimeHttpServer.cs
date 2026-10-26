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
    private readonly RuntimeBackgroundWork _backgroundWork = new();

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
        // RUNTIME-TASK-001 切片 1（定形 T4）：instance 只读查询端点。
        app.MapGet("/api/uniclaw-runtime/tasks/{taskInstanceId}", server.GetTaskInstanceAsync);
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
        ReportDeliveryEndpoints.Map(app, server.ToolHost(), server._runsRoot);
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
        try { await _app.WaitForShutdownAsync(cancellationToken); }
        finally
        {
            var drain = await _backgroundWork.DrainAsync(TimeSpan.FromSeconds(10));
            foreach (var runId in drain.PendingRunIds)
                Console.Error.WriteLine($"runtime-work-unconfirmed: run={runId}; required reports remain queryable by runtime-run:{runId}");
            foreach (var runId in drain.FailedRunIds)
                Console.Error.WriteLine($"runtime-background-work-failed: run={runId}; inspect durable run/report state");
            if (_toolHost is not null)
            {
                var reports = await _toolHost.CloseReportProcessesAsync(TimeSpan.FromSeconds(5));
                foreach (var operation in reports.UnconfirmedOperations)
                    Console.Error.WriteLine($"report-work-unconfirmed: {operation}; inspect durable delivery state");
            }
        }
    }

    /// <summary>
    /// RUNTIME-IDENTITY-001 · ADR-0045 Step 1：Runtime 铸造 taskInstanceId。
    /// 调用方不传 ID；同 idempotencyKey 重入返回已有实例。
    /// RUNTIME-TASK-001 切片 1：请求体可选 declaration 块（定形 T1），先持久化后
    /// 成功；同 key 异声明 → 409 input-conflict（A6-②）。
    /// </summary>
    private Task<IResult> CreateTaskInstanceAsync(JsonElement? body, CancellationToken ct) =>
        RuntimeTaskEndpoints.CreateTaskInstanceAsync(_taskStore, body);

    /// <summary>RUNTIME-TASK-001 切片 1（定形 T4）：instance 只读查询。</summary>
    private Task<IResult> GetTaskInstanceAsync(string taskInstanceId) =>
        RuntimeTaskEndpoints.GetTaskInstanceAsync(_taskStore, taskInstanceId);

    /// <summary>
    /// RUNTIME-TASK-001 切片 1：admission（POST /tasks + declaration 块）与
    /// instance 只读查询（GET /tasks/{id}，定形 T4）端点实现。静态可映射，
    /// 供 RuntimeHttpServer 与端点锚定测试复用同一条实现。
    /// </summary>
    public static class RuntimeTaskEndpoints
    {
        public static void Map(WebApplication app, TaskInstanceStore store)
        {
            app.MapPost("/api/uniclaw-runtime/tasks", (JsonElement? body, CancellationToken ct) => CreateTaskInstanceAsync(store, body));
            app.MapGet("/api/uniclaw-runtime/tasks/{taskInstanceId}", (string taskInstanceId) => GetTaskInstanceAsync(store, taskInstanceId));
        }

        public static Task<IResult> CreateTaskInstanceAsync(TaskInstanceStore store, JsonElement? body)
        {
            string taskRef, idempotencyKey;
            JsonElement? declaration = null;
            var declarationInvalid = false;
            if (body is { ValueKind: JsonValueKind.Object } json)
            {
                taskRef = json.TryGetProperty("taskRef", out var tr) && tr.ValueKind == JsonValueKind.String ? tr.GetString() ?? "" : "";
                idempotencyKey = json.TryGetProperty("idempotencyKey", out var ik) && ik.ValueKind == JsonValueKind.String ? ik.GetString() ?? "" : "";
                if (json.TryGetProperty("declaration", out var d))
                {
                    // 定形 T1 校验：必须是对象且 schemaVersion == "task-declaration.v1"。
                    if (d.ValueKind == JsonValueKind.Object
                        && d.TryGetProperty("schemaVersion", out var sv)
                        && sv.ValueKind == JsonValueKind.String
                        && sv.GetString() == "task-declaration.v1")
                    {
                        declaration = d;
                    }
                    else
                    {
                        declarationInvalid = true;
                    }
                }
            }
            else { taskRef = ""; idempotencyKey = ""; }
            if (string.IsNullOrWhiteSpace(taskRef) || string.IsNullOrWhiteSpace(idempotencyKey))
                return Task.FromResult<IResult>(Error(400, "invalid-request", "taskRef and idempotencyKey are required", retryable: false));
            if (declarationInvalid)
                return Task.FromResult<IResult>(Error(400, "invalid-declaration",
                    "declaration must be an object with schemaVersion 'task-declaration.v1'", retryable: false));
            TaskInstanceRecord record;
            try
            {
                // Runtime 原样持久化声明块（不改写、不解析语义）；先持久化后成功。
                record = store.Create(taskRef, idempotencyKey, declaration is null ? null : declaration.Value.GetRawText());
            }
            catch (TaskInstanceInputConflictException ex)
            {
                return Task.FromResult<IResult>(Error(409, "input-conflict", ex.Message, retryable: false));
            }
            catch (JsonException ex)
            {
                return Task.FromResult<IResult>(Error(400, "invalid-declaration", ex.Message, retryable: false));
            }
            return Task.FromResult<IResult>(Results.Ok(new
            {
                ok = true,
                taskInstanceId = record.TaskInstanceId,
                taskRef = record.TaskRef,
                declaration = record.Declaration is null ? (object?)null : JsonDocument.Parse(record.Declaration).RootElement.Clone(),
                declarationFingerprint = record.DeclarationFingerprint,
                productSessionId = record.ProductSessionId,
            }));
        }

        public static Task<IResult> GetTaskInstanceAsync(TaskInstanceStore store, string taskInstanceId)
        {
            var record = store.Get(taskInstanceId);
            if (record is null)
                return Task.FromResult<IResult>(Results.Json(
                    new { ok = false, error = "task-instance-not-found" }, statusCode: StatusCodes.Status404NotFound));
            return Task.FromResult<IResult>(Results.Ok(new
            {
                ok = true,
                taskInstanceId = record.TaskInstanceId,
                taskRef = record.TaskRef,
                declaration = record.Declaration is null ? (object?)null : JsonDocument.Parse(record.Declaration).RootElement.Clone(),
                declarationFingerprint = record.DeclarationFingerprint,
                productSessionId = record.ProductSessionId,
                createdAt = record.CreatedAt,
            }));
        }
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
            if (request.RequireReport && !(await ToolHost().ListRequiredReportsAsync(_runsRoot,
                    "runtime-run:" + activation.RunId, cancellationToken)).Any(x => x.Record.Request.RequestId == "primary"))
                return Error(StatusCodes.Status409Conflict, "report-requirement-not-accepted",
                    "this activation did not accept a required report; request a report explicitly", retryable: false);
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
        string? reportDeliveryId = null;
        try
        {
            if (request.RequireReport)
                reportDeliveryId = (await ToolHost().RequireReportAsync(_runsRoot, "runtime-run:" + run.RunId,
                    cancellationToken: cancellationToken)).DeliveryId;
            _sessionStore.BindRun(productSessionId, run.RunId);
            _activationStore.Finalize(activationKey, run.RunId);
        }
        catch (Exception ex)
        {
            _activationStore.Fail(activationKey, "blocked", $"activation-finalize-failed:{ex.Message}");
            _store.Transition(run.RunId, "failed", "admission", "run.failed", "runtime", "uniclaw-runtime",
                "Runtime activation could not be accepted", p => p with { Outcome = "failure", Reason = ex.Message });
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
            // RUNTIME-TASK-001 S2-4：查 TaskInstanceStore 取 taskRef 与声明指纹
            // 传入（HostRunner 不解析声明内容；S2-1 布局据此落 tasks/<taskDir>/）。
            TaskRef: taskRecord.TaskRef,
            DeclarationFingerprint: taskRecord.DeclarationFingerprint,
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
        _backgroundWork.Track(running.RunId, Task.Run(() => RunBackground(running.RunId, device, launch, agent, channel, peer,
            requirement, title, taskSet, modelRef, reportDeliveryId), CancellationToken.None));
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

    private async Task RunBackground(string runId, string device, HostRunner.LaunchContext launch, DshAgentAdapter agent, DshOpenedDecisionChannel channel, DshOpenedHttpPeer peer, string? requirement, string? title, string? taskSet, string model, string? reportDeliveryId)
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
            var result = await HostRunner.RunOnceAsync(_runsRoot, new HostRunner.HostOptions
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

            // Execution is terminal before report work begins. Report retry has no execution authority.
            // RUNTIME-TASK-001 S2-8：run 目录改为相对多段路径（tasks/<taskDir>/<ti>）。
            var dirName = Path.GetRelativePath(Path.GetFullPath(_runsRoot), result.RunDir);
            var artifacts = BuildArtifacts(runId);
            if (reportDeliveryId is not null)
                artifacts = [.. artifacts, new("report", "report", "partial", "report-deliveries/" + reportDeliveryId)];
            _store.Transition(runId, status, "finalize", "run." + status, "runtime", "uniclaw-runtime",
                "Runtime execution finished", p => p with { Outcome = outcome, Reason = result.Reason, Artifacts = artifacts });
            try
            {
                try
                {
                    WriteRunMetadata(result.RunDir, runId, launch, device, model, requirement, title, taskSet, status, outcome, result);
                    ExportRuntimeEvents(result.RunDir, runId);
                }
                catch (Exception metadataError) { LogReportError(result.RunDir, metadataError); }
                if (reportDeliveryId is not null)
                {
                    var delivered = await ToolHost().RetryRequiredReportAsync(_runsRoot, reportDeliveryId, dirName);
                    artifacts = [.. BuildArtifacts(runId), new("report", "report", delivered.Ok ? "present" : "partial",
                        "report-deliveries/" + reportDeliveryId)];
                }
                else
                {
                    var report = await ToolHost().InvokeAsync("run-report", _runsRoot, dirName).ConfigureAwait(false);
                    artifacts = BuildArtifacts(runId, dirName, report.ReportMd is not null, report.ReportJson is not null);
                }
                _store.UpdateArtifacts(runId, artifacts);
            }
            catch (Exception reportError)
            {
                LogReportError(result.RunDir, reportError);
            }
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

    private static void LogReportError(string runDir, Exception error)
    {
        try { File.AppendAllText(Path.Combine(runDir, "report-generation.log"),
            $"{DateTimeOffset.UtcNow:O} report-delivery-failed: {error.Message}\n"); }
        catch (Exception) { }
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

    private async Task<IResult> InvokeToolAsync(string name, ToolInvokeRequest? request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RunDir))
            return Error(StatusCodes.Status400BadRequest, "invalid-request", "body with runDir is required", retryable: false);
        var host = ToolHost();
        // PNL-012：model-procedure 与 deterministic-script 响应形状不同
        //（model/text/reportRef vs exitCode/stdout…）；错误 envelope 保持统一。
        if (host.Find(name) is { Invocation: "model-procedure" })
            return await InvokeProcedureToolAsync(host, name, request.RunDir.Trim(), request.DevelopmentAssets, cancellationToken);
        if (request.DevelopmentAssets is not null)
            return Error(StatusCodes.Status400BadRequest, "invalid-request", "developmentAssets applies to model-procedure tools", retryable: false);
        try
        {
            var delivery = name == "run-report" ? await host.InvokeRequiredReportAsync(_runsRoot, request.RunDir.Trim(),
                request.ReportRequestId ?? "primary", request.StrictReport) : null;
            var result = delivery?.Result ?? await host.InvokeAsync(name, _runsRoot, request.RunDir.Trim());
            return Results.Ok(new
            {
                schemaVersion = "uniclaw.workspace.runtime-tool-invoke-response.v1",
                contractVersion = ContractVersion,
                ok = result.Ok,
                tool = name,
                deliveryId = delivery?.Delivery.Record.DeliveryId,
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

    public sealed record ToolInvokeRequest(string? RunDir = null, string? ReportRequestId = null, bool StrictReport = false,
        RuntimeToolHost.DevelopmentAssetRequest? DevelopmentAssets = null);

    /// <summary>
    /// PNL-012：model-procedure 工具执行（run-diagnosis）。模型经产品缝解析
    /// （复用 agent.decision——诊断与执行决策同档语义能力；不新增 choice、
    /// 不硬编码模型名）；解析不到 → 显式 ROUTING_UNAVAILABLE，不静默降级。
    /// 传输缝复用 DshOpenedHttpPeer 的 slow 端点；诊断 prompt 大，有界等待
    /// 放宽到 120s（state.md 记录）。诊断文本只返回，不写任何 store。
    /// </summary>
    private async Task<IResult> InvokeProcedureToolAsync(RuntimeToolHost host, string name, string runDir,
        RuntimeToolHost.DevelopmentAssetRequest? developmentAssets, CancellationToken cancellationToken)
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
            RuntimeToolHost.DiagnosisTransport transport = (requestId, prompt, choice, token) => TransportAsync(requestId, prompt, choice, token);
            var result = developmentAssets is null
                ? await host.InvokeProcedureAsync(name, _runsRoot, runDir, transport, model).ConfigureAwait(false)
                : await host.InvokeProcedureWithAssetsAsync(name, _runsRoot, runDir, transport, model,
                    developmentAssets, cancellationToken: cancellationToken).ConfigureAwait(false);
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
                    packetRef = result.PacketRef,
                    packetSha256 = result.PacketSha256,
                    signalRefs = result.SignalRefs,
                    assetChangeId = result.AssetChangeId,
                    assetRunId = result.AssetRunId,
                    assetFinalizationError = result.AssetFinalizationError,
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
                "development-assets-unavailable" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status400BadRequest,
            };
            return Error(status, code, message, retryable: status != StatusCodes.Status400BadRequest, details: AssetErrorDetails(ex));
        }
        catch (OperationCanceledException ex)
        {
            return Error(499, "diagnosis-cancelled", "diagnosis cancelled; assets retained", retryable: false, details: AssetErrorDetails(ex));
        }
    }

    private static object? AssetErrorDetails(Exception error) => error.Data.Contains("assetRunId")
        ? new { assetChangeId = error.Data["assetChangeId"], assetRunId = error.Data["assetRunId"],
            assetFinalizationError = error.Data["assetFinalizationError"] } : null;

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
    /// realization 关切，产品 HostRunner 不拥有）。
    /// RUNTIME-TASK-001 S2-5：身份/指纹字段（schemaVersion/runId/productSessionId/
    /// taskInstanceId/taskRef/launchId/correlationId/changeId/declarationFingerprint/
    /// implementationFingerprint/runDir/createdAt）已由 HostRunner 统一写入
    /// uniclaw.run-metadata.v2——本方法只合并 adapter 层字段，不覆盖、不丢字段。
    /// </summary>
    private void WriteRunMetadata(string runDir, string runId, HostRunner.LaunchContext launch, string device, string model,
        string? requirement, string? title, string? taskSet, string status, string outcome, HostRunner.HostRunResult result)
    {
        var path = Path.Combine(runDir, "metadata.json");
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (File.Exists(path))
        {
            // HostRunner 已写入 v2 投影——原样保留其字段，仅并入 adapter 层观测。
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var property in document.RootElement.EnumerateObject())
                metadata[property.Name] = JsonSerializer.Deserialize<JsonElement>(property.Value.GetRawText());
        }
        else
        {
            // 兜底（HostRunner 未写到的历史路径）：保留旧 v1 形状，不丢字段。
            metadata["schemaVersion"] = "uniclaw.host-metadata.v1";
            metadata["runDir"] = runDir;
            metadata["runId"] = runId;
            metadata["productSessionId"] = launch.ProductSessionId;
            metadata["launchId"] = launch.LaunchId;
            metadata["correlationId"] = launch.CorrelationId;
        }
        var dshSessionId = _store.Get(runId)?.HostSessionRef?.SessionId;
        metadata["dshSessionId"] = dshSessionId;
        metadata["requirement"] = requirement;
        metadata["productSessionTitle"] = title;
        metadata["taskSet"] = taskSet;
        metadata["workspace"] = taskSet;
        metadata["device"] = device;
        metadata["productModel"] = model;
        metadata["dshEndpoint"] = _bindings.Service.BaseUri?.ToString();
        metadata["real"] = true;
        metadata["status"] = status;
        metadata["outcome"] = outcome;
        metadata["deliveredEffects"] = result.DeliveredEffects;
        metadata["receipts"] = result.ReceiptOutcomes;
        File.WriteAllText(path, JsonSerializer.Serialize(metadata, ArtifactJson));
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
    public sealed record LaunchRequest(string? RunId, string? ProductSessionId, string? LaunchId, string? IdempotencyKey, string? CorrelationId, JsonElement? ProjectRef = null, JsonElement? TestSetRef = null, JsonElement? TaskRef = null, JsonElement? EnvironmentIntent = null, string? TaskInstanceId = null, string? AcceptedContractViewRef = null, int? ContractGeneration = null, bool Resume = false, bool RequireReport = false);
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
