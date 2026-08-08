using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B14 SC-P1-005 Same-text Element Disambiguation 正式场景测试
/// （scenarios/catalog.md SC-P1-005 — 同文本消歧：Text="WiFi" 同时以标题（SwitchState=null）与
/// 开关（SwitchState=false）出现时，grounding 必须选择 state-bearing 开关元素 — 裁决 3；
/// 不新增 coordinate / hierarchy model — 见 Guard 6）。
/// 测试名以 SC-P1-005 Assertion ID 为键（断言 1-3）。
/// 记录式 evaluator（B12 pattern）：捕获每个 post-action Observation，委托 EvaluateWifiSwitchEvidence
/// 并写入 harness.Evidence；经 harness.Agent.RunAsync(goal, plan, runId) 直接注入。
/// 断言从实际观测推导元素身份（不硬编码 0/1 — Index 观测间稳定，裁决 3）。
/// </summary>
public class SameTextElementDisambiguationTests
{
    // ── 断言 1：SetSwitch 的 TargetElementIndex == 开关元素 Index（≠ 标题元素 Index）───────────────────

    [Fact]
    public async Task Assertion1_TargetElementIndex_EqualsSwitchIndex_NotTitleIndex()
    {
        var harness = ScenarioHarness.Create("same-text");

        var run = await RunWithRecordingEvaluatorAsync(harness);

        // captured[1] = post-Tap("WiFi") 观测（WiFi Settings 屏：标题 + 开关两个 "WiFi" 元素）
        var (switchIndex, titleIndex) = IdentifySameTextElements(run.Captured[1]);
        // Step-3 SetSwitch 事件：TargetElementIndex 是 grounding 解析出的元素引用（DeviceAction.TargetElementIndex）
        var step3 = Assert.Single(run.Trace.Where(e => e.StepId == "Step-3"));
        var setSwitch = Assert.IsType<DeviceAction.SetSwitch>(step3.Action);
        Assert.Equal(switchIndex, setSwitch.TargetElementIndex); // 选中 state-bearing 开关元素（Traversal.Select 消歧）
        Assert.NotEqual(titleIndex, setSwitch.TargetElementIndex); // ≠ 标题元素
    }

    // ── 断言 2：post-action 观测 — 开关 true、标题仍 null；证据满足 → Completed ─────────────────────────

    [Fact]
    public async Task Assertion2_PostAction_SwitchTrue_TitleStillNull_Completed()
    {
        var harness = ScenarioHarness.Create("same-text");

        var run = await RunWithRecordingEvaluatorAsync(harness);

        // 用断言 1 的身份（同一观测推导 — Index 稳定）检查最终 post-action 观测
        var (switchIndex, titleIndex) = IdentifySameTextElements(run.Captured[1]);
        var finalObservation = run.Captured[^1];
        Assert.True(finalObservation.Elements[switchIndex].SwitchState == true, "开关元素在 SetSwitch(ON) 后必须为 true。");
        Assert.Null(finalObservation.Elements[titleIndex].SwitchState); // 标题元素仍是 null（未被误操作）
        // 最终证据满足（开关 true）→ Agent 判定 Completed（I-10 — 证据来自 Observation）
        Assert.True(harness.Evidence[^1].Satisfied);
        Assert.Equal(RunState.Completed, run.FinalState);
        Assert.Equal(RunState.Completed, harness.Agent.State);
    }

    // ── 断言 3（错误路径对照）：SetSwitch 作用于标题元素 → ActionResult.Rejected（环境不替 Runtime 消歧）──

    [Fact]
    public async Task Assertion3_ErrorPath_SetSwitchOnTitleElement_Rejected()
    {
        var harness = ScenarioHarness.Create("same-text");

        var run = await RunWithRecordingEvaluatorAsync(harness);

        var (_, titleIndex) = IdentifySameTextElements(run.Captured[1]);
        // 直接向环境分发作用于标题元素的 SetSwitch：物理能力语义 — 非开关承载元素 → Rejected
        //（Environment 按元素身份应用物理效果，不替 Runtime 决定选哪个元素 — §8 / SC-P1-005）
        var rejected = await harness.Environment.ExecuteAsync(
            new DeviceAction.SetSwitch(titleIndex, true), CancellationToken.None);
        Assert.Equal(ActionResultOutcome.Rejected, rejected.Outcome);
        Assert.Contains("非开关承载", rejected.Info!, StringComparison.Ordinal); // 物理能力原因（非越界等其它 Rejected）
    }

    /// <summary>从同文本观测中识别标题 / 开关元素身份（Text == "WiFi"；SwitchState 区分 — 裁决 3；Index 观测间稳定）。</summary>
    private static (int SwitchIndex, int TitleIndex) IdentifySameTextElements(Observation observation)
    {
        var wifiElements = observation.Elements
            .Select((element, index) => (Element: element, Index: index))
            .Where(x => string.Equals(x.Element.Text, "WiFi", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, wifiElements.Count); // 标题 + 开关两个同文本元素
        var switchElement = Assert.Single(wifiElements.Where(x => x.Element.SwitchState is not null));
        var titleElement = Assert.Single(wifiElements.Where(x => x.Element.SwitchState is null));
        return (switchElement.Index, titleElement.Index);
    }

    /// <summary>
    /// 记录式 evaluator 执行路径（B12 pattern）：捕获每个 post-action Observation 并写入 harness.Evidence，
    /// 证据评估委托 ScenarioGoals.EvaluateWifiSwitchEvidence；经 harness.Agent.RunAsync 直接注入。
    /// </summary>
    /// <param name="harness">已装配的 ScenarioHarness（Agent / Plan / RunId / Evidence 为执行与观察面）。</param>
    /// <returns>执行结果：最终 RunState + Trace 快照 + 捕获的 Observation 序列。</returns>
    private static async Task<RecordingRun> RunWithRecordingEvaluatorAsync(ScenarioHarness harness)
    {
        var captured = new List<Observation>();
        var goal = new Goal(observation =>
        {
            captured.Add(observation);
            var evidence = ScenarioGoals.EvaluateWifiSwitchEvidence(observation);
            harness.Evidence.Add(evidence);
            return evidence;
        });

        var finalState = await harness.Agent.RunAsync(goal, harness.Plan, harness.RunId, CancellationToken.None);
        return new RecordingRun(finalState, harness.Agent.Trace.ToArray(), captured.ToArray());
    }

    /// <summary>记录式执行的结果载体：最终状态 + Trace 快照 + evaluator 捕获的 Observation 序列。</summary>
    /// <param name="FinalState">Agent.RunAsync 返回的最终 RunState。</param>
    /// <param name="Trace">run 结束后的 Trace 快照（Agent.Trace 是活后备列表 — 必须在 run 后快照）。</param>
    /// <param name="Captured">evaluator 收到的 post-action Observation 序列（按评估顺序；captured[1] = WiFi Settings 屏）。</param>
    private sealed record RecordingRun(
        RunState FinalState,
        TraceEvent[] Trace,
        Observation[] Captured);
}
