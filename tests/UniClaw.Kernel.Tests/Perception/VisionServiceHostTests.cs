using System.Runtime.InteropServices;
using System.Text;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// PER-005 A2（host 启动 fail-loud，DETERMINISTIC）——VisionServiceHost 对
/// 启动期进程退出（paddle 被配置而环境缺 paddle 的 fail-closed 代理：脚本
/// double 以非零退出 + stderr）与探活超时的如实上报。健康路径由
/// PerceptionLiveEnvironmentTests（ENVIRONMENT，A4）用真实服务验证。
/// </summary>
public sealed class VisionServiceHostTests
{
    private static string CorpusRoot => Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus");

    private static (string Path, string Content) ExecutableScript(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"uniclaw-host-{Guid.NewGuid():N}.sh");
        // UTF8Encoding(false)：无 BOM——BOM 会挡在 "#!" 前 → ENOEXEC（实测）
        File.WriteAllText(path, "#!/bin/sh\n" + content + "\n", new UTF8Encoding(false));
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        return (path, content);
    }

    private static VisionServiceHostOptions Options(string executable, TimeSpan? timeout = null,
        IReadOnlyDictionary<string, string>? env = null) => new(
        executable,
        ProviderRoot: Path.Combine(CorpusRoot, "..", "..", ".."), // 任意存在目录（脚本 double 不读）
        Transport: new VisionServiceTransport.UnixDomainSocket(
            Path.Combine(Path.GetTempPath(), $"uniclaw-host-{Guid.NewGuid():N}.sock")),
        StartupTimeout: timeout,
        ExtraEnvironment: env);

    [Fact]
    public async Task Startup_ProcessExitsNonZero_FailLoudWithExitCode()
    {
        var (script, _) = ExecutableScript("exit 3");
        await using var host = new VisionServiceHost(Options(script));
        var result = await host.StartAsync();
        Assert.False(result.Healthy);
        Assert.NotNull(result.Error);
        Assert.Contains("exit code 3", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_StderrCapturedInTail_FailLoudFirstScene()
    {
        // paddle fail-loud 代理：配置了未安装后端 → 启动期 stderr + 非零退出
        var (script, _) = ExecutableScript("echo 'ModuleNotFoundError: No module named paddle' >&2; exit 1");
        await using var host = new VisionServiceHost(Options(script));
        var result = await host.StartAsync();
        Assert.False(result.Healthy);
        Assert.Contains("paddle", result.StderrTail, StringComparison.Ordinal);
        Assert.Contains("ModuleNotFoundError", result.StderrTail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_HealthNeverGreenWithinTimeout_FailLoud()
    {
        var (script, _) = ExecutableScript("sleep 30");
        await using var host = new VisionServiceHost(Options(script, timeout: TimeSpan.FromMilliseconds(800)));
        var result = await host.StartAsync();
        Assert.False(result.Healthy);
        Assert.Contains("探活超时", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispose_KillsProcessTree_Idempotent()
    {
        var (script, _) = ExecutableScript("sleep 30");
        var host = new VisionServiceHost(Options(script, timeout: TimeSpan.FromMilliseconds(200)));
        _ = await host.StartAsync();
        await host.DisposeAsync();
        await host.DisposeAsync(); // 幂等
    }

    [Fact]
    public void Options_RequireExecutableAndRoot_FailClosed()
    {
        // 校验单点在 VisionServiceHost 构造器（options record 不自带执法）
        Assert.Throws<ArgumentException>(() =>
            new VisionServiceHost(new VisionServiceHostOptions(
                " ", "/tmp", new VisionServiceTransport.UnixDomainSocket("/tmp/x.sock"))));
        Assert.Throws<ArgumentException>(() =>
            new VisionServiceHost(new VisionServiceHostOptions(
                "/bin/sh", " ", new VisionServiceTransport.UnixDomainSocket("/tmp/x.sock"))));
    }
}
