using UniClaw.Agent.Dsh;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

/// <summary>
/// PNL-012：model-procedure 工具（run-diagnosis）执行执法与诊断流程。
/// 用合成 registry + 合成 run 目录（手写最小 report.json），Transport 注入 fake。
/// </summary>
public sealed class RuntimeToolHostProcedureTests
{
    private static readonly ModelConfiguration Model = new("prov", "diag-1");

    private sealed record Fixture(RuntimeToolHost Host, string RunsRoot, string RunDirName)
    {
        public string ReportPath => Path.Combine(RunsRoot, RunDirName, "report", "report.json");
    }

    private static Fixture CreateFixture(string status = "implemented", string surfaces = "[coder, workbench]",
        string? skillRef = "skill", bool withReport = true, string invocation = "model-procedure")
    {
        var dir = Directory.CreateTempSubdirectory("pnl012-diagnosis-");
        var registryDir = Path.Combine(dir.FullName, "registry");
        Directory.CreateDirectory(registryDir);
        if (skillRef is not null)
        {
            var skillDir = Path.Combine(registryDir, "skill");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"),
                "# Procedure\n\nE0-E4 evidence levels. Expected/Observed/Gap analysis. Owner localization.");
        }
        var lines = new[]
        {
            "version: 1",
            "tools:",
            "  run-diagnosis:",
            "    summary: 诊断",
            "    backing: harness-native",
            $"    invocation: {invocation}",
            skillRef is null ? "# no skillRef" : $"    skillRef: {skillRef}",
            "    requiredCapability: semantic_analysis",
            "    posture: read-only",
            $"    surfaces: {surfaces}",
            $"    status: {status}",
        };
        File.WriteAllLines(Path.Combine(registryDir, "tool-registry.yaml"), lines);
        var host = RuntimeToolHost.Load(Path.Combine(registryDir, "tool-registry.yaml"));
        var runsRoot = Path.Combine(dir.FullName, "runs");
        var runDirName = "run-20260101-000000-000";
        Directory.CreateDirectory(Path.Combine(runsRoot, runDirName));
        if (withReport)
        {
            Directory.CreateDirectory(Path.Combine(runsRoot, runDirName, "report"));
            File.WriteAllText(Path.Combine(runsRoot, runDirName, "report", "report.json"),
                """{"schemaVersion":"uniclaw.run-report.v1","runId":"run-20260101-000000-000","status":"failed","verdict":"failure"}""");
        }
        return new Fixture(host, runsRoot, runDirName);
    }

    private static RuntimeToolHost.DiagnosisTransport FakeTransport(string text = "诊断输出：非权威观察，不构成 Runtime truth\n根因：A") =>
        (_, _, _, _) => Task.FromResult(text);

    [Fact]
    public async Task Invoke_RejectedWhenPlanned()
    {
        var fixture = CreateFixture(status: "planned");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName, FakeTransport(), Model));
        Assert.Contains("tool-not-invokable", error.Message);
        Assert.Contains("planned", error.Message);
    }

    [Fact]
    public async Task Invoke_RejectedWhenNotWorkbench()
    {
        var fixture = CreateFixture(surfaces: "[coder]");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName, FakeTransport(), Model));
        Assert.Contains("not exposed to the workbench surface", error.Message);
    }

    [Fact]
    public async Task Invoke_RejectedWhenSkillRefMissing()
    {
        var fixture = CreateFixture(skillRef: null);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName, FakeTransport(), Model));
        Assert.Contains("no skillRef", error.Message);
    }

    [Fact]
    public async Task Invoke_RejectedWhenProcedureFileMissing()
    {
        var fixture = CreateFixture(skillRef: "absent-skill");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName, FakeTransport(), Model));
        Assert.Contains("procedure does not exist", error.Message);
    }

    [Fact]
    public async Task Invoke_ReportMissing_ReturnsExplicitErrorPointingAtRunReport()
    {
        var fixture = CreateFixture(withReport: false);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName, FakeTransport(), Model));
        Assert.Contains("diagnosis-report-missing", error.Message);
        Assert.Contains("run-report", error.Message);
    }

    [Fact]
    public async Task Invoke_HappyPath_PromptCarriesProcedureAndReport_ReturnsTextModelReportRef()
    {
        var fixture = CreateFixture();
        string? capturedPrompt = null;
        var result = await fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName,
            (_, prompt, _, _) => { capturedPrompt = prompt; return Task.FromResult("诊断输出：非权威观察，不构成 Runtime truth\n结论：X"); },
            Model);
        Assert.NotNull(capturedPrompt);
        Assert.Contains("E0-E4 evidence levels", capturedPrompt);          // SKILL.md 关键内容
        Assert.Contains("Expected/Observed/Gap analysis", capturedPrompt);
        Assert.Contains("run-20260101-000000-000", capturedPrompt);         // 报告内容
        Assert.Contains("\"verdict\":\"failure\"", capturedPrompt);
        Assert.Contains(RuntimeToolHost.DiagnosisNotice, capturedPrompt);   // 输出指示
        Assert.Equal("prov/diag-1", result.Model);
        Assert.Contains("非权威观察", result.Text);
        Assert.Equal(Path.Combine(fixture.RunDirName, "report", "report.json"), result.ReportRef);
    }

    [Fact]
    public async Task Invoke_TransportFailure_ReturnsHonestError()
    {
        var fixture = CreateFixture();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName,
            (_, _, _, _) => throw new HttpRequestException("connection refused"),
            Model));
        Assert.Contains("diagnosis-transport-failed", error.Message);
        Assert.Contains("connection refused", error.Message);
    }

    [Fact]
    public async Task Invoke_TransportTimeout_ReturnsHonestTimeoutError()
    {
        var fixture = CreateFixture();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName,
            (_, _, _, ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct).ContinueWith(_ => string.Empty, ct),
            Model,
            timeout: TimeSpan.FromMilliseconds(50)));
        Assert.Contains("diagnosis-timeout", error.Message);
    }

    [Fact]
    public async Task Invoke_EmptyModelText_ReturnsHonestError()
    {
        var fixture = CreateFixture();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName, FakeTransport("  "), Model));
        Assert.Contains("empty text", error.Message);
    }

    [Fact]
    public async Task Invoke_RejectedForDeterministicScriptTool()
    {
        var fixture = CreateFixture(invocation: "deterministic-script");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.InvokeProcedureAsync(
            "run-diagnosis", fixture.RunsRoot, fixture.RunDirName, FakeTransport(), Model));
        Assert.Contains("not a model-procedure tool", error.Message);
    }

    [Fact]
    public void RealRegistry_ProcedureCarrierExists()
    {
        var host = RuntimeToolHost.Load(Path.Combine(RuntimeToolHostTests.RepoRoot(), "tool-registry.yaml"));
        var diagnosis = host.Find("run-diagnosis");
        Assert.NotNull(diagnosis);
        Assert.Equal("implemented", diagnosis!.Status);
        Assert.Equal("read-only", diagnosis.Posture);
        Assert.True(File.Exists(Path.Combine(
            RuntimeToolHostTests.RepoRoot(), diagnosis.SkillRef!, "SKILL.md")),
            "procedure carrier SKILL.md must exist");
    }
}
