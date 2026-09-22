using UniClaw.Kernel.Runtime;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 / P25 deterministic UniAgent double：按 bundle 内 AgentScriptStep
/// 单射决策（不调用 live model）。同时记录每次 consultation context 与
/// 消费纪律违规（unexpected-phase / duplicate-call / missing-run-correlation /
/// late-call——MarkTerminal 后的任何 Consult），供 runner 在场景末尾
/// fail-closed 核对（AssertDiscipline → DisciplineViolations）。
/// </summary>
internal sealed class ScriptedUniAgent
{
    private readonly AgentScriptStep _script;
    private bool _terminal;
    private int _callCount;

    public ScriptedUniAgent(AgentScriptStep script) => _script = script;

    /// <summary>每次 consultation 的 context（append-only）。</summary>
    public IReadOnlyList<AgentDecisionContext> Calls => _calls;
    private readonly List<AgentDecisionContext> _calls = new();

    /// <summary>消费纪律违规清单（不中断执行，场景末尾统一核对）。</summary>
    public IReadOnlyList<string> Violations => _violations;
    private readonly List<string> _violations = new();

    /// <summary>
    /// driver ConsultAgent seam 的 double 实现：机械入口校验 + 脚本回放。
    /// 违规时记录并（duplicate-call / late-call）返回 null；NoResponse 脚本
    /// 显式返回 null。
    /// </summary>
    public AgentDecision? Consult(AgentDecisionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _calls.Add(ctx);
        _callCount++;

        // RUN-004 multi-turn：首次脚本回放，后续 NoAction（目标应已达成——
        // 由 TerminalEvaluation 如实判定；简单场景兼容）
        if (_callCount > 1)
        {
            return new AgentDecision.NoAction(new AgentNoActionProposal(
                ctx.DecisionId, "script-exhausted-goal-should-be-met"));
        }

        if (_terminal)
        {
            // D19 late-call modeling：run terminal 后任何 consultation 都是违规
            _violations.Add("late-call");
            return null;
        }
        if (ctx.Phase != AgentDecisionPhase.InitialPlanning)
            _violations.Add("unexpected-phase");
        if (_calls.Count > 1)
        {
            _violations.Add("duplicate-call");
            return null;
        }
        if (ctx.RunId.Length == 0)
            _violations.Add("missing-run-correlation");

        return _script.Kind switch
        {
            AgentScriptKind.Act => new AgentDecision.Act(new AgentActionProposal(
                ctx.DecisionId,
                _script.Steps.Select(s => new AgentActionStep(
                    s.TargetRole, s.TargetDescriptor, s.EffectClass, s.DesiredState)).ToList(),
                _script.Justification)),
            AgentScriptKind.NoAction => new AgentDecision.NoAction(
                new AgentNoActionProposal(ctx.DecisionId, _script.Justification ?? "")),
            AgentScriptKind.NoResponse => null,
            _ => null,
        };
    }

    /// <summary>
    /// D19：run terminal 标记（runner / Host.DriveOnce 在 terminal 后调用）。
    /// 之后的任何 Consult 记录 "late-call" 违规并返回 null。
    /// </summary>
    public void MarkTerminal() => _terminal = true;

    /// <summary>
    /// 场景末尾纪律核对（非抛出版）：违规 + 调用次数与期望不符 →
    /// 追加违规说明；runner 聚合进 AgentViolations 并强制
    /// AcceptancePassed=false（fail closed，不静默降级）。
    /// </summary>
    public IReadOnlyList<string> CheckDiscipline(int expectedCalls)
    {
        var violations = _violations.ToList();
        if (_calls.Count != expectedCalls)
            violations.Add($"consult-count-mismatch:expected-{expectedCalls}:got-{_calls.Count}");
        return violations;
    }

    /// <summary>抛出版（单元测试用）：存在任何纪律违规 → InvalidOperationException。</summary>
    public void AssertDiscipline(int expectedCalls)
    {
        var violations = CheckDiscipline(expectedCalls);
        if (violations.Count > 0)
            throw new InvalidOperationException(
                "ScriptedUniAgent 纪律违规: " + string.Join(", ", violations));
    }
}
