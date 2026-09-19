using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 Phase 1 per-cycle 零接触纪律（belt-and-braces；字符串扫描面）。
/// KernelFacts 是首选只读投影，并非编译级 enforcement：同一 test
/// assembly 的 internal 可达性仍在。本文件是当前可执行的源码纪律门：
/// 场景测试代码（含本文件）绝不直接调用 kernel 的 per-cycle
/// 内部面（.Process / SelectIntent / ActViaCurrentGrounding /
/// EvaluateTerminal / .Act）。
/// Driver.Activate()/Drive() 与 Host.DriveOnce()/Host.SubmitStimulus() 是
/// host 一次性提交面（phased 场景 D21/D22 合法使用），不在禁止列。
/// 禁止子串在本文件内以拼接字面量表达，避免自扫描自伤。
/// </summary>
public sealed class PerCycleZeroDisciplineTests
{
    private static readonly string[] ScenarioTestFiles =
    {
        "DeterministicScenarioTests.cs",
        "TraceArmsAndDigestTests.cs",
        "FailClosedScenarioTests.cs",
        "TwoStepBarrierTests.cs",
        "ImportReDriveTests.cs",
        "BundleIntegrityTests.cs",
        "SimulationHostSmokeTests.cs",
        "SemanticDigestTests.cs",
        "ScriptedUniAgentTests.cs",
        "ScenarioImporterTests.cs",
        "PerCycleZeroDisciplineTests.cs",
        // CORE-011：执行记录仿真同样只经合法面驱动（Admit→Activate→
        // DriveOnce + 组合层 Bind/Judge/Dispatch 产品 seam）
        "ReliableExecutionSourceFixture.cs",
        "ReliableExecutionRecordTests.cs",
    };

    /// <summary>per-cycle 内部调用面子串（运行时拼接，避免本文件自匹配）。</summary>
    private static string[] PerCycleInvocations() => new[]
    {
        "." + "Process" + "(",
        "Select" + "Intent" + "(",
        "ActViaCurrent" + "Grounding" + "(",
        "Evaluate" + "Terminal" + "(",
        "." + "Act" + "(",
    };

    private static string ReadScenarioSource(string fileName) =>
        File.ReadAllText(Path.Combine(
            GoldenPaths.RepoRoot(), "tests", "UniClaw.Simulation.Tests", fileName));

    private static int CountOccurrences(string source, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    /// <summary>
    /// 全部场景测试源文件（含本文件自身）从磁盘读取后核对：
    /// 不含任何 per-cycle 内部调用子串。Host.Driver.Activate/Drive、
    /// Host.DriveOnce/SubmitStimulus 与 digest/report/Facts 读取是合法的
    /// host 提交/观察面，不在禁止列。
    /// </summary>
    [Fact]
    public void TestCode_MakesNoPerCycleRuntimeCalls()
    {
        foreach (var file in ScenarioTestFiles)
        {
            var source = ReadScenarioSource(file);
            foreach (var forbidden in PerCycleInvocations())
            {
                Assert.True(
                    CountOccurrences(source, forbidden) == 0,
                    $"{file} 含 per-cycle 内部调用子串: {forbidden}");
            }
        }
    }

    /// <summary>
    /// runner 源码纪律（单相位语义）：恰好一次 Drive（经 Host.DriveOnce，
    /// 不直接触碰 Driver.Drive）、至少一次 AdmitContract 与 Activate、
    /// 零 per-cycle 内部调用；且真实 UniAgent 参与目标评估。phased 场景的
    /// 后续 Drive 由测试经 Host.DriveOnce 驱动（不在 runner 内）。
    /// </summary>
    [Fact]
    public void Runner_MakesExactlyOneSubmission()
    {
        var source = ReadScenarioSource("ScenarioRunner.cs");

        Assert.Equal(1, CountOccurrences(source, ".DriveOnce" + "("));
        Assert.Equal(0, CountOccurrences(source, ".Driver" + ".Drive" + "("));
        Assert.True(CountOccurrences(source, "AdmitContract") >= 1, "missing AdmitContract");
        Assert.True(CountOccurrences(source, ".Activate" + "(") >= 1, "missing Activate");

        foreach (var forbidden in PerCycleInvocations())
        {
            Assert.True(
                CountOccurrences(source, forbidden) == 0,
                $"ScenarioRunner.cs 含 per-cycle 内部调用子串: {forbidden}");
        }

        // 真实 Goal Evaluation 已接线（非 double）
        Assert.True(CountOccurrences(source, "UniAgent" + "(") >= 1, "missing real UniAgent wiring");
    }
}
