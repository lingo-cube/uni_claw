using Xunit;

namespace UniClaw.Host.Dsh.Tests;

public sealed class RuntimeToolHostTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tool-registry.yaml")))
            dir = dir.Parent!;
        return dir?.FullName ?? throw new InvalidOperationException("repo root with tool-registry.yaml not found");
    }

    private static RuntimeToolHost LoadReal() => RuntimeToolHost.Load(Path.Combine(RepoRoot(), "tool-registry.yaml"));

    [Fact]
    public void ParsesRealRegistry_WorkbenchSurfaceHasBothBuyers()
    {
        var host = LoadReal();
        var workbench = host.ForSurface("workbench");
        var names = workbench.Select(tool => tool.Name).ToArray();
        Assert.Contains("run-report", names);
        Assert.Contains("run-diagnosis", names);
        Assert.Equal(2, names.Length); // 排序稳定
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
    }

    [Fact]
    public void ParsesRealRegistry_FieldsSurviveRoundTrip()
    {
        var host = LoadReal();
        var report = host.Find("run-report");
        Assert.NotNull(report);
        Assert.Equal("harness-native", report!.Backing);
        Assert.Equal("deterministic-script", report.Invocation);
        Assert.Equal("tools/gen-run-report.py", report.Entry);
        Assert.Equal("tool_only", report.RequiredCapability);
        Assert.Equal("local-write", report.Posture);
        Assert.Equal("implemented", report.Status);
        Assert.Equal(new[] { "cli", "workbench" }, report.Surfaces);

        var diagnosis = host.Find("run-diagnosis");
        Assert.NotNull(diagnosis);
        Assert.Equal("planned", diagnosis!.Status);
        Assert.Equal("model-procedure", diagnosis.Invocation);
        Assert.Equal(".agents/skills/uniclaw-debug-evidence", diagnosis.SkillRef);
        Assert.Equal(new[] { "run-report" }, diagnosis.Consumes);
    }

    [Fact]
    public void ParsesMinimalSyntheticRegistry_InlineListsAndComments()
    {
        var dir = Directory.CreateTempSubdirectory("pnl008-registry-");
        try
        {
            var path = Path.Combine(dir.FullName, "registry.yaml");
            File.WriteAllLines(path, new[]
            {
                "# comment",
                "version: 1",
                "tools:",
                "  alpha:",
                "    summary: 测试工具 # 行内注释",
                "    backing: harness-native",
                "    invocation: deterministic-script",
                "    entry: tools/alpha.py",
                "    requiredCapability: tool_only",
                "    posture: read-only",
                "    surfaces: [cli, workbench]",
                "    status: implemented",
            });
            var host = RuntimeToolHost.Load(path);
            var alpha = host.Find("alpha");
            Assert.NotNull(alpha);
            Assert.Equal("测试工具", alpha!.Summary);
            Assert.Equal(new[] { "cli", "workbench" }, alpha.Surfaces);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Invoke_RejectsUnknownTool()
    {
        var host = LoadReal();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.InvokeAsync("no-such-tool", Path.GetTempPath(), "x"));
        Assert.StartsWith("tool-not-found", error.Message);
    }

    [Fact]
    public async Task Invoke_RejectsPlannedAndNonWorkbenchTools()
    {
        var host = LoadReal();
        var planned = await Assert.ThrowsAsync<InvalidOperationException>(() => host.InvokeAsync("run-diagnosis", Path.GetTempPath(), "x"));
        Assert.StartsWith("tool-not-invokable", planned.Message);
        Assert.Contains("planned", planned.Message);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("nested/path")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData("..")]
    public async Task Invoke_RejectsUnsafeRunDirs(string runDir)
    {
        var host = LoadReal();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.InvokeAsync("run-report", Path.GetTempPath(), runDir));
        Assert.StartsWith("invalid-run-dir", error.Message);
    }

    [Fact]
    public async Task Invoke_RejectsMissingRunDirectory()
    {
        var host = LoadReal();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.InvokeAsync("run-report", Path.GetTempPath(), "definitely-not-here-000"));
        Assert.StartsWith("invalid-run-dir", error.Message);
    }

    [Fact]
    public async Task Invoke_HappyPath_GeneratesReportIntoRunDir()
    {
        // 真实工具 + 真 python3（与仓库其他 live 测试同等环境假设）。
        var runsRoot = Directory.CreateTempSubdirectory("pnl008-runs-");
        try
        {
            var runDir = Path.Combine(runsRoot.FullName, "run-test-001");
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, "metadata.json"),
                """{"productSessionId":"s1","productSessionTitle":"t","device":"d","consultations":1}""");

            var host = LoadReal();
            var result = await host.InvokeAsync("run-report", runsRoot.FullName, "run-test-001");

            Assert.True(result.Ok);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("run-test-001/report/report.json", result.ReportJson?.Replace('\\', '/'));
            Assert.Equal("run-test-001/report/report.md", result.ReportMd?.Replace('\\', '/'));
            Assert.True(File.Exists(Path.Combine(runDir, "report", "report.json")));
            Assert.Contains("timelineEvents=", result.StdoutTail);
        }
        finally
        {
            runsRoot.Delete(recursive: true);
        }
    }
}
