using System.Diagnostics;
using System.Text;
using UniClaw.Device;

namespace UniClaw.Host.Tests.Integration;

/// <summary>
/// 显式集成测试的 ADB 上下文：串行解析、runner/capture/action 装配，
/// 以及 artifacts/runs/integration/ 下的证据落盘。
/// ScreenState (AdbScreenStateProvider) 已随 UIA 层级移除 (delete-uia)。
/// </summary>
internal sealed class AdbTestContext
{
    public static string RepoRoot => FindRepoRoot();

    public string Serial { get; }
    public IAdbSession Runner { get; }
    public AdbScreenCapture Capture { get; }
    public AdbActionExecutor Actions { get; }
    public string ArtifactRoot { get; }

    private AdbTestContext(
        string serial,
        IAdbSession runner,
        AdbScreenCapture capture,
        AdbActionExecutor actions,
        string artifactRoot)
    {
        Serial = serial;
        Runner = runner;
        Capture = capture;
        Actions = actions;
        ArtifactRoot = artifactRoot;
    }

    public static async Task<AdbTestContext> CreateAsync(
        string category = "adb",
        CancellationToken ct = default)
    {
        var serial = await ResolveSerialAsync(ct);
        var adbPath = Environment.GetEnvironmentVariable("UNICLAW_ADB_PATH") ?? "adb";
        var runner = new ProcessAdbSession(
            new AdbCommandRunnerOptions(
                serial,
                adbPath,
                TimeSpan.FromSeconds(30)));
        var artifactRoot = Path.Combine(
            FindRepoRoot(),
            "artifacts", "runs", "integration", category,
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(artifactRoot);

        return new AdbTestContext(
            serial,
            runner,
            new AdbScreenCapture(runner),
            new AdbActionExecutor(runner),
            artifactRoot);
    }

    /// <summary>串行：UNICLAW_ADB_SERIAL → 唯一在线设备 → 明确报错。</summary>
    public static async Task<string> ResolveSerialAsync(CancellationToken ct = default)
    {
        var fromEnv = Environment.GetEnvironmentVariable("UNICLAW_ADB_SERIAL");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var output = await RunRawAdbAsync(["devices", "-l"], ct);
        var devices = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(line => line.Split(
                ['\t', ' '],
                StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && parts[1] == "device")
            .Select(parts => parts[0])
            .ToList();

        return devices.Count switch
        {
            1 => devices[0],
            0 => throw new InvalidOperationException(
                "未检测到在线 ADB 设备。先启动模拟器（scripts/android-emulator.sh start）"
                + "或连接真机，或设 UNICLAW_ADB_SERIAL=<serial> 指定。"),
            _ => throw new InvalidOperationException(
                $"检测到多个在线设备：{string.Join(", ", devices)}。"
                + "请设 UNICLAW_ADB_SERIAL=<serial> 指定目标。"),
        };
    }

    public async Task WriteArtifactAsync(
        string name,
        byte[] bytes,
        CancellationToken ct = default) =>
        await File.WriteAllBytesAsync(Path.Combine(ArtifactRoot, name), bytes, ct);

    public async Task WriteArtifactAsync(
        string name,
        string content,
        CancellationToken ct = default) =>
        await File.WriteAllTextAsync(Path.Combine(ArtifactRoot, name), content, ct);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(
                    dir.FullName,
                    "src",
                    "UniClaw.Core.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "找不到仓库根目录（src/UniClaw.Core.sln）。");
    }

    private static async Task<string> RunRawAdbAsync(
        string[] arguments,
        CancellationToken ct)
    {
        var adbPath = Environment.GetEnvironmentVariable("UNICLAW_ADB_PATH") ?? "adb";
        var startInfo = new ProcessStartInfo
        {
            FileName = adbPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"ADB 进程启动失败：{adbPath}");
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"adb {string.Join(' ', arguments)} 失败 (exit {process.ExitCode}): {stderr.Trim()}");
        }

        return stdout;
    }
}
