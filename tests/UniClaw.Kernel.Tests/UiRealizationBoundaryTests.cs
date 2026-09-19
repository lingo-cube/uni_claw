using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// CORE-015 Step 4（选项 3）—— UI realization 模块边界执法：
/// `src/UniClaw.Kernel/World/UiRealization/` 是 UI World realization 的
/// 模块落点（realization 契约 §1/§6 的结构化身）。模块内源文件的 using
/// 面只允许：
///   - System.*（BCL）
///   - UniClaw.Core（顶层抽象契约）
///   - UniClaw.Kernel.World（World owner——组合宿主）
///   - UniClaw.Kernel.Evidence（契约 §2 声明输入缝：accepted Evidence）
///   - 自身命名空间
/// 任何对 Control / Assurance / Effects / Run / Trace / Perception /
/// Diagnostics / Agent 的依赖都越界（realization 契约 §4 不得拥有清单的
/// 类型级化身）。owner → 模块方向的组合引用不在本测试约束内（单
/// realization 现状下 revision 即 UI realization 产物——契约 §3）。
/// 程序集升格（独立 csproj）推迟到第二个 realization buyer 出现。
/// </summary>
public sealed class UiRealizationBoundaryTests
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
        throw new InvalidOperationException("未定位到仓库根（AGENTS.md + slnx 标记）");
    }

    private static readonly string[] AllowedUsingPrefixes =
    {
        "using System",
        "using UniClaw.Core",
        "using UniClaw.Kernel.World;",
        "using UniClaw.Kernel.World.UiRealization;",
        "using UniClaw.Kernel.Evidence;",
    };

    [Fact]
    public void UiRealizationModule_DependsOnlyOnAllowedSurfaces()
    {
        var moduleDir = Path.Combine(
            RepoRoot(), "src", "UniClaw.Kernel", "World", "UiRealization");
        var sources = Directory.GetFiles(moduleDir, "*.cs");
        Assert.NotEmpty(sources);

        foreach (var file in sources)
        {
            foreach (var line in File.ReadAllLines(file))
            {
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("using ", StringComparison.Ordinal)
                    || trimmed.StartsWith("using (", StringComparison.Ordinal))
                    continue;
                Assert.True(
                    AllowedUsingPrefixes.Any(p => trimmed.StartsWith(p, StringComparison.Ordinal)),
                    $"{Path.GetFileName(file)} 越界依赖：{trimmed}（UI realization 模块只允许 System/Core/World owner/Evidence 输入缝）");
            }
        }
    }
}
