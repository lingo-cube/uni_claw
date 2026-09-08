using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// UIW-003 验收 S1–S15 —— UI Entity Model belief 侧（UWM-009 v0.3 §35–§41 /
/// ADR-0013 / ADR-0014 / P23）。全部确定性（level: DETERMINISTIC）；
/// observation / continuity 为 deterministic seam doubles。
/// </summary>
public sealed class UIWorldContinuityTests
{
    private static readonly TransitionContext Scroll = new("scroll", "attempt-1", TransitionStrength.Attempt);

    private static (UniKernel Kernel, WorldModel World) NewKernel(
        IUiObservationStrategy observation, IContinuityStrategy continuity) =>
        UIWorldContinuityDoubles.NewKernel(observation, continuity);

    private static OccurrenceBelief SingleOccurrence(WorldModel world) =>
        Assert.Single(world.Current!.Occurrences!);

    // ---- S1：occurrence 派生 + revision-locality ------------------------------

    [Fact]
    public void S1_OccurrencesAreDerivedPerRevision_ReplacedNotInherited_IdsRemintedEachRound()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        var first = kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        var second = kernel.Process(UIWorldDoubles.Observation("cancel-button:secondary", UIWorldDoubles.T1));

        var rev1 = world.RevisionHistory[0];
        var rev2 = world.RevisionHistory[1];

        var occ1 = Assert.Single(rev1.Occurrences!);
        Assert.Equal("save-button", occ1.Role);
        Assert.Equal("primary", occ1.SemanticDescriptor);
        Assert.Equal(new[] { first.Admission.EvidenceId! }, occ1.EvidenceBasis);
        Assert.StartsWith("occ-", occ1.OccurrenceId);

        // revision-local：rev2 的 occurrence 集合派生自触发它的 evidence（替换，不从 rev1 继承）
        var occ2 = Assert.Single(rev2.Occurrences!);
        Assert.Equal("cancel-button", occ2.Role);
        Assert.Equal(new[] { second.Admission.EvidenceId! }, occ2.EvidenceBasis);
        Assert.NotEqual(occ1.OccurrenceId, occ2.OccurrenceId); // id 每轮新铸
        Assert.DoesNotContain(occ1.OccurrenceId, rev2.Occurrences!.Select(o => o.OccurrenceId));
    }

    // ---- S2：anchor fail-closed / descriptor-scoped 可登记 ---------------------

    [Fact]
    public void S2_StaleOccurrenceAnchorFailsClosed_DescriptorScopedDemandRegisterable()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        var current = world.Current!;

        // anchor 不在 Current.Occurrences → fail-closed（stale anchor，ADR-0014 timing）
        Assert.Throws<InvalidOperationException>(() => world.RegisterContinuityDemand(
            UIWorldContinuityDoubles.Demand("d-stale", "save-button", anchor: "occ-bogus",
                anchorRevision: current.RevisionId)));

        // current occurrence anchor 合法
        var anchor = SingleOccurrence(world);
        var handle = world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand(
            "d-anchored", "save-button", descriptor: "primary",
            anchor: anchor.OccurrenceId, anchorRevision: current.RevisionId));
        Assert.Equal("d-anchored", handle.DemandId);

        // descriptor-scoped（无 anchor）合法
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d-scoped", "cancel-button"));
        Assert.Equal(2, world.ContinuityDemands.Count);

        // 同 DemandId 幂等：复用既有登记，不重复
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d-scoped", "cancel-button"));
        Assert.Equal(2, world.ContinuityDemands.Count);
    }

    // ---- S3：demand 登记不产生 revision（P-UW-32）------------------------------

    [Fact]
    public void S3_DemandRegistrationNeverProducesRevision_P_UW_32()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        var before = world.Current;
        var count = world.RevisionHistory.Count;

        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "save-button", descriptor: "primary"));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d2", "cancel-button"));

        Assert.Same(before, world.Current);
        Assert.Equal(count, world.RevisionHistory.Count);
        Assert.Empty(world.ContinuityLog);
    }

    // ---- S4：ReferenceEstablished → mint + commit + basis 不变 -----------------

    [Fact]
    public void S4_ReferenceEstablished_MintsLogicalItemAndCommitsRevision_BasisUnchanged()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        var observed = kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        var current = world.Current!;
        var occurrence = SingleOccurrence(world);
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand(
            "d1", "save-button", descriptor: "primary",
            anchor: occurrence.OccurrenceId, anchorRevision: current.RevisionId));
        var revisionsBefore = world.RevisionHistory.Count;

        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, result.Outcome.Kind);
        Assert.NotNull(result.LogicalItemId);
        var itemId = result.LogicalItemId!;
        Assert.StartsWith("li-", itemId);
        Assert.Equal(revisionsBefore + 1, world.RevisionHistory.Count);
        Assert.Equal(world.Current!.RevisionId, result.RevisionId);

        var revision = world.Current!;
        Assert.Equal(current.RevisionId, revision.ParentRevisionId); // ParentRevisionId 链正确
        Assert.Equal(current.EvidenceBasis, revision.EvidenceBasis); // basis 集合不变
        Assert.Equal(occurrence.OccurrenceId, SingleOccurrence(world).OccurrenceId); // occurrences 沿用

        var item = Assert.Single(revision.LogicalItems!);
        Assert.Equal(itemId, item.LogicalItemId);
        Assert.Equal("save-button", item.Role);
        Assert.Equal(LogicalItemLifecycle.Established, item.Lifecycle);
        Assert.Null(item.EndedReason);
        Assert.Equal(new[] { observed.Admission.EvidenceId! }, item.EvidenceBasis); // evidence-established

        // registry 维护（非 belief）：demand 绑定 item → Hot
        Assert.Equal(itemId, world.ContinuityDemands.Single(d => d.DemandId == "d1").LogicalItemId);
        Assert.True(world.IsHotItem(itemId));

        var decision = Assert.Single(world.ContinuityLog);
        Assert.Equal(revision.RevisionId, decision.RevisionId);
        Assert.Equal("d1", decision.DemandId);
        Assert.Equal(ContinuityProposedOutcomeKind.ReferenceEstablished, decision.ProposedOutcome);
        Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, decision.EffectiveOutcome.Kind);
        Assert.Equal(occurrence.OccurrenceId, decision.MatchedOccurrenceId);
        Assert.Equal(itemId, decision.MatchedLogicalItemId);
    }

    // ---- S5：demand 无证据 → Insufficient ---------------------------------------

    [Fact]
    public void S5_DemandWithoutEvidenceIsInsufficient_NoItemNoRevision()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "cancel-button"));
        var before = world.Current;
        var count = world.RevisionHistory.Count;

        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityResolutionOutcomeKind.Adjudicated, result.Outcome.Kind);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.Insufficient, result.Outcome.AdjudicationKind);
        Assert.Null(result.LogicalItemId);
        Assert.Same(before, world.Current); // 无 revision 变化
        Assert.Equal(count, world.RevisionHistory.Count);
        Assert.Null(world.Current!.LogicalItems); // 无 item

        // 无 commit，但决策入 ContinuityLog
        var decision = Assert.Single(world.ContinuityLog);
        Assert.Null(decision.RevisionId);
        Assert.Equal(ContinuityProposedOutcomeKind.Insufficient, decision.ProposedOutcome);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.Insufficient, decision.EffectiveOutcome.AdjudicationKind);
    }

    [Fact]
    public void S5b_DemandOnlyMintIsBlockedByAuthority_P_UW_26()
    {
        // 劣质 strategy：零 supporting（demand-only）提议铸造 → gate 降级 Insufficient
        var continuity = new ScriptedContinuityStrategy(i => new ContinuityProposal(
            ContinuityProposedOutcomeKind.ReferenceEstablished,
            i.Candidates[0].OccurrenceId, MatchedLogicalItemId: null,
            Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ProposedTermination>(), "demand-only-mint"));
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), continuity);
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "save-button"));
        var before = world.Current;
        var count = world.RevisionHistory.Count;

        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityAdjudicationOutcomeKind.Insufficient, result.Outcome.AdjudicationKind);
        Assert.Null(result.LogicalItemId);
        Assert.Same(before, world.Current);
        Assert.Equal(count, world.RevisionHistory.Count);
        var decision = Assert.Single(world.ContinuityLog);
        Assert.Equal(ContinuityProposedOutcomeKind.ReferenceEstablished, decision.ProposedOutcome);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.Insufficient, decision.EffectiveOutcome.AdjudicationKind);
        Assert.StartsWith("authority-blocked", decision.Reason, StringComparison.Ordinal);
    }

    // ---- S6：SameReferent 延续 + basis 扩展 -------------------------------------

    [Fact]
    public void S6_SameReferentAfterRevisionAdvance_ExtendsItemEvidenceBasis()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        var first = kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        var occurrence = SingleOccurrence(world);
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand(
            "d1", "save-button", descriptor: "primary",
            anchor: occurrence.OccurrenceId, anchorRevision: world.Current!.RevisionId));
        var mint = world.ResolveContinuity(new DemandHandle("d1"));
        var itemId = mint.LogicalItemId!;

        // revision advance（scroll 帧）：occurrence 替换、item 跨 revision 延续
        var second = kernel.Process(
            UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T1), Scroll);
        var advanced = world.Current!;
        Assert.DoesNotContain(occurrence.OccurrenceId, advanced.Occurrences!.Select(o => o.OccurrenceId));
        Assert.Equal(itemId, Assert.Single(advanced.LogicalItems!).LogicalItemId);
        var count = world.RevisionHistory.Count;

        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityResolutionOutcomeKind.Adjudicated, result.Outcome.Kind);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.SameReferent, result.Outcome.AdjudicationKind);
        Assert.Equal(itemId, result.LogicalItemId);
        Assert.Equal(count + 1, world.RevisionHistory.Count); // SameReferent = commit

        var revision = world.Current!;
        Assert.Equal(advanced.RevisionId, revision.ParentRevisionId);
        Assert.Equal(advanced.EvidenceBasis, revision.EvidenceBasis); // basis 集合不变
        var item = Assert.Single(revision.LogicalItems!);
        Assert.Equal(itemId, item.LogicalItemId);
        Assert.Equal(LogicalItemLifecycle.Established, item.Lifecycle);
        Assert.Contains(first.Admission.EvidenceId!, item.EvidenceBasis);
        Assert.Contains(second.Admission.EvidenceId!, item.EvidenceBasis); // item basis 扩展

        var decision = world.ContinuityLog[^1];
        Assert.Equal(revision.RevisionId, decision.RevisionId);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.SameReferent, decision.EffectiveOutcome.AdjudicationKind);
    }

    // ---- S7：recycled row 反证 → Contradicted（不 Ended、presence 不动）---------

    [Fact]
    public void S7_RecycledRowContradiction_ContradictedWithoutEndingItemOrTouchingPresence()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        var occurrence = SingleOccurrence(world);
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand(
            "d1", "save-button", descriptor: "primary",
            anchor: occurrence.OccurrenceId, anchorRevision: world.Current!.RevisionId));
        var mint = world.ResolveContinuity(new DemandHandle("d1"));
        var itemId = mint.LogicalItemId!;

        kernel.Process(UIWorldDoubles.Observation("save-button:contradicts:recycled-row", UIWorldDoubles.T1));
        var before = world.Current;
        var count = world.RevisionHistory.Count;
        var itemBefore = Assert.Single(before!.LogicalItems!);

        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityAdjudicationOutcomeKind.Contradicted, result.Outcome.AdjudicationKind);
        Assert.Null(result.LogicalItemId);
        Assert.Same(before, world.Current); // 零 commit
        Assert.Equal(count, world.RevisionHistory.Count);
        var item = Assert.Single(world.Current!.LogicalItems!);
        Assert.Equal(itemBefore, item); // item 不 Ended、basis / presence 不动
        Assert.Equal(LogicalItemLifecycle.Established, item.Lifecycle);
        Assert.Null(item.EndedReason);

        var decision = world.ContinuityLog[^1];
        Assert.Equal(ContinuityAdjudicationOutcomeKind.Contradicted, decision.EffectiveOutcome.AdjudicationKind);
        Assert.Null(decision.RevisionId);
    }

    // ---- S8：无候选 → NoCurrentCandidate ---------------------------------------

    [Fact]
    public void S8_NoCurrentCandidate_ZeroBeliefAndLifecycleSideEffects_StrategyNotConsulted()
    {
        var continuity = new ScriptedContinuityStrategy();
        var (kernel, world) = NewKernel(new EmptyObservationStrategy(), continuity);
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "save-button"));
        var before = world.Current;
        var count = world.RevisionHistory.Count;

        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityResolutionOutcomeKind.NoCurrentCandidate, result.Outcome.Kind);
        Assert.Null(result.Outcome.AdjudicationKind);
        Assert.Null(result.LogicalItemId);
        Assert.Equal(before!.RevisionId, result.RevisionId);
        Assert.Same(before, world.Current);
        Assert.Equal(count, world.RevisionHistory.Count);
        Assert.Null(world.Current.LogicalItems);
        Assert.Empty(continuity.Seen); // pre-gate：不经 strategy

        var decision = Assert.Single(world.ContinuityLog);
        Assert.Null(decision.ProposedOutcome); // strategy 未被咨询
        Assert.Equal(ContinuityResolutionOutcomeKind.NoCurrentCandidate, decision.EffectiveOutcome.Kind);
        Assert.Null(decision.RevisionId);
    }

    // ---- S9：双胞胎 → Ambiguous（不强制选择）-----------------------------------

    [Fact]
    public void S9_TwinCandidatesAreAmbiguous_NoForcedChoice_NoMint()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        kernel.Process(UIWorldDoubles.Observation("save-button:row-a+save-button:row-b", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "save-button"));
        var before = world.Current;
        var count = world.RevisionHistory.Count;

        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityAdjudicationOutcomeKind.Ambiguous, result.Outcome.AdjudicationKind);
        Assert.Null(result.LogicalItemId);
        Assert.Same(before, world.Current); // Identity never creates information：不 mint
        Assert.Equal(count, world.RevisionHistory.Count);
        Assert.Null(world.Current!.LogicalItems);
        var decision = Assert.Single(world.ContinuityLog);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.Ambiguous, decision.EffectiveOutcome.AdjudicationKind);
    }

    // ---- S10：referent 终止正证 → Ended；证据不足 → 保持 Established ------------

    [Fact]
    public void S10_ReferentTerminationWithSufficientEvidence_EndsItemReferentTerminated()
    {
        var continuity = new ScriptedContinuityStrategy(
            i => new ContinuityProposal( // 1st：铸造
                ContinuityProposedOutcomeKind.ReferenceEstablished,
                i.Candidates[0].OccurrenceId, MatchedLogicalItemId: null,
                i.Candidates[0].SupportingEvidenceIds.ToArray(), Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "mint"),
            i => new ContinuityProposal( // 2nd：SameReferent + 终止提议（support ⊆ basis）
                ContinuityProposedOutcomeKind.SameReferent,
                i.Candidates[0].OccurrenceId, i.ExistingItems[0].LogicalItemId,
                i.Candidates[0].SupportingEvidenceIds.ToArray(), Array.Empty<string>(),
                new[] { new ProposedTermination(
                    i.ExistingItems[0].LogicalItemId,
                    i.Current.EvidenceBasis.ToArray(), "referent-removed") },
                "continuity-with-termination"));
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), continuity);
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "save-button"));
        var mint = world.ResolveContinuity(new DemandHandle("d1"));
        var itemId = mint.LogicalItemId!;

        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T1));
        var count = world.RevisionHistory.Count;

        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityAdjudicationOutcomeKind.SameReferent, result.Outcome.AdjudicationKind);
        Assert.Equal(count + 1, world.RevisionHistory.Count); // Ended = commit
        var item = Assert.Single(world.Current!.LogicalItems!);
        Assert.Equal(itemId, item.LogicalItemId);
        Assert.Equal(LogicalItemLifecycle.Ended, item.Lifecycle);
        Assert.Equal("referent-terminated", item.EndedReason);
    }

    [Fact]
    public void S10b_TerminationWithoutSufficientEvidence_KeepsItemEstablished()
    {
        var continuity = new ScriptedContinuityStrategy(
            i => new ContinuityProposal( // 铸造
                ContinuityProposedOutcomeKind.ReferenceEstablished,
                i.Candidates[0].OccurrenceId, MatchedLogicalItemId: null,
                i.Candidates[0].SupportingEvidenceIds.ToArray(), Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "mint"),
            i => new ContinuityProposal( // SameReferent + 零 supporting 的终止提议
                ContinuityProposedOutcomeKind.SameReferent,
                i.Candidates[0].OccurrenceId, i.ExistingItems[0].LogicalItemId,
                i.Candidates[0].SupportingEvidenceIds.ToArray(), Array.Empty<string>(),
                new[] { new ProposedTermination(
                    i.ExistingItems[0].LogicalItemId, Array.Empty<string>(), "guess-terminated") },
                "continuity-with-unfounded-termination"));
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), continuity);
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "save-button"));
        var mint = world.ResolveContinuity(new DemandHandle("d1"));

        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T1));
        world.ResolveContinuity(new DemandHandle("d1"));

        var item = Assert.Single(world.Current!.LogicalItems!);
        Assert.Equal(mint.LogicalItemId, item.LogicalItemId);
        Assert.Equal(LogicalItemLifecycle.Established, item.Lifecycle); // 不 Ended
        Assert.Null(item.EndedReason);
    }

    [Fact]
    public void S10c_MissingOwningContainerCascadesToContainerScopeEnded()
    {
        // v0.1 无 container 终止操作，级联正常路径不可达；此处手工构造缺失
        // container（occurrence 携带不存在的 owning container id）触发规则。
        var (kernel, world) = NewKernel(
            new RoleObservationStrategy(owningContainerId: "ctr-ghost"), new RoleContinuityStrategy());
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "save-button", descriptor: "primary"));

        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, result.Outcome.Kind);
        var item = Assert.Single(world.Current!.LogicalItems!);
        Assert.Equal(LogicalItemLifecycle.Ended, item.Lifecycle);
        Assert.Equal("container-scope-ended", item.EndedReason);
    }

    // ---- S11：Revoke / Hot→Cold 派生 --------------------------------------------

    [Fact]
    public void S11_RevokeRemovesDemandOnly_ItemSurvives_NoRevision_HotTurnsCold()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "save-button", descriptor: "primary"));
        var mint = world.ResolveContinuity(new DemandHandle("d1"));
        var itemId = mint.LogicalItemId!;
        Assert.True(world.IsHotItem(itemId)); // active demand 引用 → Hot

        var before = world.Current;
        var count = world.RevisionHistory.Count;

        world.RevokeContinuityDemand("d1");

        Assert.Empty(world.ContinuityDemands); // 只删 demand
        Assert.Same(before, world.Current); // 零 revision / belief 副作用
        Assert.Equal(count, world.RevisionHistory.Count);
        Assert.Equal(itemId, Assert.Single(world.Current!.LogicalItems!).LogicalItemId); // item 留存
        Assert.Equal(LogicalItemLifecycle.Established, world.Current.LogicalItems![0].Lifecycle); // 不 Ended
        Assert.False(world.IsHotItem(itemId)); // Hot → Cold（纯派生）

        // 不存在的 demandId → no-op（本实现选择：不抛）
        world.RevokeContinuityDemand("no-such-demand");
        Assert.Empty(world.ContinuityDemands);
    }

    // ---- S12：状态/值变化不终止 continuity ---------------------------------------

    [Fact]
    public void S12_StateOrValueChangeDoesNotTerminateContinuity_SameReferentSurvives()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        kernel.Process(UIWorldDoubles.Observation("todo-item:text=hello", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(
            UIWorldContinuityDoubles.Demand("d1", "todo-item", descriptor: "text=hello"));
        var mint = world.ResolveContinuity(new DemandHandle("d1"));
        var itemId = mint.LogicalItemId!;

        // 状态变化（文本/值变）：occurrence descriptor 变了，role 连续性仍在
        kernel.Process(UIWorldDoubles.Observation("todo-item:text=world+checked", UIWorldDoubles.T1));
        var result = world.ResolveContinuity(new DemandHandle("d1"));

        Assert.Equal(ContinuityAdjudicationOutcomeKind.SameReferent, result.Outcome.AdjudicationKind);
        Assert.Equal(itemId, result.LogicalItemId);
        var item = Assert.Single(world.Current!.LogicalItems!);
        Assert.Equal(LogicalItemLifecycle.Established, item.Lifecycle); // 状态变化 ≠ Ended
        Assert.Null(item.EndedReason);
        Assert.Equal(2, item.EvidenceBasis.Count); // basis 扩展（两帧 evidence）
    }

    // ---- S13：replay 确定性 ------------------------------------------------------

    [Fact]
    public void S13_ReplayDeterminism_SameInputsSameIdsDecisionsAndRevisionSequence()
    {
        var a = RunScrollContinuityFixture();
        var b = RunScrollContinuityFixture();

        // 同 occurrence id（内容派生、每轮新铸但 replay 稳定）
        Assert.Equal(
            a.OccurrenceIds.SelectMany(x => x),
            b.OccurrenceIds.SelectMany(x => x));
        // 同 LogicalItem id / 同 revision 序列
        Assert.Equal(a.ItemId, b.ItemId);
        Assert.Equal(a.RevisionIds, b.RevisionIds);
        // 同决策序列（proposed / effective / matched ids / commit revision）
        Assert.Equal(
            a.Decisions.Select(d => (d.DemandId, d.ProposedOutcome, d.EffectiveOutcome,
                d.MatchedOccurrenceId, d.MatchedLogicalItemId, d.RevisionId)),
            b.Decisions.Select(d => (d.DemandId, d.ProposedOutcome, d.EffectiveOutcome,
                d.MatchedOccurrenceId, d.MatchedLogicalItemId, d.RevisionId)));

        return;

        static (string[] RevisionIds, string[][] OccurrenceIds, string ItemId,
            IReadOnlyList<ContinuityDecision> Decisions) RunScrollContinuityFixture()
        {
            var world = new WorldModel(
                new HashSet<string> { UIWorldDoubles.Observed },
                observationStrategy: new RoleObservationStrategy(),
                continuityStrategy: new RoleContinuityStrategy());
            var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
            kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
            var occurrence = Assert.Single(world.Current!.Occurrences!);
            world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand(
                "d1", "save-button", descriptor: "primary",
                anchor: occurrence.OccurrenceId, anchorRevision: world.Current.RevisionId));
            var mint = world.ResolveContinuity(new DemandHandle("d1"));
            kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T1), Scroll);
            world.ResolveContinuity(new DemandHandle("d1"));
            return (
                world.RevisionHistory.Select(r => r.RevisionId).ToArray(),
                world.RevisionHistory.Select(r => r.Occurrences!.Select(o => o.OccurrenceId).ToArray()).ToArray(),
                mint.LogicalItemId!,
                world.ContinuityLog);
        }
    }

    // ---- S14：多 demand 同 item 的 Hot/Cold 派生 ---------------------------------

    [Fact]
    public void S14_MultipleDemandsOnSameItem_RevokeOneStillHot_RevokeAllTurnsCold()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d1", "save-button", descriptor: "primary"));
        var mint = world.ResolveContinuity(new DemandHandle("d1"));
        var itemId = mint.LogicalItemId!;

        // 第二个 demand（descriptor-scoped）resolve 后按 role 关联到同一 item
        world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand("d2", "save-button"));
        var second = world.ResolveContinuity(new DemandHandle("d2"));
        Assert.Equal(ContinuityAdjudicationOutcomeKind.SameReferent, second.Outcome.AdjudicationKind);
        Assert.Equal(itemId, second.LogicalItemId);
        Assert.Equal(itemId, world.ContinuityDemands.Single(d => d.DemandId == "d2").LogicalItemId);

        Assert.True(world.IsHotItem(itemId)); // 两 demand 皆引用

        world.RevokeContinuityDemand("d1");
        Assert.True(world.IsHotItem(itemId)); // 撤一仍 Hot（d2 在）

        world.RevokeContinuityDemand("d2");
        Assert.False(world.IsHotItem(itemId)); // 撤尽转 Cold
        Assert.Single(world.Current!.LogicalItems!); // item 本体不受 Revoke 影响
    }

    // ---- S15：既有路径零回归（行为级）---------------------------------------------

    [Fact]
    public void S15_LegacyConstructionPathsKeepOccurrencesAndLogicalItemsNull()
    {
        // 旧 1-arg ctor：Occurrences / LogicalItems 为 null（旧语义）
        var world1 = new WorldModel(new HashSet<string> { UIWorldDoubles.Observed });
        var kernel1 = new UniKernel(new EvidenceLedger(), world1, DisabledRunTrace.Instance);
        var rev = kernel1.Process(UIWorldDoubles.Observation("page:a:v1", UIWorldDoubles.T0)).ResultingRevision!;
        Assert.Null(rev.Occurrences);
        Assert.Null(rev.LogicalItems);

        // 旧 2-arg ctor（仅 association strategy）：既有 UIW-001 行为不变
        var world2 = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed }, new SignatureAssociationStrategy());
        var kernel2 = new UniKernel(new EvidenceLedger(), world2, DisabledRunTrace.Instance);
        kernel2.Process(UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T0));
        kernel2.Process(UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T1), Scroll);
        var current = world2.Current!;
        Assert.Null(current.Occurrences);
        Assert.Null(current.LogicalItems);
        Assert.Single(current.Containers); // scroll continuity 行为不变
        Assert.True(world2.AssociationLog[^1].EffectiveKind == AssociationDispositionKind.Matched);
    }
}
