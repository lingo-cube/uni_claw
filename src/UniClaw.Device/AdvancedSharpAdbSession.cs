using System.Collections.Immutable;
using System.Text;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;

namespace UniClaw.Device;

public sealed class AdvancedSharpAdbSession : IAdbSession
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly AdbClient _client;
    private readonly DeviceData _device;
    private readonly string _adbPath;
    private readonly TimeSpan _defaultTimeout;
    private readonly AdbCommandRunner _binaryRunner;
    private bool _disposed;

    public string Serial => _device.Serial;

    public AdvancedSharpAdbSession(
        string serial,
        string adbPath = "adb",
        TimeSpan? defaultTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(serial))
            throw new ArgumentException(
                "ADB serial is required.", nameof(serial));

        _device = new DeviceData { Serial = serial.Trim() };
        _adbPath = adbPath;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(30);
        _client = new AdbClient();
        _binaryRunner = new AdbCommandRunner(new AdbCommandRunnerOptions(
            serial.Trim(), adbPath, _defaultTimeout));
    }

    public async Task<byte[]> CaptureScreenshotAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_defaultTimeout);

        // AdvancedSharpAdbClient 的 shell 输出回调只暴露 string（内部按 UTF-8
        // 解码），二进制 screencap 经其传输必然损坏——实测首字节 0x89 被替换成
        // U+FFFD（EF BF BD）→ PIL UnidentifiedImageError。截图改走进程
        // exec-out（stdout 二进制直读，与 ProcessAdbSession 同一通道），
        // shell/UI dump 等文本命令保持库实现。
        var result = await _binaryRunner.RunAsync(
            new AdbCommandRequest(
                ImmutableArray.Create("exec-out", "screencap", "-p"),
                CaptureBinaryOutput: true),
            timeoutCts.Token);
        if (result.Failure?.Kind == "cancelled")
            throw new OperationCanceledException(result.Failure.Message, timeoutCts.Token);
        if (!result.Succeeded)
        {
            throw new AdbCommandException(
                "ADB screenshot capture",
                new ShellResult(false, result.StandardOutput, result.StandardError));
        }

        if (result.BinaryOutput.IsDefaultOrEmpty)
        {
            throw new AdbCommandException(
                "ADB screenshot capture",
                new ShellResult(
                    false,
                    string.Empty,
                    "screenshot capture returned no bytes"));
        }

        return result.BinaryOutput.ToArray();
    }

    public async Task<ShellResult> ExecuteShellAsync(
        string command,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException(
                "Shell command is required.", nameof(command));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_defaultTimeout);

        return await ExecuteSerializedAsync(
            async linkedCt =>
            {
                var receiver = new ConsoleOutputReceiver();
                await _client.ExecuteRemoteCommandAsync(
                    command,
                    _device,
                    receiver,
                    Encoding.UTF8,
                    linkedCt);

                return new ShellResult(
                    true,
                    receiver.ToString() ?? string.Empty,
                    string.Empty);
            },
            timeoutCts.Token);
    }

    public async Task<string> DumpUiHierarchyAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_defaultTimeout);

        return await ExecuteSerializedAsync(
            async linkedCt =>
            {
                const string remotePath = "/sdcard/uniclaw-window-dump.xml";

                // Step 1: dump UI hierarchy to file
                var dumpReceiver = new ConsoleOutputReceiver();
                await _client.ExecuteRemoteCommandAsync(
                    $"uiautomator dump {remotePath}",
                    _device,
                    dumpReceiver,
                    Encoding.UTF8,
                    linkedCt);

                // Step 2: read the dumped file
                var catReceiver = new ConsoleOutputReceiver();
                await _client.ExecuteRemoteCommandAsync(
                    $"cat {remotePath}",
                    _device,
                    catReceiver,
                    Encoding.UTF8,
                    linkedCt);

                var xml = catReceiver.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(xml))
                {
                    throw new AdbCommandException(
                        "UI dump read",
                        new ShellResult(
                            false,
                            string.Empty,
                            "UI dump returned no bytes"));
                }

                return xml;
            },
            timeoutCts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        _semaphore.Dispose();
        await Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AdvancedSharpAdbSession));
    }

    private async Task<T> ExecuteSerializedAsync<T>(
        Func<CancellationToken, Task<T>> execute,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await _semaphore.WaitAsync(ct);
                try
                {
                    return await execute(ct);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException
                                       && ex is not ObjectDisposedException
                                       && ex is not ArgumentException
                                       && ex is not AdbCommandException)
            {
                if (attempt == 2)
                {
                    throw new AdbCommandException(
                        $"ADB session connection lost after {attempt + 1} retries",
                        new ShellResult(
                            false,
                            string.Empty,
                            ex.Message));
                }

                var delay = attempt switch
                {
                    0 => TimeSpan.Zero,
                    1 => TimeSpan.FromMilliseconds(500),
                    _ => TimeSpan.FromMilliseconds(1000),
                };

                if (attempt == 1)
                {
                    try
                    {
                        var server = new AdbServer();
                        await server.StartServerAsync(
                            _adbPath,
                            restartServerIfNewer: false,
                            ct);
                    }
                    catch
                    {
                        // Server restart is best-effort
                    }
                }

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, ct);
            }
        }

        throw new InvalidOperationException("Retry loop exited unexpectedly.");
    }
}
