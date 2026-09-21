using System.Reflection;
using UniClaw.Host;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// HOST-001 验收：完整最小闭环（观察→决策→effect→Outcome 语义）+ journal
/// 必注入产物 + 仿真可复现（两次 run digest 一致，spec v0.3 Acceptance #7）。
/// </summary>
public sealed class HostEndToEndTests
{
    private static string TempRoot()
        => Path.Combine(Path.GetTempPath(), "uniclaw-host-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FullLoop_DeliversOnce_WritesAllArtifacts()
    {
        var root = TempRoot();
        try
        {
            var result = HostRunner.RunOnce(root);

            // 闭环证据（Acceptance #1/#2）：Completed + Completion outcome + journal 有 pre-dispatch 记录
            Assert.Equal(RunDriveStatus.Completed, result.Status);
            Assert.Equal("Completion", result.OutcomeClassification);
            Assert.Single(result.ReceiptOutcomes);
            Assert.Equal("DeliveryCompleted", result.ReceiptOutcomes[0]);
            Assert.True(result.JournalBytes > 0, "journal 应有 pre-dispatch 记录");
            Assert.True(File.Exists(Path.Combine(result.RunDir, "exec.journal")));
            Assert.True(File.Exists(Path.Combine(result.RunDir, "trace.json")));
            Assert.True(File.ReadAllText(Path.Combine(result.RunDir, "facts.json")).Length > 0);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SimulationIsReproducible_TwoRunsSameDigest_Acceptance7()
    {
        var rootA = TempRoot();
        var rootB = TempRoot();
        try
        {
            var a = HostRunner.RunOnce(rootA);
            var b = HostRunner.RunOnce(rootB);

            Assert.Equal(a.Status, b.Status);
            Assert.Equal(a.ReceiptOutcomes, b.ReceiptOutcomes);
            Assert.Equal(a.FactsDigest, b.FactsDigest); // 仿真流程=正式能力：可复现
        }
        finally
        {
            if (Directory.Exists(rootA)) Directory.Delete(rootA, recursive: true);
            if (Directory.Exists(rootB)) Directory.Delete(rootB, recursive: true);
        }
    }
}

/// <summary>
/// HOST-001 依赖闭包执法（Acceptance #4，还 RFS-001 债）：Host 程序集
/// UniClaw.* 引用 ⊆ {UniClaw.Kernel}；无 Simulation/Replay/Oracle/Importer 类型。
/// </summary>
public sealed class HostClosureTests
{
    [Fact]
    public void HostReferencesOnlyKernelAmongUniClawAssemblies()
    {
        var references = typeof(HostRunner).Assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith("UniClaw.", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(new[] { "UniClaw.Kernel" }, references.OrderBy(n => n));
    }

    [Fact]
    public void HostAssemblyContainsNoSimulationOrScenarioReplayTypes()
    {
        // G23 精确执法：禁的是**场景回放机制**（Simulation Host 领地）。
        // 感知回放（录制感知锚喂能力缝，ReplayPerception）是能力层合法件
        // ——2026-09-20 定性：「感知回放模拟真实感知功能；Trace 回放模拟
        // 整个场景」。故禁词用具体类型名，不用裸词 Replay。
        var forbidden = new[]
        {
            "ScenarioStimulus", "ScenarioImporter", "ScenarioRunner",
            "SemanticDigest", "Oracle", "ScriptedUniAgent", "MinimalScenarioBundle",
        };
        var typeNames = typeof(HostRunner).Assembly
            .GetTypes()
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(typeNames.Where(name => forbidden.Any(f => name.Contains(f, StringComparison.Ordinal))));
    }
}
