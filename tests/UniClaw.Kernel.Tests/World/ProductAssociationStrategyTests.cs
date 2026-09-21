using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

/// <summary>
/// UIW-005 — 产品 container association realization（HOST-001 前置）：
/// 逐字节 signature 匹配（owner 约定 ui.container.signature.&lt;id&gt; =
/// establishing claim 原文）；四分类词汇完整；确定性；经 WorldModel
/// Authority gates 的端到端语义（首帧铸根容器 / 同屏重看不换身份 /
/// 屏幕变化 = 新身份且留痕 / 空 claim 不断言）。
/// </summary>
public sealed class ProductAssociationStrategyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>真实形态的 evidence id：ev- + 64 位十六进制（MintContainerIdentity 取 [3..15]）。</summary>
    private static string EvidenceId(int seq)
        => "ev-" + seq.ToString("x2") + "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd";

    private static EvidenceRecord FrameEvidence(string value, int seq) => new(
        EvidenceId(seq),
        new ObservationClaim("live.frame", value),
        IngressKind.Observation,
        ObservationContext.External,
        new Provenance("uiw005-test", T0, "scope:live.frame", new[] { $"uiw005:{seq}" }));

    private static AssociationInput Input(string claimValue, WorldBeliefRevision? previous = null)
        => new(previous, FrameEvidence(claimValue, seq: 0), Transition: null);

    private static WorldModel ComposeWorld()
        => new(new HashSet<string> { "live.frame" }, new ProductAssociationStrategy());

    private static void Observe(WorldModel world, string value, int seq)
    {
        var record = FrameEvidence(value, seq);
        world.Reconcile(record, world.JudgeRelevance(record));
    }

    [Fact]
    public void EmptyClaimValue_ProposesInsufficient_NeverAssertsIdentity()
    {
        var proposal = new ProductAssociationStrategy().Propose(Input("  "));

        Assert.Equal(AssociationDispositionKind.Insufficient, proposal.Kind);
        Assert.Null(proposal.MatchedContainerId);
        Assert.Empty(proposal.Candidates);
    }

    [Fact]
    public void NoPreviousContainer_ProposesNew_EvidenceBacked()
    {
        var proposal = new ProductAssociationStrategy().Propose(Input("{\"detects\":\"a\"}"));

        Assert.Equal(AssociationDispositionKind.New, proposal.Kind);
        var candidate = Assert.Single(proposal.Candidates);
        Assert.Equal(EvidenceId(0), Assert.Single(candidate.SupportingEvidenceIds));
    }

    [Fact]
    public void SameClaimValueTwice_KeepsSingleContainerIdentity_Integration()
    {
        var world = ComposeWorld();

        Observe(world, "{\"detects\":\"a\"}", seq: 1);
        var firstRoot = Assert.Single(world.Current!.Containers).Identity.ContainerId;

        Observe(world, "{\"detects\":\"a\"}", seq: 2);

        Assert.Single(world.Current!.Containers);
        Assert.Equal(firstRoot, Assert.Single(world.Current.Containers).Identity.ContainerId);
        var decision = world.AssociationLog.Last();
        Assert.Equal(AssociationDispositionKind.Matched, decision.EffectiveKind);
        Assert.Equal(firstRoot, decision.MatchedContainerId);
    }

    [Fact]
    public void ChangedClaimValue_MintsNewIdentity_AndLeavesTrace()
    {
        var world = ComposeWorld();

        Observe(world, "{\"detects\":\"a\"}", seq: 1);
        var firstRoot = Assert.Single(world.Current!.Containers).Identity.ContainerId;

        Observe(world, "{\"detects\":\"b\"}", seq: 2);
        var current = world.Current!;

        Assert.Equal(2, current.Containers.Count);
        var newRoot = current.Containers.Select(c => c.Identity.ContainerId)
            .Single(id => id != firstRoot);
        var decision = world.AssociationLog.Last();
        Assert.Equal(AssociationDispositionKind.New, decision.EffectiveKind);
        Assert.Equal(newRoot, decision.EstablishedContainerId);
    }

    [Fact]
    public void SameInput_ProducesSameProposal_Determinism()
    {
        var strategy = new ProductAssociationStrategy();
        var input = Input("{\"detects\":\"a\"}");

        // record 数组字段按引用比较，整记录相等不适用——按语义字段断言
        var first = strategy.Propose(input);
        var second = strategy.Propose(input);

        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.MatchedContainerId, second.MatchedContainerId);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.Candidates.Count, second.Candidates.Count);
        Assert.Equal(
            first.Candidates.Single().SupportingEvidenceIds,
            second.Candidates.Single().SupportingEvidenceIds);
    }
}
