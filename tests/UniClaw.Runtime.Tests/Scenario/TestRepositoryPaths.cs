namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>测试仓库路径定位（从测试输出目录向上找含 AGENTS.md 的仓库根）。</summary>
public static class TestRepositoryPaths
{
    /// <summary>仓库根目录（同时含 AGENTS.md 与 src/UniClaw.Runtime.sln 的目录 —
    /// 子级区域地图只满足 AGENTS.md，不满足 sln 标记）。</summary>
    /// <returns>仓库根绝对路径。</returns>
    /// <exception cref="InvalidOperationException">未找到仓库根。</exception>
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
            && !(File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(dir.FullName, "src", "UniClaw.Runtime.sln"))))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未找到仓库根（AGENTS.md + src/UniClaw.Runtime.sln）。");
    }

    /// <summary>仓库根下的相对路径（跨平台分隔符）。</summary>
    /// <param name="relative">仓库根下的相对路径段。</param>
    /// <returns>拼接后的绝对路径。</returns>
    public static string RepoPath(params string[] relative)
        => Path.Combine(new[] { RepoRoot() }.Concat(relative).ToArray());
}
