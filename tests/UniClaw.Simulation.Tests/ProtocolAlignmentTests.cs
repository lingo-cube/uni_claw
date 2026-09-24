using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-004 Step 13 — Protocol Alignment Tests（A1-A8）：scripted double 的
/// 响应语义类型<b>直接来自 Product</b>——Act / NoAction+Completion / Defer /
/// Policy / PolicyInvalidated 多轮全部返回 Product AgentDecision 成员，
/// Simulation 零专门类型；fail-closed 面（wrong phase / extra consultation /
/// unconsumed turn）显式可观察。场景级 Policy/PolicyInvalidated 回归由
/// PolicyScenarioTests（P1-P11）承载；驱动侧 completion/defer 协议由
/// Kernel.Tests 承载——本文件锁 double→Product 的类型对齐。
/// </summary>
public sealed class ProtocolAlignmentTests
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

    // ---- A1：Act —— script 直接返回 Product AgentDecision.Act -------------

    [Fact]
    public void A1_ActTurn_ReturnsProductAct_AllStepFieldsEchoed()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.ActAt(
                AgentDecisionPhase.InitialPlanning,
                new[]
                {
                    new AgentActionStep("toggle", "primary", "tap", "true"),
                    new AgentActionStep("menuItem", null, "tap", null),
                },
                "flip then open"),
        }));

        var decision = agent.Consult(Context(AgentDecisionPhase.InitialPlanning));
        var act = Assert.IsType<AgentDecision.Act>(decision); // 无 Simulation Act 类型
        Assert.Equal("decision-run0000000-1", act.Proposal.DecisionId); // D2 回带
        Assert.Equal(2, act.Proposal.Steps.Count);
        Assert.Equal("toggle", act.Proposal.Steps[0].TargetRole);
        Assert.Equal("primary", act.Proposal.Steps[0].TargetDescriptor);
        Assert.Equal("tap", act.Proposal.Steps[0].EffectClass);
        Assert.Equal("true", act.Proposal.Steps[0].DesiredState);
        Assert.Equal("flip then open", act.Proposal.Justification);
        Assert.Empty(agent.ScriptViolations);
    }

    // ---- A2：NoAction + Completion —— Product CompletionEvidence 载荷 ------

    [Fact]
    public void A2_NoActionTurn_CarriesProductCompletionEvidence_NoSimCompletionType()
    {
        var completion = new CompletionEvidence("kernel.completion-verifier", new[] { "step:1.0", "step:1.1" });
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.NoActionAt(AgentDecisionPhase.StepVerified, "done", completion),
        }));

        var decision = agent.Consult(Context(AgentDecisionPhase.StepVerified));
        var noAction = Assert.IsType<AgentDecision.NoAction>(decision);
        Assert.Equal("decision-run0000000-1", noAction.Proposal.DecisionId);
        Assert.Equal("done", noAction.Proposal.Justification);
        Assert.Same(completion, noAction.Proposal.Completion); // Product 载荷原样（零映射）
    }

    // ---- A3：Defer —— Product ObserveSpec 全表达（Subject + MaxRounds）----

    [Fact]
    public void A3_DeferTurn_ReturnsProductDefer_WithAuthoredSpec()
    {
        var spec = new ObserveSpec(Subject: "hvac.temp", MaxRounds: 3);
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.DeferAt(AgentDecisionPhase.InitialPlanning, spec),
        }));

        var defer = Assert.IsType<AgentDecision.Defer>( // 无 Simulation Defer taxonomy
            agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        Assert.Equal("decision-run0000000-1", defer.DecisionId);
        Assert.Same(spec, defer.Spec); // Product ObserveSpec 原样（Subject 可 authoring）
        Assert.Equal(3, defer.Spec.MaxRounds);
    }

    // ---- A4：Policy —— Product PolicyProposal 原样（零映射）----------------

    [Fact]
    public void A4_PolicyTurn_ReturnsProductPolicy_ProposalPassedThroughUnmapped()
    {
        var proposal = new PolicyProposal(
            "pol-temp-1",
            new PolicyPredicate[]
            {
                new PolicyPredicate.ClaimInSet("hvac.temp", new[] { "24", "23", "22", "21" }),
            },
            new PolicyActionTemplate("toggle", null, "tap", "true"),
            new PolicyPredicate[] { new PolicyPredicate.ClaimEquals("hvac.temp", "20") },
            new PolicyGuard[] { new PolicyGuard.ObservationUnchanged("hvac.temp", 2) },
            MaxApplications: 4,
            Justification: null);
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.PolicyAt(AgentDecisionPhase.InitialPlanning, proposal),
        }));

        var decision = agent.Consult(Context(AgentDecisionPhase.InitialPlanning));
        var policy = Assert.IsType<AgentDecision.Policy>(decision); // 无 Simulation Policy taxonomy
        Assert.Equal("decision-run0000000-1", policy.DecisionId);
        Assert.Same(proposal, policy.Proposal); // Product 提案原样引用——零映射零复制
        Assert.IsType<PolicyPredicate.ClaimInSet>(policy.Proposal.Match[0]);
        Assert.IsType<PolicyPredicate.ClaimEquals>(policy.Proposal.Termination[0]);
        Assert.IsType<PolicyGuard.ObservationUnchanged>(policy.Proposal.Guards[0]);
    }

    // ---- A5：PolicyInvalidated 多轮 —— 相位期待 + 下一 Product decision ----

    [Fact]
    public void A5_PolicyInvalidatedReConsult_ExpectPhase_ReturnsNextProductDecision()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.PolicyAt(
                AgentDecisionPhase.InitialPlanning,
                new PolicyProposal(
                    "pol-temp-1",
                    new PolicyPredicate[] { new PolicyPredicate.ClaimEquals("hvac.temp", "24") },
                    new PolicyActionTemplate("toggle", null, "tap", "true"),
                    new PolicyPredicate[] { new PolicyPredicate.ClaimEquals("hvac.temp", "20") },
                    Array.Empty<PolicyGuard>(),
                    MaxApplications: 2,
                    Justification: null)),
            ScriptedTurn.NoActionAt(AgentDecisionPhase.PolicyInvalidated, "handled"),
        }));

        Assert.IsType<AgentDecision.Policy>(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        // RUN-005 §7 合法再咨询相位：脚本显式期待并应答下一 Product decision
        var second = Assert.IsType<AgentDecision.NoAction>(
            agent.Consult(Context(AgentDecisionPhase.PolicyInvalidated)));
        Assert.Equal("handled", second.Proposal.Justification);
        Assert.Empty(agent.ScriptViolations); // 合法多轮——无隐藏策略、无 duplicate-call
        Assert.Empty(agent.UnconsumedTurns());
    }

    // ---- A6：wrong phase —— fail closed -------------------------------------

    [Fact]
    public void A6_WrongPhase_FailClosed_ReturnsNull_RecordsViolation()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.NoActionAt(AgentDecisionPhase.InitialPlanning, "first"),
        }));

        Assert.Null(agent.Consult(Context(AgentDecisionPhase.VerificationFailed)));
        Assert.Contains(agent.ScriptViolations,
            v => v.StartsWith("phase-mismatch:got-VerificationFailed", StringComparison.Ordinal));
    }

    // ---- A7：extra consultation —— 脚本耗尽 fail closed ---------------------

    [Fact]
    public void A7_ExtraConsultation_ScriptExhausted_FailClosed()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.NoActionAt(AgentDecisionPhase.InitialPlanning, "only"),
        }));

        Assert.NotNull(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        Assert.Null(agent.Consult(Context(AgentDecisionPhase.StepVerified)));
        Assert.Contains("script-exhausted", agent.ScriptViolations);
    }

    // ---- A8：unconsumed scripted turn —— 场景完成时显式暴露 -----------------

    [Fact]
    public void A8_UnconsumedScriptedTurn_ExplicitlyReported_AtScenarioEnd()
    {
        var agent = new ScriptedUniAgent(new PhaseAwareAgentScript(new[]
        {
            ScriptedTurn.ActAt(
                AgentDecisionPhase.InitialPlanning,
                new[] { new AgentActionStep("toggle", null, "tap", "true") }),
            ScriptedTurn.NoActionAt(AgentDecisionPhase.StepVerified, "script-exhausted-goal-should-be-met"),
        }));

        // 场景中途收尾（如 cancel 截断）：turn 2 未被消费——显式纪律事实
        Assert.NotNull(agent.Consult(Context(AgentDecisionPhase.InitialPlanning)));
        Assert.Equal(new[] { "script-unconsumed-turns:1" }, agent.UnconsumedTurns());
    }
}
