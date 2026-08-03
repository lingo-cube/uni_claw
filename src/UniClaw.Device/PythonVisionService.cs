using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace UniClaw.Device;

/// <summary>
/// Python FastAPI vision service process manager.
/// UDS on macOS/Linux, TCP loopback on Windows.
/// Auto-restart with exponential backoff, health-check gating on warm:true.
/// </summary>
public sealed class PythonVisionService : IPythonVisionService
{
    private Process? _process;
    private int _restartCount;
    private readonly string _socketPath;
    private readonly int _port;
    private readonly string _uvicornPath;
    private readonly int _maxRestarts;
    private bool _disposed;

    private static readonly TimeSpan[] BackoffSequence =
    {
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(10),
    };

    public HttpClient HttpClient { get; private set; } = null!;
    public bool IsRunning { get; private set; }

    public PythonVisionService(
        string? socketPath = null,
        int? port = null,
        string? uvicornPath = null,
        int maxRestarts = 5)
    {
        _socketPath = socketPath
            ?? Environment.GetEnvironmentVariable("UNICLAW_VISION_SOCK")
            ?? "/tmp/uniclaw-vision.sock";
        _port = port
            ?? (int.TryParse(Environment.GetEnvironmentVariable("UNICLAW_VISION_PORT"), out var p) ? p : 8765);
        _uvicornPath = uvicornPath
            ?? Environment.GetEnvironmentVariable("UNICLAW_UVICORN_PATH")
            ?? "uvicorn";
        _maxRestarts = maxRestarts;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PythonVisionService));

        // Clean up stale socket
        if (!OperatingSystem.IsWindows() && File.Exists(_socketPath))
        {
            try { File.Delete(_socketPath); } catch { /* best-effort */ }
        }

        await StartProcessAsync(ct);
    }

    private async Task StartProcessAsync(CancellationToken ct)
    {
        var serverScript = Path.GetFullPath(
            Path.Combine("tools", "local_vision", "server.py"));

        string args;
        if (OperatingSystem.IsWindows())
        {
            args = $@"""{serverScript}"" --host 127.0.0.1 --port {_port}";
        }
        else
        {
            args = $@"""{serverScript}"" --uds ""{_socketPath}""";
        }

        var psi = new ProcessStartInfo
        {
            FileName = _uvicornPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // Pass OMP_NUM_THREADS as env var (Python reads it before imports)
        if (Environment.GetEnvironmentVariable("UNICLAW_OMP_THREADS") is { Length: > 0 } omp)
            psi.Environment["UNICLAW_OMP_THREADS"] = omp;
        else
            psi.Environment["UNICLAW_OMP_THREADS"] = "4";

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.Exited += OnProcessExited;

        _process.Start();

        // Build HttpClient with appropriate transport
        HttpClient = CreateHttpClient();

        // Wait for health check (warm: true)
        await WaitForReadyAsync(ct);

        IsRunning = true;
        _restartCount = 0;
    }

    private HttpClient CreateHttpClient()
    {
        if (OperatingSystem.IsWindows())
        {
            return new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
        }

        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, ct) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), ct);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    private async Task WaitForReadyAsync(CancellationToken ct, int timeoutMs = 30000)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var resp = await HttpClient.GetAsync("/health", cts.Token);
                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("warm", out var warm) && warm.GetBoolean())
                        return;
                }
            }
            catch (HttpRequestException) { /* server not ready yet */ }
            catch (TaskCanceledException) { /* timeout */ }

            await Task.Delay(200, cts.Token);
        }

        throw new TimeoutException(
            $"Python vision service did not become ready within {timeoutMs}ms");
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        IsRunning = false;

        if (_disposed || _restartCount >= _maxRestarts)
            return;

        // Probe existing socket — reuse if alive
        try
        {
            var check = HttpClient.GetAsync("/health").Result;
            if (check.IsSuccessStatusCode)
            {
                IsRunning = true;
                return;
            }
        }
        catch { /* process is truly dead */ }

        _ = RestartAsync();
    }

    private async Task RestartAsync()
    {
        for (int i = 0; i < _maxRestarts && !_disposed; i++)
        {
            var delay = i < BackoffSequence.Length
                ? BackoffSequence[i]
                : BackoffSequence[^1];
            await Task.Delay(delay);

            try
            {
                await StartProcessAsync(CancellationToken.None);
                return; // success
            }
            catch
            {
                _restartCount++;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_process is not null)
        {
            _process.Exited -= OnProcessExited;

            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
            catch { /* best-effort cleanup */ }

            _process.Dispose();
            _process = null;
        }

        HttpClient?.Dispose();
        IsRunning = false;
    }
}
