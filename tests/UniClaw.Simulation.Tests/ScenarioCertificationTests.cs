using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-002 G2（S2）验收面：golden 认证持久化 + Verify-only。
/// 验收语义（spec）：「修改 Expected 后重跑测试不再自动通过认证」——
/// 本测试族对整个场景库执法：期望值摘要、运行时源码哈希、致因 change
/// 引用三者钉扎在 certification 块中，test runtime 无写回路径。
/// </summary>
public sealed class ScenarioCertificationTests
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
    public void ScenarioLibrary_EveryEntryCertifiedAndMatching()
    {
        var repo = RepoRoot();
        var (violations, files) = ScenarioCertification.VerifyLibrary(
            Path.Combine(repo, "scenarios"), repo);

        Assert.True(files > 0, "场景库为空（scenarios/SCN-*.json 未发现）");
        Assert.Empty(violations);
    }

    /// <summary>
    /// 验收正例（spec 原文场景）：期望值被改动（effects+1）后，认证必须
    /// 拒绝——不再存在「重跑自动重新认证」路径。在临时副本上模拟篡改，
    /// 不触碰仓库文件。
    /// </summary>
    [Fact]
    public void TamperedExpectations_FailCertification_AutoReSealImpossible()
    {
        var repo = RepoRoot();
        var source = Path.Combine(repo, "scenarios", "SCN-WIFI-001.json");
        var temp = Path.Combine(Path.GetTempPath(), "uniclaw-cert-tamper-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            // 篡改：effects 1 → 2（不带 certification 更新——这正是要堵的路径）
            File.WriteAllText(temp, File.ReadAllText(source).Replace("\"effects\": 1", "\"effects\": 2"));

            var violations = ScenarioCertification.VerifyFile(temp, repo);

            Assert.Contains(violations, v => v.Contains("摘要不匹配"));
            Assert.DoesNotContain(violations, v => v.Contains("源码哈希不匹配")); // 源未变，仅期望被改
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    /// <summary>
    /// 运行时源变更同样使认证失效（Kernel/Agent 源在认证后改动而未重认证）。
    /// 用伪造 certification 的临时副本模拟「源码哈希过期」。
    /// </summary>
    [Fact]
    public void StaleRuntimeSourceHash_FailsCertification()
    {
        var repo = RepoRoot();
        var source = Path.Combine(repo, "scenarios", "SCN-WIFI-001.json");
        var temp = Path.Combine(Path.GetTempPath(), "uniclaw-cert-stale-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var json = File.ReadAllText(source)
                .Replace(
                    ScenarioCertification.RuntimeSourceHash(repo),
                    new string('0', 64)); // 伪造：认证时的源码哈希 ≠ 当前
            File.WriteAllText(temp, json);

            var violations = ScenarioCertification.VerifyFile(temp, repo);

            Assert.Contains(violations, v => v.Contains("源码哈希不匹配"));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
