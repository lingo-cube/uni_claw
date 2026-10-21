using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class WorldIdentityReconciliationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);

    private static EvidenceRecord Observe(EvidenceLedger ledger, WorldModel world,
        string page, int capturedAt, string? scope = null, string producer = "page-observer")
    {
        var (_, record) = ledger.Admit(new ObservationProposal(new("ui.screen", page),
            IngressKind.Observation, ObservationContext.External,
            new(producer, T0.AddSeconds(capturedAt), scope ?? $"capture:{capturedAt}", ["fixture"])));
        world.Reconcile(record!, world.JudgeRelevance(record!));
        return record!;
    }

    private static string CurrentPage(WorldModel world) =>
        world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;

    [Fact]
    public void OlderPageObservation_RemainsEvidenceWithoutRollingBackCurrentIdentity()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen" }, new ProductAssociationStrategy());
        Observe(ledger, world, "A", 1);
        Observe(ledger, world, "B", 3);
        var current = CurrentPage(world);
        var before = world.Current!;

        var late = Observe(ledger, world, "A", 2);

        Assert.Equal(current, CurrentPage(world));
        Assert.Equal("B", world.Current!.WorldState["ui.screen"].Value);
        Assert.Contains(late.EvidenceId, world.Current.EvidenceBasis);
        Assert.Contains(late.EvidenceId, ledger.CanonicalRecords.Keys);
        Assert.Equal(before.Containers, world.Current.Containers);
        Assert.Empty(world.Current.Relations!);
        Assert.Equal(AssociationDispositionKind.Insufficient, world.AssociationLog[^1].EffectiveKind);
    }

    [Fact]
    public void SameCaptureContradiction_CannotEstablishAnotherContainer()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen" }, new ProductAssociationStrategy());
        var first = Observe(ledger, world, "A", 1, "capture:shared");
        var current = CurrentPage(world);

        var contradiction = Observe(ledger, world, "B", 1, "capture:shared");

        Assert.Equal(current, CurrentPage(world));
        Assert.Single(world.Current!.Containers!);
        Assert.Equal("A", world.Current.WorldState["ui.screen"].Value);
        var conflict = Assert.Single(world.Current.Conflicts);
        Assert.Equal(first.EvidenceId, conflict.EstablishedEvidenceId);
        Assert.Equal(contradiction.EvidenceId, conflict.ChallengingEvidenceId);
        Assert.Equal(AssociationDispositionKind.Insufficient, world.AssociationLog[^1].EffectiveKind);
        Assert.Empty(world.Current.Relations!);
    }

    [Fact]
    public void NewIdentityWithExplicitContradictingEvidence_IsRejectedByWorldOwner()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen" }, new ContradictingNewStrategy());

        Observe(ledger, world, "A", 1);

        Assert.Empty(world.Current!.Containers!);
        Assert.False(world.Current.WorldState.ContainsKey(WorldModel.CurrentContainerSubject));
        Assert.Equal(AssociationDispositionKind.Insufficient, world.AssociationLog[^1].EffectiveKind);
    }

    [Fact]
    public void ReaffirmationAdvancesCurrentObservation_AndFreshReturnCanStillMatch()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen" }, new ProductAssociationStrategy());
        Observe(ledger, world, "A", 1);
        var a = CurrentPage(world);
        Observe(ledger, world, "A", 3);

        Observe(ledger, world, "B", 2);
        Assert.Equal(a, CurrentPage(world));
        Assert.Single(world.Current!.Containers!);

        Observe(ledger, world, "B", 4);
        Assert.NotEqual(a, CurrentPage(world));
        Observe(ledger, world, "A", 5);
        Assert.Equal(a, CurrentPage(world));
        Assert.Equal(2, world.Current!.Containers!.Count);
    }

    private sealed class ContradictingNewStrategy : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => new(
            AssociationDispositionKind.New, null,
            [new("(new)", [input.Current.EvidenceId], [input.Current.EvidenceId])],
            [], "contradicting-new");
    }
}
