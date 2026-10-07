using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// ARCH-DOC-022 — src/ 维度 README 登记执法：src/ 六项目全部 .cs 文件
/// 必须在其维度归属 README 中点名（归属映射由 ARCH-DOC-020 十维度划分、
/// AGT-018、HOST-002 与 ARCH-DOC-022 冻结）。两条执法线：① .cs 文件
/// 所在目录必须有归属条目（新目录无条目 = RED）；② 文件 stem 必须在
/// 归属 README 中出现（新文件未登记 = RED）。映射变更须经 change 显式
/// 修订本表（KernelRuntimeSurfaceWhitelistTests 先例）；obj/bin 生成物
/// 豁免。
/// </summary>
public sealed class SourceReadmeOwnershipTests
{
    private static readonly (string Dir, string Readme)[] Ownership =
    {
        ("src/UniClaw.Kernel", "src/UniClaw.Kernel/README.md"),
        ("src/UniClaw.Kernel/Core", "src/UniClaw.Kernel/README.md"),
        ("src/UniClaw.Kernel/Capability", "src/UniClaw.Kernel/Capability/README.md"),
        ("src/UniClaw.Kernel/Perception", "src/UniClaw.Kernel/Perception/README.md"),
        ("src/UniClaw.Kernel/Perception/Fusion", "src/UniClaw.Kernel/Perception/README.md"),
        ("src/UniClaw.Kernel/Perception/UiHierarchy", "src/UniClaw.Kernel/Perception/README.md"),
        ("src/UniClaw.Kernel/Evidence", "src/UniClaw.Kernel/Evidence/README.md"),
        ("src/UniClaw.Kernel/World", "src/UniClaw.Kernel/World/README.md"),
        ("src/UniClaw.Kernel/World/UiRealization", "src/UniClaw.Kernel/World/README.md"),
        ("src/UniClaw.Kernel/Run", "src/UniClaw.Kernel/Run/README.md"),
        ("src/UniClaw.Kernel/Outcome", "src/UniClaw.Kernel/Run/README.md"),
        ("src/UniClaw.Kernel/Control", "src/UniClaw.Kernel/Control/README.md"),
        ("src/UniClaw.Kernel/Assurance", "src/UniClaw.Kernel/Assurance/README.md"),
        ("src/UniClaw.Kernel/Effects", "src/UniClaw.Kernel/Effects/README.md"),
        ("src/UniClaw.Kernel/Effects/ExecutionSource", "src/UniClaw.Kernel/Effects/README.md"),
        ("src/UniClaw.Kernel/Trace", "src/UniClaw.Kernel/Trace/README.md"),
        ("src/UniClaw.Kernel/Diagnostics", "src/UniClaw.Kernel/Diagnostics/README.md"),
        ("src/UniClaw.Kernel/Runtime", "src/UniClaw.Kernel/Runtime/README.md"),
        ("src/UniClaw.Agent", "src/UniClaw.Agent/README.md"),
        ("src/UniClaw.Agent/Goal", "src/UniClaw.Agent/README.md"),
        ("src/UniClaw.Agent/Evaluation", "src/UniClaw.Agent/README.md"),
        ("src/UniClaw.Agent.Dsh", "src/UniClaw.Agent.Dsh/README.md"),
        ("src/UniClaw.Host", "src/UniClaw.Host/README.md"),
        ("src/UniClaw.Host/Capability", "src/UniClaw.Host/README.md"),
        ("src/UniClaw.Host/Runtime", "src/UniClaw.Host/README.md"),
        ("src/UniClaw.Host/SettingsCoverage", "src/UniClaw.Host/README.md"),
        ("src/UniClaw.Host.Dsh", "src/UniClaw.Host.Dsh/README.md"),
        ("src/UniClaw.Core", "src/UniClaw.Core/README.md"),
    };

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    [Fact]
    public void SourceFiles_AreRegistered_InDimensionReadme()
    {
        var root = RepoRoot();
        var byDir = Ownership.ToDictionary(entry => entry.Dir, entry => entry.Readme, StringComparer.Ordinal);
        var readmeCache = new Dictionary<string, string>(StringComparer.Ordinal);
        var violations = new List<string>();
        var checkedFiles = 0;

        foreach (var project in Directory.EnumerateDirectories(Path.Combine(root, "src"), "*", SearchOption.TopDirectoryOnly))
        {
            foreach (var file in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            {
                if (IsGenerated(file))
                    continue;
                checkedFiles++;
                var directory = Path.GetDirectoryName(file)!;
                var relativeDir = Relative(root, directory);
                if (!byDir.TryGetValue(relativeDir, out var readme))
                {
                    violations.Add($"{Relative(root, file)}: 目录 '{relativeDir}' 无归属条目（新目录须经 change 修订 Ownership 表）");
                    continue;
                }
                if (!readmeCache.TryGetValue(readme, out var text))
                    readmeCache[readme] = text = File.ReadAllText(Path.Combine(root, readme));
                var stem = Path.GetFileNameWithoutExtension(file);
                if (!text.Contains(stem, StringComparison.Ordinal))
                    violations.Add($"{Relative(root, file)}: 未在归属 README {readme} 中点名");
            }
        }

        Assert.True(checkedFiles > 50, $"src/ 源文件扫描异常（仅 {checkedFiles} 个）");
        Assert.True(violations.Count == 0,
            $"维度 README 登记违规（{checkedFiles} 个源文件受检，ARCH-DOC-022 执法）：\n" + string.Join("\n", violations));
    }

    private static bool IsGenerated(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
    }

    private static string Relative(string root, string path) => path[(root.Length + 1)..];
}
