using UniClaw.Kernel.Assurance;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// FRS-007 测试替身：deterministic freshness evaluator（无时钟 / 无阈值，
/// 脚本化 sufficiency；Deferred ④/⑪ 不偷解）。
/// </summary>
internal static class FreshnessDoubles
{
    /// <summary>happy-path 默认替身：恒 Sufficient。</summary>
    public sealed class Satisfying : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "scripted:sufficient");
    }

    /// <summary>按 effect class 映射 sufficiency 的确定性替身（未命中走
    /// fallback，默认 Sufficient）——同输入同结果。</summary>
    public sealed class ByEffectClass(
        FreshnessSufficiency fallback = FreshnessSufficiency.Sufficient,
        params (string EffectClass, FreshnessSufficiency Outcome)[] rules) : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input)
        {
            foreach (var (effectClass, outcome) in rules)
                if (input.Requirement.EffectClass == effectClass)
                    return new(outcome, $"scripted:{outcome.ToString().ToLowerInvariant()}");
            return new(fallback, $"scripted:{fallback.ToString().ToLowerInvariant()}");
        }
    }
}
