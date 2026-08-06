using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using UniClaw.ClaudeProvider;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Observation;
using UniClaw.Core.Simulation;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.DeepSeekProvider;
using UniClaw.Device;
using UniClaw.Host.Analysis;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Hooks;
using UniClaw.Host.HostServices;
using UniClaw.Host.Observability;
using UniClaw.Host.Runner;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using UniClaw.Host.Verification;

namespace UniClaw.Host.Commands;

public static class HostExitCodes
{
    public const int Success = 0;
    public const int InvalidArguments = 2;
    public const int PreparationFailure = 10;
    public const int RuntimeFailure = 20;
    public const int Cancelled = 130;
}

public sealed record class HostCommandOptions(
    string Command,
    string DeviceSerial,
    string OutputRoot,
    string ProviderId,
    string? Model,
    string Mode,
    string? ScenarioPath = null,
    string? Purpose = null,
    string? TaskId = null);

public sealed record class DoctorCheck(
    string Name,
    string Status,
    string Message,
    long DurationMs);

public sealed record class DoctorReport(
    string SchemaVersion,
    string DeviceSerial,
    bool Ready,
    ImmutableArray<DoctorCheck> Checks,
    DateTimeOffset Timestamp);

public sealed record class AnalyzeReport(
    string SchemaVersion,
    string RunId,
    string DeviceSerial,
    string ProviderId,
    string? Model,
    string Mode,
    PageAnalysis Analysis,
    string TracePath,
    int DeviceActionsSent,
    DateTimeOffset Timestamp);

public interface IDeviceDoctor
{
    Task<DoctorReport> InspectAsync(CancellationToken cancellationToken = default);
}

public interface IDeviceAnalyzer
{
    Task<AnalyzeReport> AnalyzeAsync(CancellationToken cancellationToken = default);
}

public interface IHostCommandFactory
{
    IDeviceDoctor CreateDoctor(HostCommandOptions options);

    IDeviceAnalyzer CreateAnalyzer(HostCommandOptions options);
}

public interface IHostScenarioCommandFactory
{
    Task<ScenarioRunOutcome> RunScenarioAsync(
        HostCommandOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class HostPreparationException : Exception
{
    public HostPreparationException(string message)
        : base(message)
    {
    }
}

public sealed class DeviceDoctor : IDeviceDoctor
{
    private readonly IAdbSession _runner;
    private readonly IScreenCapture _screenCapture;
    private readonly string _outputRoot;
    private readonly bool _providerReady;
    private readonly ITraceRecorder? _traceRecorder;

    public DeviceDoctor(
        IAdbSession runner,
        IScreenCapture screenCapture,
        string outputRoot,
        bool providerReady,
        ITraceRecorder? traceRecorder = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _screenCapture = screenCapture
                         ?? throw new ArgumentNullException(nameof(screenCapture));
        _outputRoot = Path.GetFullPath(outputRoot);
        _providerReady = providerReady;
        _traceRecorder = traceRecorder;
    }

    public async Task<DoctorReport> InspectAsync(
        CancellationToken cancellationToken = default)
    {
        var checks = ImmutableArray.CreateBuilder<DoctorCheck>();
        var stopwatch = Stopwatch.StartNew();
        // M6: doctor diagnostics go through ITraceRecorder (no parallel diagnostic
        // output). traceRecorder == null keeps the legacy no-trace behavior.
        var runId = _traceRecorder is null
            ? null
            : $"doctor-{Guid.NewGuid():N}";
        if (_traceRecorder is not null)
        {
            await _traceRecorder.StartSessionAsync(
                runId!,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["command"] = "doctor",
                    ["deviceSerial"] = _runner.Serial,
                    ["providerReady"] = _providerReady,
                },
                cancellationToken);
        }

        var failed = false;
        try
        {
            await CheckAdbAsync(
                checks,
                "device",
                "echo ok",
                result => result.Success,
                cancellationToken);
            await RecordLastCheckAsync(checks, runId, cancellationToken);
            await CheckAdbAsync(
                checks,
                "boot",
                "getprop sys.boot_completed",
                result => string.Equals(
                    result.StandardOutput.Trim(),
                    "1",
                    StringComparison.Ordinal),
                cancellationToken);
            await RecordLastCheckAsync(checks, runId, cancellationToken);
            await CheckScreenshotAsync(checks, cancellationToken);
            await RecordLastCheckAsync(checks, runId, cancellationToken);
            checks.Add(new DoctorCheck(
                "provider",
                _providerReady ? "ready" : "not_configured",
                _providerReady
                    ? "Vision provider credentials and model are configured."
                    : "Vision provider credentials or model are missing.",
                0));
            await RecordLastCheckAsync(checks, runId, cancellationToken);
            checks.Add(CheckOutput());
            await RecordLastCheckAsync(checks, runId, cancellationToken);

            failed = !checks.All(check => check.Status == "ready");
            return new DoctorReport(
                "1",
                _runner.Serial,
                checks.All(check => check.Status == "ready"),
                checks.ToImmutable(),
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_traceRecorder is not null)
            {
                await _traceRecorder.RecordErrorAsync(
                    new ErrorRecord(
                        ex.GetType().Name,
                        ex.Message,
                        ErrorSeverity.Error,
                        new TraceContext(TraceId: runId),
                        DateTimeOffset.UtcNow),
                    CancellationToken.None);
            }
            throw;
        }
        finally
        {
            if (_traceRecorder is not null)
            {
                await _traceRecorder.RecordExecutionAsync(
                    new ExecutionRecord(
                        "doctor",
                        failed ? "failed" : "ready",
                        Context: new TraceContext(TraceId: runId),
                        DurationMs: stopwatch.Elapsed.TotalMilliseconds,
                        Timestamp: DateTimeOffset.UtcNow),
                    CancellationToken.None);
                await _traceRecorder.EndSessionAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Record the most recently completed check onto the trace session
    /// (no-op when no recorder is injected).
    /// </summary>
    private async Task RecordLastCheckAsync(
        ImmutableArray<DoctorCheck>.Builder checks,
        string? runId,
        CancellationToken cancellationToken)
    {
        if (_traceRecorder is null)
            return;
        var check = checks[^1];
        await _traceRecorder.RecordExecutionAsync(
            new ExecutionRecord(
                check.Name,
                check.Status,
                Context: new TraceContext(TraceId: runId),
                DurationMs: check.DurationMs,
                Timestamp: DateTimeOffset.UtcNow,
                Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["message"] = check.Message,
                }),
            cancellationToken);
    }

    private async Task CheckAdbAsync(
        ImmutableArray<DoctorCheck>.Builder checks,
        string name,
        string command,
        Func<ShellResult, bool> verify,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _runner.ExecuteShellAsync(command, cancellationToken);
        var ready = result.Success && verify(result);
        checks.Add(new DoctorCheck(
            name,
            ready ? "ready" : result.Success ? "verification_failed" : "adb_failure",
            ready
                ? $"{name} check passed."
                : BuildDiagnostic(result, $"{name} check did not verify."),
            stopwatch.ElapsedMilliseconds));
    }

    private async Task CheckScreenshotAsync(
        ImmutableArray<DoctorCheck>.Builder checks,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var bytes = await _screenCapture.CaptureAsync(cancellationToken);
            checks.Add(new DoctorCheck(
                "screenshot",
                bytes.Length > 0 ? "ready" : "invalid_output",
                bytes.Length > 0
                    ? $"Captured {bytes.Length} bytes."
                    : "Screenshot output was empty.",
                stopwatch.ElapsedMilliseconds));
        }
        catch (AdbCommandException ex)
        {
            checks.Add(new DoctorCheck(
                "screenshot",
                "adb_failure",
                ex.Message,
                stopwatch.ElapsedMilliseconds));
        }
    }

    private DoctorCheck CheckOutput()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Directory.CreateDirectory(_outputRoot);
            var probePath = Path.Combine(
                _outputRoot,
                $".write-probe-{Guid.NewGuid():N}");
            using (File.Create(probePath))
            {
            }
            File.Delete(probePath);
            return new DoctorCheck(
                "output",
                "ready",
                $"Output root is writable: {_outputRoot}",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new DoctorCheck(
                "output",
                "write_failure",
                $"Output root is not writable: {ex.GetType().Name}: {ex.Message}",
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static string BuildDiagnostic(
        ShellResult result,
        string fallback)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardError))
            return result.StandardError.Trim();
        return fallback;
    }
}

public sealed class PageAnalysisDeviceAnalyzer : IDeviceAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly IPageAnalyzer _pageAnalyzer;
    private readonly ITraceRecorder _traceRecorder;
    private readonly HostCommandOptions _options;

    public PageAnalysisDeviceAnalyzer(
        IPageAnalyzer pageAnalyzer,
        ITraceRecorder traceRecorder,
        HostCommandOptions options)
    {
        _pageAnalyzer = pageAnalyzer
                        ?? throw new ArgumentNullException(nameof(pageAnalyzer));
        _traceRecorder = traceRecorder
                         ?? throw new ArgumentNullException(nameof(traceRecorder));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<AnalyzeReport> AnalyzeAsync(
        CancellationToken cancellationToken = default)
    {
        var runId = $"analyze-{Guid.NewGuid():N}";
        var stopwatch = Stopwatch.StartNew();
        await _traceRecorder.StartSessionAsync(
            runId,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["command"] = "analyze",
                ["deviceSerial"] = _options.DeviceSerial,
                ["providerId"] = _options.ProviderId,
                ["model"] = _options.Model ?? "unknown",
                ["mode"] = _options.Mode,
            },
            cancellationToken);
        try
        {
            var analysis = await _pageAnalyzer.AnalyzeCurrentPageAsync(
                               cancellationToken)
                           ?? throw new HostPreparationException(
                               "Page analyzer returned no analysis.");
            stopwatch.Stop();
            var context = new TraceContext(TraceId: runId);
            await _traceRecorder.RecordAICallAsync(
                new AICallRecord(
                    ModelCapabilities.AnalyzeVisual,
                    _options.ProviderId,
                    true,
                    stopwatch.Elapsed.TotalMilliseconds,
                    context,
                    Metadata: new Dictionary<string, object>
                    {
                        ["model"] = _options.Model ?? "unknown",
                        ["mode"] = _options.Mode,
                    },
                    Timestamp: DateTimeOffset.UtcNow),
                cancellationToken);
            await _traceRecorder.RecordExecutionAsync(
                new ExecutionRecord(
                    "page_analysis",
                    "success",
                    SpanType.PageAnalysis,
                    context,
                    PageId: ComputePageFingerprint(analysis),
                    DurationMs: stopwatch.Elapsed.TotalMilliseconds,
                    Timestamp: DateTimeOffset.UtcNow),
                cancellationToken);

            var report = new AnalyzeReport(
                "1",
                runId,
                _options.DeviceSerial,
                _options.ProviderId,
                _options.Model,
                _options.Mode,
                analysis,
                Path.Combine("trace", runId, "trace.jsonl")
                    .Replace(Path.DirectorySeparatorChar, '/'),
                0,
                DateTimeOffset.UtcNow);
            Directory.CreateDirectory(_options.OutputRoot);
            await File.WriteAllTextAsync(
                Path.Combine(_options.OutputRoot, $"{runId}.analysis.json"),
                JsonSerializer.Serialize(report, JsonOptions),
                new UTF8Encoding(false),
                cancellationToken);
            return report;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _traceRecorder.RecordErrorAsync(
                new ErrorRecord(
                    ex.GetType().Name,
                    ex.Message,
                    ErrorSeverity.Error,
                    new TraceContext(TraceId: runId),
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
            throw;
        }
        finally
        {
            await _traceRecorder.EndSessionAsync(CancellationToken.None);
        }
    }

    private static string ComputePageFingerprint(PageAnalysis analysis)
    {
        var stable = JsonSerializer.Serialize(analysis, JsonOptions);
        return Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(stable)))
            .ToLowerInvariant();
    }
}

public sealed class HostCompositionFactory :
    IHostCommandFactory,
    IHostScenarioCommandFactory
{
    public IDeviceDoctor CreateDoctor(HostCommandOptions options)
    {
        var runner = CreateRunner(options.DeviceSerial);
        var traceStorage = new FileTraceStorage(
            new PhysicalFileProvider(),
            Path.Combine(options.OutputRoot, "trace"));
        var traceRecorder = new InMemoryTraceRecorder(traceStorage);
        return new DeviceDoctor(
            runner,
            new AdbScreenCapture(runner),
            options.OutputRoot,
            ProviderReady(options),
            traceRecorder);
    }

    public IDeviceAnalyzer CreateAnalyzer(HostCommandOptions options)
    {
        var runner = CreateRunner(options.DeviceSerial);
        var traceStorage = new FileTraceStorage(
            new PhysicalFileProvider(),
            Path.Combine(options.OutputRoot, "trace"));
        var traceRecorder = new InMemoryTraceRecorder(traceStorage);
        var brain = CreateUniBrain(
            options,
            new AdbScreenCapture(runner),
            traceRecorder);
        return new PageAnalysisDeviceAnalyzer(
            brain.PageAnalyzer,
            traceRecorder,
            options);
    }

    public HostRunServices CreateRunServices(
        HostCommandOptions options,
        ScenarioSnapshot snapshot,
        RunAssetSession assets,
        HttpClient? pythonClient = null,
        string? labelMappingPath = null,
        CurrentPageAnalysisAccessor? accessor = null,
        bool evidenceStorage = false,
        ILogger? hostLogger = null,
        ILogger<SafeActionExecutor>? safetyLogger = null,
        ILogger<InvalidatingPageAnalysisCache>? analysisLogger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(assets);
        var runner = CreateRunner(options.DeviceSerial);
        var isLocal = string.Equals(
            options.ProviderId, "local", StringComparison.OrdinalIgnoreCase);
        // The analyzer reads the primary in-memory store while the mirror writes
        // the same records into the run's durable trace directory.
        var traceStorage = new InMemoryTraceStorage();
        var durableTraceStorage = new FileTraceStorage(
            new PhysicalFileProvider(),
            Path.Combine(assets.RunDirectory, "trace"));
        var traceRecorder = new InMemoryTraceRecorder(
            new MirroringTraceStorage(traceStorage, durableTraceStorage));
        var traceService = new InMemoryTraceService(traceStorage);
        var evaluator = new SettingsSafetyEvaluator(snapshot);
        var safetyContext = new SafetyExecutionContext();
        var safetyJournal = new SafetyDecisionJournal();
        var safetySink = new CompositeSafetyDecisionSink(
            new TraceSafetyDecisionSink(traceRecorder),
            safetyJournal);
        // D-1/D-5: the Core pipeline replaces StepAssetSink. Failures are
        // subscribed to the issue sink (asset_write_failed); counters are read
        // post-drain as trace events — no manifest writeback.
        var pipeline = CreateAssetPipeline(
            assets,
            issue => assets.AppendIssueAsync(issue, CancellationToken.None),
            hostLogger);
        var providerBrain = CreateUniBrain(
            options,
            new AdbScreenCapture(runner),
            traceRecorder,
            pythonClient,
            labelMappingPath,
            pipeline,
            evidenceStorage);

        // UIA hierarchy removed (delete-uia): the single screen-state source is
        // VisionScreenStateProvider driven by the current PageAnalysis
        // (local-vision accessor; null-safe elsewhere — no device hierarchy).
        // Non-local mode wraps the analyzer in ObservationPipeline for
        // back-navigation analysis reuse.
        var screenState = new VisionScreenStateProvider(
            getCurrentAnalysis: () => accessor?.Current);
        IPageAnalyzer innerAnalyzer;
        Action? onBackSuccess = null;

        if (isLocal)
        {
            innerAnalyzer = providerBrain.PageAnalyzer;
        }
        else
        {
            var observationPipeline = new ObservationPipeline(
                providerBrain.PageAnalyzer,
                traceRecorder: traceRecorder);
            innerAnalyzer = observationPipeline;
            onBackSuccess = observationPipeline.MarkBackNavigation;
        }

        var cache = new InvalidatingPageAnalysisCache(innerAnalyzer, analysisLogger);
        // D-197: 分析证据落盘 — run 场景下装饰器把每次分析快照提交到资产管道
        // (assets/{runId}/analysis.jsonl, finalize 时 drain)。
        var pageAnalyzer = accessor is not null
            ? new AnalysisWritingDecorator(cache, accessor, pipeline, assets.RunDirectory)
            : (IPageAnalyzer)cache;

        var brain = new CachedPageAnalysisUniBrain(
            pageAnalyzer,
            providerBrain.Advisor,
            providerBrain.Text);
        var settleDelayMs = int.TryParse(
            Environment.GetEnvironmentVariable("UNICLAW_SETTLE_DELAY_MS"),
            out var s) ? s : 300;
        var safeActions = new SafeActionExecutor(
            new PageInvalidatingActionExecutor(
                new AdbActionExecutor(runner),
                cache.Invalidate,
                onBackSuccess: onBackSuccess,
                settleDelayMs: settleDelayMs),
            evaluator,
            safetySink,
            safetyContext,
            traceService,
            traceRecorder,
            logger: safetyLogger);
        var safeEntry = new SafeEntryActionDriver(
            new AdbEntryActionDriver(runner),
            evaluator,
            safetySink,
            safetyContext);
        var entryPolicyExecutor = new EntryPolicyExecutor(safeEntry);

        var visualPageAnalyzer = accessor is not null
            ? new AnalysisWritingDecorator(providerBrain.PageAnalyzer, accessor, pipeline, assets.RunDirectory)
            : providerBrain.PageAnalyzer;

        return new HostRunServices(
            runner,
            pageAnalyzer,
            visualPageAnalyzer,
            safeActions,
            screenState,
            safeEntry,
            entryPolicyExecutor,
            brain,
            safetyContext,
            evaluator,
            safetySink,
            safetyJournal,
            traceRecorder,
            assets,
            traceService,
            pipeline);
    }

    public async Task<ScenarioRunOutcome> RunScenarioAsync(
        HostCommandOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ScenarioPath))
        {
            throw new HostPreparationException(
                "--scenario is required for run.");
        }

        var snapshot = new ScenarioCatalog().LoadSnapshot(
            options.ScenarioPath);
        var plan = new ScenarioPlanCompiler(CreateIntentExtractor()).Compile(snapshot);
        var scenario = snapshot.Scenario;

        // ── local-vision host wiring: path resolution + Python lifecycle ──
        var isLocal = string.Equals(
            options.ProviderId, "local", StringComparison.OrdinalIgnoreCase);
        PythonVisionService? pythonService = null;
        CurrentPageAnalysisAccessor? accessor = null;
        string? labelMappingPath = null;
        HttpClient? pythonClient = null;

        if (isLocal)
        {
            // D4: Repo root is the single anchor for all local-vision paths.
            // UNICLAW_REPO_ROOT overrides; CWD is the fallback for CLI runs
            // from the repo root.  testhost CWD (bin dir) breaks CWD-relative
            // resolution, so tests must set UNICLAW_REPO_ROOT.
            var repoRoot = Environment.GetEnvironmentVariable(
                "UNICLAW_REPO_ROOT");
            if (string.IsNullOrWhiteSpace(repoRoot))
                repoRoot = Directory.GetCurrentDirectory();
            repoRoot = Path.GetFullPath(repoRoot);

            // D4: Host resolves label-mapping.json path once.  Env var overrides
            // the default project-relative location; the resolved path is set as
            // UNICLAW_LABEL_MAPPING so the Python server can consume it.
            labelMappingPath = Environment.GetEnvironmentVariable(
                "UNICLAW_LABEL_MAPPING");
            if (string.IsNullOrWhiteSpace(labelMappingPath))
            {
                labelMappingPath = Path.Combine(
                    repoRoot, "tools", "local_vision", "label-mapping.json");
            }

            Environment.SetEnvironmentVariable(
                "UNICLAW_LABEL_MAPPING", labelMappingPath);

            // D4: Resolve model path absolutely — the Python server's default
            // is CWD-relative.  Child inherits.
            Environment.SetEnvironmentVariable(
                "UNICLAW_YOLO_MODEL",
                Path.Combine(repoRoot, "artifacts", "local-vision", "models", "android_ui_detection_yolov8", "best.pt"));

            // D4: Resolve server.py absolute path — anchored to repo root.
            var serverScriptPath = Path.Combine(
                repoRoot, "tools", "local_vision", "server.py");

            // D5: uvicorn requires "<module>:<attribute>" import strings;
            // server.py is a package member (relative imports), so it must be
            // launched as tools.local_vision.server:app with --app-dir at the
            // repo root.  Derived from the script path, no CWD dependency.
            var serverModule = "tools.local_vision.server:app";
            var serverAppDir = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(serverScriptPath)!,
                    "..",
                    ".."));

            // D1: Python process lifecycle at RunScenarioAsync level — aligned
            // with engine lifetime.  CreateProviders only assembles the
            // provider dictionary; it does not start the service.
            pythonService = new PythonVisionService(
                serverScriptPath,
                serverModule: serverModule,
                serverAppDir: serverAppDir,
                // Cold start includes paddle first-compile + model warmup,
                // which exceeds the 30s default on a fresh boot.
                readyTimeoutMs: 120_000);
            await pythonService.StartAsync(cancellationToken);
            pythonClient = pythonService.HttpClient;

            // D2: Shared state holder — AnalysisWritingDecorator writes,
            // VisionScreenStateProvider reads.
            accessor = new CurrentPageAnalysisAccessor();
        }

        // ── trace-analyzer metadata enrichment: host machine info (always)
        // + Android system info (ADB-dependent, null-safe) feed the manifest.
        var machineInfo = RunMachineInfoCollector.Collect();

        // A temporary ADB session is created solely for getprop collection;
        // CreateRunServices builds the run-lifetime session further down.
        var adbForInfo = CreateRunner(options.DeviceSerial);
        RunSystemInfo? systemInfo = null;
        try
        {
            systemInfo = await AdbSystemInfoCollector.CollectAsync(
                adbForInfo,
                cancellationToken);
        }
        catch
        {
            // Collection failure is non-fatal
        }
        finally
        {
            if (adbForInfo is IDisposable disposable)
                disposable.Dispose();
            else if (adbForInfo is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
        }

        // ── trace-correlated logging (D-4/D-8): run-scoped logger factory.
        // Console sink always; the run file sink (trace/{runId}/run.log) is
        // attached below once the run id is known — before any logger is
        // created, so every logger carries both sinks. ──
        var logLevel = LogLevelConfig.GetMinimumLevel();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(logLevel);
            builder.AddProvider(new TraceCorrelatedConsoleProvider());
        });

        var assets = await new RunAssetStore(
                new AssetRedactor(
                    [
                        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                        ?? string.Empty,
                        LoadSensenovaApiKey() ?? string.Empty,
                    ]))
            .CreateAsync(
                options.OutputRoot,
                snapshot,
                plan,
                new RunManifestInput(
                    null,
                    null,
                    null,
                    null,
                    options.DeviceSerial,
                    null,
                    options.ProviderId,
                    options.Model,
                    options.Mode,
                    options.Purpose,
                    options.TaskId,
                    systemInfo,
                    machineInfo),
                cancellationToken);

        var runId = assets.Manifest.RunId;

        // Evidence storage gate (default off): direct runs read the env var;
        // the test link injects via integration.config providers.local.evidenceStorage.
        var evidenceStorage = string.Equals(
            Environment.GetEnvironmentVariable("UNICLAW_EVIDENCE_STORAGE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        // ── run-scoped file provider (D-8): trace/{runId}/run.log. Added to
        // the factory BEFORE any logger is created, so every logger carries the
        // file sink; flushed + closed in the finally below — the run log file
        // MUST be closed even on exception paths. One file per run (runId
        // isolation, never interleaved). ──
        var runLogPath = Path.Combine(
            assets.RunDirectory, "trace", runId, "run.log");
        var fileProvider = new TraceCorrelatedFileProvider(runLogPath);
        loggerFactory.AddProvider(fileProvider);

        // ── run boundary (D-4): RunTraceContext is active for the whole run —
        // engine/FSM log lines pick up [t={runId}] via the AsyncLocal channel
        // without parameter plumbing. ──
        RunTraceContext.Instance.Push(runId);
        try
        {
            var hostLogger = loggerFactory.CreateLogger("UniClaw.Host");
            var safetyLogger = loggerFactory.CreateLogger<SafeActionExecutor>();
            var analysisLogger = loggerFactory.CreateLogger<InvalidatingPageAnalysisCache>();
            var services = CreateRunServices(
                options,
                snapshot,
                assets,
                pythonClient,
                labelMappingPath,
                accessor,
                evidenceStorage,
                hostLogger,
                safetyLogger,
                analysisLogger);

            var tStart = DateTimeOffset.UtcNow;
            await services.TraceRecorder.StartSessionAsync(
                runId,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["scenarioId"] = scenario.ScenarioId,
                    ["scenarioHash"] = snapshot.ScenarioHash,
                    ["policyHash"] = snapshot.PolicyHash,
                    ["planId"] = plan.PlanId,
                },
                cancellationToken);

            // L5 — run start
            hostLogger.LogInformation(
                "Run {RunId} started mode={Mode} provider={Provider}",
                runId,
                options.Mode,
                options.ProviderId);

            TraversalResult engineResult;
            RunAssetHook? runAssetHook = null;
            var engineFailed = true;
            try
            {
                // D5: entry policy executes and the reset page is verified BEFORE
                // the engine starts. Composition concern — the engine is not modified.
                await ExecuteEntryAsync(services, plan, scenario, runId, cancellationToken);

                runAssetHook = new RunAssetHook(
                    assets,
                    new AdbScreenCapture(services.Adb),
                    services.AssetPipeline);
                var hooks = ImmutableArray.Create<ITraversalHook>(
                    new SafetyContextHook(
                        services.SafetyContext,
                        runId,
                        scenario.AppPackage,
                        scenario.ResetProcedure.ExpectedPageIdentity,
                        scenario.Boundaries.MaxSteps,
                        scenario.Boundaries.MaxScrolls),
                    runAssetHook,
                    new BoundaryHook(
                        () => ReadCurrentPackageAsync(services.Adb, CancellationToken.None),
                        scenario.AppPackage,
                        scenario.Boundaries.AllowedPages
                            .AddRange(scenario.SuccessCriteria.ExpectedPageIdentities)
                            .Distinct(StringComparer.OrdinalIgnoreCase),
                        services.TraceRecorder,
                        runId,
                        allowFirstLevelChildPages: string.Equals(
                            scenario.Mode,
                            "enumerate_first_level",
                            StringComparison.Ordinal)),
                    new VerifyHook(services.TraceRecorder, runId));

                // D-4: composition root injects real loggers into the
                // FSM/Engine components (optional ctor params; NullLogger
                // default keeps non-composition call sites untouched).
                var engine = services.CreateTraversalEngine(
                    plan,
                    services.Brain,
                    new TraversalEngineConfig
                    {
                        Hooks = hooks,
                        MaxSteps = scenario.Boundaries.MaxSteps,
                        MaxDepth = scenario.Boundaries.MaxDepth,
                        DelayPerStepMs = 0,
                    },
                    logger: loggerFactory.CreateLogger<TraversalEngine>(),
                    fsmLogger: loggerFactory.CreateLogger<TraversalFSM>(),
                    errorHandler: new ErrorHandler(
                        logger: loggerFactory.CreateLogger<ErrorHandler>()));

                // Completion detection is handled externally by TraceTool trace analyze
                // (diagnose / verify). The engine runs directly on the caller's
                // cancellation token; boundaries (maxSteps / maxScrolls / maxDuration)
                // are the only in-process guardrails.
                engineResult = await engine.RunAsync(cancellationToken);
                engineFailed = false;
            }
            finally
            {
                await services.TraceRecorder.EndSessionAsync(CancellationToken.None);
                if (engineFailed)
                {
                    // Failure/cancellation path: no further evidence submissions
                    // follow, so drain now (idempotent) to persist accepted writes.
                    await services.AssetPipeline.DrainAsync(CancellationToken.None);
                }

            }

            // D1 修正: Python process stops AFTER the post-action verification —
            // locate/enumerate run a final VisualPageAnalyzer pass (below) that
            // needs the live pythonClient; disposing here (right after the engine)
            // left the shared HttpClient disposed for that pass.
            ScenarioRunOutcome outcome;
            try
            {
                outcome = new VerificationAnalyzer(
                    services.Trace,
                    services.SafetyJournal,
                    runId).Analyze(engineResult);

                PageAnalysis? finalAnalysis = null;
                if (string.Equals(scenario.Mode, "locate_one_item", StringComparison.Ordinal)
                    || string.Equals(
                        scenario.Mode,
                        "enumerate_first_level",
                        StringComparison.Ordinal))
                {
                    // ExecuteThenStop returns immediately after the successful target
                    // action. Give Android a short stabilization window, then make one
                    // independent post-action visual observation for the success gate.
                    await Task.Delay(750, cancellationToken);
                    // Post-target verification demands AI-quality page identity
                    // (UIA leg removed with the UIA pipeline, delete-uia).
                    finalAnalysis = await services.VisualPageAnalyzer.AnalyzeCurrentPageAsync(
                        cancellationToken);
                    if (runAssetHook is not null)
                        await runAssetHook.RefreshLastAfterAsync();
                }

                Console.Error.WriteLine(
                    $"[DBG] t0={tStart:HH:mm:ss.fff} now={DateTimeOffset.UtcNow:HH:mm:ss.fff} "
                    + $"engineReason={engineResult.CompletionReason} "
                    + $"engineSteps={engineResult.TotalSteps} "
                    + $"actions={engineResult.ActionHistory.Length} "
                    + $"finalPath={finalAnalysis?.CurrentPath.LastOrDefault() ?? "<null>"} "
                    + $"finalItems={finalAnalysis?.Items.Length}");

                // V2: Host writes engine facts + pending_verification; TraceTool judges.
                // enumerate verification stays in Host (not yet migrated).
                if (string.Equals(scenario.Mode, "enumerate_first_level", StringComparison.Ordinal))
                {
                    outcome = await ScenarioCompletionVerifier.Verify(
                        scenario,
                        engineResult,
                        finalAnalysis,
                        outcome,
                        services.Trace,
                        services.SafetyJournal,
#pragma warning disable CS0618 // IsEndOfList is [Obsolete] — see D9 in openspec/changes/e2e-dedup-vision-quality/design.md
                        services.ScreenState.IsEndOfList(),
#pragma warning restore CS0618
                        (category, phase, severity, summary, stepNumber) =>
                        {
                            var issue = assets.CreateIssue(
                                category, phase, severity, summary, stepNumber);
                            return assets.AppendIssueAsync(issue, cancellationToken);
                        });
                }
                else
                {
                    outcome = outcome with
                    {
                        Status = "pending_verification",
                        CompletionReason = engineResult.CompletionReason,
                    };

                    // Write criteria.json snapshot for TraceTool consumption
                    await assets.WriteCriteriaAsync(
                        new VerificationCriteria(
                            scenario.SuccessCriteria.ExpectedPageIdentities,
                            scenario.Mode),
                        cancellationToken);
                }

                // L6 — run end
                hostLogger.LogInformation(
                    "Run {RunId} ended status={Status} duration={DurationMs}ms",
                    runId,
                    outcome.Status,
                    (long)(DateTimeOffset.UtcNow - tStart).TotalMilliseconds);
            }
            finally
            {
                // D1: Python process stops after engine + post-action verification
                // (normal or error).
                if (pythonService is not null)
                {
                    await pythonService.DisposeAsync();
                }
            }

            // Drain all accepted step evidence (including the stabilized
            // post-target capture) before the run result is recorded.
            await services.AssetPipeline.DrainAsync(cancellationToken);
            var stats = services.AssetPipeline.Stats;
            if (stats.WriteFailures > 0 || stats.Dropped > 0)
            {
                await services.TraceRecorder.RecordExecutionAsync(
                    new ExecutionRecord(
                        Action: "assets.sink_failure",
                        Status: "failed",
                        SpanType: SpanType.ErrorHandling,
                        Context: new TraceContext(
                            NodeId: null,
                            StepNumber: null,
                            TraceId: runId),
                        PageId: null,
                        Timestamp: DateTimeOffset.UtcNow,
                        Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["failed_count"] = stats.WriteFailures,
                            ["accepted_count"] = stats.Accepted,
                            ["dropped_count"] = stats.Dropped,
                        }));
            }

            await FinalizeRunAssetsAsync(assets, outcome, engineResult);

            // L8 — run final state
            hostLogger.LogInformation(
                "Run {RunId} final state: {Status} reason={Reason}",
                runId,
                outcome.Status,
                outcome.CompletionReason);
            return outcome;
        }
        finally
        {
            // The run log file MUST be closed even on exception paths.
            RunTraceContext.Instance.Pop();
            fileProvider.Flush();
            fileProvider.Close();
        }
    }

    /// <summary>
    /// Assemble the run-scoped asset pipeline (D-1/D-7): backend store keyed by
    /// UNICLAW_ASSET_BACKEND (default file), rooted at <c>assets/{runId}</c> under
    /// the run directory; each write failure surfaces as an issue entry
    /// (asset_write_failed, path + exception) via <paramref name="issueSink"/>.
    /// </summary>
    private static ITracePipeline CreateAssetPipeline(
        RunAssetSession assets,
        Func<RunIssue, Task> issueSink,
        ILogger? hostLogger = null)
    {
        var backend = Environment.GetEnvironmentVariable("UNICLAW_ASSET_BACKEND");
        if (!string.IsNullOrWhiteSpace(backend)
            && !string.Equals(backend, "file", StringComparison.OrdinalIgnoreCase))
        {
            throw new HostPreparationException(
                $"Unsupported UNICLAW_ASSET_BACKEND '{backend}' — only 'file' is implemented.");
        }

        var runDir = assets.RunDirectory;
        var runId = assets.Manifest.RunId;
        var assetsRoot = Path.Combine(runDir, "assets", runId);
        var store = new FileAssetStore(assetsRoot);

        var failureSink = new AssetPipelineFailureSink(
            (submission, ex) =>
            {
                // L7 — asset write failure, synchronized with the
                // asset_write_failed issue entry below.
                hostLogger?.LogError(
                    ex,
                    "Asset write failed: {RelativePath}",
                    submission.RelativePath);
                var issue = assets.CreateIssue(
                    "reporting", "finalize", "error",
                    $"asset_write_failed: {submission.RelativePath} — {ex.Message}",
                    null);
                issueSink(issue);
            });

        return new TracePipeline(store, runId, failureSink);
    }

    /// <summary>
    /// D5 entry: run the plan's entry policy through the decorated driver and
    /// confirm the reset page is analyzable before the engine takes over.
    /// </summary>
    private static async Task ExecuteEntryAsync(
        HostRunServices services,
        TraversalPlan plan,
        AndroidSettingsScenario scenario,
        string runId,
        CancellationToken cancellationToken)
    {
        var candidate = new SafetyCandidate(
            "launch",
            scenario.AppPackage,
            "settings_home",
            null,
            null,
            scenario.AppPackage,
            1,
            true,
            true,
            0,
            scenario.Boundaries.MaxSteps,
            scenario.Boundaries.MaxScrolls,
            runId,
            0,
            "preparation",
            "entry");
        EntryResult entryResult;
        using (services.SafetyContext.Push(candidate))
        {
            entryResult = await services.EntryPolicyExecutor.ExecuteAsync(
                plan.EntryPolicy,
                new EntryConfig(
                    WaitMode: WaitMode.Polling,
                    WaitTimeoutSeconds: scenario.ResetProcedure.TimeoutSeconds,
                    WaitIntervalMs: 500,
                    ActionDelayMs: 1000),
                scenario.AppPackage,
                cancellationToken);
        }

        if (!entryResult.Success)
        {
            throw new HostPreparationException(
                $"Settings reset/entry failed: {entryResult.Description}");
        }

        // D-19x: reset 验证必须走 raw VisualPageAnalyzer (与 final analysis 一致) 且带 settle 门 —
        // 1) 走 services.PageAnalyzer (InvalidatingPageAnalysisCache) 会把首帧/半渲染的退化分析
        //    (如 1 item) 填入引擎缓存, 引擎全部 step 复用 → 0 actions → 整 run 失败;
        // 2) 首帧分析可能截到列表未渲染完的瞬间 (本次实测: launch 后 8s 分析得 1 item,
        //    9s 后同屏 21 items), settle 门等待内容真实出现再放行引擎。
        var resetAnalysis = await AnalyzeUntilSettledAsync(
            services.VisualPageAnalyzer,
            TimeSpan.FromSeconds(scenario.ResetProcedure.TimeoutSeconds),
            cancellationToken);
        if (resetAnalysis is null)
        {
            throw new HostPreparationException(
                "Reset page analysis returned no analysis; the reset page was not verified.");
        }
    }

    /// <summary>
    /// 轮询分析直到页面内容可见 (≥3 items 或可滚动), 预算内未就绪则返回最后一次分析。
    /// 首帧/半渲染的退化结果 (少量 item) 不满足条件 → 继续等渲染 settle。
    /// </summary>
    private static async Task<PageAnalysis?> AnalyzeUntilSettledAsync(
        IPageAnalyzer analyzer,
        TimeSpan budget,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        PageAnalysis? last = null;
        while (stopwatch.Elapsed < budget)
        {
            last = await analyzer.AnalyzeCurrentPageAsync(cancellationToken);
            if (last is null)
                return null;
            if (last.Items.Length >= 3 || last.HasScroll)
                return last;
            await Task.Delay(1000, cancellationToken);
        }
        return last;
    }

    /// <summary>
    /// Mirror the legacy runner's asset finalization for the engine path.
    /// </summary>
    private static async Task FinalizeRunAssetsAsync(
        RunAssetSession assets,
        ScenarioRunOutcome outcome,
        TraversalResult engineResult)
    {
        var success = outcome.Status == "success";
        await assets.FinalizeAsync(
            new RunResult(
                RunAssetVocabulary.SchemaVersion,
                assets.Manifest.RunId,
                outcome.Status,
                outcome.CompletionReason,
                outcome.DiscoveredEntries,
                outcome.VisitedEntries,
                outcome.SkippedEntries,
                outcome.FailedEntries,
                outcome.ActionsAttempted,
                outcome.ActionsSucceeded,
                outcome.SafetyAllowed,
                outcome.SafetyDenied,
                outcome.Steps,
                outcome.Scrolls,
                (long)Math.Round(engineResult.ElapsedSeconds * 1000),
                $"trace/{assets.Manifest.RunId}/trace.jsonl",
                $"trace/{assets.Manifest.RunId}/run.log",
                outcome.IssueFingerprints,
                outcome.SuccessCriteriaSatisfied,
                outcome.SuccessEvidence.IsDefault
                    ? ImmutableArray<string>.Empty
                    : outcome.SuccessEvidence,
                DateTimeOffset.UtcNow),
            CancellationToken.None);
    }

    /// <summary>
    /// Read the current foreground package via <c>dumpsys activity activities</c>
    /// (mirrors <c>AdbScenarioObservationSource</c>); the engine context carries
    /// no package name, so the <see cref="BoundaryHook"/> needs this device read.
    /// </summary>
    private static async Task<string> ReadCurrentPackageAsync(
        IAdbSession session,
        CancellationToken cancellationToken)
    {
        var result = await session.ExecuteShellAsync(
            "dumpsys activity activities",
            cancellationToken);
        if (!result.Success)
        {
            throw new HostPreparationException(
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? "Could not read current package."
                    : result.StandardError.Trim());
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            result.StandardOutput,
            @"(?:mResumedActivity|topResumedActivity|mCurrentFocus|mFocusedApp)[^\r\n]*?\s(?<package>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+)/",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["package"].Value : "unknown";
    }

    private static IAdbSession CreateRunner(string serial)
    {
        var configuredRoot =
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")
            ?? Environment.GetEnvironmentVariable("ANDROID_HOME");
        var platformAdb = configuredRoot is null
            ? null
            : Path.Combine(configuredRoot, "platform-tools", "adb");
        if (platformAdb is null
            && OperatingSystem.IsMacOS())
        {
            platformAdb = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                "Library",
                "Android",
                "sdk",
                "platform-tools",
                "adb");
        }
        var adbPath = platformAdb is not null && File.Exists(platformAdb)
            ? platformAdb
            : "adb";

        var backend = Environment.GetEnvironmentVariable("UNICLAW_ADB_BACKEND");
        if (string.Equals(backend, "process", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessAdbSession(
                new AdbCommandRunnerOptions(
                    serial,
                    adbPath,
                    TimeSpan.FromSeconds(30)));
        }

        return new AdvancedSharpAdbSession(
            serial,
            adbPath,
            TimeSpan.FromSeconds(30));
    }

    private static bool ProviderReady(HostCommandOptions options) =>
        string.Equals(options.ProviderId, "mock", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(options.ProviderId, "claude", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))
            && !string.IsNullOrWhiteSpace(options.Model))
        || (string.Equals(options.ProviderId, "sensenova", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(LoadSensenovaApiKey())
            && !string.IsNullOrWhiteSpace(options.Model))
        || (string.Equals(options.ProviderId, "qwen", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(LoadQwenApiKey()));

    /// <summary>
    /// Build a credential-free <see cref="UniBrainConfig"/> from <paramref name="options"/>.
    /// All three sub-interfaces route to <see cref="UniBrainConfig.DefaultProvider"/>
    /// (CapabilityRouting null → factory falls back to DefaultProvider for every capability).
    /// </summary>
    private static UniBrainConfig CreateUniBrainConfig(HostCommandOptions options)
    {
        var providerId = string.IsNullOrWhiteSpace(options.ProviderId)
            ? "mock"
            : options.ProviderId.Trim();

        // Local vision mode: route vision to local-vision, text to deepseek.
        if (string.Equals(providerId, "local", StringComparison.OrdinalIgnoreCase))
        {
            return new UniBrainConfig(
                DefaultProvider: "deepseek",  // non-vision capabilities (text)
                CapabilityRouting: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["page_analysis"] = "local-vision",  // vision capability
                }.ToImmutableDictionary());
        }

        return new UniBrainConfig(DefaultProvider: providerId);
    }

    /// <summary>
    /// Build the providerId → <see cref="IModelProvider"/> dictionary consumed by
    /// <see cref="UniBrainFactory"/>. Credentials feed this dict, not the config.
    /// mock → <see cref="MockModelProvider"/> seeded with the fixed Settings analysis
    /// JSON (replaces the deleted Host-owned deterministic mock provider);
    /// sensenova → <see cref="OpenAiCompatibleVisionProvider"/>; claude →
    /// <see cref="AnthropicModelProvider"/>.
    /// </summary>
    private static IReadOnlyDictionary<string, IModelProvider> CreateProviders(
        HostCommandOptions options,
        HttpClient? pythonClient = null,
        string? labelMappingPath = null,
        ITracePipeline? pipeline = null,
        bool evidenceStorage = false)
    {
        if (string.Equals(
                options.ProviderId,
                "mock",
                StringComparison.OrdinalIgnoreCase))
        {
            var fixture = new MockModelFixture(
                ImmutableDictionary.CreateRange(
                    new[]
                    {
                        KeyValuePair.Create(
                            ModelCapabilities.AnalyzeVisual,
                            new MockModelEntry(SettingsAnalysisJson, 0, 0, 0, true)),
                    }));
            return new Dictionary<string, IModelProvider>(StringComparer.Ordinal)
            {
                ["mock"] = new MockModelProvider(fixture, "mock"),
            };
        }
        if (string.Equals(
            options.ProviderId,
            "sensenova",
            StringComparison.OrdinalIgnoreCase))
        {
            var sensenovaApiKey = LoadSensenovaApiKey();
            if (string.IsNullOrWhiteSpace(sensenovaApiKey)
                || string.IsNullOrWhiteSpace(options.Model))
            {
                throw new HostPreparationException(
                    "SENSENOVA_API_KEY (or ~/.litellm/secrets.json) and "
                    + "--model/UNICLAW_MODEL are required for analyze.");
            }

            var baseUrl = Environment.GetEnvironmentVariable(
                               "SENSENOVA_BASE_URL")
                           ?? "https://token.sensenova.cn";
            return new Dictionary<string, IModelProvider>(StringComparer.Ordinal)
            {
                ["sensenova"] = new OpenAiCompatibleVisionProvider(
                    new HttpClient(),
                    new OpenAiCompatibleProviderConfig(
                        sensenovaApiKey,
                        options.Model,
                        baseUrl)),
            };
        }
        if (string.Equals(
            options.ProviderId,
            "qwen",
            StringComparison.OrdinalIgnoreCase))
        {
            var qwenApiKey = LoadQwenApiKey();
            if (string.IsNullOrWhiteSpace(qwenApiKey))
            {
                throw new HostPreparationException(
                    "QWEN_API_KEY (or ~/.litellm/secrets.json) is required for qwen provider.");
            }

            // Default model: --model > UNICLAW_MODEL > QWEN_MODEL > "qwen3.7-plus"
            var model = !string.IsNullOrWhiteSpace(options.Model)
                ? options.Model
                : Environment.GetEnvironmentVariable("QWEN_MODEL") ?? "qwen3.7-plus";

            // OpenAiCompatibleVisionProvider appends "/v1/chat/completions", so the
            // base must be origin-style (no API version suffix). A trailing "/v1"
            // would produce a double "/v1/v1/chat/completions" → MaaS 400 url error.
            var baseUrl = Environment.GetEnvironmentVariable("QWEN_BASE_URL")
                ?? "https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode";

            var visionMode = Environment.GetEnvironmentVariable("UNICLAW_VISION_MODE") ?? "single";

            var providers = new Dictionary<string, IModelProvider>(StringComparer.Ordinal)
            {
                ["qwen"] = new OpenAiCompatibleVisionProvider(
                    new HttpClient(),
                    new OpenAiCompatibleProviderConfig(qwenApiKey, model, baseUrl)),
            };

            // Two-stage mode: add a second provider for Stage 2 (text-only reasoning).
            // Uses deepseek-v4-flash-0731 by default — fast + cheap for text-only.
            if (string.Equals(visionMode, "two_stage", StringComparison.OrdinalIgnoreCase))
            {
                var dsModel = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL")
                    ?? "deepseek-v4-flash-0731";
                providers["deepseek"] = new OpenAiCompatibleVisionProvider(
                    new HttpClient(),
                    new OpenAiCompatibleProviderConfig(qwenApiKey, dsModel, baseUrl));
            }

            return providers;
        }
        if (string.Equals(
            options.ProviderId,
            "local",
            StringComparison.OrdinalIgnoreCase))
        {
            // labelMappingPath and pythonClient are resolved + created in
            // RunScenarioAsync.  Host owns path resolution and Python lifecycle;
            // CreateProviders only assembles the provider dictionary.
            if (string.IsNullOrWhiteSpace(labelMappingPath))
            {
                throw new HostPreparationException(
                    "labelMappingPath is required for local-vision mode.");
            }
            if (pythonClient is null)
            {
                throw new HostPreparationException(
                    "pythonClient is required for local-vision mode.");
            }

            var providers = new Dictionary<string, IModelProvider>(StringComparer.Ordinal)
            {
                // D-7: evidence storage 门控 — evidenceStorage off (默认) → 不注入
                // pipeline/traceContext → provider 内 evidence 提交完全 no-op。
                ["local-vision"] = new UniClaw.LocalVisionProvider.LocalVisionProvider(
                    pythonClient,
                    labelMappingConfigPath: labelMappingPath,
                    pipeline: evidenceStorage ? pipeline : null,
                    traceContext: evidenceStorage ? EngineStepSpanContext.Instance : null),
            };

            // Text-only provider for non-vision capabilities (parse_instruction,
            // decide_next_action, verify_page_type, screen_safety).
            var deepseekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (!string.IsNullOrWhiteSpace(deepseekApiKey))
            {
                var dsModel = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? "deepseek-v4-flash-0731";
                var dsBaseUrl = Environment.GetEnvironmentVariable("DEEPSEEK_BASE_URL") ?? "https://api.deepseek.com";
                providers["deepseek"] = new DeepSeekModelProvider(
                    new HttpClient(),
                    new DeepSeekProviderConfig(deepseekApiKey, dsModel, dsBaseUrl));
            }
            else
            {
                throw new HostPreparationException(
                    "DEEPSEEK_API_KEY is required for local-vision mode. "
                    + "Local vision handles screenshots; text reasoning (decide_next_action, "
                    + "parse_instruction) requires a separate text provider. "
                    + "Set DEEPSEEK_API_KEY to the DeepSeek API key.");
            }

            return providers;
        }
        if (!string.Equals(
            options.ProviderId,
            "claude",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new HostPreparationException(
                $"Provider '{options.ProviderId}' does not expose the required vision capability.");
        }

        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(options.Model))
        {
            throw new HostPreparationException(
                "ANTHROPIC_API_KEY and --model/UNICLAW_MODEL are required for analyze.");
        }
        return new Dictionary<string, IModelProvider>(StringComparer.Ordinal)
        {
            ["claude"] = new AnthropicModelProvider(
                new HttpClient(),
                new AnthropicProviderConfig(apiKey, options.Model)),
        };
    }

    /// <summary>
    /// Assemble an <see cref="IUniBrain"/> via <see cref="UniBrainFactory"/> from
    /// <paramref name="options"/> + the screen capture + trace recorder seams.
    /// Both the analyze and run paths route through this method (option A), so the
    /// Host owns no hand-<c>new PageAnalyzer</c> and no Host-owned
    /// <c>IModelProvider</c> leaks to callers.
    /// </summary>
    private static IUniBrain CreateUniBrain(
        HostCommandOptions options,
        IScreenCapture screenCapture,
        ITraceRecorder recorder,
        HttpClient? pythonClient = null,
        string? labelMappingPath = null,
        ITracePipeline? pipeline = null,
        bool evidenceStorage = false)
    {
        var config = CreateUniBrainConfig(options);
        var providers = CreateProviders(
            options, pythonClient, labelMappingPath, pipeline, evidenceStorage);
        var promptLibrary = new PromptLibrary(
            PromptTemplateRegistry.AnalyzeVisual,
            PromptTemplateRegistry.AnalyzeVisualLite,
            PromptTemplateRegistry.DecideNextAction,
            PromptTemplateRegistry.ParseInstruction);
        // trace-parent-linkage 2.7: PageAnalyzer 注入 EngineStepSpanContext.Instance ——
        // AsyncLocal 通道：引擎每次 engine.step scope 开启时 Set 当前 step span id，PageAnalyzer
        // 在同一 async 流内读取（ai.call 父链）。非引擎入口（AsyncLocal 为 null）→ ai.call
        // 保留孤儿根，行为与 M0 一致。
        return UniBrainFactory.Create(config, providers, promptLibrary, screenCapture, recorder, EngineStepSpanContext.Instance);
    }

    /// <summary>
    /// 创建 <see cref="IIntentExtractor"/> — 当 Sensenova（日日新）凭证可用时
    /// 使用 deepseek-v4-flash 模型经日日新端点进行 AI 意图推理；
    /// 否则返回 null，由 <see cref="ScenarioPlanCompiler"/> 回落到确定性机械映射。
    /// 读取 SENSENOVA_API_KEY（或 ~/.litellm/secrets.json）+ SENSENOVA_MODEL +
    /// SENSENOVA_BASE_URL（默认 https://token.sensenova.cn）。
    /// </summary>
    public static IIntentExtractor? CreateIntentExtractor()
    {
        var apiKey = LoadSensenovaApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var model = Environment.GetEnvironmentVariable("SENSENOVA_MODEL") ?? "deepseek-v4-flash";
        var baseUrl = Environment.GetEnvironmentVariable("SENSENOVA_BASE_URL") ?? "https://token.sensenova.cn";
        var config = new OpenAiCompatibleProviderConfig(apiKey, model, baseUrl);
        return new IntentExtractor(new OpenAiCompatibleVisionProvider(new HttpClient(), config));
    }

    /// <summary>
    /// Fixed Settings analysis JSON returned by the mock provider's
    /// <c>analyze_visual</c> preset. Previously held by the deleted Host-owned
    /// deterministic mock provider; now a <see cref="MockModelFixture"/> preset.
    /// </summary>
    private const string SettingsAnalysisJson =
        """
        {
          "level1_dir": "left",
          "level1_menus": [
            {"name": "Network \\u0026 internet",  "coordinate": {"x": 0.50, "y": 0.15}, "active": false},
            {"name": "Connected devices",   "coordinate": {"x": 0.50, "y": 0.22}, "active": false},
            {"name": "Apps",                "coordinate": {"x": 0.50, "y": 0.29}, "active": false},
            {"name": "Notifications",       "coordinate": {"x": 0.50, "y": 0.36}, "active": false},
            {"name": "Battery",             "coordinate": {"x": 0.50, "y": 0.43}, "active": false},
            {"name": "Storage",             "coordinate": {"x": 0.50, "y": 0.50}, "active": false},
            {"name": "Sound \\u0026 vibration", "coordinate": {"x": 0.50, "y": 0.57}, "active": false},
            {"name": "Display",             "coordinate": {"x": 0.50, "y": 0.64}, "active": false},
            {"name": "Accessibility",       "coordinate": {"x": 0.50, "y": 0.71}, "active": false},
            {"name": "Security",            "coordinate": {"x": 0.50, "y": 0.78}, "active": false},
            {"name": "About emulated device", "coordinate": {"x": 0.50, "y": 0.85}, "active": false}
          ],
          "level2_dir": "left",
          "level2_menus": [],
          "current_path": ["Settings"],
          "items": [
            {"name": "Network \\u0026 internet",  "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.15}},
            {"name": "Connected devices",   "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.22}},
            {"name": "Apps",                "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.29}},
            {"name": "Notifications",       "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.36}},
            {"name": "Battery",             "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.43}},
            {"name": "Storage",             "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.50}},
            {"name": "Sound \\u0026 vibration", "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.57}},
            {"name": "Display",             "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.64}},
            {"name": "Accessibility",       "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.71}},
            {"name": "Security",            "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.78}},
            {"name": "About emulated device", "type": "menu_item", "coordinate": {"x": 0.50, "y": 0.85}}
          ],
          "is_popup": false,
          "popup_info": null,
          "close_button": null,
          "back_button": null,
          "has_scroll": true,
          "is_end_of_list": false
        }
        """;

    private static string? LoadSensenovaApiKey()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(
            "SENSENOVA_API_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".litellm",
            "secrets.json");
        if (!File.Exists(path))
            return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty(
                       "SENSENOVA_API_KEY",
                       out var value)
                   ? value.GetString()
                   : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Load Qwen (通义千问) API key from <c>QWEN_API_KEY</c> env var or
    /// <c>~/.litellm/secrets.json</c>.
    /// </summary>
    private static string? LoadQwenApiKey()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("QWEN_API_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".litellm",
            "secrets.json");
        if (!File.Exists(path))
            return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("QWEN_API_KEY", out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 从 <c>UNICLAW_COMPLETION_POLL_MS</c> 解析 CompletionMonitor 轮询间隔（毫秒）。
    /// 未设或非法值 → null（回退默认 500ms）。
    /// </summary>
    private static TimeSpan? TryParsePollInterval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (int.TryParse(raw.Trim(), out var ms) && ms > 0)
            return TimeSpan.FromMilliseconds(ms);
        return null;
    }
}

/// <summary>
/// D-5: Host-side subscription to pipeline write failures. The Core pipeline
/// emits; this adapter forwards each failure to the assembly-supplied callback
/// (which writes an <c>asset_write_failed</c> issue entry).
/// </summary>
internal sealed class AssetPipelineFailureSink : IPipelineFailureSink
{
    private readonly Action<AssetSubmission, Exception> _onFailure;

    public AssetPipelineFailureSink(Action<AssetSubmission, Exception> onFailure)
    {
        _onFailure = onFailure ?? throw new ArgumentNullException(nameof(onFailure));
    }

    public void OnWriteFailed(AssetSubmission submission, Exception exception)
        => _onFailure(submission, exception);
}

public sealed record class HostRunServices(
    IAdbSession Adb,
    IPageAnalyzer PageAnalyzer,
    IPageAnalyzer VisualPageAnalyzer,
    IActionExecutor ActionExecutor,
    IObservableScreenStateProvider ScreenState,
    IEntryActionDriver EntryActionDriver,
    IEntryPolicyExecutor EntryPolicyExecutor,
    IUniBrain Brain,
    ISafetyExecutionContext SafetyContext,
    ISafetyEvaluator SafetyEvaluator,
    ISafetyDecisionSink SafetyDecisionSink,
    SafetyDecisionJournal SafetyJournal,
    ITraceRecorder TraceRecorder,
    RunAssetSession Assets,
    ITraceQuery Trace,
    ITracePipeline AssetPipeline)
{
    public TraversalEngine CreateTraversalEngine(
        TraversalPlan plan,
        IUniBrain brain,
        TraversalEngineConfig? config = null,
        ILogger<TraversalEngine>? logger = null,
        ILogger<TraversalFSM>? fsmLogger = null,
        ErrorHandler? errorHandler = null) =>
        new(
            plan,
            brain,
            ScreenState,
            ActionExecutor,
            config,
            TraceRecorder,
            logger: logger,
            fsmLogger: fsmLogger,
            errorHandler: errorHandler);
}

public sealed class HostApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly IHostCommandFactory _factory;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public HostApplication(
        IHostCommandFactory factory,
        TextWriter output,
        TextWriter error)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = Parse(args);
            if (options is null)
            {
                await _error.WriteLineAsync(
                    "Usage: uniclaw <doctor|analyze|run> --device <serial> [--scenario <file>] [--output <path>] [--provider <mock|claude|sensenova|qwen>] [--model <model>] [--purpose <purpose>] [--task-id <id>]");
                return HostExitCodes.InvalidArguments;
            }

            if (options.Command == "doctor")
            {
                var report = await _factory
                    .CreateDoctor(options)
                    .InspectAsync(cancellationToken);
                await _output.WriteLineAsync(
                    JsonSerializer.Serialize(report, JsonOptions));
                return report.Ready
                    ? HostExitCodes.Success
                    : HostExitCodes.PreparationFailure;
            }

            if (options.Command == "analyze")
            {
                var analysis = await _factory
                    .CreateAnalyzer(options)
                    .AnalyzeAsync(cancellationToken);
                await _output.WriteLineAsync(
                    JsonSerializer.Serialize(analysis, JsonOptions));
                return HostExitCodes.Success;
            }

            if (_factory is not IHostScenarioCommandFactory scenarioFactory)
            {
                throw new HostPreparationException(
                    "Host composition does not support scenario execution.");
            }
            var outcome = await scenarioFactory.RunScenarioAsync(
                options,
                cancellationToken);
            await _output.WriteLineAsync(
                JsonSerializer.Serialize(outcome, JsonOptions));
            return outcome.Status switch
            {
                "success" => HostExitCodes.Success,
                "blocked" or "incomplete" =>
                    HostExitCodes.PreparationFailure,
                "cancelled" => HostExitCodes.Cancelled,
                _ => HostExitCodes.RuntimeFailure,
            };
        }
        catch (OperationCanceledException)
        {
            await _error.WriteLineAsync("cancelled");
            return HostExitCodes.Cancelled;
        }
        catch (HostPreparationException ex)
        {
            await _error.WriteLineAsync($"preparation_failure: {ex.Message}");
            return HostExitCodes.PreparationFailure;
        }
        catch (Exception ex)
        {
            await _error.WriteLineAsync(
                $"runtime_failure: {ex.GetType().Name}: {ex.Message}");
            return HostExitCodes.RuntimeFailure;
        }
    }

    private static HostCommandOptions? Parse(string[] args)
    {
        if (args.Length == 0
            || args[0] is not ("doctor" or "analyze" or "run"))
        {
            return null;
        }

        string? device = null;
        var output = Path.GetFullPath(
            Environment.GetEnvironmentVariable("UNICLAW_OUTPUT")
            ?? "artifacts/runs/commands");
        var provider = Environment.GetEnvironmentVariable("UNICLAW_PROVIDER")
                       ?? "claude";
        var model = Environment.GetEnvironmentVariable("UNICLAW_MODEL");
        var mode = Environment.GetEnvironmentVariable("UNICLAW_VISION_MODE")
                   ?? "mode-a";
        string? scenarioPath = null;
        var purpose = Environment.GetEnvironmentVariable("UNICLAW_RUN_PURPOSE");
        var taskId = Environment.GetEnvironmentVariable("UNICLAW_TASK_ID");

        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length)
                return null;
            var value = args[index + 1];
            switch (args[index])
            {
                case "--device":
                    device = value;
                    break;
                case "--output":
                    output = Path.GetFullPath(value);
                    break;
                case "--provider":
                    provider = value;
                    break;
                case "--model":
                    model = value;
                    break;
                case "--mode":
                    mode = value;
                    break;
                case "--purpose":
                    purpose = value;
                    break;
                case "--task-id":
                    taskId = value;
                    break;
                case "--scenario":
                    scenarioPath = Path.GetFullPath(value);
                    break;
                default:
                    return null;
            }
        }

        return string.IsNullOrWhiteSpace(device)
               || args[0] == "run"
               && string.IsNullOrWhiteSpace(scenarioPath)
            ? null
            : new HostCommandOptions(
                args[0],
                device.Trim(),
                output,
                provider.Trim(),
                model?.Trim(),
                mode.Trim(),
                scenarioPath,
                purpose?.Trim(),
                taskId?.Trim());
    }
}
