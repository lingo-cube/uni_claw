using UniClaw.Host;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// SIM-002 G1：Product Host 组合根 fail-closed 执法——仿真/回放档已移出
/// 产品闭包，未显式提供外部缝（Live 感知 / ConsultAgent 咨询）必须抛，
/// 不允许任何默认仿真路径（simulation baseline C4 反向闭包）。
/// 原 HOST-001 仿真端到端回归（完整闭环 / digest 可复现）迁至
/// UniClaw.Simulation.Tests.DevLoopTests（经 DevLoopRunner 组合同一
/// Kernel 真件——双 Host 对称）。
/// </summary>
public sealed class HostFailClosedTests
{
    private static string TempRoot()
        => Path.Combine(Path.GetTempPath(), "uniclaw-host-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RunWithoutLive_ThrowsFailClosed()
    {
        var root = TempRoot();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => HostRunner.RunOnce(root));
            Assert.Contains("SIM-002 G1", ex.Message);
            Assert.Contains("Live", ex.Message);
            // fail-closed 语义：拒绝发生在组合与落盘之前（run 目录未创建）
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RunWithoutConsultAgent_ThrowsFailClosed()
    {
        var root = TempRoot();
        try
        {
            // LiveAssets 是纯数据 record——构造不启动任何服务；
            // 咨询缝缺席必须在组合真实外部件之前被拒
            var ex = Assert.Throws<InvalidOperationException>(() => HostRunner.RunOnce(root,
                new HostRunner.HostOptions
                {
                    Live = new LivePerception.LiveAssets("no-device", "wifi-settings", "provider", "python"),
                }));
            Assert.Contains("SIM-002 G1", ex.Message);
            Assert.Contains("ConsultAgent", ex.Message);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

/// <summary>
/// HOST-001 依赖闭包执法（Acceptance #4，还 RFS-001 债）：Host 程序集
/// UniClaw.* 引用 ⊆ {UniClaw.Kernel}；无 Simulation/Replay/Oracle/Importer
/// 类型。SIM-002 G1（2026-09-22 外部第三轮审阅 S1 裁决）扩禁词至仿真/
/// 回放 feed：2026-09-20「感知回放属能力层、可留产品 Host」的定性被该
/// 评审推翻——ReplayPerception / ServicePerception / 仿真帧源与咨询
/// double 一并移入 tests/UniClaw.Simulation.Tests。
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
        // SIM-002 G1：禁词覆盖全部仿真/回放路径（feed / 咨询 double /
        // 确定性投递 double——均已移入 Simulation Host）
        var forbidden = new[]
        {
            "ScenarioStimulus", "ScenarioImporter", "ScenarioRunner",
            "SemanticDigest", "Oracle", "ScriptedUniAgent", "MinimalScenarioBundle",
            "V0Runtime", "ReplayPerception", "ServicePerception",
            "ServiceReplayFrameFeed", "ReplayFrameFeed", "SimulationHost",
        };
        var typeNames = typeof(HostRunner).Assembly
            .GetTypes()
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(typeNames.Where(name => forbidden.Any(f => name.Contains(f, StringComparison.Ordinal))));
    }
}
