using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RUN-005 Slice C — P1-P11 Policy 全矩阵（FROZEN v0.3.1 §11）：正式
/// scenario 经 ScriptedUniAgent（相位感知）→ AgentDecision.Policy →
/// adoption → PolicyExpand → 既有 Act 链 → fresh observation →
/// termination/invalidation → 再咨询。零仿真专用执行路径（全部经
/// ScenarioRunner/SimulationHost → KernelRunDriver）。每个场景验证：
/// 期望咨询数 / effect 数 / Policy applications / termination 或
/// invalidation reason / 零未授权 Effect（certified expectations 由
/// ScenarioRunner 验收，policy 语义经捕获的 consultation contexts 断言）。
/// </summary>
public sealed class PolicyScenarioTests
{
    private static ScenarioExecution Run(string scenarioId) =>
        ScenarioRunner.Run(ScenarioLibrary.Load(scenarioId).Bundle);

    /// <summary>P8 注入：driftFrom 起 mint 新 container（容器身份漂移）。</summary>
    private sealed class DriftAfterAssociationStrategy(DateTimeOffset driftFrom) : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) =>
            input.Previous is null || input.Current.Provenance.CaptureTime >= driftFrom
                ? new AssociationProposal(
                    AssociationDispositionKind.New, MatchedContainerId: null,
                    new[] { new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
                    Relations: Array.Empty<ProposedRelation>(), Reason: "identity-drift")
                : new AssociationProposal(
                    AssociationDispositionKind.Insufficient, MatchedContainerId: null,
                    Candidates: Array.Empty<AssociationCandidate>(),
                    Relations: Array.Empty<ProposedRelation>(), Reason: "seed-once");
    }

    private static void AssertAccepted(ScenarioReport report)
    {
        Assert.True(report.AcceptancePassed, $"acceptance failed; violations=[{string.Join(",", report.AgentViolations)}]");
        Assert.Empty(report.AgentViolations);
    }

    private static PolicyProgressState PolicyStateOf(ScenarioExecution execution, int consultation)
    {
        var state = execution.Host.Consultations.Calls[consultation].Progress.PolicyState;
        Assert.NotNull(state);
        return state!;
    }

    // ---- P1：immediate termination（0-application 即时满足）--------------------

    [Trait("Scenario", "SCN-POLICY-001")]
    [Fact]
    public void P1_ImmediateTermination_ZeroApplications_Succeeds()
    {
        var execution = Run("SCN-POLICY-001");
        AssertAccepted(execution.Report);

        // policy 语义：adoption 轮 fresh observation 即满足 → 成功出口
        var calls = execution.Host.Consultations.Calls;
        Assert.Equal(AgentDecisionPhase.InitialPlanning, calls[0].Phase);
        Assert.Equal(AgentDecisionPhase.StepVerified, calls[1].Phase);
        Assert.Null(calls[1].FailureReason);
        var summary = PolicyStateOf(execution, 1);
        Assert.Equal(0, summary.ApplicationsUsed);
        Assert.Equal(PolicyTruth.Satisfied, summary.TerminationStatus);
        Assert.Equal(0, execution.Report.EffectDeliveries); // 零未授权 Effect
    }

    // ---- P2：repeated application → success -----------------------------------

    [Trait("Scenario", "SCN-POLICY-002")]
    [Fact]
    public void P2_RepeatedApplications_ClaimEvolution_ToSuccess()
    {
        var execution = Run("SCN-POLICY-002");
        AssertAccepted(execution.Report);

        var calls = execution.Host.Consultations.Calls;
        Assert.Equal(AgentDecisionPhase.StepVerified, calls[1].Phase);
        var summary = PolicyStateOf(execution, 1);
        Assert.Equal(2, summary.ApplicationsUsed); // 两次已验证 application
        Assert.Equal(PolicyTruth.Satisfied, summary.TerminationStatus);
        Assert.Equal(2, execution.Report.EffectDeliveries);
    }

    // ---- P3：no match → reconsult（PolicyInvalidated 相位）---------------------

    [Trait("Scenario", "SCN-POLICY-003")]
    [Fact]
    public void P3_OutOfDirectionDomain_NoMatch_Reconsult()
    {
        var execution = Run("SCN-POLICY-003");
        AssertAccepted(execution.Report);

        var calls = execution.Host.Consultations.Calls;
        Assert.Equal(AgentDecisionPhase.PolicyInvalidated, calls[1].Phase);
        Assert.Equal("policy:no-match", calls[1].FailureReason);
        var summary = PolicyStateOf(execution, 1);
        Assert.Equal(0, summary.ApplicationsUsed);
        Assert.Equal(PolicyTruth.Violated, summary.TerminationStatus);
        Assert.Equal(0, execution.Report.EffectDeliveries); // 出集即停——零未授权 Effect
    }

    // ---- P4：bounds exhausted ---------------------------------------------------

    [Trait("Scenario", "SCN-POLICY-004")]
    [Fact]
    public void P4_BoundsExhausted_AfterOneApplication()
    {
        var execution = Run("SCN-POLICY-004");
        AssertAccepted(execution.Report);

        var calls = execution.Host.Consultations.Calls;
        Assert.Equal(AgentDecisionPhase.PolicyInvalidated, calls[1].Phase);
        Assert.Equal("policy:bounds-exhausted", calls[1].FailureReason);
        var summary = PolicyStateOf(execution, 1);
        Assert.Equal(1, summary.ApplicationsUsed);
        Assert.Equal(1, execution.Report.EffectDeliveries); // 恰 MaxApplications 次，无超支
    }

    // ---- P5：guard violated（连续 unchanged）-----------------------------------

    [Trait("Scenario", "SCN-POLICY-005")]
    [Fact]
    public void P5_GuardViolated_AfterConsecutiveUnchangedRounds()
    {
        var execution = Run("SCN-POLICY-005");
        AssertAccepted(execution.Report);

        var calls = execution.Host.Consultations.Calls;
        Assert.Equal("policy:guard-violated", calls[1].FailureReason);
        var summary = PolicyStateOf(execution, 1);
        Assert.Equal(2, summary.ApplicationsUsed); // warm-up 轮 + 1 轮未变后 trip
        Assert.Equal(2, execution.Report.EffectDeliveries);
    }

    // ---- P6：guard Unknown（conflicted claim）----------------------------------

    [Trait("Scenario", "SCN-POLICY-006")]
    [Fact]
    public void P6_ConflictedClaim_GuardUnknown_FailClosed()
    {
        var execution = Run("SCN-POLICY-006");
        AssertAccepted(execution.Report);

        var calls = execution.Host.Consultations.Calls;
        Assert.Equal("policy:guard-unknown", calls[1].FailureReason);
        Assert.Equal(0, execution.Report.EffectDeliveries); // Unknown 不降级——零 dispatch
    }

    // ---- P7：verification failure midway（既有转移 + policy 作废）--------------

    [Trait("Scenario", "SCN-POLICY-007")]
    [Fact]
    public void P7_VerificationFailureMidway_VoidsPolicy_ExistingTransition()
    {
        var execution = Run("SCN-POLICY-007");
        AssertAccepted(execution.Report);

        var calls = execution.Host.Consultations.Calls;
        Assert.Equal(AgentDecisionPhase.VerificationFailed, calls[1].Phase);
        Assert.Equal("post-action-desired-state-not-satisfied", calls[1].FailureReason);
        var summary = PolicyStateOf(execution, 1);
        Assert.Equal("pol-temp-1", summary.PolicyId);
        Assert.Equal(0, summary.ApplicationsUsed); // 未验证成功不计 application
        Assert.Equal(1, execution.Report.EffectDeliveries);
    }

    // ---- P8：fresh-world change between applications（lease invalidation）------

    [Trait("Scenario", "SCN-POLICY-008")]
    [Fact]
    public void P8_LeaseIdentityDrift_BetweenApplications_ZeroNewEffect()
    {
        // drift 经 SANCTIONED 缝注入面（SeamOverrides.Association，SIM-001）；
        // 执行路径不变（feed → driver → kernel）
        var bundle = ScenarioLibrary.Load("SCN-POLICY-008").Bundle;
        var driftFrom = bundle.Stimuli[1].VirtualTime;
        var host = SimulationHost.Compose(bundle, new RunOptions
        {
            Seams = new SeamOverrides
            {
                Association = new DriftAfterAssociationStrategy(driftFrom),
            },
        });
        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        var result = host.DriveOnce();

        // certified expectations 与 policy 语义
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, host.Facts.EffectReceipts.Count); // 零新 Effect（§4 出口语义）
        var calls = host.Consultations.Calls;
        Assert.Equal(2, calls.Count);
        Assert.Equal(AgentDecisionPhase.PolicyInvalidated, calls[1].Phase);
        Assert.Equal("policy:lease-invalidated", calls[1].FailureReason);
        var summary = calls[1].Progress.PolicyState;
        Assert.NotNull(summary);
        Assert.Equal(0, summary!.ApplicationsUsed);
        Assert.Empty(host.Consultations.CheckExpectedConsultations(2));
        Assert.Empty(host.ScriptedAgent!.ScriptViolations);
        Assert.Empty(host.Feed.Remaining);
    }

    // ---- P9/P10/P11：V6 fail-closed 入口面 --------------------------------------

    [Trait("Scenario", "SCN-POLICY-009")]
    [Fact]
    public void P9_ForbiddenEffectClass_RejectedAtConsultation()
    {
        var execution = Run("SCN-POLICY-009");
        AssertAccepted(execution.Report);

        Assert.Equal(RunDriveStatus.AgentDecisionFailed.ToString(), execution.Report.RunDriveStatus);
        Assert.Equal("policy:effect-class-not-allowed", execution.Report.Reason);
        Assert.Equal(0, execution.Report.EffectDeliveries);
        Assert.Single(execution.Host.Consultations.Calls); // 单咨询即 fail closed
    }

    [Trait("Scenario", "SCN-POLICY-010")]
    [Fact]
    public void P10_PolicyBudgetExceedsContract_Rejected()
    {
        var execution = Run("SCN-POLICY-010");
        AssertAccepted(execution.Report);

        Assert.Equal(RunDriveStatus.AgentDecisionFailed.ToString(), execution.Report.RunDriveStatus);
        Assert.Equal("policy:max-applications-exceeds-steps", execution.Report.Reason);
        Assert.Equal(0, execution.Report.EffectDeliveries);
        Assert.Single(execution.Host.Consultations.Calls);
    }

    [Trait("Scenario", "SCN-POLICY-011")]
    [Fact]
    public void P11_UnknownAstNode_Rejected()
    {
        var execution = Run("SCN-POLICY-011");
        AssertAccepted(execution.Report);

        // rogue 谓词经正式 seam 入场 → V6a fail closed
        Assert.Equal(RunDriveStatus.AgentDecisionFailed.ToString(), execution.Report.RunDriveStatus);
        Assert.Equal("policy:unknown-node", execution.Report.Reason);
        Assert.Equal(0, execution.Report.EffectDeliveries);
        Assert.Single(execution.Host.Consultations.Calls);
    }
}
