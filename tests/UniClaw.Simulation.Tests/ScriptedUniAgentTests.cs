using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// ScriptedUniAgent（P25 deterministic double）单元行为锁（D21/D22 适配：
/// AgentScriptStep 携带 Steps 列表；D19 late-call 建模）。
/// </summary>
public sealed class ScriptedUniAgentTests
{
    private static AgentDecisionContext Context(string decisionId = "decision-1", string runId = "run-1") => new(
        decisionId, runId, "s1-v1", "make-wifi-switch-on",
        new HashSet<string> { "tap" },
        new Dictionary<string, string>(),
        Array.Empty<AgentObligationView>(),
        AgentDecisionPhase.InitialPlanning);

    private static AgentScriptStep TwoStepActScript() => new(
        AgentScriptKind.Act,
        new[]
        {
            new ScriptActionStep("toggle", null, "tap", "true"),
            new ScriptActionStep("menuItem", null, "tap", null),
        },
        "flip then open");

    [Fact]
    public void ActScript_EchoesCorrelation_AndAllSteps()
    {
        var agent = new ScriptedUniAgent(TwoStepActScript());
        var decision = agent.Consult(Context());
        var act = Assert.IsType<AgentDecision.Act>(decision);
        Assert.Equal("decision-1", act.Proposal.DecisionId);
        Assert.Equal(2, act.Proposal.Steps.Count);
        Assert.Equal("toggle", act.Proposal.Steps[0].TargetRole);
        Assert.Equal("tap", act.Proposal.Steps[0].EffectClass);
        Assert.Equal("true", act.Proposal.Steps[0].DesiredState);
        Assert.Equal("menuItem", act.Proposal.Steps[1].TargetRole);
        Assert.Null(act.Proposal.Steps[1].DesiredState);
        Assert.Equal("flip then open", act.Proposal.Justification);
        Assert.Empty(agent.Violations);
        agent.AssertDiscipline(1);
    }

    [Fact]
    public void NoActionNoResponseScripts_CarryEmptySteps()
    {
        var noAction = new ScriptedUniAgent(
            new AgentScriptStep(AgentScriptKind.NoAction, Array.Empty<ScriptActionStep>(), "switch already on"));
        var decision = Assert.IsType<AgentDecision.NoAction>(noAction.Consult(Context()));
        Assert.Equal("switch already on", decision.Proposal.Justification);

        var noResponse = new ScriptedUniAgent(
            new AgentScriptStep(AgentScriptKind.NoResponse, Array.Empty<ScriptActionStep>(), null));
        Assert.Null(noResponse.Consult(Context()));
        Assert.Empty(noResponse.Violations);
        noResponse.AssertDiscipline(1);
    }

    [Fact]
    public void SecondCall_RecordsDuplicateCallViolation_AndReturnsNull()
    {
        var agent = new ScriptedUniAgent(TwoStepActScript());
        Assert.NotNull(agent.Consult(Context()));
        Assert.Null(agent.Consult(Context()));
        Assert.Contains("duplicate-call", agent.Violations);
        Assert.Throws<InvalidOperationException>(() => agent.AssertDiscipline(1));
    }

    [Fact]
    public void NoResponseScript_ReturnsNull()
    {
        var agent = new ScriptedUniAgent(
            new AgentScriptStep(AgentScriptKind.NoResponse, Array.Empty<ScriptActionStep>(), null));
        Assert.Null(agent.Consult(Context()));
        Assert.Empty(agent.Violations);
        agent.AssertDiscipline(1);
    }

    [Fact]
    public void NoActionScript_EchoesCorrelation()
    {
        var agent = new ScriptedUniAgent(
            new AgentScriptStep(AgentScriptKind.NoAction, Array.Empty<ScriptActionStep>(), "switch already on"));
        var decision = agent.Consult(Context());
        var noAction = Assert.IsType<AgentDecision.NoAction>(decision);
        Assert.Equal("decision-1", noAction.Proposal.DecisionId);
        Assert.Equal("switch already on", noAction.Proposal.Justification);
        Assert.Empty(agent.Violations);
    }

    [Fact]
    public void MissingRunCorrelation_IsRecorded()
    {
        var agent = new ScriptedUniAgent(
            new AgentScriptStep(AgentScriptKind.NoAction, Array.Empty<ScriptActionStep>(), "j"));
        agent.Consult(Context(runId: ""));
        Assert.Contains("missing-run-correlation", agent.Violations);
        Assert.Throws<InvalidOperationException>(() => agent.AssertDiscipline(1));
    }

    [Fact]
    public void AssertDiscipline_ThrowsOnCountMismatch()
    {
        var agent = new ScriptedUniAgent(
            new AgentScriptStep(AgentScriptKind.NoResponse, Array.Empty<ScriptActionStep>(), null));
        agent.Consult(Context());
        Assert.Throws<InvalidOperationException>(() => agent.AssertDiscipline(2));
    }

    /// <summary>
    /// D19 late-call 建模：MarkTerminal 后的任何 Consult 记录 "late-call"
    /// 违规并返回 null（run terminal 后不存在合法 consultation 边界）。
    /// </summary>
    [Fact]
    public void ConsultAfterMarkTerminal_RecordsLateCallViolation_ReturnsNull()
    {
        var agent = new ScriptedUniAgent(TwoStepActScript());
        Assert.NotNull(agent.Consult(Context()));
        agent.MarkTerminal();

        Assert.Null(agent.Consult(Context(decisionId: "decision-abc123456789-1")));
        Assert.Contains("late-call", agent.Violations);
        Assert.Throws<InvalidOperationException>(() => agent.AssertDiscipline(1));
    }
}
