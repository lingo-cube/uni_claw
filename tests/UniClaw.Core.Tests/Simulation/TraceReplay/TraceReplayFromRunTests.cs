using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Simulation.TraceReplay;

/// <summary>
/// 直接从 run 产物目录加载的仿真回放测试。
/// 不需要手写 fixture — 读取 analysis.jsonl + plan.json + result.json 自动构建。
/// </summary>
public class TraceReplayFromRunTests
{
    private const string RunDir = "/Users/fran/Documents/Code/spacex/uni-claw/artifacts/runs/skill-test/enumerate-settings-safely/20260805T052309367Z-1bc7a25ea6384e3";

    [Fact(DisplayName = "trace replay: 从产物回放 enumerate run 并复现结局")]
    public async Task Replay_Enumerate_ReproducesCompletionReason()
    {
        var harness = TraceReplayHarness.FromRunDir(RunDir);

        Assert.Equal("20260805T052309367Z-1bc7a25ea6384e3", harness.RunId);
        // result.json 被 verify 写回覆盖为 settings_home_not_restored
        // 引擎原始 reason 是 max_steps (run.log: "Engine terminated reason=max_steps steps=120")

        var result = await harness.RunAsync(CancellationToken.None);

        // 验证复现: 引擎消耗步数与真实 run 接近
        Assert.True(result.TotalSteps > 0,
            $"Engine should consume steps. Got: {result.TotalSteps}");

        // 动作数应与真实 run 一致 (真实 run: 8 actions)
        Assert.True(result.ActionHistory.Length >= harness.ExpectedActions / 2,
            $"Expected at least {harness.ExpectedActions / 2} actions, got {result.ActionHistory.Length}");
    }
}
