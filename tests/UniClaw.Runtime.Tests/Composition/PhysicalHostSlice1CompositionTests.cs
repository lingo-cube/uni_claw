using System.Collections.Immutable;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.PhysicalHost;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;

namespace UniClaw.Runtime.Tests.Composition;

/// <summary>
/// Slice 1 组合地基（REALITY_COMPOSITION_FOUNDATION）Tier 1 测试 — 无 emulator 依赖。
/// 覆盖 tasks 2.5（组合根在注入替身下可构造）、2.6（F1 架构断言）、
/// 3.2（Startup.AttachAsync 落地 — F2 强制点）、3.1（Traversal 异步 seam）。
/// F2 强制点：Attach 是 Startup §19 step 1，先于任何 LaunchApp / Traversal 执行 —
/// 失败必须零动作分发、零观测。
/// </summary>
public class PhysicalHostSlice1CompositionTests
{
    private const string TargetApp = "com.android.settings";

    private static readonly PhysicalHostOptions TestOptions =
        new("adb", null, TargetApp, "/tmp/uniclaw-vision.sock", 1080, 1920);

    // ── 3.2 Startup.AttachAsync 落地 — F2 ──────────────────────────────────────

    [Fact]
    public async Task F2_Startup_FailingAttach_ReturnsNotReady_ZeroDispatch_ZeroObservation()
    {
        var env = NewSettingsEnvironment();

        var startup = new RuntimeStartup(
            env, TargetApp, _ => "Settings",
            attach: _ => Task.FromResult<string?>("no physical device ready"));

        var result = await startup.StartAsync(CancellationToken.None);

        var notReady = Assert.IsType<StartupResult.NotReady>(result);
        Assert.Contains("设备预检失败", notReady.Reason);
        Assert.Contains("no physical device ready", notReady.Reason);
        Assert.Empty(env.ActionHistory);     // Launch 未被分发（step 1 先于 step 2）
        Assert.Empty(env.ObservationHistory); // 无观测 = 无 Traversal 执行
    }

    [Fact]
    public async Task F2_Startup_CancelledAttach_DoesNotDispatch()
    {
        var env = NewSettingsEnvironment();
        var startup = new RuntimeStartup(
            env, TargetApp, _ => "Settings",
            attach: ct => throw new OperationCanceledException(ct));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => startup.StartAsync(new CancellationToken(canceled: true)));

        Assert.Empty(env.ActionHistory);
    }

    // ── 3.2 向后兼容：null attach = Phase 1 no-op（§33 行为不变）────────────────

    [Fact]
    public async Task Startup_NullAttach_PreservesPhase1Behavior()
    {
        var env = new ScriptedEnvironment(
            "Launcher",
            launchNextScreenName: "Settings",
            screens:
            [
                new ScreenConfig("Launcher", "com.android.launcher", []),
                new ScreenConfig("Settings", TargetApp, []),
            ]);

        var startup = new RuntimeStartup(env, TargetApp, _ => "Settings");

        var result = await startup.StartAsync(CancellationToken.None);

        var ready = Assert.IsType<StartupResult.Ready>(result);
        Assert.Equal(TargetApp, ready.Anchor.ApplicationIdentity);
        Assert.Equal("Settings", ready.Anchor.ExpectedSemanticEntry);
        Assert.Single(env.ActionHistory);
        Assert.IsType<DeviceAction.LaunchApp>(env.ActionHistory[0]);
    }

    // ── 2.5 组合根：设备不可用路径 + 注入替身可构造 + 真实 Provider 组合 ─────────

    [Fact]
    public async Task HostComposition_MissingAdbExecutable_ReturnsNotReady()
    {
        var options = TestOptions with { AdbExecutable = "definitely-missing-adb-binary" };

        var resolution = await PhysicalHostComposition.ResolveDeviceAsync(options, CancellationToken.None);

        Assert.False(resolution.IsResolved);
        Assert.NotNull(resolution.FailureReason);
    }

    [Fact]
    public void F1_BuildRealEnvironment_ConstructsRealProvidersOnly()
    {
        // Provider ctor 无设备 I/O（懒连接）— 无需 device 即可证明生产组合构造的是真实 Provider。
        var environment = PhysicalHostComposition.BuildRealEnvironment(TestOptions, "emulator-5554");

        Assert.IsType<PhysicalEnvironment>(environment);
    }

    [Fact]
    public async Task Slice1Proof_OnSubstituteEnvironment_TerminatesAtBindingBoundary_ZeroCapabilityExecution()
    {
        // 组合根在注入替身（Fake 环境）下可构造 — tasks 2.5；生产 Program 传入的是 Real Environment。
        var env = new ScriptedEnvironment(
            "Launcher",
            launchNextScreenName: "Settings",
            screens:
            [
                new ScreenConfig("Launcher", "com.android.launcher", []),
                new ScreenConfig("Settings", TargetApp,
                [
                    new ElementConfig("Wi-Fi", SwitchState: false, Transition: null),
                ]),
            ]);

        var graph = PhysicalHostComposition.BuildRuntimeGraph(env, TestOptions, attach: null);

        var probe = SemanticObject.Define("Slice1Probe", "Slice1ProbeCategory", ["Enabled"]);
        var goal = new SemanticGoalInput("Slice1Probe", "Enabled", DesiredValue: true);
        var result = await graph.Agent.RunSemanticGoalAsync(
            goal, [probe], [], runId: "t1-slice1-proof", CancellationToken.None);

        // 证明 B 终止点：完整生命周期（Ready → Fresh Observe → Initial WorldBelief）后
        // 确定性终止于语义决策边界 — BindingUnresolved，不进入能力选择/分发。
        var binding = Assert.IsType<SemanticRunResult.BindingUnresolved>(result);
        Assert.Contains("Slice1Probe", binding.Reason);
        Assert.Equal(RunState.Failed, graph.Agent.State);
        Assert.NotNull(graph.Agent.RecoveryAnchor);
        Assert.Equal("Settings", graph.Agent.Belief?.SemanticPage);
        Assert.True(graph.Agent.Trace.Any(t => t.RunState == RunState.Running));

        // 证明 C：零能力执行 — ActionHistory 只含 Startup 的 LaunchApp，无 SetSwitch/Tap/Scroll。
        Assert.Single(env.ActionHistory);
        Assert.IsType<DeviceAction.LaunchApp>(env.ActionHistory[0]);
        Assert.DoesNotContain(env.ActionHistory, a => a is DeviceAction.SetSwitch or DeviceAction.Tap or DeviceAction.ScrollForward);
    }

    // ── 3.1 Traversal 异步 seam ────────────────────────────────────────────────
    // ExecuteLoweredActionAsync 是 internal（Runtime 内部 seam，无 InternalsVisibleTo）：
    // 经 Agent 语义闭环（await）消费 — 由既有 PEG10 全闭环测试（PhysicalEnvironmentCompositionTests）
    // 与下方 Slice1Proof_OnSubstituteEnvironment 的 Tier 0 回归覆盖；此处不再重复直达调用。

    // ── 2.6 F1 架构断言（机械 Guard 风格 — 与 ArchitectureGuardTests 同构）─────

    [Fact]
    public void F1_ProductionHostSources_DoNotReferenceFakeEnvironments()
    {
        var hostSourceDir = RepoRootPath("src/UniClaw.Runtime.PhysicalHost");

        foreach (var file in Directory.EnumerateFiles(hostSourceDir, "*.cs", SearchOption.AllDirectories))
        {
            // 忽略注释（/// 文档 / // 行注释 / /* 块注释）— 只检查代码引用。
            var content = StripComments(File.ReadAllText(file));
            Assert.DoesNotContain("ScriptedEnvironment", content);
            Assert.DoesNotContain("ReplayEnvironment", content);
            Assert.DoesNotContain("SimulationEnvironment", content);
        }
    }

    [Fact]
    public void F1_RuntimeCore_HasNoEnvironmentSelectionFlag()
    {
        var runtimeSourceDir = RepoRootPath("src/UniClaw.Runtime");

        foreach (var file in Directory.EnumerateFiles(runtimeSourceDir, "*.cs", SearchOption.AllDirectories))
        {
            // 忽略注释（IEnvironment 端口文档会描述「测试侧 ScriptedEnvironment Fake」边界）。
            var content = StripComments(File.ReadAllText(file));
            // Runtime core 不得知道生产环境实现；不得出现环境选择开关/标志。
            Assert.DoesNotContain("PhysicalEnvironment", content);
            Assert.DoesNotContain("ScriptedEnvironment", content);
            Assert.DoesNotContain("UsePhysicalEnvironment", content);
            Assert.DoesNotContain("EnvironmentSelection", content);
        }
    }

    [Fact]
    public void F1_HostCsproj_ReferencesOnlyRuntimeAndAdapters()
    {
        var content = File.ReadAllText(RepoRootPath("src/UniClaw.Runtime.PhysicalHost/UniClaw.Runtime.PhysicalHost.csproj"));

        Assert.Contains("UniClaw.Runtime.csproj", content);
        Assert.Contains("UniClaw.Runtime.Adapters.csproj", content);
        Assert.DoesNotContain("UniClaw.Runtime.Harness", content);
        Assert.DoesNotContain("UniClaw.Vision.Host", content);
        // 恰好两个 ProjectReference
        Assert.Equal(2, CountOccurrences(content, "<ProjectReference"));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ScriptedEnvironment NewSettingsEnvironment()
        => new("Settings", launchNextScreenName: null, screens: [new ScreenConfig("Settings", TargetApp, [])]);

    /// <summary>
    /// 移除注释后返回代码文本：/// 文档 / // 行注释整行剔除，/* 块注释截断。
    /// 保证 F1 机械断言只检查代码引用，不因边界文档（如 IEnvironment 端口注释）误报。
    /// 简化实现：不处理字符串字面量内嵌的 "//" — 本项目源码无此形状，Guard 足够机械。
    /// </summary>
    private static string StripComments(string source)
        => string.Join(
            '\n',
            source.Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Select(line =>
                {
                    var blockStart = line.IndexOf("/*", StringComparison.Ordinal);
                    return blockStart >= 0 ? line[..blockStart] : line;
                }));

    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string RepoRootPath(string relative)
        => Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("无法定位仓库根（AGENTS.md 未找到）。");
    }
}
