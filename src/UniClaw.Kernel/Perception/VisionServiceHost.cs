using System.Diagnostics;
using System.Text;

namespace UniClaw.Kernel.Perception;

/// <summary>
/// VisionServiceHost 选项。PythonExecutable = venv 内 python；ProviderRoot =
/// 物化的 platforms/perception 根（uvicorn 以 cwd=ProviderRoot 启动，
/// uniclaw_perception 包按包相对路径解析模型）；Transport 决定 uvicorn 绑定
/// （--uds / --host 127.0.0.1 --port）；ExtraEnvironment 透传 provider 环境
/// 变量（如 UNICLAW_OCR_BACKEND——配置 paddle 而环境缺 paddle 时服务启动
/// 期 fail-closed，本 host 如实上报启动失败 + stderr，绝不静默降级，D7）。
/// </summary>
public sealed record VisionServiceHostOptions(
    string PythonExecutable,
    string ProviderRoot,
    VisionServiceTransport Transport,
    TimeSpan? StartupTimeout = null,
    IReadOnlyDictionary<string, string>? ExtraEnvironment = null);

/// <summary>启动结果：Healthy = /version 探活通过；失败含 stderr 尾部
/// （paddle fail-loud / 缺依赖等启动期错误的第一现场）。</summary>
public sealed record VisionServiceStartupResult(bool Healthy, string? Error, string StderrTail);

/// <summary>
/// VisionServiceHost — 感知服务进程生命周期（PER-005 ③；REFERENCE legacy
/// VisionServiceHost 状态机思想，大幅简化：P1 无 supervision/restart）。
/// 属 Capability Plane provider 进程管理，不进 Kernel 组合根。启动 =
/// python -m uvicorn uniclaw_perception.server:app（uds/tcp 按 transport）
/// + 轮询 /version 直到健康或超时；进程在启动期退出（如配置了未安装的
/// OCR 后端）→ 启动失败 + stderr 尾部（fail-loud）。Dispose = kill 进程树。
/// </summary>
public sealed class VisionServiceHost : IAsyncDisposable
{
    private static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(90);
    private const int StderrTailLimit = 4096;

    private readonly VisionServiceHostOptions _options;
    private readonly Process? _process;
    private readonly StringBuilder _stderr = new();
    private readonly Task _stderrDrain;

    public VisionServiceHost(VisionServiceHostOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.PythonExecutable))
            throw new ArgumentException("Python executable is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ProviderRoot))
            throw new ArgumentException("Provider root is required.", nameof(options));

        var arguments = new List<string>
        {
            "-m", "uvicorn", "uniclaw_perception.server:app",
        };
        switch (options.Transport)
        {
            case VisionServiceTransport.UnixDomainSocket uds:
                arguments.Add("--uds");
                arguments.Add(uds.SocketPath);
                break;
            case VisionServiceTransport.LoopbackTcp tcp:
                arguments.Add("--host");
                arguments.Add("127.0.0.1");
                arguments.Add("--port");
                arguments.Add(tcp.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException($"未知 transport：{options.Transport.GetType().Name}");
        }

        var startInfo = new ProcessStartInfo(options.PythonExecutable)
        {
            WorkingDirectory = options.ProviderRoot,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        if (options.ExtraEnvironment is not null)
            foreach (var (key, value) in options.ExtraEnvironment)
                startInfo.Environment[key] = value;

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("uvicorn 进程启动失败（Process.Start 返回 null）");
        _stderrDrain = DrainAsync(_process.StandardError);
        _ = DrainDiscardAsync(_process.StandardOutput);
    }

    /// <summary>轮询 /version 直到健康 / 进程退出 / 超时。</summary>
    public async Task<VisionServiceStartupResult> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null)
            throw new InvalidOperationException("host 未启动");

        var timeout = _options.StartupTimeout ?? DefaultStartupTimeout;
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        using var client = new VisionServiceClient(_options.Transport, timeout: TimeSpan.FromSeconds(2));

        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                await _stderrDrain.ConfigureAwait(false);
                return new VisionServiceStartupResult(
                    false,
                    $"perception service 进程在启动期退出（exit code {_process.ExitCode}）——fail-loud，不静默降级",
                    Tail());
            }
            if (await client.IsHealthyAsync(linked.Token))
                return new VisionServiceStartupResult(true, null, Tail());
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), linked.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        // 探活超时路径：进程可能仍活着（管道不会关闭）——只读已有尾部，不等待
        await DrainStderrIfExitedAsync();
        return new VisionServiceStartupResult(
            false,
            _process.HasExited
                ? $"perception service 进程在启动期退出（exit code {_process.ExitCode}）"
                : $"startup 探活超时（{timeout.TotalSeconds:0}s 内 /version 未健康）",
            Tail());
    }

    /// <summary>仅在进程已退出时排空 stderr 管道（活进程的管道不会关闭，
    /// 无条件 await 会阻塞至进程退出——探活超时路径不等待）。</summary>
    private async Task DrainStderrIfExitedAsync()
    {
        if (_process is { HasExited: true })
            await _stderrDrain.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null)
            return;
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // 进程已退出，无清理可做。
        }
        finally
        {
            await _stderrDrain.ConfigureAwait(false);
            _process.Dispose();
        }
    }

    private string Tail() => _stderr.Length > StderrTailLimit
        ? _stderr.ToString()[^StderrTailLimit..]
        : _stderr.ToString();

    private async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        int read;
        try
        {
            while ((read = await reader.ReadAsync(buffer)) > 0)
            {
                if (_stderr.Length < StderrTailLimit * 2)
                    _stderr.Append(buffer, 0, read);
            }
        }
        catch (ObjectDisposedException)
        {
            // 进程销毁后管道关闭——启动尾段已捕获。
        }
    }

    private static async Task DrainDiscardAsync(StreamReader reader)
    {
        try
        {
            var buffer = new char[4096];
            while (await reader.ReadAsync(buffer) > 0)
            {
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
