namespace UniClaw.Agent.Goal;

/// <summary>
/// Primary Goal — 用户希望现实世界达到的结果（Target §3.2；GEV-004 最小
/// 落地）。由 UniAgent 侧构造（本片由测试脚本 authoring），携带显式
/// satisfaction criteria：Required 支配、Preferred 可空（纯二值 goal 合法）。
/// vacuous guard：Required 为空 → 构造拒绝（GEV-004 D4，authoring 点失败
/// 优于评价点降级）。Goal revision（显式澄清链）不在当前语义内。
/// _Avoid_: task list、plan、objective（contract 的）。
/// </summary>
public sealed record PrimaryGoal
{
    /// <summary>Goal 语义身份（评价记录与 EvaluationId 溯源用）。</summary>
    public string GoalId { get; }

    /// <summary>用户意图陈述；仅为可读性存在，不进任何判定。</summary>
    public string Statement { get; }

    /// <summary>支配档判据（全满足是 Satisfied 的必要条件）。</summary>
    public IReadOnlyList<GoalCriterion> Required { get; }

    /// <summary>期望档判据（可空；未满足降档为 PartiallySatisfied）。</summary>
    public IReadOnlyList<GoalCriterion> Preferred { get; }

    /// <summary>构造即校验（vacuous guard 见类注释；GEV-004 D4）。</summary>
    public PrimaryGoal(
        string goalId,
        string statement,
        IReadOnlyList<GoalCriterion> required,
        IReadOnlyList<GoalCriterion> preferred)
    {
        if (string.IsNullOrWhiteSpace(goalId))
            throw new ArgumentException("GoalId 不得为空", nameof(goalId));
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(preferred);
        if (required.Count == 0)
            throw new ArgumentException(
                "vacuous goal：至少一条 required criterion（GEV-004 D4——无 required 的评价只能空转）",
                nameof(required));
        if (required.Concat(preferred).Any(c => c is null))
            throw new ArgumentException("criterion 不得为 null", nameof(required));

        GoalId = goalId;
        Statement = statement;
        // 防御性拷贝（REVIEW F1）：切断调用方可变 List 的别名逃逸——否则
        // 构造后突变 criterion 列表会在 EvaluationId 不变的前提下击穿
        // 幂等（GEV-004 验收 11 / D8）。
        Required = Array.AsReadOnly(required.ToArray());
        Preferred = Array.AsReadOnly(preferred.ToArray());
    }
}
