using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B9 共享 Goal 构造（测试侧；scenario-catalog Goal 段）。
/// 开关证据评估器（SC-P1-001 / SC-P1-003 正向）：证据必须来自 Observation（I-10）——
/// 仅当观测中出现 SwitchState == true 的元素时 Satisfied；dispatch 结果不构成完成判定（裁决 10）。
/// 「诚实 evaluator」（SC-P1-003 负向）与正向是同一谓词：switch-stuck 的负向结果由世界变体
/// （开关物理卡住）驱动，非 evaluator 差异。
/// 生产 Runtime 不硬编码场景字符串（裁决 11）；本文件是测试注入数据，可含场景字符串。
/// </summary>
public static class ScenarioGoals
{
    /// <summary>开关证据评估器（诚实）：仅当观测中出现 SwitchState == true 的元素时 Satisfied；证据引用观测序号（裁决 2）。</summary>
    /// <param name="observation">post-action Observation（SC-P1-003：每次动作后重新观察再评估）。</param>
    /// <returns>GoalEvidence（Satisfied / Reason / SourceObservationSequence）。</returns>
    public static GoalEvidence EvaluateWifiSwitchEvidence(Observation observation)
    {
        var switchElement = observation.Elements.FirstOrDefault(e => e.SwitchState is not null);
        var satisfied = switchElement is { SwitchState: true };
        return new GoalEvidence(
            satisfied,
            satisfied ? $"WiFi 开关已打开（观测 seq={observation.SequenceNumber}）。" : $"WiFi 开关未打开（观测 seq={observation.SequenceNumber}）。",
            observation.SequenceNumber);
    }

    /// <summary>Enable WiFi Goal + 记录式 evaluator：每次评估追加到 sink（断言可观察面 — SC-P1-001 断言 4 / SC-P1-003）。</summary>
    /// <param name="sink">评估序列接收列表（测试侧持有并断言）。</param>
    /// <returns>Goal（evaluator 写入 sink 并返回证据）。</returns>
    public static Goal EnableWifi(List<GoalEvidence> sink)
        => new(observation =>
        {
            var evidence = EvaluateWifiSwitchEvidence(observation);
            sink.Add(evidence);
            return evidence;
        });

    /// <summary>
    /// SC-P3-001 诚实 Goal evaluator：只从 post-action Observation 中的 NetworkSettings 元素证据
    /// 判断目标世界是否可见；不消费 ActionResult，也不把 TimedOut 当完成证据（I-4 / I-10）。
    /// </summary>
    /// <param name="sink">评估序列接收列表。</param>
    /// <returns>目标页 Observation evidence 驱动的 Goal。</returns>
    public static Goal ReachNetworkSettings(List<GoalEvidence> sink)
        => new(observation =>
        {
            var satisfied = observation.Elements.Any(element =>
                string.Equals(element.Text, "WiFi", StringComparison.Ordinal));
            var evidence = new GoalEvidence(
                satisfied,
                satisfied
                    ? $"目标页面可见（观测 seq={observation.SequenceNumber}）。"
                    : $"目标页面不可见（观测 seq={observation.SequenceNumber}）。",
                observation.SequenceNumber);
            sink.Add(evidence);
            return evidence;
        });

    /// <summary>最小 Goal：evaluator 永远不满足（SC-P1-002 startup-fg-fail 使用——Run 在评估前终止，evaluator 不可达）。</summary>
    /// <returns>永不满足的 Goal。</returns>
    public static Goal Minimal()
        => new(_ => new GoalEvidence(false, "证据评估不可达：Run 在评估前终止。", null));
}
