using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace UniClaw.Vision.Host;

/// <summary>Vision service lifecycle state.</summary>
public enum VisionHostState { Cold, Warming, Healthy, Unhealthy, Crashed, Shutdown }

/// <summary>Operational facts captured at successful startup.</summary>
public sealed record VisionDeploymentFacts
{
    public string? ServiceVersion { get; init; }
    public ImmutableArray<string> SupportedSchemas { get; init; } = [];
    public string? ModelId { get; init; }
    public string? ConfigHash { get; init; }
    public string? OcrBackend { get; init; }
}

/// <summary>Configuration for a VisionServiceHost instance.</summary>
public sealed record VisionHostConfig
{
    public string PythonExecutable { get; init; } = "python3";
    public string ServiceEntryPoint { get; init; } = "platforms/perception/uniclaw_perception/server.py";
    public string RepoRoot { get; init; } = ".";
    public string SocketDir { get; init; } = "/tmp";
    public string ModelPath { get; init; } = "platforms/perception/models/yolo/android_ui_detection_yolov8/best.pt";
    public string ConfigPath { get; init; } = "platforms/perception/config/label-mapping.json";
    public int MaxRestarts { get; init; } = 3;
    public TimeSpan RestartWindow { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan HealthTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan ReadinessPollInterval { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Sole mutable owner of Python Vision service lifecycle.
/// Owns process, socket, restart budget, and deployment facts.
/// Does NOT own semantic authority, Runtime state, or Agent decisions.
/// </summary>
public sealed class VisionServiceHost : IDisposable
{
    private readonly VisionHostConfig _config;
    private readonly string _sessionId;
    private readonly string _socketPath;
    private readonly List<DateTime> _restartTimestamps = [];
    private Process? _process;
    private VisionDeploymentFacts? _facts;
    private int _restartCount;

    public VisionHostState State { get; private set; } = VisionHostState.Cold;
    public string SessionId => _sessionId;
    public string SocketPath => _socketPath;
    public VisionDeploymentFacts? Facts => _facts;
    public int RestartCount => _restartCount;

    public VisionServiceHost(VisionHostConfig config)
    {
        _config = config;
        _sessionId = Guid.NewGuid().ToString("N")[..12];
        _socketPath = Path.Combine(config.SocketDir, $"uniclaw-vision-{_sessionId}.sock");
    }

    // ── STARTUP ──────────────────────────────────────────────────────────

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (State != VisionHostState.Cold)
            throw new InvalidOperationException($"Cannot start from state {State}.");

        // Validate pre-conditions
        if (!File.Exists(_config.PythonExecutable) && !IsCommandAvailable(_config.PythonExecutable))
            throw new FileNotFoundException($"Python executable not found: {_config.PythonExecutable}");
        var entryPoint = Path.Combine(_config.RepoRoot, _config.ServiceEntryPoint);
        if (!File.Exists(entryPoint))
            throw new FileNotFoundException($"Service entry point not found: {entryPoint}");
        var modelPath = Path.Combine(_config.RepoRoot, _config.ModelPath);
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Model file not found: {modelPath}");
        var configPath = Path.Combine(_config.RepoRoot, _config.ConfigPath);
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Config file not found: {configPath}");

        await LaunchProcessAsync(ct);
        await WaitForReadinessAsync(ct);
    }

    private async Task LaunchProcessAsync(CancellationToken ct)
    {
        CleanStaleSocket();
        State = VisionHostState.Warming;

        var entryPoint = Path.Combine(_config.RepoRoot, _config.ServiceEntryPoint);

        var psi = new ProcessStartInfo
        {
            FileName = _config.PythonExecutable,
            Arguments = $"-m uvicorn uniclaw_perception.server:app --uds {_socketPath}",
            WorkingDirectory = _config.RepoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["UNICLAW_VISION_SOCKET"] = _socketPath;
        // Ensure the perception package is importable from the repo root.
        var perceptionPath = Path.Combine(_config.RepoRoot, "platforms", "perception");
        var existingPythonPath = System.Environment.GetEnvironmentVariable("PYTHONPATH") ?? "";
        psi.Environment["PYTHONPATH"] = string.IsNullOrEmpty(existingPythonPath)
            ? perceptionPath
            : $"{perceptionPath}:{existingPythonPath}";

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Python process.");
    }

    private async Task WaitForReadinessAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_config.HealthTimeout);

        while (!cts.IsCancellationRequested)
        {
            if (_process?.HasExited == true)
            {
                State = VisionHostState.Crashed;
                throw new InvalidOperationException("Python process exited before ready.");
            }

            if (await TryHealthCheckAsync())
            {
                // Health OK — query /version for deployment facts
                var facts = await TryGetVersionAsync();
                if (facts is not null)
                {
                    _facts = facts;
                    State = VisionHostState.Healthy;
                    return;
                }
            }

            await Task.Delay(_config.ReadinessPollInterval, ct);
        }

        State = VisionHostState.Unhealthy;
        throw new TimeoutException("Vision service did not become healthy within timeout.");
    }

    // ── HEALTH / VERSION ─────────────────────────────────────────────────

    private async Task<bool> TryHealthCheckAsync()
    {
        try
        {
            using var client = CreateUdsClient();
            var resp = await client.GetStringAsync("/health");
            using var doc = JsonDocument.Parse(resp);
            return doc.RootElement.TryGetProperty("warm", out var warm) && warm.GetBoolean();
        }
        catch { return false; }
    }

    private async Task<VisionDeploymentFacts?> TryGetVersionAsync()
    {
        try
        {
            using var client = CreateUdsClient();
            var resp = await client.GetStringAsync("/version");
            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;

            var schemas = ImmutableArray.CreateBuilder<string>();
            if (root.TryGetProperty("supportedSchemas", out var arr))
                foreach (var s in arr.EnumerateArray())
                    schemas.Add(s.GetString() ?? "");

            return new VisionDeploymentFacts
            {
                ServiceVersion = root.TryGetProperty("pipeline", out var pl)
                    && pl.TryGetProperty("version", out var v) ? v.GetString() : null,
                SupportedSchemas = schemas.ToImmutable(),
                ModelId = root.TryGetProperty("modelId", out var m) ? m.GetString() : null,
                ConfigHash = root.TryGetProperty("configHash", out var c) ? c.GetString() : null,
                OcrBackend = root.TryGetProperty("ocr", out var o) ? o.GetString() : null,
            };
        }
        catch { return null; }
    }

    // ── RESTART ──────────────────────────────────────────────────────────

    public async Task<bool> TryRestartAsync(CancellationToken ct = default)
    {
        if (State is VisionHostState.Shutdown)
            return false;

        // Sliding window budget check
        var windowStart = DateTime.UtcNow - _config.RestartWindow;
        _restartTimestamps.RemoveAll(t => t < windowStart);
        if (_restartTimestamps.Count >= _config.MaxRestarts)
            return false;

        _restartTimestamps.Add(DateTime.UtcNow);
        _restartCount++;

        KillProcess();
        State = VisionHostState.Cold;
        try
        {
            await StartAsync(ct);
            return true;
        }
        catch
        {
            State = VisionHostState.Crashed;
            return false;
        }
    }

    // ── SHUTDOWN ─────────────────────────────────────────────────────────

    public void Shutdown()
    {
        if (State is VisionHostState.Shutdown) return;
        State = VisionHostState.Shutdown;
        KillProcess();
        CleanStaleSocket();
    }

    public void Dispose()
    {
        if (State != VisionHostState.Shutdown) Shutdown();
        _process?.Dispose();
    }

    // ── HELPERS ──────────────────────────────────────────────────────────

    private HttpClient CreateUdsClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (ctx, ct) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), ct);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost"), Timeout = TimeSpan.FromSeconds(10) };
    }

    private void KillProcess()
    {
        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); } catch { }
            _process.WaitForExit(5000);
        }
        _process?.Dispose();
        _process = null;
    }

    private void CleanStaleSocket()
    {
        try { if (File.Exists(_socketPath)) File.Delete(_socketPath); }
        catch { /* best effort */ }
    }

    private static bool IsCommandAvailable(string cmd)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("which", cmd)
            { RedirectStandardOutput = true, UseShellExecute = false });
            p?.WaitForExit(1000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }
}
