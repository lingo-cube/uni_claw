using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B9 共享 Plan 构造（测试侧；步数据来自 scenario-catalog Sequence 段 — 裁决 3 / 11：
/// 场景字符串属测试注入数据，生产 Runtime 不硬编码）。
/// Plan 是 hypothesis 不是 reality（I-5）；步数据由调用侧注入，Runtime 不解释其语义内容。
/// </summary>
public static class ScenarioPlans
{
    /// <summary>SC-P1-001 / 003 / 005（happy / switch-stuck / same-text）：3 步导航 + 开关序列。</summary>
    /// <returns>WifiEnable 完整计划。</returns>
    public static Plan WifiEnableSequence() => new([
        new PlanStep("Network & Internet", "Tap"),
        new PlanStep("WiFi", "Tap"),
        new PlanStep("WiFi", "SetSwitch true"),
    ]);

    /// <summary>SC-P1-004（missing-target）：2 步（第 2 步目标 "WiFi" 在当前屏幕无候选 → Traversal.Select 失败）。</summary>
    /// <returns>仅导航的计划（缺最终开关步）。</returns>
    public static Plan WifiNavigationOnly() => new([
        new PlanStep("Network & Internet", "Tap"),
        new PlanStep("WiFi", "Tap"),
    ]);

    /// <summary>SC-P3-001：单步非幂等 Tap；TimedOut 后不得盲目重派，世界证据决定 GoalEvidence。</summary>
    /// <returns>只含一次 Tap 的 uncertain-action 计划。</returns>
    public static Plan UncertainNetworkTransition() => new([
        new PlanStep("Network & Internet", "Tap"),
    ]);

    /// <summary>SC-P1-002（startup-fg-fail）：空 Plan（Startup 失败，Plan 永不执行）。</summary>
    /// <returns>空计划。</returns>
    public static Plan Empty() => new([]);
}
