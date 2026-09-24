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
        new Dictionary<string, ClaimSummary>(),
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

// ==== RUN-005 Slice C：相位感知脚本（正式 Agent protocol 决策面）===============

/// <summary>
/// 相位感知决策核行为锁：turn 相位匹配、Policy proposal 构建、rogue 谓词
/// 映射、Defer、相位失配/耗尽 fail closed。legacy 形态行为由上方既有测试
/// 锁定（零回归）。
/// </summary>
public sealed class ScriptedUniAgentPhaseAwareTests
{
    private static AgentDecisionContext Context(
        AgentDecisionPhase phase,
        string decisionId = "decision-run0000000-1",
        string runId = "run-1") => new(
        decisionId, runId, "policy-v1", "cool-to-20",
        new HashSet<string> { "tap" },
        new Dictionary<string, ClaimSummary>(),
        Array.Empty<AgentObligationView>(),
        phase);

    private static ScriptedTurn PolicyTurnAt(AgentDecisionPhase phase) => new(
        phase, ScriptDecisionKind.Policy,
        Array.Empty<ScriptActionStep>(),
        new ScriptPolicySpec(
            "pol-1",
            new[] { new ScriptPredicateSpec("ClaimInSet", "hvac.temp", null, new[] { "24", "23" }) },
            new ScriptActionStep("toggle", null, "tap", "true"),
            new[] { new ScriptPredicateSpec("ClaimEquals", "hvac.temp", "20", null) },
            new[] { new ScriptGuardSpec("hvac.temp", 2) },
            MaxApplications: 4),
        DeferMaxRounds: null,
        Justification: "cool");

    [Fact]
    public void PhaseMatched_PolicyTurn_BuildsFullProposalWithEchoedDecisionId()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            PolicyTurnAt(AgentDecisionPhase.InitialPlanning),
            ScriptedTurn.Respond(AgentDecisionPhase.PolicyInvalidated, ScriptDecisionKind.NoAction, "seen"),
        }));

        var decision = agent.Consult(Context(AgentDecisionPhase.InitialPlanning));
        var policy = Assert.IsType<AgentDecision.Policy>(decision);
        Assert.Equal("decision-run0000000-1", policy.DecisionId); // D2 回带
        Assert.Equal("pol-1", policy.Proposal.PolicyId);
        var match = Assert.IsType<PolicyPredicate.ClaimInSet>(policy.Proposal.Match[0]);
        Assert.Equal(new[] { "24", "23" }, match.Values);
        Assert.IsType<PolicyPredicate.ClaimEquals>(policy.Proposal.Termination[0]);
        Assert.Equal("toggle", policy.Proposal.ActionTemplate.TargetRole);
        var guard = Assert.IsType<PolicyGuard.ObservationUnchanged>(policy.Proposal.Guards[0]);
        Assert.Equal(2, guard.AfterRounds);
        Assert.Equal(4, policy.Proposal.MaxApplications);
        Assert.Empty(agent.Violations);
    }

    [Fact]
    public void PolicyInvalidated_IsLegalReConsultationPhase()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            PolicyTurnAt(AgentDecisionPhase.InitialPlanning),
            ScriptedTurn.Respond(AgentDecisionPhase.PolicyInvalidated, ScriptDecisionKind.NoAction, "handled"),
        }));

        Assert.NotNull(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        var second = Assert.IsType<AgentDecision.NoAction>(
            agent.Consult(Context(AgentDecisionPhase.PolicyInvalidated)));
        Assert.Equal("handled", second.Proposal.Justification);
        Assert.Empty(agent.Violations); // F9(b)：旧 duplicate-call 纪律不再误杀
        agent.AssertDiscipline(2);
    }

    [Fact]
    public void PhaseMismatch_FailClosed_ViolationRecorded()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            PolicyTurnAt(AgentDecisionPhase.InitialPlanning),
            ScriptedTurn.Respond(AgentDecisionPhase.PolicyInvalidated, ScriptDecisionKind.NoAction, "expected"),
        }));

        Assert.NotNull(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        // 第二咨询到达相位失配脚本 turn（StepRejected ≠ PolicyInvalidated）
        Assert.Null(agent.Consult(Context(AgentDecisionPhase.StepRejected)));
        Assert.Contains(agent.Violations, v => v.StartsWith("phase-mismatch:got-StepRejected", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => agent.AssertDiscipline(2));
    }

    [Fact]
    public void ScriptExhausted_FailClosed()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            PolicyTurnAt(AgentDecisionPhase.InitialPlanning),
        }));

        Assert.NotNull(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        Assert.Null(agent.Consult(Context(AgentDecisionPhase.StepVerified)));
        Assert.Contains("script-exhausted", agent.Violations);
    }

    [Fact]
    public void UnknownPredicateKind_MapsToRogueNode_ThroughRealSeam()
    {
        var foreignPolicy = new ScriptPolicySpec(
            "pol-1",
            new[] { new ScriptPredicateSpec("ElementRoleStartsWith", "toggle", null, null) },
            new ScriptActionStep("toggle", null, "tap", "true"),
            new[] { new ScriptPredicateSpec("ClaimEquals", "hvac.temp", "20", null) },
            Array.Empty<ScriptGuardSpec>(),
            MaxApplications: 4);
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            new ScriptedTurn(AgentDecisionPhase.InitialPlanning, ScriptDecisionKind.Policy,
                Array.Empty<ScriptActionStep>(), foreignPolicy, DeferMaxRounds: null, Justification: null),
        }));

        var decision = agent.Consult(Context(AgentDecisionPhase.InitialPlanning));
        var policy = Assert.IsType<AgentDecision.Policy>(decision);
        Assert.False(policy.Proposal.Match[0] is PolicyPredicate.ClaimEquals
            or PolicyPredicate.ClaimInSet); // rogue 派生节点——V6a 的真实输入
    }

    [Fact]
    public void DeferTurn_CarriesBoundedSpec()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            new ScriptedTurn(AgentDecisionPhase.InitialPlanning, ScriptDecisionKind.Defer,
                Array.Empty<ScriptActionStep>(), Policy: null, DeferMaxRounds: 2, Justification: null),
        }));

        var defer = Assert.IsType<AgentDecision.Defer>(
            agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        Assert.Equal(2, defer.Spec.MaxRounds);
        Assert.Empty(agent.Violations);
    }
}
