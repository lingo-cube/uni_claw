using UniClaw.Agent.Evaluation;
using UniClaw.Agent.Goal;
using UniClaw.Kernel.Outcome;

namespace UniClaw.Agent;

/// <summary>
/// UniAgent — L1 peer 首次落地（Target §3.1/§3.9；ADR-0008）。sole Goal
/// Evaluation Authority：消费 immutable RuntimeOutcome envelope，基于
/// Primary Goal 语义对最终 Goal satisfaction 作监督评价。只读消费：不回写
/// 任何执行事实（不变量 41）；只消费 envelope 暴露的稳定 outcome
/// semantics / refs，不 dereference raw runtime evidence / EffectReceipt /
/// WorldBelief 形成第二套 Runtime judgment（反例 D）。Evaluate 纯确定性
/// 幂等、零存储，不购买 evaluation history（GEV-004 D8）。
/// </summary>
public sealed class UniAgent
{
    /// <summary>ctor 注入监督对象（1 agent : 1 goal，无变更面）。</summary>
    public UniAgent(PrimaryGoal goal)
    {
        Goal = goal ?? throw new ArgumentNullException(nameof(goal));
    }

    /// <summary>监督对象：构造时固定。</summary>
    public PrimaryGoal Goal { get; }

    /// <summary>
    /// 对一个 terminal RuntimeOutcome envelope 作监督评价（§3.9 三元输入：
    /// goal 语义 + outcome + context）。判定格（GEV-004 D4/D5）：任一
    /// criterion 不可验证 → (Undetermined, NeedsFollowUp)；否则 Required
    /// 全满足 + Preferred 全满足 → Satisfied；Required 全满足 + ≥1
    /// Preferred 未满足 → PartiallySatisfied；任一 Required 未满足 →
    /// Unsatisfied（required 支配）。disposition 派生是 Empty-context
    /// policy（D6）：Satisfied→Final / PartiallySatisfied→NeedsFollowUp /
    /// Unsatisfied→Final。同输入 → 同 EvaluationId、同记录（幂等）；
    /// 无 envelope → fail-closed，不发明中途评价。
    /// </summary>
    public GoalEvaluation Evaluate(RuntimeOutcome outcome, GoalEvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        // opaque 第三输入（D7）：GEV-004 仅有 Empty 一个合法取值，不携带
        // 业务语义；未来 context buyer 到来时在此消费，签名不变。
        context ??= GoalEvaluationContext.Empty;

        var results = new List<CriterionResult>(Goal.Required.Count + Goal.Preferred.Count);
        foreach (var criterion in Goal.Required)
            results.Add(Judge(criterion, outcome));
        foreach (var criterion in Goal.Preferred)
            results.Add(Judge(criterion, outcome));

        var requiredView = results.Take(Goal.Required.Count).ToList();
        var preferredView = results.Skip(Goal.Required.Count).ToList();

        GoalSatisfaction satisfaction;
        EvaluationDisposition disposition;
        if (results.Any(r => r.Outcome == CriterionOutcome.Unverifiable))
        {
            // 判据不可验证 → 诚实出口（D5）；唯一长期不变量（D6）
            satisfaction = GoalSatisfaction.Undetermined;
            disposition = EvaluationDisposition.NeedsFollowUp;
        }
        else if (requiredView.Any(r => r.Outcome == CriterionOutcome.Unmet))
        {
            satisfaction = GoalSatisfaction.Unsatisfied;
            disposition = EvaluationDisposition.Final;            // GEV-004 policy
        }
        else if (preferredView.Any(r => r.Outcome == CriterionOutcome.Unmet))
        {
            satisfaction = GoalSatisfaction.PartiallySatisfied;
            disposition = EvaluationDisposition.NeedsFollowUp;    // GEV-004 policy
        }
        else
        {
            satisfaction = GoalSatisfaction.Satisfied;
            disposition = EvaluationDisposition.Final;            // GEV-004 policy
        }

        var evaluationId = $"goal:{Goal.GoalId}:run:{outcome.RunId}:proof:{outcome.OutcomeProofId}";
        var rationale = string.Join("; ", results.Select(r => $"{Label(r.Criterion)}={r.Outcome}"));
        return new GoalEvaluation(
            evaluationId, Goal.GoalId, outcome.RunId, outcome.OutcomeProofId,
            satisfaction, disposition, results, rationale);
    }

    /// <summary>
    /// criterion 语义身份 → envelope 稳定 outcome semantics 的解析
    /// （evaluator 私事；criterion 类型本身不知 envelope 形状）。
    /// id 缺席 = Unverifiable（≠ false，D5；反例 C）。
    /// </summary>
    private static CriterionResult Judge(GoalCriterion criterion, RuntimeOutcome outcome) => criterion switch
    {
        GoalCriterion.ClassificationIs expected => new CriterionResult(criterion,
            outcome.Classification == expected.Expected
                ? CriterionOutcome.Met
                : CriterionOutcome.Unmet),
        GoalCriterion.ObligationFulfilled obligation => new CriterionResult(criterion,
            outcome.Obligations.FirstOrDefault(o => o.ObligationId == obligation.ObligationId) is { } status
                ? (status.Satisfied ? CriterionOutcome.Met : CriterionOutcome.Unmet)
                : CriterionOutcome.Unverifiable),
        _ => throw new InvalidOperationException($"未知 criterion 种类：{criterion.GetType().Name}"),
    };

    private static string Label(GoalCriterion criterion) => criterion switch
    {
        GoalCriterion.ClassificationIs c => $"classification=={c.Expected}",
        GoalCriterion.ObligationFulfilled o => $"obligation[{o.ObligationId}]",
        _ => criterion.GetType().Name,
    };
}
