using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class WorldObservedNavigationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    private static string EvidenceId(int sequence) =>
        "ev-" + sequence.ToString("x2") + "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd";

    private static EvidenceRecord PageObservation(string pageContext, int sequence) => new(
        EvidenceId(sequence),
        new ObservationClaim("ui.screen", pageContext),
        IngressKind.Observation,
        ObservationContext.External,
        new Provenance("settings-feed", T0.AddSeconds(sequence),
            $"scope:ui.screen:{sequence}", new[] { $"capture:{sequence}" }));

    private static WorldModel CreateWorld() =>
        new(new HashSet<string> { "ui.screen" }, new ProductAssociationStrategy());

    private static WorldBeliefRevision Observe(WorldModel world, string pageContext, int sequence)
    {
        var record = PageObservation(pageContext, sequence);
        return world.Reconcile(record, world.JudgeRelevance(record));
    }

    private static string CurrentPage(WorldBeliefRevision revision) =>
        revision.WorldState[WorldModel.CurrentContainerSubject].Value;

    private static ObservedNavigation Verified(
        WorldBeliefRevision sourceRevision,
        string source,
        string sourceEvidenceId,
        WorldBeliefRevision targetRevision,
        string target,
        string targetEvidenceId,
        string scope = "run:settings-coverage-v1") => new(
        sourceRevision.RevisionId,
        source,
        sourceEvidenceId,
        targetRevision.RevisionId,
        target,
        targetEvidenceId,
        scope,
        "verification:post-action:verified");

    [Fact]
    public void VerifiedEntryAndReturn_CreateObservedEdges_AndProjectionUsesCurrentAnchor()
    {
        var world = CreateWorld();
        var rootRevision = Observe(world, "root", 1);
        var root = CurrentPage(rootRevision);

        var childRevision = Observe(world, "root/entry:network", 2);
        var child = CurrentPage(childRevision);
        Assert.NotEqual(root, child);
        Assert.Empty(childRevision.Relations!);

        Assert.True(world.RecordObservedNavigation(Verified(
            rootRevision, root, EvidenceId(1), childRevision, child, EvidenceId(2))));

        var returnSourceRevision = world.Current!;
        var returnRevision = Observe(world, "root", 3);
        Assert.Equal(root, CurrentPage(returnRevision));
        Assert.True(world.RecordObservedNavigation(Verified(
            returnSourceRevision, child, EvidenceId(2), returnRevision, root, EvidenceId(3))));

        var projection = world.DeriveGraphProjection(world.Current!.RevisionId, "run:settings-coverage-v1");
        Assert.Equal(root, projection.CurrentContainerId);
        Assert.Equal(2, projection.Nodes.Count);
        Assert.Equal(2, projection.Edges.Count);
        Assert.Contains(projection.Edges, edge => edge.SourceContainerId == root && edge.TargetContainerId == child);
        Assert.Contains(projection.Edges, edge => edge.SourceContainerId == child && edge.TargetContainerId == root);
        Assert.All(projection.Edges, edge => Assert.Equal("run:settings-coverage-v1", edge.Scope));
        Assert.All(projection.Edges, edge => Assert.NotNull(edge.VerificationSourceRef));
        Assert.Contains("navigation-graph-is-observation-bounded", projection.Unknown);
        Assert.True(world.RecordObservedNavigation(Verified(
            rootRevision, root, EvidenceId(1), childRevision, child, EvidenceId(2))));
        var otherScope = world.DeriveGraphProjection(world.Current!.RevisionId, "run:other-contract");
        Assert.Empty(otherScope.Edges);
        Assert.Contains("no-navigation-edge-observed-in-scope", otherScope.Unknown);
    }

    [Fact]
    public void SamePageContextAcrossScrollObservations_KeepsIdentity_AndUnverifiedAttemptAddsNoEdge()
    {
        var world = CreateWorld();
        var root = Observe(world, "root", 1);
        var child = Observe(world, "root/entry:display", 2);
        var childId = CurrentPage(child);
        var scrolled = Observe(world, "root/entry:display", 3);

        Assert.Equal(childId, CurrentPage(scrolled));
        Assert.Empty(scrolled.Relations!);
        Assert.False(world.RecordObservedNavigation(Verified(
            root, CurrentPage(root), EvidenceId(999), child, childId, EvidenceId(2))));

        var projection = world.DeriveGraphProjection(scrolled.RevisionId, "run:settings-coverage-v1");
        Assert.Empty(projection.Edges);
        Assert.Equal(childId, projection.CurrentContainerId);
        Assert.Contains("no-navigation-edge-observed-in-scope", projection.Unknown);
    }

    [Fact]
    public void IdenticallyNamedPagesWithDifferentParentContext_KeepSeparateIdentities()
    {
        var world = CreateWorld();
        var root = Observe(world, "root", 1);
        var detailFromA = Observe(world, "root/branch-a/detail", 2);
        var detailAId = CurrentPage(detailFromA);
        var rootAgain = Observe(world, "root", 3);
        Assert.Equal(CurrentPage(root), CurrentPage(rootAgain));

        var detailFromB = Observe(world, "root/branch-b/detail", 4);

        Assert.NotEqual(detailAId, CurrentPage(detailFromB));
        Assert.Equal(3, detailFromB.Containers!.Count);
        Assert.Equal(3, world.DeriveGraphProjection(detailFromB.RevisionId, "run:settings-coverage-v1").Nodes.Count);
    }

    [Fact]
    public void TwoObservedEntrancesFromOnePage_AreBothRetained()
    {
        var world = CreateWorld();
        var root = Observe(world, "root", 1);
        var rootId = CurrentPage(root);

        var firstEntry = Observe(world, "root/entry:display", 2);
        var firstId = CurrentPage(firstEntry);
        Assert.True(world.RecordObservedNavigation(Verified(
            root, rootId, EvidenceId(1), firstEntry, firstId, EvidenceId(2))));

        var returnSourceRevision = world.Current!;
        var returnedToRoot = Observe(world, "root", 3);
        Assert.Equal(rootId, CurrentPage(returnedToRoot));
        Assert.True(world.RecordObservedNavigation(Verified(
            returnSourceRevision, firstId, EvidenceId(2), returnedToRoot, rootId, EvidenceId(3))));

        var secondSourceRevision = world.Current!;
        var secondEntry = Observe(world, "root/entry:sound", 4);
        var secondId = CurrentPage(secondEntry);
        Assert.True(world.RecordObservedNavigation(Verified(
            secondSourceRevision, rootId, EvidenceId(3), secondEntry, secondId, EvidenceId(4))));

        var projection = world.DeriveGraphProjection(world.Current!.RevisionId, "run:settings-coverage-v1");
        Assert.Equal(2, projection.Edges.Count(edge => edge.SourceContainerId == rootId));
        Assert.Contains(projection.Edges, edge => edge.TargetContainerId == firstId);
        Assert.Contains(projection.Edges, edge => edge.TargetContainerId == secondId);
    }

    [Fact]
    public void NavigationRegistrationIsIdempotentAndRejectsUnknownRevisionOrEvidence()
    {
        var world = CreateWorld();
        var root = Observe(world, "root", 1);
        var child = Observe(world, "root/entry:privacy", 2);
        var nav = Verified(root, CurrentPage(root), EvidenceId(1), child, CurrentPage(child), EvidenceId(2));

        Assert.True(world.RecordObservedNavigation(nav));
        var revisionAfterFirstRegistration = world.Current!.RevisionId;
        Assert.True(world.RecordObservedNavigation(nav));
        Assert.Equal(revisionAfterFirstRegistration, world.Current!.RevisionId);

        var invalidRevision = nav with { SourceRevisionId = "rev-missing" };
        Assert.False(world.RecordObservedNavigation(invalidRevision));
        var invalidEvidence = nav with { TargetObservationEvidenceId = EvidenceId(77) };
        Assert.False(world.RecordObservedNavigation(invalidEvidence));
        var reverseTime = nav with
        {
            SourceRevisionId = nav.TargetRevisionId,
            SourceContainerId = nav.TargetContainerId,
            SourceObservationEvidenceId = nav.TargetObservationEvidenceId,
            TargetRevisionId = nav.SourceRevisionId,
            TargetContainerId = nav.SourceContainerId,
            TargetObservationEvidenceId = nav.SourceObservationEvidenceId,
        };
        Assert.False(world.RecordObservedNavigation(reverseTime));
    }

    [Fact]
    public void AssociationProposalAloneCannotAssertSuccessfulNavigation()
    {
        var world = CreateWorld();
        var root = Observe(world, "root", 1);
        var child = Observe(world, "root/entry:network", 2);
        var rootId = CurrentPage(root);
        var childId = CurrentPage(child);

        var untrusted = new WorldModel(
            new HashSet<string> { "ui.screen" },
            new AssociationProposalStrategy(rootId, childId));
        var first = PageObservation("root", 1);
        untrusted.Reconcile(first, untrusted.JudgeRelevance(first));
        var second = PageObservation("root/entry:network", 2);
        untrusted.Reconcile(second, untrusted.JudgeRelevance(second));
        var repeat = PageObservation("root/entry:network", 3);
        var resulting = untrusted.Reconcile(repeat, untrusted.JudgeRelevance(repeat));

        Assert.DoesNotContain(resulting.Relations!, relation => relation.Kind == ContainerRelationKind.NavigatesTo);
    }

    private sealed class AssociationProposalStrategy(string sourceId, string targetId) : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input)
        {
            if (input.Previous is null || input.Previous.Containers?.All(
                    container => container.Identity.ContainerId != targetId) != false)
                return new AssociationProposal(AssociationDispositionKind.New, null,
                    new[] { new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
                    Array.Empty<ProposedRelation>(), "test-new");

            return new AssociationProposal(AssociationDispositionKind.Matched, targetId,
                new[] { new AssociationCandidate(targetId, new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
                new[] { new ProposedRelation(ContainerRelationKind.NavigatesTo, sourceId, targetId,
                    new[] { input.Current.EvidenceId }) },
                "untrusted-navigation-proposal");
        }
    }
}
