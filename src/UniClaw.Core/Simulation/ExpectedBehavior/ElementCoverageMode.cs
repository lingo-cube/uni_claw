namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 元素覆盖严格度模式 (C-11 constitution schema 变更: requiredRatio → mode)。
/// 替代旧的 ratio 阈值语义, 把「怎么证明做了」从百分比改成精确 set-diff。
/// </summary>
public enum ElementCoverageMode
{
    /// <summary>
    /// 精确集合差: pass iff missed ⊆ AllowedMisses.Ids 且 extra = ∅。
    /// 用于完备遍历 (CompletionPolicy 非 TargetFound)。完备性证明的唯一权威模式。
    /// </summary>
    Exact,

    /// <summary>
    /// 过游走 guard (TargetFound 计划): 不做覆盖断言, 仅断言 target 命中后无新元素 tap。
    /// 本就该早停的计划, 要求 exact 会误判正确早停为失败。
    /// </summary>
    Subset,

    /// <summary>
    /// 过渡兼容: 保留旧 ratio 阈值语义 (RequiredRatio)。
    /// 仅用于尚未迁移的 JSON; 全量迁移完成后删除 (task 8.1)。
    /// </summary>
    LegacyRatio,
}
