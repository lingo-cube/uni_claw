using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.ClaudeProvider;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Observation;
using UniClaw.Core.Simulation;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.DeepSeekProvider;
using UniClaw.Device;
using UniClaw.Host.Analysis;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Hooks;
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
    string? ScenarioPath = null);

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
    private readonly IAdbCommandRunner _runner;
    private readonly IScreenCapture _screenCapture;
    private readonly string _outputRoot;
    private readonly bool _providerReady;

    public DeviceDoctor(
        IAdbCommandRunner runner,
        IScreenCapture screenCapture,
        string outputRoot,
        bool providerReady)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _screenCapture = screenCapture
                         ?? throw new ArgumentNullException(nameof(screenCapture));
        _outputRoot = Path.GetFullPath(outputRoot);
        _providerReady = providerReady;
    }

    public async Task<DoctorReport> InspectAsync(
        CancellationToken cancellationToken = default)
    {
        var checks = ImmutableArray.CreateBuilder<DoctorCheck>();
        await CheckAdbAsync(
            checks,
            "device",
            ["get-state"],
            result => string.Equals(
                result.StandardOutput.Trim(),
                "device",
                StringComparison.OrdinalIgnoreCase),
            cancellationToken);
        await CheckAdbAsync(
            checks,
            "boot",
            ["shell", "getprop", "sys.boot_completed"],
            result => string.Equals(
                result.StandardOutput.Trim(),
                "1",
                StringComparison.Ordinal),
            cancellationToken);
        await CheckScreenshotAsync(checks, cancellationToken);
        await CheckAdbAsync(
            checks,
            "uiautomator",
            ["exec-out", "uiautomator", "dump", "/dev/tty"],
            result => result.StandardOutput.Contains(
                "<hierarchy",
                StringComparison.OrdinalIgnoreCase),
            cancellationToken);
        checks.Add(new DoctorCheck(
            "provider",
            _providerReady ? "ready" : "not_configured",
            _providerReady
                ? "Vision provider credentials and model are configured."
                : "Vision provider credentials or model are missing.",
            0));
        checks.Add(CheckOutput());

        return new DoctorReport(
            "1",
            _runner.Serial,
            checks.All(check => check.Status == "ready"),
            checks.ToImmutable(),
            DateTimeOffset.UtcNow);
    }

    private async Task CheckAdbAsync(
        ImmutableArray<DoctorCheck>.Builder checks,
        string name,
        IEnumerable<string> arguments,
        Func<AdbCommandResult, bool> verify,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _runner.RunAsync(
            AdbCommandRequest.Create(arguments, TimeSpan.FromSeconds(20)),
            cancellationToken);
        ThrowIfCancelled(result, cancellationToken);
        var ready = result.Succeeded && verify(result);
        checks.Add(new DoctorCheck(
            name,
            ready ? "ready" : result.Failure?.Kind ?? "verification_failed",
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
                ex.Result.Failure?.Kind ?? "adb_failure",
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
        AdbCommandResult result,
        string fallback)
    {
        if (result.Failure is not null)
            return $"{result.Failure.Kind}: {result.Failure.Message}";
        if (!string.IsNullOrWhiteSpace(result.StandardError))
            return result.StandardError.Trim();
        return fallback;
    }

    private static void ThrowIfCancelled(
        AdbCommandResult result,
        CancellationToken cancellationToken)
    {
        if (result.Failure?.Kind == "cancelled")
            throw new OperationCanceledException(
                result.Failure.Message,
                cancellationToken);
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
        return new DeviceDoctor(
            runner,
            new AdbScreenCapture(runner),
            options.OutputRoot,
            ProviderReady(options));
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
        RunAssetSession assets)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(assets);
        var runner = CreateRunner(options.DeviceSerial);
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
            new RunAssetSafetyDecisionSink(assets),
            new TraceSafetyDecisionSink(traceRecorder),
            safetyJournal);
        var providerBrain = CreateUniBrain(
            options,
            new AdbScreenCapture(runner),
            traceRecorder);
        var screenState = new AdbScreenStateProvider(runner);
        var captureStore = new StepCaptureStore();
        var assetSink = new StepAssetSink();
        // D1/D6: the UIA→AI cascade lives in Core's ObservationPipeline. The
        // pipeline consumes the before-step capture when valid (zero extra
        // ADB refresh) and reads the device's UIAutomator availability flag
        // (first dump failure → UIA_disabled for the session, AC5).
        var observationPipeline = new ObservationPipeline(
            providerBrain.PageAnalyzer,
            screenState,
            captureStore: captureStore,
            traceRecorder: traceRecorder);
        var pageAnalyzer = new InvalidatingPageAnalysisCache(
            observationPipeline);
        var brain = new CachedPageAnalysisUniBrain(
            pageAnalyzer,
            providerBrain.Advisor,
            providerBrain.Text);
        var safeActions = new SafeActionExecutor(
            new PageInvalidatingActionExecutor(
                new AdbActionExecutor(runner),
                pageAnalyzer.Invalidate,
                captureStore,
                // D2/AC6: after a successful back, the pipeline reuses the
                // pre-back page analysis — no dump, no AI.
                onBackSuccess: observationPipeline.MarkBackNavigation),
            evaluator,
            safetySink,
            safetyContext,
            traceService,
            traceRecorder);
        var safeEntry = new SafeEntryActionDriver(
            new AdbEntryActionDriver(runner),
            evaluator,
            safetySink,
            safetyContext);
        var entryPolicyExecutor = new EntryPolicyExecutor(safeEntry);

        return new HostRunServices(
            runner,
            pageAnalyzer,
            providerBrain.PageAnalyzer,
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
            captureStore,
            assetSink);
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
                    options.Mode),
                cancellationToken);
        var services = CreateRunServices(options, snapshot, assets);
        var runId = assets.Manifest.RunId;

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
                services.ScreenState,
                services.CaptureStore,
                services.AssetSink);
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

            var engine = services.CreateTraversalEngine(
                plan,
                services.Brain,
                new TraversalEngineConfig
                {
                    Hooks = hooks,
                    MaxSteps = scenario.Boundaries.MaxSteps,
                    MaxDepth = scenario.Boundaries.MaxDepth,
                    DelayPerStepMs = 300,
                });

            // ── trace-span-observability P4-P6: baseline + real-time completion monitor ──
            // The monitor polls the span tree every 500 ms while the engine runs and
            // cancels the linked CTS on a Halt/Terminate-class verdict. The engine treats
            // that cancellation as a normal exit (TraversalResult.Reasons.Cancelled), so
            // the run flows through the regular success path (no OCE escapes here).
            var baselineBuilder = new BaselineBuilder(services.Trace);
            var baselineProfile = BaselineProfile.Load(scenario.ScenarioId);
            var analyzers = ImmutableArray.Create<ICompletionAnalyzer>(
                new EnumerateCompletionAnalyzer(services.TraceRecorder, baselineProfile),
                new ErrorLoopAnalyzer(services.TraceRecorder));
            using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var monitor = new CompletionMonitor(
                analyzers,
                services.Trace,
                services.TraceRecorder,
                monitorCts,
                pollInterval: TimeSpan.FromMilliseconds(500));
            _ = monitor.StartAsync();

            try
            {
                engineResult = await engine.RunAsync(monitorCts.Token);
                engineFailed = false;
            }
            finally
            {
                // Stop the poll loop; the linked CTS is NOT cancelled here — only
                // analyzers trigger cancellation (CompletionMonitor contract).
                monitor.Stop();
            }

            // P4: append this run's aggregate to the scenario's baseline file. The span
            // tree remains readable after the engine finishes, so thresholds for the
            // NEXT run pick up this run's data.
            await baselineBuilder.AppendRunAsync(scenario.ScenarioId, cancellationToken);
        }
        finally
        {
            await services.TraceRecorder.EndSessionAsync(CancellationToken.None);
            if (engineFailed)
            {
                // Failure/cancellation path: no further evidence submissions
                // follow, so drain now (idempotent) to persist accepted writes.
                await services.AssetSink.DrainAsync(CancellationToken.None);
            }
        }

        var outcome = new VerificationAnalyzer(
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
            // Post-target verification demands AI-quality page identity;
            // UIAutomator-first is used during traversal only.
            finalAnalysis = await services.VisualPageAnalyzer.AnalyzeCurrentPageAsync(
                cancellationToken);
            runAssetHook?.RefreshLastAfterAsync();
        }

        outcome = ScenarioCompletionVerifier.Verify(
            scenario,
            engineResult,
            finalAnalysis,
            outcome,
            services.Trace,
            services.SafetyJournal,
            services.ScreenState.IsEndOfList());

        // Drain all accepted step evidence (including the stabilized
        // post-target capture) before the run result is recorded.
        await services.AssetSink.DrainAsync(cancellationToken);
        if (services.AssetSink.FailedCount > 0)
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
                        ["failed_count"] = services.AssetSink.FailedCount,
                        ["accepted_count"] = services.AssetSink.AcceptedCount,
                        ["message"] =
                            services.AssetSink.LastError?.Message ?? string.Empty,
                    }));
        }

        await FinalizeRunAssetsAsync(assets, outcome, engineResult);
        return outcome;
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

        var resetAnalysis = await services.PageAnalyzer.AnalyzeCurrentPageAsync(
            cancellationToken);
        if (resetAnalysis is null)
        {
            throw new HostPreparationException(
                "Reset page analysis returned no analysis; the reset page was not verified.");
        }
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
        IAdbCommandRunner runner,
        CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(
            AdbCommandRequest.Create(
                ["shell", "dumpsys", "activity", "activities"],
                TimeSpan.FromSeconds(10)),
            cancellationToken);
        if (result.Failure?.Kind == "cancelled")
            throw new OperationCanceledException(cancellationToken);
        if (!result.Succeeded)
        {
            throw new HostPreparationException(
                result.Failure?.Message ?? "Could not read current package.");
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            result.StandardOutput,
            @"(?:mResumedActivity|topResumedActivity|mCurrentFocus|mFocusedApp)[^\r\n]*?\s(?<package>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+)/",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["package"].Value : "unknown";
    }

    private static AdbCommandRunner CreateRunner(string serial)
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
        return new AdbCommandRunner(
            new AdbCommandRunnerOptions(
                serial,
                adbPath,
                TimeSpan.FromSeconds(30)));
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
        HostCommandOptions options)
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
        ITraceRecorder recorder)
    {
        var config = CreateUniBrainConfig(options);
        var providers = CreateProviders(options);
        var promptLibrary = new PromptLibrary(
            PromptTemplateRegistry.AnalyzeVisual,
            PromptTemplateRegistry.AnalyzeVisualLite,
            PromptTemplateRegistry.DecideNextAction,
            PromptTemplateRegistry.ParseInstruction);
        return UniBrainFactory.Create(config, providers, promptLibrary, screenCapture, recorder);
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
}

public sealed record class HostRunServices(
    IAdbCommandRunner Adb,
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
    StepCaptureStore CaptureStore,
    StepAssetSink AssetSink)
{
    public TraversalEngine CreateTraversalEngine(
        TraversalPlan plan,
        IUniBrain brain,
        TraversalEngineConfig? config = null) =>
        new(
            plan,
            brain,
            ScreenState,
            ActionExecutor,
            config,
            TraceRecorder);
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
                    "Usage: uniclaw <doctor|analyze|run> --device <serial> [--scenario <file>] [--output <path>] [--provider <mock|claude|sensenova|qwen>] [--model <model>]");
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
                scenarioPath);
    }
}
