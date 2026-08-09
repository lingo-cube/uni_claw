using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// 执行计划：为了完成 Goal 目前预计采取的执行结构（宪章 §13）。
/// Plan 是 hypothesis，不是 reality（I-5）——世界变化时可以偏离、修订或放弃计划；
/// 禁止「因为 Plan 里存在某步，所以默认现实一定存在对应目标」。
/// 步数据由调用侧注入（裁决 3 / 11），生产 Runtime 不硬编码场景字符串；
/// Plan 步数耗尽本身不构成完成判定（§43 / SC-P1-003）。
/// </summary>
/// <param name="Steps">计划步骤（不可变）。</param>
public sealed record Plan(ImmutableArray<PlanStep> Steps);

/// <summary>
/// 计划中的一步：目标与动作的语义描述（字段最小化 — 裁决 9：只保留断言消费的字段）。
/// 具体匹配 / 执行语义由调用侧数据与 Traversal 决定，本模型不解释描述内容。
/// </summary>
/// <param name="TargetDescription">该步目标的语义描述（调用侧注入的 target 数据）。</param>
/// <param name="ActionDescription">该步动作的语义描述（调用侧注入的 action 数据）。</param>
/// <param name="BranchEffectEvidenceEvaluator">SC-P3-CAND-005：可选的 bounded branch-effect criterion。
/// 仅在 fresh Observation 上确定性求值：true = effect proven；false = contradiction proven；
/// null = evidence insufficient。criterion 是 Plan hypothesis，不是 completion truth。</param>
public sealed record PlanStep(
    string TargetDescription,
    string ActionDescription,
    Func<Observation, bool?>? BranchEffectEvidenceEvaluator = null);
