using System.Text.RegularExpressions;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// RFS-001 / baseline §24.8 — Product Host 依赖闭包可执行执法。
///
/// HONEST FRAMING（RFS-001 D23）：本测试证明 CURRENT truth——当前产品
/// 程序集（UniClaw.Kernel / UniClaw.Agent）不含 Simulation 功能/标识符，
/// 且对 tests/ 无任何编译依赖；唯一跨边界是 Kernel.csproj 显式声明的
/// InternalsVisibleTo test seam（恰好两个测试程序集）。它不是「完整未来
/// Product Host 依赖闭包」的证明——Product Host 尚不存在（RFS-001 D23），
/// 其组合面未来引入的依赖只能由届时的事实测试执法，本测试不预支该结论。
/// Simulation 只存在于独立 Simulation Host（tests/UniClaw.Simulation.Tests）。
/// 本测试不引用该项目或其类型——缺席性以磁盘扫描方式执法。
/// </summary>
public sealed class ProductHostClosureTests
{
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

    private static string Relative(string path) => path[(RepoRoot().Length + 1)..];

    [Fact]
    public void ProductProjects_DoNotReferenceSimulationProjects()
    {
        var root = RepoRoot();
        var kernelCsproj = File.ReadAllText(Path.Combine(root, "src", "UniClaw.Kernel", "UniClaw.Kernel.csproj"));
        var agentCsproj = File.ReadAllText(Path.Combine(root, "src", "UniClaw.Agent", "UniClaw.Agent.csproj"));
        var violations = new List<string>();

        // Kernel：只允许直接引用领域无关 Core；不得引用 legacy、Agent、Simulation、Harness。
        var kernelReferences = Regex.Matches(kernelCsproj, @"<ProjectReference\s+Include=""([^""]+)""")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Replace('\\', '/'))
            .ToList();
        var allowedKernelReference = "UniClaw.Core/UniClaw.Core.csproj";
        foreach (var include in kernelReferences)
        {
            var normalized = include.TrimStart('.', '/');
            if (!normalized.EndsWith(allowedKernelReference, StringComparison.Ordinal))
                violations.Add($"src/UniClaw.Kernel/UniClaw.Kernel.csproj: ProjectReference 越界（{include}，仅允许 UniClaw.Core）");
        }
        if (kernelReferences.Count != 1)
            violations.Add($"src/UniClaw.Kernel/UniClaw.Kernel.csproj: 必须恰好引用 UniClaw.Core（实际 {kernelReferences.Count} 项）");

        // Agent：ProjectReference 仅允许 UniClaw.Kernel
        foreach (var match in Regex.Matches(agentCsproj, @"<ProjectReference\s+Include=""([^""]+)""").Cast<Match>())
        {
            var include = match.Groups[1].Value;
            if (!include.Replace('\\', '/').EndsWith("src/UniClaw.Kernel/UniClaw.Kernel.csproj", StringComparison.Ordinal)
                && !include.Replace('\\', '/').Equals("../UniClaw.Kernel/UniClaw.Kernel.csproj", StringComparison.Ordinal))
                violations.Add($"src/UniClaw.Agent/UniClaw.Agent.csproj: ProjectReference 越界（{include}，仅允许 UniClaw.Kernel）");
        }

        // D23 test-seam boundary：Kernel 的 InternalsVisibleTo 集合恰好等于
        // 两个测试程序集（显式、穷举——多一项即 Simulation 功能越界进入
        // 产品可见面，少一项即测试 seam 缺失）
        var internalsVisibleTo = Regex.Matches(kernelCsproj, @"<InternalsVisibleTo\s+Include=""([^""]+)""")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();
        var expected = new HashSet<string> { "UniClaw.Kernel.Tests", "UniClaw.Simulation.Tests" };
        if (!internalsVisibleTo.ToHashSet().SetEquals(expected))
            violations.Add(
                "src/UniClaw.Kernel/UniClaw.Kernel.csproj: InternalsVisibleTo 集合须恰好等于 "
                + "{ UniClaw.Kernel.Tests, UniClaw.Simulation.Tests }，实际为 ["
                + string.Join(", ", internalsVisibleTo) + "]");
        if (internalsVisibleTo.Count != internalsVisibleTo.Distinct().Count())
            violations.Add("src/UniClaw.Kernel/UniClaw.Kernel.csproj: InternalsVisibleTo 存在重复条目");

        // 两个产品 csproj 均不得以 <Compile Include / <ProjectReference 指向 tests/ 下任何内容
        foreach (var (path, text) in new[]
        {
            ("src/UniClaw.Kernel/UniClaw.Kernel.csproj", kernelCsproj),
            ("src/UniClaw.Agent/UniClaw.Agent.csproj", agentCsproj),
        })
        {
            foreach (var pattern in new[] { @"<Compile\s+Include=""([^""]+)""", @"<ProjectReference\s+Include=""([^""]+)""" })
            {
                foreach (var match in Regex.Matches(text, pattern).Cast<Match>())
                {
                    var include = match.Groups[1].Value.Replace('\\', '/');
                    if (include.Contains("tests/", StringComparison.Ordinal) || include.StartsWith("tests", StringComparison.Ordinal))
                    {
                        var itemKind = pattern.StartsWith("<Compile", StringComparison.Ordinal) ? "Compile" : "ProjectReference";
                        violations.Add($"{path}: {itemKind} 引用 tests/ 下内容（{include}）");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Product Host 依赖闭包违规（baseline §24.8）：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void ProductSources_ContainNoSimulationIdentifiers()
    {
        var root = Path.Combine(RepoRoot(), "src");
        Assert.True(Directory.Exists(root), "src/ 目录不存在");
        string[] forbidden =
        [
            "ScenarioStimulus",
            "ScriptedUniAgent",
            "MinimalScenarioBundle",
            "ScenarioImporter",
            "SimulationHost",
            "UniClaw.Simulation",
        ];
        var violations = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Contains("obj") || parts.Contains("bin"))
                continue;
            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{Relative(path)}: 含禁止标识 {token}");
            }
        }

        Assert.True(violations.Count == 0,
            "产品源码泄漏 Simulation 功能（baseline §24.8）：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Solution_SimulationTestProjectIsSeparateCompositionRoot()
    {
        var root = RepoRoot();
        var slnx = File.ReadAllText(Path.Combine(root, "UniClaw.Kernel.slnx"));
        Assert.True(slnx.Contains("tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj", StringComparison.Ordinal),
            "slnx 缺 Simulation Host 测试项目");
        Assert.True(slnx.Contains("src/UniClaw.Kernel/UniClaw.Kernel.csproj", StringComparison.Ordinal)
            && slnx.Contains("src/UniClaw.Agent/UniClaw.Agent.csproj", StringComparison.Ordinal),
            "slnx 缺产品项目（/src/ 下须含 Kernel 与 Agent）");

        var simCsprojPath = Path.Combine(root, "tests", "UniClaw.Simulation.Tests", "UniClaw.Simulation.Tests.csproj");
        Assert.True(File.Exists(simCsprojPath), "simulation test project missing");

        var simCsproj = File.ReadAllText(simCsprojPath);
        var references = Regex.Matches(simCsproj, @"<ProjectReference\s+Include=""([^""]+)""")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Replace('\\', '/'))
            .ToList();
        Assert.True(references.Count == 2
                && references.Any(r => r.EndsWith("src/UniClaw.Kernel/UniClaw.Kernel.csproj", StringComparison.Ordinal))
                && references.Any(r => r.EndsWith("src/UniClaw.Agent/UniClaw.Agent.csproj", StringComparison.Ordinal)),
            $"Simulation Host 须恰好引用两个产品 csproj（Kernel + Agent，无其他）：实际为 [{string.Join(", ", references)}]");
    }
}
