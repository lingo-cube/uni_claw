using UniClaw.Kernel.Runtime;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 / P25 deterministic UniAgent double。RUN-005 Slice C 相位感知
/// 重做（设计稿 §11/F9(b)：非「给旧脚本追加 Policy case」——决策核按
/// Phase/Progress/PolicyState 驱动的 turn 表求值；PolicyInvalidated 为
/// 合法再咨询相位）。两种脚本形态共用同一决策面：
/// <list type="bullet">
/// <item><b>相位感知脚本</b>（<see cref="PhaseAwareAgentScript"/>）：显式
/// （ExpectedPhase → Act/Policy/NoAction/Defer/NoResponse）turn 序列；
/// 相位失配/耗尽 = 纪律违规（fail closed）——模拟正式 Agent protocol 的
/// 完整决策面，无测试专用旁路。</item>
/// <item><b>legacy 脚本</b>（<see cref="AgentScriptStep"/>）：既有 golden
/// 场景兼容形态——首调回放脚本、StepVerified 相位自动 NoAction 兜底、
/// 其余二次调用 = duplicate-call 违规；语义与重做前逐字节一致（零回归）。</item>
/// </list>
/// 同时记录每次 consultation context 与消费纪律违规（unexpected-phase /
/// phase-mismatch / script-exhausted / duplicate-call / late-call /
/// missing-run-correlation），供 runner 在场景末尾 fail-closed 核对
/// （AssertDiscipline → DisciplineViolations）。
/// </summary>
internal sealed class ScriptedUniAgent
{
    /// <summary>
    /// 测试域 rogue predicate：closed AST 之外的派生节点。脚本携带未知谓词
    /// Kind 时经本类型映射入场——模拟外来/畸形 AST 走<b>正式 seam</b> 被
    /// V6a 入口拒绝（fail closed），不是 double 内建旁路。
    /// </summary>
    private sealed record RoguePredicate : PolicyPredicate;

    private readonly PhaseAwareAgentScript? _phaseScript;
    private readonly AgentScriptStep? _legacyScript;
    private readonly List<AgentDecisionContext> _calls = new();
    private readonly List<string> _violations = new();
    private bool _terminal;
    private int _turnIndex;

    public ScriptedUniAgent(AgentScriptStep script) => _legacyScript = script;

    public ScriptedUniAgent(PhaseAwareAgentScript script) => _phaseScript = script;

    /// <summary>每次 consultation 的 context（append-only）。</summary>
    public IReadOnlyList<AgentDecisionContext> Calls => _calls;

    /// <summary>消费纪律违规清单（不中断执行，场景末尾统一核对）。</summary>
    public IReadOnlyList<string> Violations => _violations;

    /// <summary>
    /// driver ConsultAgent seam 的 double 实现：机械入口纪律 + 脚本回放。
    /// 违规时记录并（duplicate-call / late-call / phase-mismatch /
    /// script-exhausted）返回 null；NoResponse 脚本显式返回 null。
    /// </summary>
    public AgentDecision? Consult(AgentDecisionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _calls.Add(ctx);

        if (_terminal)
        {
            // D19 late-call modeling：run terminal 后任何 consultation 都是违规
            _violations.Add("late-call");
            return null;
        }

        if (ctx.RunId.Length == 0)
            _violations.Add("missing-run-correlation");

        return _phaseScript is not null
            ? ConsultPhaseAware(ctx)
            : ConsultLegacy(ctx);
    }

    /// <summary>相位感知决策核：turn 表按到达相位匹配求值。</summary>
    private AgentDecision? ConsultPhaseAware(AgentDecisionContext ctx)
    {
        if (_turnIndex >= _phaseScript!.Turns.Count)
        {
            _violations.Add("script-exhausted");
            return null;
        }
        var turn = _phaseScript.Turns[_turnIndex];
        if (ctx.Phase != turn.ExpectedPhase)
        {
            _violations.Add($"phase-mismatch:got-{ctx.Phase}:expected-{turn.ExpectedPhase}");
            return null;
        }
        _turnIndex++;
        return turn.Kind switch
        {
            ScriptDecisionKind.Act => new AgentDecision.Act(new AgentActionProposal(
                ctx.DecisionId,
                turn.Steps.Select(s => new AgentActionStep(
                    s.TargetRole, s.TargetDescriptor, s.EffectClass, s.DesiredState)).ToList(),
                turn.Justification)),
            ScriptDecisionKind.Policy => new AgentDecision.Policy(
                ctx.DecisionId, BuildProposal(turn.Policy!)),
            ScriptDecisionKind.NoAction => new AgentDecision.NoAction(
                new AgentNoActionProposal(ctx.DecisionId, turn.Justification ?? "")),
            ScriptDecisionKind.Defer => new AgentDecision.Defer(
                ctx.DecisionId, new ObserveSpec(Subject: null, MaxRounds: turn.DeferMaxRounds ?? 1)),
            ScriptDecisionKind.NoResponse => null,
            _ => throw new InvalidOperationException($"未知 ScriptDecisionKind: {turn.Kind}"),
        };
    }

    /// <summary>脚本谓词 → closed AST；未知 Kind → rogue 节点（见 RoguePredicate）。</summary>
    private static PolicyPredicate BuildPredicate(ScriptPredicateSpec spec) => spec.Kind switch
    {
        "ClaimEquals" => new PolicyPredicate.ClaimEquals(spec.Subject, spec.Value ?? ""),
        "ClaimInSet" => new PolicyPredicate.ClaimInSet(spec.Subject, spec.Values ?? Array.Empty<string>()),
        _ => new RoguePredicate(),
    };

    private static PolicyProposal BuildProposal(ScriptPolicySpec spec) => new(
        spec.PolicyId,
        spec.Match.Select(BuildPredicate).ToList(),
        new PolicyActionTemplate(
            spec.Template.TargetRole, spec.Template.TargetDescriptor,
            spec.Template.EffectClass, spec.Template.DesiredState),
        spec.Termination.Select(BuildPredicate).ToList(),
        spec.Guards.Select(g => new PolicyGuard.ObservationUnchanged(g.Subject, g.AfterRounds)).ToList(),
        spec.MaxApplications,
        Justification: null);

    /// <summary>legacy 决策核（既有 golden 场景语义，原样保留）。</summary>
    private AgentDecision? ConsultLegacy(AgentDecisionContext ctx)
    {
        if (_calls.Count > 1)
        {
            // RUN-004 multi-turn：StepVerified 边界的再咨询 = 合法（脚本已跑完、
            // 目标应已达成）→ NoAction 让 TerminalEvaluation 如实判。
            // 其他 Phase 的第二次调用 = 非预期（duplicate-call 违规保持旧语义）。
            if (ctx.Phase == AgentDecisionPhase.StepVerified)
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    ctx.DecisionId, "script-exhausted-goal-should-be-met"));
            _violations.Add("duplicate-call");
            return null;
        }
        if (ctx.Phase != AgentDecisionPhase.InitialPlanning)
            _violations.Add("unexpected-phase");

        var script = _legacyScript!;
        return script.Kind switch
        {
            AgentScriptKind.Act => new AgentDecision.Act(new AgentActionProposal(
                ctx.DecisionId,
                script.Steps.Select(s => new AgentActionStep(
                    s.TargetRole, s.TargetDescriptor, s.EffectClass, s.DesiredState)).ToList(),
                script.Justification)),
            AgentScriptKind.NoAction => new AgentDecision.NoAction(
                new AgentNoActionProposal(ctx.DecisionId, script.Justification ?? "")),
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
