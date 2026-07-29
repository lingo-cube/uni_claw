using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.ClaudeProvider;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Device;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Runner;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;

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
        var provider = CreateProvider(options);
        var analyzer = new PageAnalyzer(
            provider,
            new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual),
            new AdbScreenCapture(runner));
        var traceStorage = new FileTraceStorage(
            new PhysicalFileProvider(),
            Path.Combine(options.OutputRoot, "trace"));
        return new PageAnalysisDeviceAnalyzer(
            analyzer,
            new InMemoryTraceRecorder(traceStorage),
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
        var traceStorage = new FileTraceStorage(
            new PhysicalFileProvider(),
            Path.Combine(assets.RunDirectory, "trace"));
        var traceRecorder = new InMemoryTraceRecorder(traceStorage);
        var evaluator = new SettingsSafetyEvaluator(snapshot);
        var safetyContext = new SafetyExecutionContext();
        var safetyJournal = new SafetyDecisionJournal();
        var safetySink = new CompositeSafetyDecisionSink(
            new RunAssetSafetyDecisionSink(assets),
            new TraceSafetyDecisionSink(traceRecorder),
            safetyJournal);
        var provider = CreateProvider(options);
        var pageAnalyzer = new PageAnalyzer(
            provider,
            new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual),
            new AdbScreenCapture(runner));
        var safeActions = new SafeActionExecutor(
            new AdbActionExecutor(runner),
            evaluator,
            safetySink,
            safetyContext);
        var safeEntry = new SafeEntryActionDriver(
            new AdbEntryActionDriver(runner),
            evaluator,
            safetySink,
            safetyContext);

        return new HostRunServices(
            runner,
            pageAnalyzer,
            safeActions,
            new AdbScreenStateProvider(runner),
            safeEntry,
            safetyContext,
            evaluator,
            safetySink,
            safetyJournal,
            traceRecorder,
            assets);
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
        var plan = new ScenarioPlanCompiler().Compile(snapshot);
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
        var observations = new AdbScenarioObservationSource(
            services.Adb,
            new AdbScreenCapture(services.Adb),
            (AdbScreenStateProvider)services.ScreenState,
            services.PageAnalyzer,
            useUiAutomatorAnalysis: string.Equals(
                options.ProviderId,
                "mock",
                StringComparison.OrdinalIgnoreCase));
        return await new IncrementalScenarioRunner(
                snapshot,
                plan,
                services,
                observations)
            .RunAsync(cancellationToken);
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
            && !string.IsNullOrWhiteSpace(options.Model));

    private static IModelProvider CreateProvider(HostCommandOptions options)
    {
        if (string.Equals(
                options.ProviderId,
                "mock",
                StringComparison.OrdinalIgnoreCase))
        {
            return new DeterministicSettingsModelProvider(
                options.Model ?? "deterministic-settings-v1");
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
            return new OpenAiCompatibleVisionProvider(
                new HttpClient(),
                new OpenAiCompatibleProviderConfig(
                    sensenovaApiKey,
                    options.Model,
                    baseUrl));
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
        return new AnthropicModelProvider(
            new HttpClient(),
            new AnthropicProviderConfig(apiKey, options.Model));
    }

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
}

public sealed record class HostRunServices(
    IAdbCommandRunner Adb,
    IPageAnalyzer PageAnalyzer,
    IActionExecutor ActionExecutor,
    IScreenStateProvider ScreenState,
    IEntryActionDriver EntryActionDriver,
    ISafetyExecutionContext SafetyContext,
    ISafetyEvaluator SafetyEvaluator,
    ISafetyDecisionSink SafetyDecisionSink,
    SafetyDecisionJournal SafetyJournal,
    ITraceRecorder TraceRecorder,
    RunAssetSession Assets)
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

public sealed class DeterministicSettingsModelProvider(
    string model) : IModelProvider
{
    private const string AnalysisJson =
        """
        {
          "level1_dir": "left",
          "level1_menus": [],
          "level2_dir": "left",
          "level2_menus": [],
          "current_path": ["Settings"],
          "items": [],
          "is_popup": false,
          "popup_info": null,
          "close_button": null,
          "back_button": null,
          "has_scroll": true,
          "is_end_of_list": false
        }
        """;

    public string ProviderId => "mock";

    public Task<ModelResponse> CompleteTextAsync(
        ModelRequest request,
        CancellationToken ct = default) =>
        Task.FromResult(Failure("Text capability is not enabled."));

    public Task<ModelResponse> CompleteVisionAsync(
        ModelRequest request,
        byte[] imageData,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (imageData.Length == 0)
            return Task.FromResult(Failure("Screenshot was empty."));
        return Task.FromResult(
            new ModelResponse(
                AnalysisJson,
                ProviderId,
                "deterministic",
                0,
                0,
                0,
                model));
    }

    public Task<ModelResponse> CompleteMultimodalAsync(
        ModelRequest request,
        byte[] imageData,
        CancellationToken ct = default) =>
        CompleteVisionAsync(request, imageData, ct);

    private ModelResponse Failure(string message) =>
        new(
            string.Empty,
            ProviderId,
            "deterministic",
            0,
            0,
            0,
            model,
            false,
            message);
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
                    "Usage: uniclaw <doctor|analyze|run> --device <serial> [--scenario <file>] [--output <path>] [--provider <mock|claude|sensenova>] [--model <model>]");
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
