using UniClaw.Kernel.Runtime;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// P25 deterministic UniAgent double（RFS-001；RUN-005 Slice C 相位感知决策核
/// 沿用；SIM-004 收敛为纯 multi-turn sequencer）。职责只有：
/// ordered consultation expectations（ExpectedPhase = Product 相位，失配
/// fail closed）+ deterministic response factory（载荷为 Product 类型，
/// DecisionId 以 ctx 回带——D2）+ script 纪律（exhaustion / unconsumed）。
/// 观测面（calls transcript / late-call / missing-run-correlation / count）
/// 已移居 seam 消费侧 <see cref="ConsultationJournal"/>（ABS-002）——本类型
/// 不再持有 Calls/MarkTerminal/CheckDiscipline，host 不再因 probe 依赖
/// 本 concrete（ABS-001）。legacy 隐藏 agent 策略（StepVerified→自动
/// NoAction、second-call→duplicate-call）已随 legacy AgentScriptStep 形态
/// 删除——没有脚本就没有决策，不得隐式帮场景补 NoAction。
/// </summary>
internal sealed class ScriptedUniAgent : IScriptedAgentProbe
{
    private readonly PhaseAwareAgentScript _script;
    private readonly List<string> _violations = new();
    private int _turnIndex;

    public ScriptedUniAgent(PhaseAwareAgentScript script)
    {
        _script = script ?? throw new ArgumentNullException(nameof(script));
        for (var index = 0; index < script.Turns.Count; index++)
            ValidateTurn(script.Turns[index], index);
    }

    /// <summary>script 纪律违规（phase-mismatch / script-exhausted；append-only）。</summary>
    public IReadOnlyList<string> ScriptViolations => _violations;

    /// <summary>
    /// driver ConsultAgent seam 的 double 实现：turn 表按到达相位匹配求值。
    /// 相位失配 / 脚本耗尽 = 纪律违规（fail closed，返回 null）；NoResponse
    /// turn 显式返回 null。
    /// </summary>
    public AgentDecision? Consult(AgentDecisionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (_turnIndex >= _script.Turns.Count)
        {
            _violations.Add("script-exhausted");
            return null;
        }
        var turn = _script.Turns[_turnIndex];
        if (ctx.Phase != turn.ExpectedPhase)
        {
            _violations.Add($"phase-mismatch:got-{ctx.Phase}:expected-{turn.ExpectedPhase}");
            return null;
        }
        _turnIndex++;
        return Respond(turn, ctx);
    }

    /// <summary>场景收尾核对：脚本 turn 未被全部消费 = 显式纪律事实（A8）。</summary>
    public IReadOnlyList<string> UnconsumedTurns() =>
        _turnIndex >= _script.Turns.Count
            ? Array.Empty<string>()
            : new[] { $"script-unconsumed-turns:{_script.Turns.Count - _turnIndex}" };

    /// <summary>
    /// presence 包装（唯一 Product-union 触点）：DecisionId 由 Kernel 铸造、
    /// double 以 ctx 回带（D2 防串话）——预铸 decision 无法携带，故脚本
    /// 载荷须在消费点包装。全空 Respond 载荷 = NoAction。
    /// </summary>
    private static AgentDecision? Respond(ScriptedTurn turn, AgentDecisionContext ctx) =>
        turn.Behavior == ScriptedDoubleBehavior.NoResponse
            ? null
            : turn.Act is { } steps
                ? new AgentDecision.Act(new AgentActionProposal(ctx.DecisionId, steps, turn.Justification))
                : turn.Policy is { } proposal
                    ? new AgentDecision.Policy(ctx.DecisionId, proposal)
                    : turn.Defer is { } spec
                        ? new AgentDecision.Defer(ctx.DecisionId, spec)
                        : new AgentDecision.NoAction(new AgentNoActionProposal(
                            ctx.DecisionId, turn.Justification ?? "", turn.Completion));

    /// <summary>authoring fail closed（组合期拒绝 malformed turn）。</summary>
    private static void ValidateTurn(ScriptedTurn turn, int index)
    {
        if (turn.Behavior == ScriptedDoubleBehavior.NoResponse)
        {
            if (turn.Act is not null || turn.Completion is not null
                || turn.Defer is not null || turn.Policy is not null)
                throw new InvalidOperationException(
                    $"scripted-turn-invalid:turn[{index}]:no-response-cannot-carry-payload");
            return;
        }
        var payloadCount = new object?[] { turn.Act, turn.Defer, turn.Policy }
            .Count(payload => payload is not null);
        if (payloadCount > 1)
            throw new InvalidOperationException(
                $"scripted-turn-invalid:turn[{index}]:act-defer-policy-mutually-exclusive");
        if (turn.Act is { Count: 0 })
            throw new InvalidOperationException(
                $"scripted-turn-invalid:turn[{index}]:act-requires-steps");
        if (turn.Completion is not null && payloadCount != 0)
            throw new InvalidOperationException(
                $"scripted-turn-invalid:turn[{index}]:completion-belongs-to-no-action");
    }
}

/// <summary>
/// SIM-004 Step 3：scripted double 的 sim-only 观测面（test-only probe
/// interface）。Product contract / Kernel 零感知；real DSH realization 无需
/// 实现本接口——注入非 scripted realization 时 host 的对应属性为 null，
/// 报告纪律只余 seam 级（ConsultationJournal）。
/// </summary>
internal interface IScriptedAgentProbe
{
    /// <summary>script 纪律违规（phase-mismatch / script-exhausted）。</summary>
    IReadOnlyList<string> ScriptViolations { get; }

    /// <summary>场景收尾时的未消费 turn 显式纪律事实。</summary>
    IReadOnlyList<string> UnconsumedTurns();
}
