using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// UIW-001 验收 S1–S8 —— UIWorld 核心 vertical slice（UWM-009 §32 +
/// baseline L4）。全部确定性（level: DETERMINISTIC）；strategy 为
/// deterministic seam double，无真实 Vector/VLM。
/// </summary>
public sealed class UIWorldAssociationTests
{
    private static readonly TransitionContext Scroll = new("scroll", "attempt-1", TransitionStrength.Attempt);
    private static readonly TransitionContext Navigate = new("navigate", "attempt-2", TransitionStrength.EffectFlow);

    // ---- S1：First Container ------------------------------------------------

    [Fact]
    public void S1_FirstObservationEstablishesNewCanonicalContainerIdentity()
    {
        var kernel = UIWorldDoubles.NewKernel(new SignatureAssociationStrategy());

        var result = kernel.Process(UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T0));

        var revision = result.ResultingRevision!;
        var evidenceId = result.Admission.EvidenceId!;
        var decision = Assert.Single(kernel.AssociationLog);

        // AssociationDisposition = New，canonical identity 建立
        Assert.Equal(AssociationDispositionKind.New, decision.ProposedKind);
        Assert.Equal(AssociationDispositionKind.New, decision.EffectiveKind);
        var containerId = decision.EstablishedContainerId!;
        Assert.StartsWith("ctr-", containerId);

        // revision 携带 container belief（identity + evidence basis）
        var container = Assert.Single(revision.Containers);
        Assert.Equal(containerId, container.Identity.ContainerId);
        Assert.Equal(new[] { evidenceId }, container.EvidenceBasis);

        // CurrentContainer / signature 以 owner-derived claims 表达（WorldState 语义）
        Assert.Equal(containerId, revision.WorldState[WorldModel.CurrentContainerSubject].Value);
        Assert.Equal("page:settings:v1",
            revision.WorldState[WorldModel.SignatureSubjectPrefix + containerId].Value);
        Assert.Contains(evidenceId, revision.EvidenceBasis);
    }

    // ---- S2：Scroll Continuity（P22 buyer）----------------------------------

    [Fact]
    public void S2_ScrollTransitionContextPlusConsistentEvidenceMatchesSameIdentity()
    {
        var kernel = UIWorldDoubles.NewKernel(new SignatureAssociationStrategy());
        var first = kernel.Process(UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T0));
        var containerA = Assert.Single(first.ResultingRevision!.Containers).Identity.ContainerId;

        var second = kernel.Process(
            UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T1), Scroll);

        var revision = second.ResultingRevision!;
        // Matched A：identity 不变，basis 延续
        var container = Assert.Single(revision.Containers);
        Assert.Equal(containerA, container.Identity.ContainerId);
        Assert.Contains(first.Admission.EvidenceId!, container.EvidenceBasis);
        Assert.Contains(second.Admission.EvidenceId!, container.EvidenceBasis);
        Assert.Equal(containerA, revision.WorldState[WorldModel.CurrentContainerSubject].Value);

        // decision context 可关联（R-UW-02/03）
        var decision = kernel.AssociationLog[^1];
        Assert.Equal(AssociationDispositionKind.Matched, decision.EffectiveKind);
        Assert.Equal(containerA, decision.MatchedContainerId);
        Assert.Equal("attempt-1", decision.TransitionCorrelation);
        Assert.Equal(TransitionStrength.Attempt, decision.TransitionStrength);
    }

    [Fact]
    public void S2b_PriorAloneCannotMatchOrMutateCanonicalBelief()
    {
        // (a) TransitionContext 没有独立于 accepted evidence 的作用路径：
        //     无 evidence（irrelevant subject）→ 零 revision、belief 不动
        var kernel = UIWorldDoubles.NewKernel(new SignatureAssociationStrategy());
        kernel.Process(UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T0));
        var before = kernel.CurrentBelief!;

        var irrelevant = kernel.Process(
            UIWorldDoubles.SubjectObservation("device.battery", "80", UIWorldDoubles.T1), Scroll);
        Assert.Null(irrelevant.ResultingRevision);
        Assert.Same(before, kernel.CurrentBelief);

        // (b) prior-only Matched proposal（support 为空）被 Authority gate 降级
        var scripted = new ScriptedAssociationStrategy(
            UIWorldDoubles.ScriptedNew("establish"),
            i => new AssociationProposal(
                AssociationDispositionKind.Matched,
                MatchedContainerId: i.Previous!.Containers[0].Identity.ContainerId,
                new[] { new AssociationCandidate(
                    i.Previous.Containers[0].Identity.ContainerId,
                    Array.Empty<string>(), Array.Empty<string>()) },
                Relations: Array.Empty<ProposedRelation>(), Reason: "scroll-prior-alone"));
        var kernel2 = UIWorldDoubles.NewKernel(scripted);
        var establish = kernel2.Process(UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T0));
        var established = Assert.Single(establish.ResultingRevision!.Containers);

        kernel2.Process(UIWorldDoubles.Observation("page:settings:v2", UIWorldDoubles.T1), Scroll);

        var decision = kernel2.AssociationLog[^1];
        Assert.Equal(AssociationDispositionKind.Matched, decision.ProposedKind);
        Assert.Equal(AssociationDispositionKind.Insufficient, decision.EffectiveKind); // 降级
        var only = Assert.Single(kernel2.CurrentBelief!.Containers);
        Assert.Equal(established.Identity.ContainerId, only.Identity.ContainerId);
        Assert.Equal(established.EvidenceBasis, only.EvidenceBasis); // basis 未被 prior 扩写
    }

    // ---- S3：Prior 不能压过 Evidence -----------------------------------------

    [Fact]
    public void S3_StrongContradictionBeatsScrollPrior_NoForcedMatch()
    {
        var kernel = UIWorldDoubles.NewKernel(new SignatureAssociationStrategy());
        kernel.Process(UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T0));
        var containerA = Assert.Single(kernel.CurrentBelief!.Containers).Identity.ContainerId;

        // scroll prior + 语义反证 → 不强判 Matched
        var contradicted = kernel.Process(
            UIWorldDoubles.Observation("contradicts:page:settings:v1", UIWorldDoubles.T1), Scroll);

        var decision = kernel.AssociationLog[^1];
        Assert.Equal(AssociationDispositionKind.Insufficient, decision.EffectiveKind);
        Assert.Null(decision.MatchedContainerId);
        var container = Assert.Single(contradicted.ResultingRevision!.Containers);
        Assert.Equal(containerA, container.Identity.ContainerId); // identity 未被改写

        // 反向证明：navigate prior 建议换容器，但 signature evidence 决定 Matched
        kernel.Process(UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T2), Navigate);
        Assert.Equal(AssociationDispositionKind.Matched, kernel.AssociationLog[^1].EffectiveKind);
        Assert.Equal(containerA, kernel.AssociationLog[^1].MatchedContainerId);
    }

    // ---- S4：相似外观 ≠ 同一身份（Authority backstop）------------------------

    [Fact]
    public void S4_HighSimilarityMatchWithContradictingEvidenceIsBlockedByAuthority()
    {
        // 劣质 strategy：视觉高相似 → 提议 Matched，但同一 evidence 同时列为反证。
        // WorldModel Authority gate 必须拒绝（P-UW-04 / R-UW-05：反证与支持不可压平）。
        var scripted = new ScriptedAssociationStrategy(
            UIWorldDoubles.ScriptedNew("establish"),
            i =>
            {
                var id = i.Previous!.Containers[0].Identity.ContainerId;
                return new AssociationProposal(
                    AssociationDispositionKind.Matched, MatchedContainerId: id,
                    new[] { new AssociationCandidate(id,
                        new[] { i.Current.EvidenceId }, new[] { i.Current.EvidenceId }) },
                    Relations: Array.Empty<ProposedRelation>(), Reason: "visual-similarity-with-semantic-contradiction");
            });
        var kernel = UIWorldDoubles.NewKernel(scripted);
        var establish = kernel.Process(UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T0));
        var established = Assert.Single(establish.ResultingRevision!.Containers);

        var blocked = kernel.Process(UIWorldDoubles.Observation("similar:settings:v1", UIWorldDoubles.T1));

        var decision = kernel.AssociationLog[^1];
        Assert.Equal(AssociationDispositionKind.Matched, decision.ProposedKind);
        Assert.Equal(AssociationDispositionKind.Insufficient, decision.EffectiveKind); // gate 降级
        Assert.Null(decision.MatchedContainerId);
        var only = Assert.Single(blocked.ResultingRevision!.Containers);
        Assert.Equal(established.Identity.ContainerId, only.Identity.ContainerId); // 无假匹配
        Assert.Equal(established.EvidenceBasis, only.EvidenceBasis);
    }

    // ---- S5：Ambiguous --------------------------------------------------------

    [Fact]
    public void S5_AmbiguousLeavesIdentityUnmutated()
    {
        var kernel = UIWorldDoubles.NewKernel(new SignatureAssociationStrategy());
        kernel.Process(UIWorldDoubles.Observation("page:a:v1", UIWorldDoubles.T0));
        kernel.Process(UIWorldDoubles.Observation("page:b:v1", UIWorldDoubles.T1)); // 无 transition → New B
        var two = kernel.CurrentBelief!;
        Assert.Equal(2, two.Containers.Count);
        var currentBefore = two.WorldState[WorldModel.CurrentContainerSubject].Value;

        var ambiguous = kernel.Process(UIWorldDoubles.Observation("ambiguous:???:", UIWorldDoubles.T2));

        var decision = kernel.AssociationLog[^1];
        Assert.Equal(AssociationDispositionKind.Ambiguous, decision.EffectiveKind);
        var revision = ambiguous.ResultingRevision!;
        Assert.Equal(2, revision.Containers.Count); // 无 identity 创建/替换
        Assert.Equal(two.Containers[0].EvidenceBasis, revision.Containers[0].EvidenceBasis);
        Assert.Equal(two.Containers[1].EvidenceBasis, revision.Containers[1].EvidenceBasis);
        Assert.Equal(currentBefore, revision.WorldState[WorldModel.CurrentContainerSubject].Value); // claim 不动
    }

    // ---- S6：Insufficient ≠ New ----------------------------------------------

    [Fact]
    public void S6_InsufficientObservationDoesNotCreateNewIdentity()
    {
        var kernel = UIWorldDoubles.NewKernel(new SignatureAssociationStrategy());
        kernel.Process(UIWorldDoubles.Observation("page:a:v1", UIWorldDoubles.T0));
        var containerA = Assert.Single(kernel.CurrentBelief!.Containers);

        var glimpse = kernel.Process(UIWorldDoubles.Observation("glimpse:par", UIWorldDoubles.T1));

        Assert.Equal(AssociationDispositionKind.Insufficient, kernel.AssociationLog[^1].EffectiveKind);
        var revision = glimpse.ResultingRevision!;
        var still = Assert.Single(revision.Containers);
        Assert.Equal(containerA.Identity.ContainerId, still.Identity.ContainerId); // 不是 New
    }

    [Fact]
    public void S6b_NewWithoutEvidenceBackingIsDegradedToInsufficient()
    {
        // 劣质 strategy：直接提议 New（零 evidence support）→ Authority 降级，不建身份
        var scripted = new ScriptedAssociationStrategy(
            i => new AssociationProposal(
                AssociationDispositionKind.New, MatchedContainerId: null,
                Candidates: Array.Empty<AssociationCandidate>(),
                Relations: Array.Empty<ProposedRelation>(), Reason: "new-from-nothing"));
        var kernel = UIWorldDoubles.NewKernel(scripted);

        kernel.Process(UIWorldDoubles.Observation("page:a:v1", UIWorldDoubles.T0));

        var decision = Assert.Single(kernel.AssociationLog);
        Assert.Equal(AssociationDispositionKind.New, decision.ProposedKind);
        Assert.Equal(AssociationDispositionKind.Insufficient, decision.EffectiveKind);
        Assert.Null(decision.EstablishedContainerId);
        Assert.Empty(kernel.CurrentBelief!.Containers);
    }

    // ---- S7：Conflict（association 路径与既有 conflict 机制共存）--------------

    [Fact]
    public void S7_ConflictingAcceptedEvidenceRecordsConflict_NoLatestWins()
    {
        var kernel = UIWorldDoubles.NewKernel(new SignatureAssociationStrategy());
        var first = kernel.Process(UIWorldDoubles.Observation("page:a:v1", UIWorldDoubles.T0));
        var second = kernel.Process(UIWorldDoubles.Observation("page:b:v1", UIWorldDoubles.T1));

        var revision = second.ResultingRevision!;
        var conflict = Assert.Single(revision.Conflicts);
        Assert.Equal(UIWorldDoubles.Observed, conflict.Subject);
        Assert.Equal("page:a:v1", conflict.EstablishedValue);
        Assert.Equal("page:b:v1", conflict.ChallengingValue);
        Assert.Equal(first.Admission.EvidenceId, conflict.EstablishedEvidenceId);
        Assert.Equal(second.Admission.EvidenceId, conflict.ChallengingEvidenceId);
        // established 未被覆盖（latest 不获胜）
        Assert.Equal("page:a:v1", revision.WorldState[UIWorldDoubles.Observed].Value);
    }

    // ---- S8：Slice invariants -------------------------------------------------

    [Fact]
    public void S8_SliceIsRevisionBoundScopedImmutable_OmissionIsNotAbsence()
    {
        // UIW-004 迁移：Slice 重构为 container-anchored 新形状——scope 以
        // rootContainerId 锚定；「scoped claim 携值 / omission ≠ absence」经
        // 分区约定 subject <containerId>.<rest> 表达（场景事实不变）。
        // 首条 evidence 的 EvidenceId 内容确定性 → probe ledger 预知 minted id。
        var seedObservation = UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T0);
        var (probeAdmission, _) = new EvidenceLedger().Admit(seedObservation);
        var root = "ctr-" + probeAdmission.EvidenceId![3..15];
        var scopedSpatial = $"{root}.spatial.screen.bounds";
        var kernel = UIWorldDoubles.NewKernel(
            new SignatureAssociationStrategy(), scopedSpatial, "spatial.bounds");
        kernel.Process(seedObservation);
        kernel.Process(UIWorldDoubles.SubjectObservation(scopedSpatial, "0,0,800,600", UIWorldDoubles.T1));
        var revision = kernel.CurrentBelief!;

        // revision-bound + root-anchored + 默认 InScope = {root}
        var slice = kernel.DeriveSlice(root);
        Assert.Equal(revision.RevisionId, slice.SourceRevisionId);
        Assert.Equal(root, slice.RootContainerId);
        Assert.Equal(new[] { root }, slice.InScopeContainerIds);
        // 分区内的 claim 入 ScopedClaims
        Assert.Contains(scopedSpatial, slice.ScopedClaims.Keys);

        // omission ≠ absence：分区外的 claim 不在 slice，但仍在 belief 中
        Assert.False(slice.ScopedClaims.ContainsKey(UIWorldDoubles.Observed));
        Assert.True(revision.WorldState.ContainsKey(UIWorldDoubles.Observed));

        // spatial value 暴露于 Slice 时必须携带 explicit SpatialFrame（subject 段结构）
        Assert.Equal("0,0,800,600", slice.ScopedClaims[scopedSpatial]);
        Assert.Equal("screen", scopedSpatial.Split('.')[2]); // frame = containerId 后第 1 段

        // 裸 spatial subject（无 frame）fail-closed（P-UW-16 owner 侧执法）
        Assert.Throws<ArgumentException>(() => kernel.Process(
            UIWorldDoubles.SubjectObservation("spatial.bounds", "1,2,3", UIWorldDoubles.T2)));

        // immutability：世界推进后旧 slice 派生失效，本体不被改写（E2B 验收 6 语义）
        var stale = slice;
        kernel.Process(UIWorldDoubles.Observation("page:other:v1", UIWorldDoubles.T2));
        Assert.False(kernel.IsSliceValid(stale));
        Assert.True(kernel.IsSliceValid(kernel.DeriveSlice(root)));
        Assert.Equal(revision.RevisionId, stale.SourceRevisionId); // 旧 slice 本体未变
    }

    // ---- 补充：graph relations 是 evidence-backed belief claims（D3）---------

    [Fact]
    public void RelationsAreRevisionBoundEvidenceBackedClaims_InvalidOnesSkipped()
    {
        var kernel = UIWorldDoubles.NewKernel(new ScriptedAssociationStrategy(
            UIWorldDoubles.ScriptedNew("establish-a"),
            UIWorldDoubles.ScriptedNew("establish-b"),
            i =>
            {
                var a = i.Previous!.Containers[0].Identity.ContainerId;
                var b = i.Previous.Containers[1].Identity.ContainerId;
                return new AssociationProposal(
                    AssociationDispositionKind.Insufficient, MatchedContainerId: null,
                    Candidates: Array.Empty<AssociationCandidate>(),
                    Relations: new[]
                    {
                        // 无效 backing（伪造 id）→ 必须被跳过
                        new ProposedRelation(ContainerRelationKind.Contains, a, b, new[] { "ev-bogus" }),
                        // 有效 backing（当前真实 evidence）→ 进入 belief
                        new ProposedRelation(ContainerRelationKind.Overlays, b, a, new[] { i.Current.EvidenceId }),
                    },
                    Reason: "carry-relations");
            }));

        var first = kernel.Process(UIWorldDoubles.Observation("page:a:v1", UIWorldDoubles.T0));
        var second = kernel.Process(UIWorldDoubles.Observation("page:b:v1", UIWorldDoubles.T1));
        var idA = Assert.Single(first.ResultingRevision!.Containers).Identity.ContainerId;
        var idB = second.ResultingRevision!.Containers.Single(c => c.Identity.ContainerId != idA)
            .Identity.ContainerId;

        var last = kernel.Process(UIWorldDoubles.Observation("page:a:v1", UIWorldDoubles.T2),
            new TransitionContext("scroll", "attempt-3", TransitionStrength.Attempt));

        var relation = Assert.Single(last.ResultingRevision!.Relations);
        Assert.Equal(ContainerRelationKind.Overlays, relation.Kind);
        Assert.Equal(idB, relation.SourceContainerId);
        Assert.Equal(idA, relation.TargetContainerId);
        Assert.Equal(last.Admission.EvidenceId, Assert.Single(relation.EvidenceBasis));
    }
}
