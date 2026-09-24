using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

/// <summary>
/// RUN-005 Slice A — Policy 协议基础面（FROZEN v0.3 §2/§3/§4/§4.1/§8/§9）：
/// 第四成员、closed vocabulary、PolicyTruth 三态推导表、PolicyEvaluationView
/// 派生、semantic lease identity 规则、V6 纯形态校验。边界测试，不含
/// Policy execution（Slice B）。
/// </summary>
public sealed class PolicyProtocolTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    /// <summary>测试域 rogue predicate（closed union 之外的派生类型）：V6a 防御面。</summary>
    private sealed record RoguePredicate : PolicyPredicate;

    /// <summary>测试域 rogue guard（closed union 之外的派生类型）。</summary>
    private sealed record RogueGuard : PolicyGuard;

    // ---- §2 第四成员与 closed vocabulary --------------------------------------

    [Fact]
    public void Policy_IsFourthMemberOfAgentDecisionUnion()
    {
        var decision = new AgentDecision.Policy("decision-x-1", ValidProposal());

        Assert.IsType<AgentDecision.Policy>(decision);
        Assert.Equal("decision-x-1", decision.DecisionId);
        Assert.Equal("pol-1", decision.Proposal.PolicyId);
        // 封闭 union 恰四成员（Act/NoAction/Defer/Policy）
        Assert.Equal(
            new[] { "Act", "Defer", "NoAction", "Policy" },
            typeof(AgentDecision).GetNestedTypes().Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void ClosedVocabulary_IsFrozen()
    {
        // v0.3.1：谓词恰两员（ElementExists 删除——DEFER coverage-aware buyer）；
        // 守卫恰一员；PolicyTruth 恰三态；invalidation 恰八因
        Assert.Equal(
            new[] { "ClaimEquals", "ClaimInSet" },
            typeof(PolicyPredicate).GetNestedTypes().Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "ObservationUnchanged" },
            typeof(PolicyGuard).GetNestedTypes().Select(t => t.Name));
        Assert.Equal(
            new[] { nameof(PolicyTruth.Satisfied), nameof(PolicyTruth.Violated), nameof(PolicyTruth.Unknown) },
            Enum.GetNames<PolicyTruth>());
        Assert.Equal(8, Enum.GetValues<PolicyInvalidationReason>().Length);
    }

    // ---- §3 PolicyTruth 推导表 --------------------------------------------------

    private static PolicyEvaluationView View(
        IReadOnlyDictionary<string, PolicyClaimFact>? claims = null,
        IReadOnlyList<PolicyOccurrenceFact>? occurrences = null) =>
        new(claims ?? new Dictionary<string, PolicyClaimFact>(), occurrences ?? Array.Empty<PolicyOccurrenceFact>());

    private static PolicyClaimFact Fact(string value, bool inConflict = false) =>
        new(value, inConflict ? "conflicted" : "established", inConflict);

    [Fact]
    public void ClaimEquals_TruthTable()
    {
        var view = View(new Dictionary<string, PolicyClaimFact>
        {
            ["temp"] = Fact("20"),
            ["mode"] = Fact("eco"),
        });

        Assert.Equal(PolicyTruth.Satisfied, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimEquals("temp", "20"), view));
        Assert.Equal(PolicyTruth.Violated, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimEquals("temp", "24"), view));
        // subject 缺席 → Unknown（不得降级为 false/satisfied）
        Assert.Equal(PolicyTruth.Unknown, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimEquals("absent", "20"), view));
        // conflicted claim → Unknown（SR-022 族：永不满足终止），即使值字面相等
        var conflicted = View(new Dictionary<string, PolicyClaimFact> { ["temp"] = Fact("20", inConflict: true) });
        Assert.Equal(PolicyTruth.Unknown, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimEquals("temp", "20"), conflicted));
    }

    [Fact]
    public void ClaimInSet_BoundedDirectionDomain()
    {
        IReadOnlyList<string> domain = new[] { "24", "23", "22", "21" };

        // 正例：值在方向域内
        var inSet = View(new Dictionary<string, PolicyClaimFact> { ["temp"] = Fact("24") });
        Assert.Equal(PolicyTruth.Satisfied, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimInSet("temp", domain), inSet));
        Assert.Equal(PolicyTruth.Satisfied, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimInSet("temp", domain),
            View(new Dictionary<string, PolicyClaimFact> { ["temp"] = Fact("21") })));

        // 反例：出集即停（19 过冲 / 25 反向——方向语义）
        Assert.Equal(PolicyTruth.Violated, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimInSet("temp", domain),
            View(new Dictionary<string, PolicyClaimFact> { ["temp"] = Fact("19") })));
        Assert.Equal(PolicyTruth.Violated, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimInSet("temp", domain),
            View(new Dictionary<string, PolicyClaimFact> { ["temp"] = Fact("25") })));

        // 缺席 / 冲突 → Unknown
        Assert.Equal(PolicyTruth.Unknown, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimInSet("temp", domain), View()));
        Assert.Equal(PolicyTruth.Unknown, PolicyEvaluation.Evaluate(new PolicyPredicate.ClaimInSet("temp", domain),
            View(new Dictionary<string, PolicyClaimFact> { ["temp"] = Fact("24", inConflict: true) })));
    }

    [Fact]
    public void Conjunction_AnyUnknownDominates_ThenViolated()
    {
        var view = View(new Dictionary<string, PolicyClaimFact>
        {
            ["temp"] = Fact("24"),   // ∈ 域 → Satisfied（与 ClaimEquals("mode","eco") 联合）
            ["mode"] = Fact("eco"),  // 满足
        });
        var domain = new[] { "24", "23", "22", "21" };

        // 全 Satisfied → Satisfied
        Assert.Equal(PolicyTruth.Satisfied, PolicyEvaluation.EvaluateConjunction(new PolicyPredicate[]
        {
            new PolicyPredicate.ClaimInSet("temp", domain),
            new PolicyPredicate.ClaimEquals("mode", "eco"),
        }, view));

        // 任一 Violated（无 Unknown）→ Violated
        Assert.Equal(PolicyTruth.Violated, PolicyEvaluation.EvaluateConjunction(new PolicyPredicate[]
        {
            new PolicyPredicate.ClaimInSet("temp", domain),
            new PolicyPredicate.ClaimEquals("mode", "sport"), // Violated
        }, view));

        // 任一 Unknown → 整体 Unknown（即使另有 Violated——§3 合取语义序）
        Assert.Equal(PolicyTruth.Unknown, PolicyEvaluation.EvaluateConjunction(new PolicyPredicate[]
        {
            new PolicyPredicate.ClaimEquals("absent", "x"),   // Unknown
            new PolicyPredicate.ClaimEquals("mode", "sport"), // Violated
        }, view));
    }

    // ---- §3/§8 Guard：warm-up ≠ Unknown ---------------------------------------

    [Fact]
    public void ObservationUnchanged_WarmUp_IsSatisfied_NotUnknown()
    {
        var view = View(new Dictionary<string, PolicyClaimFact> { ["temp"] = Fact("24") });
        var guard = new PolicyGuard.ObservationUnchanged("temp", AfterRounds: 2);

        // 首样本前（cursor 未初始化）→ warm-up → Satisfied（≠ Unknown）
        var fresh = new PolicyGuardCursor("temp", null, 0);
        Assert.True(fresh.InWarmUp);
        Assert.Equal(PolicyTruth.Satisfied, PolicyEvaluation.Evaluate(guard, view, fresh));

        // 窗口未满（count < n）→ Satisfied
        Assert.Equal(PolicyTruth.Satisfied,
            PolicyEvaluation.Evaluate(guard, view, new PolicyGuardCursor("temp", "24", 1)));

        // 连续 n 轮不变 → Violated
        Assert.Equal(PolicyTruth.Violated,
            PolicyEvaluation.Evaluate(guard, view, new PolicyGuardCursor("temp", "24", 2)));
    }

    [Fact]
    public void ObservationUnchanged_CurrentValueUnknown_IsUnknown_EvenInWarmUp()
    {
        var guard = new PolicyGuard.ObservationUnchanged("temp", AfterRounds: 2);
        var fresh = new PolicyGuardCursor("temp", null, 0);

        // 本轮 subject 缺席 → Unknown（证据不足），不因 warm-up 降级
        Assert.Equal(PolicyTruth.Unknown, PolicyEvaluation.Evaluate(guard, View(), fresh));

        // 本轮 subject 冲突 → Unknown
        var conflicted = View(new Dictionary<string, PolicyClaimFact> { ["temp"] = Fact("24", inConflict: true) });
        Assert.Equal(PolicyTruth.Unknown, PolicyEvaluation.Evaluate(guard, conflicted, fresh));
    }

    // ---- §4.1 PolicyEvaluationView（owner-derived 独立最小投影）----------------

    private static WorldBeliefRevision Revision(
        IReadOnlyDictionary<string, WorldClaim>? state = null,
        IReadOnlyList<Conflict>? conflicts = null,
        IReadOnlyList<OccurrenceBelief>? occurrences = null,
        IReadOnlyList<ContainerBelief>? containers = null) =>
        new("rev-1", null, 1,
            state ?? new Dictionary<string, WorldClaim>(),
            Array.Empty<string>(),
            new HashSet<string>(),
            new FreshnessBasis(T0),
            new Uncertainty(conflicts?.Count ?? 0),
            conflicts ?? Array.Empty<Conflict>(),
            Containers: containers,
            Occurrences: occurrences);

    [Fact]
    public void EvaluationView_FromBelief_ProjectsMinimalClaimAndOccurrenceFacts()
    {
        var belief = Revision(
            state: new Dictionary<string, WorldClaim>
            {
                ["temp"] = new WorldClaim("24", "ev-1"),
                ["mode"] = new WorldClaim("eco", "ev-2",
                    SupersededEvidenceIds: new[] { "ev-0" }), // 痕迹链非空 → revised
            },
            conflicts: new[] { new Conflict("temp", "24", "26", "ev-1", "ev-3") },
            occurrences: new[] { new OccurrenceBelief("occ-1", "ctr-1", "switch", null, new[] { "ev-1" }) });

        var view = PolicyEvaluationView.FromBelief(belief);

        // 最小 claim 投影：Value / Disposition / InConflict
        Assert.Equal("24", view.Claims["temp"].Value);
        Assert.Equal("conflicted", view.Claims["temp"].Disposition);
        Assert.True(view.Claims["temp"].InConflict);
        Assert.Equal("revised", view.Claims["mode"].Disposition);
        Assert.False(view.Claims["mode"].InConflict);

        // occurrence 投影：Role / Epistemic（v1：presence ⇒ Observed）
        var occurrence = Assert.Single(view.Occurrences);
        Assert.Equal("switch", occurrence.Role);
        Assert.Equal(ElementEpistemic.Observed, occurrence.Epistemic);
    }

    [Fact]
    public void EvaluationView_FromBelief_NullOccurrences_ProjectsEmpty()
    {
        var view = PolicyEvaluationView.FromBelief(Revision());
        Assert.Empty(view.Occurrences);
        Assert.Empty(view.Claims);
    }

    // ---- §4 semantic lease：adoption 绑定规则（identity 规则）-------------------

    [Fact]
    public void Lease_DerivationTable()
    {
        // 无 belief → 无 active lease
        Assert.Equal("no-active-lease", PolicyLease.TryDerive(null).Rejection);

        // 零 root container → 无 active lease
        Assert.Equal("no-active-lease", PolicyLease.TryDerive(Revision()).Rejection);

        // 多 root（≠ 唯一）→ 无 active lease（scope 语义：单根持存）
        var multi = Revision(containers: new[]
        {
            new ContainerBelief(new ContainerIdentity("ctr-a"), Array.Empty<string>()),
            new ContainerBelief(new ContainerIdentity("ctr-b"), Array.Empty<string>()),
        });
        Assert.Equal("no-active-lease", PolicyLease.TryDerive(multi).Rejection);

        // 唯一 root + 合法 identity → 绑定（ref = 容器身份；内容变化不参战）
        var single = Revision(containers: new[]
        {
            new ContainerBelief(new ContainerIdentity("ctr-a"), Array.Empty<string>()),
        });
        var bound = PolicyLease.TryDerive(single);
        Assert.Null(bound.Rejection);
        Assert.NotNull(bound.Lease);
        Assert.Equal("ctr-a", bound.Lease!.RootContainerId);

        // 唯一 root 但 identity 不合法（空白）→ lease identity invalid
        var blank = Revision(containers: new[]
        {
            new ContainerBelief(new ContainerIdentity("  "), Array.Empty<string>()),
        });
        Assert.Equal("lease-identity-invalid", PolicyLease.TryDerive(blank).Rejection);
    }

    // ---- §9 V6 纯形态校验 --------------------------------------------------------

    private static readonly ExecutionContractView ContractView = new(
        "v1", "cool-down", new HashSet<string> { "temp" },
        new HashSet<string> { "tap" }, new HashSet<string>(),
        new[] { "temp-20" });

    internal static PolicyProposal ValidProposal() => new(
        PolicyId: "pol-1",
        Match: new[] { new PolicyPredicate.ClaimInSet("temp", new[] { "24", "23", "22", "21" }) },
        ActionTemplate: new PolicyActionTemplate("minus-button", "temperature", "tap", null),
        Termination: new[] { new PolicyPredicate.ClaimEquals("temp", "20") },
        Guards: new[] { new PolicyGuard.ObservationUnchanged("temp", 2) },
        MaxApplications: 4,
        Justification: "step-down-until-20");

    [Fact]
    public void V6_ValidProposal_Passes()
    {
        Assert.Null(PolicyValidation.ValidateProposal(ValidProposal(), ContractView, stepsRemaining: 256));
    }

    [Fact]
    public void V6_BlankPolicyId_Rejected()
    {
        var p = ValidProposal() with { PolicyId = " " };
        Assert.Equal("policy:invalid-policy-id", PolicyValidation.ValidateProposal(p, ContractView, 256));
    }

    [Fact]
    public void V6_UnknownAstNode_Rejected()
    {
        var unknownPredicate = ValidProposal() with { Match = new PolicyPredicate[] { new RoguePredicate() } };
        Assert.Equal("policy:unknown-node", PolicyValidation.ValidateProposal(unknownPredicate, ContractView, 256));

        var unknownGuard = ValidProposal() with { Guards = new PolicyGuard[] { new RogueGuard() } };
        Assert.Equal("policy:unknown-node", PolicyValidation.ValidateProposal(unknownGuard, ContractView, 256));
    }

    [Fact]
    public void V6_MalformedKnownNode_Rejected()
    {
        // 空 ClaimInSet 值域（零方向 = 畸形有限方向域）
        Assert.Equal("policy:invalid-node", PolicyValidation.ValidateProposal(
            ValidProposal() with { Match = new[] { new PolicyPredicate.ClaimInSet("temp", Array.Empty<string>()) } },
            ContractView, 256));
        // 空白 subject
        Assert.Equal("policy:invalid-node", PolicyValidation.ValidateProposal(
            ValidProposal() with { Termination = new[] { new PolicyPredicate.ClaimEquals(" ", "20") } },
            ContractView, 256));
        // Guard AfterRounds ≤ 0（「连续 n 轮」退化）
        Assert.Equal("policy:invalid-node", PolicyValidation.ValidateProposal(
            ValidProposal() with { Guards = new[] { new PolicyGuard.ObservationUnchanged("temp", 0) } },
            ContractView, 256));
    }

    [Fact]
    public void V6_EmptyMatchOrTermination_Rejected()
    {
        Assert.Equal("policy:empty-match", PolicyValidation.ValidateProposal(
            ValidProposal() with { Match = Array.Empty<PolicyPredicate>() }, ContractView, 256));
        Assert.Equal("policy:empty-termination", PolicyValidation.ValidateProposal(
            ValidProposal() with { Termination = Array.Empty<PolicyPredicate>() }, ContractView, 256));
    }

    [Fact]
    public void V6_Template_TargetRoleAndEffectClass_Rejected()
    {
        Assert.Equal("policy:missing-target-role", PolicyValidation.ValidateProposal(
            ValidProposal() with { ActionTemplate = new PolicyActionTemplate(" ", null, "tap", null) },
            ContractView, 256));
        Assert.Equal("policy:effect-class-not-allowed", PolicyValidation.ValidateProposal(
            ValidProposal() with { ActionTemplate = new PolicyActionTemplate("minus-button", null, "swipe", null) },
            ContractView, 256));
    }

    [Fact]
    public void V6_MaxApplicationsBounds_Rejected()
    {
        // bounds > 0
        Assert.Equal("policy:max-applications-invalid", PolicyValidation.ValidateProposal(
            ValidProposal() with { MaxApplications = 0 }, ContractView, 256));
        // MaxApplications ≤ StepsRemaining（不得扩大合同步数预算）
        Assert.Equal("policy:max-applications-exceeds-steps", PolicyValidation.ValidateProposal(
            ValidProposal() with { MaxApplications = 5 }, ContractView, stepsRemaining: 4));
        Assert.Null(PolicyValidation.ValidateProposal(
            ValidProposal() with { MaxApplications = 4 }, ContractView, stepsRemaining: 4));
    }

    [Fact]
    public void V6f_DuplicatePolicyIdRule_SameConsultRevalidationIsNotDuplicate()
    {
        var adopted = new Dictionary<string, int> { ["pol-1"] = 1 };

        // 同一咨询的重验（幂等重 Drive）≠ 重复
        Assert.False(PolicyValidation.IsDuplicatePolicyId(adopted, "pol-1", currentConsultNumber: 1));
        // 新咨询携带同 PolicyId → 重复（同 run 内唯一）
        Assert.True(PolicyValidation.IsDuplicatePolicyId(adopted, "pol-1", currentConsultNumber: 2));
        // 不同 id 不受影响
        Assert.False(PolicyValidation.IsDuplicatePolicyId(adopted, "pol-2", currentConsultNumber: 2));
    }
}
