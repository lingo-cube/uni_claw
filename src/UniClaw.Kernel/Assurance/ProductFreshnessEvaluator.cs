namespace UniClaw.Kernel.Assurance;

/// <summary>
/// FRS-008 — <see cref="IFreshnessEvaluator"/> 的第一个产品实现
/// （FRS-007 缝；HOST-001 D7 前置）。规则（最小 revision-agnostic 语义）：
/// Freshness basis 的 <c>AsOf</c> 缺失（default）→ <c>Unknown</c>（判定
/// 输入不足）；<c>clock() − AsOf ≤ window</c> → <c>Sufficient</c>；否则
/// <c>Insufficient</c>。三态不折叠、fail-closed（缝文档 FRS-007 D3）。
/// 时间权威不在本类型：clock 由组合根注入（仓库先例 = effect driver /
/// screenshot acquisition 的 <c>Func&lt;DateTimeOffset&gt;? clock</c> 构造
/// 注入）；确定性口径 = 同输入 × 同 clock ⇒ 同结果（缝约束「同输入必须
/// 同结果」按注入时钟成立——deterministic profile 冻结时钟即全确定）。
/// 负龄（AsOf 晚于 clock）按 ≤ window 归 Sufficient（宽松方向，注释存证）。
/// Requirement 内容本版不参与判定；requirement-scoped 规则 = 未来 buyer。
/// </summary>
public sealed class ProductFreshnessEvaluator : IFreshnessEvaluator
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _window;

    /// <summary>
    /// clock 为 null 或 window 为负 = composition/configuration error
    /// （缝文档：未配置 ≠ runtime Unknown，ctor fail-fast）。
    /// </summary>
    public ProductFreshnessEvaluator(Func<DateTimeOffset> clock, TimeSpan window)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (window < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), "window must be non-negative");
        _window = window;
    }

    public FreshnessJudgment Evaluate(FreshnessEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.FreshnessBasis.AsOf == default)
            return new FreshnessJudgment(FreshnessSufficiency.Unknown, "freshness-basis-missing-asof");

        var age = _clock() - input.FreshnessBasis.AsOf;
        return age <= _window
            ? new FreshnessJudgment(
                FreshnessSufficiency.Sufficient,
                $"age:{Format(age)}<=window:{Format(_window)}")
            : new FreshnessJudgment(
                FreshnessSufficiency.Insufficient,
                $"age:{Format(age)}>window:{Format(_window)}");
    }

    private static string Format(TimeSpan span)
        => $"{Math.Floor(span.TotalMilliseconds)}ms";
}
