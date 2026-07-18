namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 元素覆盖严格度模式 (C-11 schema: requiredRatio → mode)。
/// 把「怎么证明做了」从百分比改成精确 set-diff。
/// elementcoverage-mode-cleanup 后仅 2 值 (移除了过渡 legacy_ratio)。
/// </summary>
public enum ElementCoverageMode
{
    /// <summary>
    /// 精确集合差: pass iff missed ⊆ AllowedMisses.Ids 且 extra = ∅。
    /// 用于完备遍历 (CompletionPolicy 非 TargetFound)。完备性证明的唯一权威模式。
    /// 缺省值 (JSON 缺省 mode 时回落到此)。
    /// </summary>
    Exact,

    /// <summary>
    /// 过游走 guard (TargetFound 计划): 不做覆盖断言, 仅断言 target 命中后无新元素 tap。
    /// 本就该早停的计划, 要求 exact 会误判正确早停为失败。
    /// </summary>
    Subset,
}
