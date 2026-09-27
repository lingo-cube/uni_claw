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
///   SharedSubjects
///   常量、LegacyStateProjection egress 投影、AgentPlanPolicy 通用 subject
///   前缀逻辑（subject-parametric，非 legacy 耦合）。
/// - 生产 `*.state` reader = 0（零 UNJUSTIFIED readers）；T10
///   （Per014CutoverTests）按 file:line 复算 reader inventory。
/// </summary>
public sealed class LegacyStateSurfaceFreezeTests
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

    [Fact]
    public void ProductionSource_HasNoLegacyStateSurface()
    {
        var root = RepoRoot();
        var source = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Select(File.ReadAllText)
            .ToArray();
        Assert.DoesNotContain(source, text => text.Contains("LegacyState", StringComparison.Ordinal));
        Assert.DoesNotContain(source, text => text.Contains("MapTargetStateClaim", StringComparison.Ordinal));
        Assert.DoesNotContain(source, text => text.Contains("switch.state", StringComparison.Ordinal));
        Assert.DoesNotContain(source, text => text.Contains("SharedSubjects.State", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionLivePath_HasNoFixedDeviceOrPickFirstContract()
    {
        var root = RepoRoot();
        var liveFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains(Path.DirectorySeparatorChar + "Host" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal));
        var source = string.Join("\n", liveFiles.Select(File.ReadAllText));
        Assert.DoesNotContain("emulator-5554", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstOrDefault()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("devices.First()", source, StringComparison.Ordinal);
    }
}
