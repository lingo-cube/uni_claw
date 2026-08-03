namespace UniClaw.Device;

/// <summary>
/// Manages the Python FastAPI vision service process lifecycle.
/// UDS (macOS/Linux) or TCP (Windows) dual-mode transport.
/// </summary>
public interface IPythonVisionService : IAsyncDisposable
{
    /// <summary>Pre-configured HttpClient for communicating with the Python service.</summary>
    HttpClient HttpClient { get; }

    /// <summary>Start the Python process and wait for health check (warm: true).</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Whether the Python process is currently running and healthy.</summary>
    bool IsRunning { get; }
}
