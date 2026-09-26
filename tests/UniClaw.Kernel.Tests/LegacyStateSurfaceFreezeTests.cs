using Xunit;
using System.Text.RegularExpressions;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// PER-013 Slice E / M-09：legacy `*.state` surface 冻结守卫——reader inventory
/// 落档后，任何新增 `*.state` 触点（reader/writer，含注释提及）= RED，须经
/// change 显式修订本名单（PER-012 §2「禁止新增 legacy consumer」的机械执法；
/// 同 KernelRuntimeSurfaceWhitelistTests 哲学）。移除触点同样 RED（删除须走
/// M-10 四 gate）。
/// PER-014 post-migration reality（R2/R4/R5 修订后）：
/// - 本名单内允许的触点 = egress writers（UiAutomatorDump / LivePerception /
///   HostRunner scope）、rollback flag（HostRunner.LegacyStateEgress，默认关）、
///   ConflictResolver legacy 裁决路径（R4 休眠 rollback 面）、SharedSubjects
///   常量、LegacyStateProjection egress 投影、AgentPlanPolicy 通用 subject
///   前缀逻辑（subject-parametric，非 legacy 耦合）。
/// - 生产 `*.state` reader = 0（零 UNJUSTIFIED readers）；T10
///   （Per014CutoverTests）按 file:line 复算 reader inventory。
/// </summary>
public sealed class LegacyStateSurfaceFreezeTests
{
    /// <summary>
    /// 2026-09-27 reader inventory（file:line 级记录见 changes/PER-013/state.md
    /// Slice E 节）：`*.state` subject/value 触点的全部 src 文件。
    /// PER-014 R2 修订：UniKernel 生产验证路径改走 typed（SemanticCheckedResolver
    /// 缝），`{role}.state` 读取已删除——本名单随 change 显式修订（移除 =
    /// M-10 zero-production-readers 事实登记；legacy surface 本身不删）。
    /// </summary>
    private static readonly string[] FrozenSurfaceFiles =
    {
        "src/UniClaw.Host/HostRunner.cs",            // TargetState 词汇（checked/unchecked）+ egress scope
        "src/UniClaw.Host/LivePerception.cs",        // switch.state writer（host.live）+ 组合
        "src/UniClaw.Host/UiAutomatorDump.cs",       // {role}.state 映射 writer（legacy/egress 路由）
        "src/UniClaw.Kernel/Compatibility/LegacyStateProjection.cs", // egress 投影（值域 on/off）
        "src/UniClaw.Kernel/Evidence/SharedSubjects.cs",             // State() 常量
        "src/UniClaw.Kernel/Runtime/AgentPlanPolicy.cs",             // subject 前缀聚焦
        "src/UniClaw.Kernel/World/ConflictResolver.cs",               // *.state 冲突裁决（legacy-only，R4 休眠）
    };

    private static readonly Regex StateSurface = new(@"\.state\b|StateSubject", RegexOptions.Compiled);

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
    public void LegacyStateSurface_MatchesFrozenInventory_NoNewLegacyConsumers()
    {
        var root = RepoRoot();
        var actual = new List<string>();
        foreach (var project in new[] { "src/UniClaw.Host", "src/UniClaw.Kernel" })
        {
            var dir = Path.Combine(root, project);
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/obj/") || relative.Contains("/bin/"))
                    continue;
                if (StateSurface.IsMatch(File.ReadAllText(file)))
                    actual.Add(relative);
            }
        }

        actual.Sort(StringComparer.Ordinal);
        var expected = FrozenSurfaceFiles.OrderBy(f => f, StringComparer.Ordinal).ToArray();
        Assert.True(
            actual.SequenceEqual(expected),
            "legacy *.state surface 变更 = RED（PER-012：禁止新增 legacy consumer；"
            + $"删除须走 M-10 四 gate）。新增：[{string.Join(", ", actual.Except(expected))}] "
            + $"移除：[{string.Join(", ", expected.Except(actual))}]——须 change 显式修订本名单");
    }
}
