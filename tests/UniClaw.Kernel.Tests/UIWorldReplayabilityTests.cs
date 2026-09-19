using System.Reflection;
using Xunit;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

using UniClaw.Kernel.Trace;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// UIW-001 验收 S9–S12 —— Replayability Readiness（R-UW-01..07）。
/// 这些不是 Replay Engine 测试：只验证未来 Trace/Replay/Simulation 所需的
/// product-side properties（可引用性 / 可重执行 / 可分支 / 无隐式 canonical 输入）。
/// ReplayFixture 是测试工具，不是产品协议（不提交为 cross-component DTO）。
/// </summary>
public sealed class UIWorldReplayabilityTests
{
    private static readonly TransitionContext Scroll = new("scroll", "attempt-1", TransitionStrength.Attempt);

    /// <summary>
    /// ReplayFixture（测试工具）：重放一次 UIWorld lifecycle 所需的全部语义输入
    /// —— accepted evidence proposals（含 provenance）+ P22 TransitionContext? +
    /// deterministic AssociationStrategy factory。经真实 WorldModel semantic
    /// boundary（Admit → JudgeRelevance → Reconcile）重新驱动。
    /// </summary>
    private sealed record ReplayFixture(
        IReadOnlyList<(ObservationProposal Input, TransitionContext? Transition)> Inputs,
        Func<IAssociationStrategy> StrategyFactory);

    private static ReplayFixture ScrollContinuityFixture() => new(
        new (ObservationProposal, TransitionContext?)[]
        {
            (UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T0), null),
            (UIWorldDoubles.Observation("page:settings:v1", UIWorldDoubles.T1), Scroll),
        },
        () => new SignatureAssociationStrategy());

    private static (UniKernel Kernel, WorldModel World) Run(ReplayFixture fixture)
    {
        var scope = new HashSet<string> { UIWorldDoubles.Observed };
        var world = new WorldModel(scope, fixture.StrategyFactory());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
        foreach (var (input, transition) in fixture.Inputs)
            kernel.Process(input, transition);
        return (kernel, world);
    }

    // ---- S9：Lifecycle Referenceability（R-UW-01/02/03/04）-------------------

    [Fact]
    public void S9_LifecycleCanonicalArtifactsAreReferenceableAndCausallyLinked()
    {
        var (kernel, world) = Run(ScrollContinuityFixture());
        var rev1 = world.RevisionHistory[0];
        var rev2 = world.RevisionHistory[1];

        // R-UW-01：canonical identity/reference 存在（evidence / revisions /
        // container identity / slice source / P22 provenance）
        Assert.Matches("^ev-[0-9a-f]{64}$", rev1.EvidenceBasis.Single());
        Assert.Matches("^rev-[0-9]+$", rev1.RevisionId);
        Assert.Matches("^rev-[0-9]+$", rev2.RevisionId);
        var containerId = Assert.Single(rev2.Containers).Identity.ContainerId;
        Assert.Matches("^ctr-[0-9a-f]+$", containerId);

        // R-UW-02：revision causality 可恢复（parent / evidence / transition）
        Assert.Equal(rev1.RevisionId, rev2.ParentRevisionId);
        Assert.True(rev2.EvidenceBasis.Count > rev1.EvidenceBasis.Count);
        Assert.All(rev1.EvidenceBasis, id => Assert.Contains(id, rev2.EvidenceBasis));
        var decision = world.AssociationLog[^1];
        Assert.Equal(rev2.EvidenceBasis.Single(e => !rev1.EvidenceBasis.Contains(e)), decision.EvidenceId);
        Assert.Equal(rev2.RevisionId, decision.RevisionId);
        Assert.Equal("attempt-1", decision.TransitionCorrelation);        // P22 provenance
        Assert.Equal(TransitionStrength.Attempt, decision.TransitionStrength);

        // R-UW-03：identity 变异可解释（decision context + evidence basis 均可追溯）
        var firstDecision = world.AssociationLog[0];
        Assert.Equal(AssociationDispositionKind.New, firstDecision.EffectiveKind);
        Assert.Equal(AssociationDispositionKind.Matched, decision.EffectiveKind);
        Assert.Equal(containerId, firstDecision.EstablishedContainerId);
        Assert.Equal(containerId, decision.MatchedContainerId);
        var containerBasis = Assert.Single(rev2.Containers, c => c.Identity.ContainerId == containerId).EvidenceBasis;
        Assert.Contains(firstDecision.EvidenceId, containerBasis);
        Assert.Contains(decision.EvidenceId, containerBasis);

        // R-UW-04：Slice 指回 source revision
        var slice = kernel.DeriveSlice(containerId);
        Assert.Equal(rev2.RevisionId, slice.SourceRevisionId);

        // R-UW-05：prior 与 evidence basis 可区分（decision 分开携带两者）
        Assert.NotEqual(decision.EvidenceId, decision.TransitionCorrelation);
    }

    // ---- S10：Re-executable Semantic Input（R-UW-07）--------------------------

    [Fact]
    public void S10_ReplayingSameSemanticInputsYieldsSemanticallyEquivalentBelief()
    {
        var fixture = ScrollContinuityFixture();
        var (originalKernel, originalWorld) = Run(fixture);
        var (replayedKernel, replayedWorld) = Run(fixture);

        var a = originalWorld.Current!;
        var b = replayedWorld.Current!;

        // 同 AssociationDisposition 序列（含 matched/established identity / prior 关联）
        Assert.Equal(
            originalWorld.AssociationLog.Select(d => (d.ProposedKind, d.EffectiveKind,
                d.MatchedContainerId, d.EstablishedContainerId, d.TransitionCorrelation)),
            replayedWorld.AssociationLog.Select(d => (d.ProposedKind, d.EffectiveKind,
                d.MatchedContainerId, d.EstablishedContainerId, d.TransitionCorrelation)));

        // 同 ContainerIdentity outcome（铸造确定性：同 evidence → 同 identity）
        Assert.Equal(
            a.Containers.Select(c => (c.Identity.ContainerId, Basis: string.Join(",", c.EvidenceBasis.OrderBy(x => x)))),
            b.Containers.Select(c => (c.Identity.ContainerId, Basis: string.Join(",", c.EvidenceBasis.OrderBy(x => x)))));

        // 语义等价的 WorldBelief（revision identity / claims / basis / conflicts / uncertainty）
        Assert.Equal(a.RevisionId, b.RevisionId);
        Assert.Equal(a.ParentRevisionId, b.ParentRevisionId);
        Assert.Equal(
            a.WorldState.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value, kv.Value.EvidenceId)),
            b.WorldState.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value, kv.Value.EvidenceId)));
        Assert.Equal(a.EvidenceBasis, b.EvidenceBasis);
        Assert.Equal(a.Conflicts.Count, b.Conflicts.Count);
        Assert.Equal(a.Uncertainty, b.Uncertainty);
        Assert.Equal(a.FreshnessBasis, b.FreshnessBasis);
    }

    // ---- S11：Counterfactual Branch Safety ------------------------------------

    [Fact]
    public void S11_BranchesFromSamePriorDivergeWithoutMutatingHistoryOrSiblings()
    {
        // R0：原实例
        var world0 = new WorldModel(new HashSet<string> { UIWorldDoubles.Observed },
            new SignatureAssociationStrategy());
        var kernel0 = new UniKernel(new EvidenceLedger(), world0, DisabledRunTrace.Instance);
        var r0Result = kernel0.Process(UIWorldDoubles.Observation("page:home:v1", UIWorldDoubles.T0));
        var r0 = r0Result.ResultingRevision!;

        // R0 ├─ Evidence A → RA（独立 replay 实例）
        var (kernelA, worldA) = Run(new ReplayFixture(
            new (ObservationProposal, TransitionContext?)[]
            {
                (UIWorldDoubles.Observation("page:home:v1", UIWorldDoubles.T0), null),
                (UIWorldDoubles.Observation("page:branch-a:v1", UIWorldDoubles.T1), null),
            },
            () => new SignatureAssociationStrategy()));
        var ra = worldA.Current!;

        // R0 ├─ Evidence B → RB（另一独立实例；A 已完成之后才运行 B）
        var (kernelB, worldB) = Run(new ReplayFixture(
            new (ObservationProposal, TransitionContext?)[]
            {
                (UIWorldDoubles.Observation("page:home:v1", UIWorldDoubles.T0), null),
                (UIWorldDoubles.Observation("page:branch-b:v1", UIWorldDoubles.T1), null),
            },
            () => new SignatureAssociationStrategy()));
        var rb = worldB.Current!;

        // 分叉前提：两分支的 R0 前缀与原 R0 等价（同输入同结果）
        Assert.Equal(r0.RevisionId, worldA.RevisionHistory[0].RevisionId);
        Assert.Equal(r0.RevisionId, worldB.RevisionHistory[0].RevisionId);
        Assert.Equal(
            r0.WorldState.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value)),
            worldA.RevisionHistory[0].WorldState.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value)));

        // RA != RB：identity 结果不同、signature 不同
        var idA = ra.WorldState[WorldModel.CurrentContainerSubject].Value;
        var idB = rb.WorldState[WorldModel.CurrentContainerSubject].Value;
        Assert.NotEqual(idA, idB);
        Assert.Equal("page:branch-a:v1", ra.WorldState[WorldModel.SignatureSubjectPrefix + idA].Value);
        Assert.Equal("page:branch-b:v1", rb.WorldState[WorldModel.SignatureSubjectPrefix + idB].Value);
        Assert.DoesNotContain(idA, rb.Containers.Select(c => c.Identity.ContainerId));

        // R0 remains unchanged：原实例历史仅 rev-1，内容未动（immutable history）
        var onlyRevision = Assert.Single(world0.RevisionHistory);
        Assert.Same(r0, onlyRevision);
        Assert.Equal("rev-1", r0.RevisionId);
        Assert.Equal(r0Result.Admission.EvidenceId, Assert.Single(r0.EvidenceBasis));
        Assert.Equal("page:home:v1", r0.WorldState[UIWorldDoubles.Observed].Value);

        // RA does not mutate RB：分支隔离（A 完成后 B 仍只反映自身输入）
        Assert.Equal(AssociationDispositionKind.New, worldB.AssociationLog[^1].EffectiveKind);
        Assert.Equal(2, worldB.RevisionHistory.Count);
        Assert.Equal(2, worldA.RevisionHistory.Count);
        Assert.Equal(idB, rb.WorldState[WorldModel.CurrentContainerSubject].Value);
    }

    // ---- S12：Hidden-State Audit（R-UW-06/07）---------------------------------

    [Fact]
    public void S12_NoStaticMutableStateOnUIWorldCanonicalTypes()
    {
        var audited = new[]
        {
            typeof(WorldModel), typeof(ContainerIdentity), typeof(ContainerBelief),
            typeof(ContainerRelation), typeof(AssociationCandidate), typeof(AssociationInput),
            typeof(AssociationProposal), typeof(AssociationDecision), typeof(TransitionContext),
        };

        foreach (var type in audited)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                Assert.True(field.IsInitOnly || field.IsLiteral,
                    $"{type.Name} 携带可变静态字段 {field.Name} —— REPLAYABILITY GAP");
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
                Assert.True(property.SetMethod is null,
                    $"{type.Name} 携带可变静态属性 {property.Name} —— REPLAYABILITY GAP");
        }
    }

    [Fact]
    public void S12b_DeterminismDoubleRunProducesIdenticalCanonicalResult()
    {
        // 隐藏输入（wall clock / randomness / ambient state）的行为学反证：
        // 同输入三次独立执行 → canonical 结果完全一致（S10 的零共享实例版）
        var fixture = ScrollContinuityFixture();
        var runs = new[] { Run(fixture), Run(fixture), Run(fixture) };
        var reference = runs[0].World.Current!;
        foreach (var run in runs.Skip(1))
        {
            Assert.Equal(reference.RevisionId, run.World.Current!.RevisionId);
            Assert.Equal(
                reference.WorldState.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value, kv.Value.EvidenceId)),
                run.World.Current.WorldState.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value, kv.Value.EvidenceId)));
            Assert.Equal(
                reference.Containers.Select(c => c.Identity.ContainerId),
                run.World.Current.Containers.Select(c => c.Identity.ContainerId));
        }
    }
}
