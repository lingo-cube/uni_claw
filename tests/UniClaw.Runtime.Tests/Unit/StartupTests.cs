using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
// 注：命名空间 UniClaw.Runtime.Startup 与类 Startup 同名——本测试位于 UniClaw.Runtime 之下，
// 裸名 Startup 会先绑定到命名空间（CS0118），故用类型别名引用类。
using StartupProgram = UniClaw.Runtime.Startup.Startup;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// B4 Startup 程序机制测试（run-lifecycle SHALL §19 顺序 / SC-P1-001 / SC-P1-002）：
/// happy → Ready(anchor 三字段) + action history 仅 [LaunchApp] + ForegroundApplication 验证通过；
/// startup-fg-fail → NotReady(显式原因) + 无 anchor + 无进一步动作；§19 调用顺序可验证。
/// </summary>
public class StartupTests
{
    private const string TargetApplication = "Settings";

    [Fact]
    public async Task HappyVariant_Startup_ReadyWithThreeFieldAnchor_AndLaunchOnlyHistory()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        var startup = new StartupProgram(env, TargetApplication, ResolveSettingsMain);

        var result = await startup.StartAsync(CancellationToken.None);

        var ready = Assert.IsType<StartupResult.Ready>(result);
        Assert.Equal(TargetApplication, ready.Anchor.ApplicationIdentity);
        Assert.Equal("SettingsMain", ready.Anchor.ExpectedSemanticEntry);
        Assert.False(string.IsNullOrWhiteSpace(ready.Anchor.VerificationCriteria));
        Assert.Equal(new DeviceAction.LaunchApp(TargetApplication), Assert.Single(env.ActionHistory));
    }

    [Fact]
    public async Task StartupForegroundFailVariant_Startup_NotReadyExplicitReason_NoAnchor_NoFurtherActions()
    {
        var env = ScriptedEnvironmentVariants.StartupForegroundFail();
        var startup = new StartupProgram(env, TargetApplication, ResolveSettingsMain);

        var result = await startup.StartAsync(CancellationToken.None);

        var notReady = Assert.IsType<StartupResult.NotReady>(result);
        Assert.False(string.IsNullOrWhiteSpace(notReady.Reason));
        Assert.Contains("Launcher", notReady.Reason, StringComparison.Ordinal); // 显式原因包含观测到的实际前台
        Assert.Equal(new DeviceAction.LaunchApp(TargetApplication), Assert.Single(env.ActionHistory)); // 无恢复动作
    }

    [Fact]
    public async Task HappyVariant_Startup_ExecutesLaunchBeforeObserve_ExactlyOnceEach()
    {
        var recording = new RecordingEnvironment(ScriptedEnvironmentVariants.Happy());
        var startup = new StartupProgram(recording, TargetApplication, ResolveSettingsMain);

        var result = await startup.StartAsync(CancellationToken.None);

        Assert.IsType<StartupResult.Ready>(result);
        // §19 顺序：Attach(no-op) → Launch → Observe（无多余 Observe / 动作）
        Assert.Equal(new[] { "Execute:LaunchApp", "Observe" }, recording.Calls);
    }

    [Fact]
    public async Task StartupForegroundFailVariant_NoFurtherCallsAfterVerificationFailure()
    {
        var recording = new RecordingEnvironment(ScriptedEnvironmentVariants.StartupForegroundFail());
        var startup = new StartupProgram(recording, TargetApplication, ResolveSettingsMain);

        await startup.StartAsync(CancellationToken.None);

        // Verify 失败后无任何进一步调用（无重新 Observe / 重试 / 恢复动作 — SC-P1-002）
        Assert.Equal(new[] { "Execute:LaunchApp", "Observe" }, recording.Calls);
    }

    [Fact]
    public async Task SemanticResolutionFailure_NotReadyWithExplicitReason_NoAnchor()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        var startup = new StartupProgram(env, TargetApplication, _ => null);

        var result = await startup.StartAsync(CancellationToken.None);

        var notReady = Assert.IsType<StartupResult.NotReady>(result);
        Assert.False(string.IsNullOrWhiteSpace(notReady.Reason));
        Assert.Single(env.ActionHistory); // 无进一步动作
    }

    private static string? ResolveSettingsMain(Observation observation)
        => observation.ForegroundApplication == TargetApplication ? "SettingsMain" : null;

    /// <summary>调用顺序记录包装（B4「§19 step order verifiable」的观察面；测试专用，非生产类型）。</summary>
    private sealed class RecordingEnvironment : IEnvironment
    {
        private readonly IEnvironment _inner;

        public RecordingEnvironment(IEnvironment inner) => _inner = inner;

        /// <summary>按调用顺序记录的外部调用（"Execute:&lt;动作类型&gt;" / "Observe"）。</summary>
        public List<string> Calls { get; } = [];

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            Calls.Add("Observe");
            return await _inner.ObserveAsync(cancellationToken);
        }

        public async Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            Calls.Add($"Execute:{action.GetType().Name}");
            return await _inner.ExecuteAsync(action, cancellationToken);
        }
    }
}
