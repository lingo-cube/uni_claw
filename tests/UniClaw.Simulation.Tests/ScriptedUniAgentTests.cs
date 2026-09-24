using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// ScriptedUniAgent（SIM-004 纯 sequencer 形态）单元行为锁：authoring
/// fail closed、相位匹配求值、script 纪律（phase-mismatch / exhaustion /
/// unconsumed）、turn 游标推进。观测面（calls transcript / late-call /
/// count）归 ConsultationJournal——由 SeamOverrideTests 与场景测试承载。
/// Product 类型对齐（A1-A8）由 ProtocolAlignmentTests 专门承载。
/// </summary>
public sealed class ScriptedUniAgentTests
{
    private static AgentDecisionContext Context(
        AgentDecisionPhase phase,
        string decisionId = "decision-run0000000-1",
        string runId = "run-1") => new(
        decisionId, runId, "s1-v1", "make-wifi-switch-on",
        new HashSet<string> { "tap" },
        new Dictionary<string, ClaimSummary>(),
        Array.Empty<AgentObligationView>(),
        phase);

    private static AgentActionStep[] Steps(params (string Role, string? Desired)[] steps) =>
        steps.Select(s => new AgentActionStep(s.Role, null, "tap", s.Desired)).ToArray();

    [Fact]
    public void MalformedTurns_FailClosedAtConstruction()
    {
        // NoResponse 不得携带载荷
        Assert.Throws<InvalidOperationException>(() => new ScriptedUniAgent(
            new PhaseAwareAgentScript(new[]
            {
                ScriptedTurn.NoResponseAt(AgentDecisionPhase.InitialPlanning)
                    with { Act = Steps(("toggle", "true")) },
            })));
        // Act / Defer / Policy 互斥
        Assert.Throws<InvalidOperationException>(() => new ScriptedUniAgent(
            new PhaseAwareAgentScript(new[]
            {
                ScriptedTurn.ActAt(AgentDecisionPhase.InitialPlanning, Steps(("toggle", "true")))
                    with { Defer = new ObserveSpec(null, 1) },
            })));
        // Act 必须携带至少一步
        Assert.Throws<InvalidOperationException>(() => new ScriptedUniAgent(
            new PhaseAwareAgentScript(new[]
            {
                ScriptedTurn.ActAt(AgentDecisionPhase.InitialPlanning, Array.Empty<AgentActionStep>()),
            })));
        // Completion 只属于 NoAction turn
        Assert.Throws<InvalidOperationException>(() => new ScriptedUniAgent(
            new PhaseAwareAgentScript(new[]
            {
                ScriptedTurn.NoActionAt(AgentDecisionPhase.InitialPlanning, "j",
                    new CompletionEvidence("basis", new[] { "check" }))
                    with { Defer = new ObserveSpec(null, 1) },
            })));
    }

    [Fact]
    public void PhaseMismatch_FailClosed_ViolationRecorded()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.ActAt(AgentDecisionPhase.InitialPlanning, Steps(("toggle", "true"))),
            ScriptedTurn.NoActionAt(AgentDecisionPhase.PolicyInvalidated, "handled"),
        }));

        Assert.NotNull(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        // 第二咨询到达相位失配脚本 turn（StepRejected ≠ PolicyInvalidated）
        Assert.Null(agent.Consult(Context(AgentDecisionPhase.StepRejected)));
        Assert.Contains(agent.ScriptViolations,
            v => v.StartsWith("phase-mismatch:got-StepRejected", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtraConsultation_BeyondScript_FailClosed()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.NoActionAt(AgentDecisionPhase.InitialPlanning, "done"),
        }));

        Assert.NotNull(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        Assert.Null(agent.Consult(Context(AgentDecisionPhase.StepVerified)));
        Assert.Contains("script-exhausted", agent.ScriptViolations);
    }

    [Fact]
    public void UnconsumedTurns_ExplicitlyReported()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.ActAt(AgentDecisionPhase.InitialPlanning, Steps(("toggle", "true"))),
            ScriptedTurn.NoActionAt(AgentDecisionPhase.StepVerified, "script-exhausted-goal-should-be-met"),
            ScriptedTurn.NoResponseAt(AgentDecisionPhase.PolicyInvalidated),
        }));

        // 只消费 1/3 turn
        Assert.NotNull(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));

        Assert.Equal(new[] { "script-unconsumed-turns:2" }, agent.UnconsumedTurns());
    }

    [Fact]
    public void TurnCursor_AdvancesPerConsumedTurn_InOrder()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.ActAt(AgentDecisionPhase.InitialPlanning, Steps(("toggle", "true"))),
            ScriptedTurn.NoActionAt(AgentDecisionPhase.PolicyInvalidated, "handled"),
            ScriptedTurn.NoResponseAt(AgentDecisionPhase.StepRejected),
        }));

        Assert.IsType<AgentDecision.Act>(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        Assert.IsType<AgentDecision.NoAction>(agent.Consult(Context(AgentDecisionPhase.PolicyInvalidated)));
        Assert.Null(agent.Consult(Context(AgentDecisionPhase.StepRejected))); // NoResponse turn（显式）
        Assert.Empty(agent.ScriptViolations);                                 // 3/3 按序消费
        Assert.Empty(agent.UnconsumedTurns());
    }
}
