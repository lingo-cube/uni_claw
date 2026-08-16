using System.Collections.Immutable;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// Slice 2 约束断言（tasks 6.7）— 架构检索 + 机械约束：
///   - 生产路径（src/）不得调用 `svc wifi` / `cmd wifi` / 隐藏 emulator API
///     （emulator console wifi 命令、UiAutomator 隐藏接口）
///   - 不得直接改写 WorldState / WorldBelief（状态仅经 Observation evidence 进入判定）
///   - 生产路径无场景状态注入；`wifi_on` 仅以只读 `settings get` 形式出现（宿主基线校验）
///   - 像素坐标设备准备（`input tap`）仅存在于 PhysicalHost 宿主（agent 路径零硬编码坐标）
/// </summary>
public sealed class PhysicalWifiSlice2ConstraintTests
{
    private static readonly string SrcRoot = Path.Combine(TestRepositoryPaths.RepoRoot(), "src");

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToImmutableArray();

    [Fact]
    public void NoForbiddenWifiShellCommands()
    {
        var hits = SourceFiles(SrcRoot)
            .SelectMany(file => File.ReadLines(file)
                .Select((line, i) => (file, line: i + 1, text: line)))
            .Where(x => x.text.Contains("svc wifi", StringComparison.Ordinal)
                     || x.text.Contains("cmd wifi", StringComparison.Ordinal))
            .Select(x => $"{Path.GetFileName(x.file)}:{x.line}")
            .ToArray();
        Assert.Empty(hits);
    }

    [Fact]
    public void NoHiddenEmulatorApis()
    {
        var hits = SourceFiles(SrcRoot)
            .SelectMany(file => File.ReadLines(file)
                .Select((line, i) => (file, line: i + 1, text: line)))
            .Where(x => x.text.Contains("emulator console", StringComparison.OrdinalIgnoreCase)
                     || x.text.Contains("UiAutomator", StringComparison.Ordinal))
            .Select(x => $"{Path.GetFileName(x.file)}:{x.line}")
            .ToArray();
        Assert.Empty(hits);
    }

    [Fact]
    public void WifiOnOnlyReadOnlySettingsGet()
    {
        var lines = SourceFiles(SrcRoot)
            .SelectMany(file => File.ReadLines(file)
                .Select((text, i) => (file, line: i + 1, text)))
            .Where(x => x.text.Contains("wifi_on", StringComparison.Ordinal))
            .ToArray();

        // 绝无写入（settings put / 直接赋值）
        Assert.DoesNotContain(lines, x => x.text.Contains("put global wifi_on", StringComparison.Ordinal)
                                       || x.text.Contains("settings put", StringComparison.Ordinal)
                                       || x.text.Contains("wifi_on =", StringComparison.Ordinal));

        // 只读回读存在：PhysicalHost 以 settings → get → global → wifi_on token 顺序构建命令
        var argLine = Assert.Single(lines, x => x.text.Contains("Add(\"wifi_on\"", StringComparison.Ordinal));
        var fileLines = File.ReadAllLines(argLine.file);
        int i = argLine.line - 1;
        Assert.True(i >= 3
            && fileLines[i - 3].Contains("\"settings\"", StringComparison.Ordinal)
            && fileLines[i - 2].Contains("\"get\"", StringComparison.Ordinal)
            && fileLines[i - 1].Contains("\"global\"", StringComparison.Ordinal));
    }

    [Fact]
    public void NoWorldStateTypeAndNoDirectBeliefRewrite()
    {
        // WorldState 类型不存在于生产路径
        Assert.Empty(SourceFiles(SrcRoot)
            .SelectMany(file => File.ReadLines(file))
            .Where(l => l.Contains("WorldState", StringComparison.Ordinal)));

        // WorldBelief 唯一构造点是 Reconcile（Observation → belief 的纯函数），
        // Agent 仅持有不可变快照；无生产代码直接 `new WorldBelief(` 或字段级改写
        var beliefCreations = SourceFiles(SrcRoot)
            .SelectMany(file => File.ReadLines(file)
                .Select((text, i) => (file, line: i + 1, text)))
            .Where(x => x.text.Contains("new WorldBelief(", StringComparison.Ordinal))
            .Select(x => $"{Path.GetFileName(x.file)}:{x.line}")
            .ToArray();
        Assert.Equal(new[] { "Reconcile.cs:27", "Reconcile.cs:32" }, beliefCreations);
    }

    [Fact]
    public void PixelCoordinatesOnlyInPhysicalHostDevicePrep()
    {
        // `input tap` 像素坐标仅用于宿主基线准备（非 agent 语义路径）— 生产机制零硬编码坐标
        var hits = SourceFiles(SrcRoot)
            .SelectMany(file => File.ReadLines(file)
                .Select((text, i) => (file, line: i + 1, text)))
            .Where(x => x.text.Contains("input tap", StringComparison.Ordinal))
            .Select(x => x.file)
            .Distinct()
            .ToArray();
        Assert.All(hits, p => Assert.Contains("PhysicalHost", p, StringComparison.Ordinal));
    }
}
