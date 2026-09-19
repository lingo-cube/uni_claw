using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Kernel.Tests;

public sealed class WorldModelPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void ResolveCurrent_UsesRevisionRoleIndex_AndPreservesCandidateOrder()
    {
        var observations = Enumerable.Range(0, 100)
            .Select(i => new ProposedOccurrence(
                OwningContainerId: "ctr-1",
                Role: $"role-{i % 10}",
                SemanticDescriptor: $"item-{i}"))
            .ToArray();
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(
            new HashSet<string> { "ui.observed" },
            associationStrategy: null,
            new FixedObservationStrategy(observations));
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);

        kernel.Process(Proposal("ui.observed", "frame-1"));
        var view = world.ResolveCurrent(new TargetDescriptor("role-3"));

        Assert.Equal(
            Enumerable.Range(0, 100).Where(i => i % 10 == 3).Select(i => $"item-{i}"),
            view.Candidates.Select(c => c.SemanticDescriptor));
        Assert.Equal(CurrentCandidateSetResultKind.MultipleCandidates, view.Result);
        var resolution = metrics.WorldModelPerformance[WorldModelOperation.ResolveCurrent];
        Assert.Equal(10, resolution.ScannedEntries);
        Assert.Equal(10, resolution.OutputEntries);
        WriteAggregate("resolve-current", resolution);
    }

    [Fact]
    public void DeriveSlice_UsesContainerIndex_AndPreservesGlobalOccurrenceOrder()
    {
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(
            new HashSet<string> { "ui.observed" },
            new AlwaysNewAssociationStrategy(),
            new AllKnownContainersObservationStrategy(100));
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);
        for (var i = 0; i < 4; i++)
            kernel.Process(Proposal("ui.observed", $"frame-{i}"));

        var current = world.Current!;
        var scope = new[]
        {
            current.Containers![0].Identity.ContainerId,
            current.Containers[2].Identity.ContainerId,
        };
        var expected = current.Occurrences!
            .Where(o => scope.Contains(o.OwningContainerId))
            .Select(o => o.OccurrenceId)
            .ToArray();
        var slice = world.DeriveSlice(scope[0], scope);

        Assert.Equal(expected, slice.Occurrences.Select(o => o.OccurrenceId));
        var derivation = metrics.WorldModelPerformance[WorldModelOperation.DeriveSlice];
        Assert.Equal(scope.Length + expected.Length, derivation.ScannedEntries);
        Assert.True(derivation.ScannedEntries
            < current.Containers!.Count + current.Occurrences!.Count + current.WorldState.Count);
        WriteAggregate("derive-slice", derivation);
    }

    [Fact]
    public void DeriveSlice_UsesContainerClaimIndex_AndPreservesScopedClaimContent()
    {
        var firstProposal = Proposal("ui.observed", "frame-0");
        var seed = new WorldModel(
            new HashSet<string> { "ui.observed" }, new AlwaysNewAssociationStrategy());
        var seedKernel = new UniKernel(new EvidenceLedger(), seed, DisabledRunTrace.Instance);
        seedKernel.Process(firstProposal);
        var root = seed.Current!.Containers!.Single().Identity.ContainerId;
        var claimSubjects = Enumerable.Range(0, 64).Select(i => $"{root}.claim-{i}").ToArray();
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(
            claimSubjects.Append("ui.observed").ToHashSet(),
            new AlwaysNewAssociationStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);
        kernel.Process(firstProposal);
        for (var i = 0; i < claimSubjects.Length; i++)
            kernel.Process(Proposal(claimSubjects[i], $"value-{i}"));

        var slice = world.DeriveSlice(root);

        Assert.Equal(64, slice.ScopedClaims.Count);
        Assert.Equal("value-0", slice.ScopedClaims[$"{root}.claim-0"]);
        Assert.Equal("value-63", slice.ScopedClaims[$"{root}.claim-63"]);
        var derivation = metrics.WorldModelPerformance[WorldModelOperation.DeriveSlice];
        Assert.Equal(65, derivation.ScannedEntries); // root lookup + 64 indexed scoped claims
        Assert.True(derivation.ScannedEntries < world.Current!.WorldState.Count);
        WriteAggregate("derive-slice-scoped-claims", derivation);
    }

    [Fact]
    public void ContainerClaimIndex_RetainsClaimAcceptedBeforeContainerExists()
    {
        var establish = Proposal("ui.establish", "container");
        var seed = new WorldModel(
            new HashSet<string> { "ui.establish" }, new AlwaysNewAssociationStrategy());
        var seedKernel = new UniKernel(new EvidenceLedger(), seed, DisabledRunTrace.Instance);
        seedKernel.Process(establish);
        var futureContainer = seed.Current!.Containers!.Single().Identity.ContainerId;
        var earlySubject = $"{futureContainer}.title";
        var world = new WorldModel(
            new HashSet<string> { earlySubject, "ui.establish" },
            new AlwaysNewAssociationStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);

        kernel.Process(Proposal(earlySubject, "accepted-before-container"));
        kernel.Process(establish);
        var slice = world.DeriveSlice(futureContainer);

        Assert.Equal("accepted-before-container", slice.ScopedClaims[earlySubject]);
    }

    [Fact]
    public void ConsumerViews_UseConflictAndRoleIndexes_AndPreserveConflictOrder()
    {
        var subjects = Enumerable.Range(0, 40).Select(i => $"claim-{i}").ToHashSet();
        var observations = Enumerable.Range(0, 100)
            .Select(i => new ProposedOccurrence(null, $"role-{i % 10}", $"item-{i}", State: "ready"))
            .ToArray();
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(
            subjects,
            associationStrategy: null,
            new FixedObservationStrategy(observations));
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);
        for (var i = 0; i < 40; i++)
        {
            kernel.Process(Proposal($"claim-{i}", "v1"));
            kernel.Process(Proposal($"claim-{i}", "v2"));
        }

        var view = world.DeriveOutcomeAssuranceView(
            new[] { "claim-30", "claim-3" },
            new[]
            {
                ("obligation-1", new TargetDescriptor("role-3", "item-13"), "ready"),
            });

        Assert.Equal(new[] { "claim-3", "claim-30" }, view.Conflicts.Select(c => c.Subject));
        Assert.Equal(EntityObligationFactKind.Satisfied, Assert.Single(view.EntityFacts!).Kind);
        Assert.True(world.DeriveActionAssuranceView("claim-30").HasConflictOnTarget);
        var aggregate = metrics.WorldModelPerformance[WorldModelOperation.ConsumerViewDerivation];
        Assert.Equal(2, aggregate.Invocations);
        Assert.Equal(53, aggregate.ScannedEntries); // 40 claims + 2 conflicts + 10 role candidates + 1 target lookup
        WriteAggregate("consumer-views", aggregate);
    }

    [Fact]
    public void DemandIndexes_UpdateAndDelete_WithoutChangingRegistryOrder()
    {
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(new HashSet<string> { "ui.observed" });
        _ = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);
        for (var i = 0; i < 1_000; i++)
        {
            world.RegisterContinuityDemand(new ContinuityDemand(
                $"demand-{i}", ContinuityDemandSourceKind.EffectTargetCommitment,
                OwningContainerId: null, Role: "Button", SemanticDescriptor: null,
                AnchorOccurrenceId: null, AnchorRevisionId: null, LogicalItemId: $"item-{i}"));
        }

        Assert.Equal(Enumerable.Range(0, 1_000).Select(i => $"demand-{i}"),
            world.ContinuityDemands.Select(d => d.DemandId));
        Assert.True(world.IsHotItem("item-999"));
        world.RevokeContinuityDemand("demand-999");
        Assert.False(world.IsHotItem("item-999"));
        Assert.Equal("demand-998", world.ContinuityDemands[^1].DemandId);
        world.RevokeContinuityDemand("demand-500");
        Assert.False(world.IsHotItem("item-500"));
        Assert.Equal("demand-499", world.ContinuityDemands[499].DemandId);
        Assert.Equal("demand-501", world.ContinuityDemands[500].DemandId);
        Assert.Equal(498, metrics.WorldModelPerformance[WorldModelOperation.DemandLookup].ScannedEntries);
        WriteAggregate("demand-lookup", metrics.WorldModelPerformance[WorldModelOperation.DemandLookup]);
    }

    [Fact]
    public void ContinuityIndex_AppendsLogicalItemsIncrementally_AndPreservesItemOrder()
    {
        const int size = 32;
        var observations = Enumerable.Range(0, size)
            .Select(i => new ProposedOccurrence(null, "Button", $"item-{i}"))
            .ToArray();
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(
            new HashSet<string> { "ui.observed" },
            associationStrategy: null,
            new FixedObservationStrategy(observations),
            new DemandSelectedContinuityStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);
        kernel.Process(Proposal("ui.observed", "frame"));

        for (var i = 0; i < size; i++)
        {
            var handle = world.RegisterContinuityDemand(new ContinuityDemand(
                $"demand-{i}", ContinuityDemandSourceKind.EffectTargetCommitment,
                OwningContainerId: null, Role: "Button", SemanticDescriptor: $"item-{i}",
                AnchorOccurrenceId: null, AnchorRevisionId: null, LogicalItemId: null));
            var result = world.ResolveContinuity(handle);
            Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, result.Outcome.Kind);
        }

        Assert.Equal(size + 1, world.RevisionHistory.Count);
        Assert.Equal(
            Enumerable.Range(0, size).Select(i => $"item-{i}"),
            world.Current!.LogicalItems!.Select(i => i.SemanticDescriptor));
        var indexBuild = metrics.WorldModelPerformance[WorldModelOperation.RevisionIndexBuild];
        Assert.Equal(size + 1, indexBuild.Invocations);
        Assert.Equal(2L * size + 1, indexBuild.ScannedEntries); // initial claim+occurrences + one appended item/revision
        Assert.Equal(3L * size + 1, indexBuild.CopiedEntries); // two occurrence lookups + one claim prefix + item appends
        var continuity = metrics.WorldModelPerformance[WorldModelOperation.ResolveContinuity];
        Assert.Equal(size, continuity.Invocations);
        Assert.Equal(size * size + size * (size - 1) / 2, continuity.ScannedEntries);
        WriteAggregate("continuity-index-build", indexBuild);
        WriteAggregate("resolve-continuity", continuity);
    }

    [Fact]
    public void RevisionAdvance_RegroundsFreshOccurrence_AndRejectsStaleBindingReference()
    {
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(
            new HashSet<string> { "ui.observed" },
            associationStrategy: null,
            new FixedObservationStrategy(new[]
            {
                new ProposedOccurrence(null, "Button", "save"),
            }));
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);

        kernel.Process(Proposal("ui.observed", "frame-1"));
        var first = world.ResolveCurrent(new TargetDescriptor("Button", "save"));
        var staleOccurrenceId = Assert.Single(first.Candidates).OccurrenceId;
        Assert.True(world.DeriveBindingView(subject: null, staleOccurrenceId).HasTargetOccurrence);

        kernel.Process(new ObservationProposal(
            new ObservationClaim("ui.observed", "frame-2"),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("wmp-test", DateTimeOffset.UnixEpoch.AddSeconds(1),
                "artifact:wmp-2", new[] { "raw" })));

        var second = world.ResolveCurrent(new TargetDescriptor("Button", "save"));
        var freshOccurrenceId = Assert.Single(second.Candidates).OccurrenceId;
        Assert.NotEqual(first.SourceRevisionId, second.SourceRevisionId);
        Assert.NotEqual(staleOccurrenceId, freshOccurrenceId);
        Assert.False(world.DeriveBindingView(subject: null, staleOccurrenceId).HasTargetOccurrence);
        Assert.True(world.DeriveBindingView(subject: null, freshOccurrenceId).HasTargetOccurrence);
    }

    [Fact]
    public void Reconcile_Reaffirm_UsesExactCopyOnWriteForUnchangedCollections()
    {
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(new HashSet<string> { "ui.observed", "ui.second" });
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);

        var first = kernel.Process(Proposal("ui.observed", "same")).ResultingRevision!;
        var second = kernel.Process(new ObservationProposal(
            new ObservationClaim("ui.observed", "same"),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("wmp-test", DateTimeOffset.UnixEpoch.AddSeconds(1),
                "artifact:wmp-2", new[] { "raw" }))).ResultingRevision!;

        Assert.Same(first.WorldState, second.WorldState);
        Assert.Same(first.WorldGraph, second.WorldGraph);
        Assert.Same(first.Conflicts, second.Conflicts);
        Assert.Same(first.Containers, second.Containers);
        Assert.Same(first.Relations, second.Relations);
        Assert.Same(first.Occurrences, second.Occurrences);
        Assert.Same(first.LogicalItems, second.LogicalItems);
        Assert.NotSame(first.EvidenceBasis, second.EvidenceBasis);
        Assert.Equal(2, second.EvidenceBasis.Count);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(512)]
    public void Reconcile_SmallMediumLarge_CopiedEntriesStayLinearInChanges(int size)
    {
        var subjects = Enumerable.Range(0, size).Select(i => $"claim-{i}").ToHashSet();
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(subjects);
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);

        for (var i = 0; i < size; i++)
            kernel.Process(Proposal($"claim-{i}", $"value-{i}"));

        Assert.Equal(size, world.RevisionHistory.Count);
        Assert.Equal(size, world.Current!.WorldState.Count);
        Assert.Equal(Enumerable.Range(1, size), world.RevisionHistory.Select(r => r.RevisionNumber));
        var reconcile = metrics.WorldModelPerformance[WorldModelOperation.Reconcile];
        Assert.Equal(size, reconcile.Invocations);
        Assert.Equal(size, reconcile.ScannedEntries);
        Assert.Equal(3L * size, reconcile.CopiedEntries);
        Assert.True(reconcile.AllocatedBytes > 0);
        Assert.True(reconcile.TotalTicks > 0);
        Assert.Equal(size,
            metrics.WorldModelPerformance[WorldModelOperation.RevisionIndexBuild].ScannedEntries);
        output.WriteLine(
            $"WMP-STAGE size={size} "
            + Format("reconcile", reconcile) + " "
            + Format("index-build", metrics.WorldModelPerformance[WorldModelOperation.RevisionIndexBuild]));
    }

    [Fact]
    public void Reconcile_ConflictAndRevise_CopyOnlyAffectedCanonicalCollections()
    {
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(new HashSet<string> { "ui.observed" });
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);
        var established = kernel.Process(Proposal("ui.observed", "v1")).ResultingRevision!;

        var conflict = kernel.Process(Proposal("ui.observed", "v2")).ResultingRevision!;
        Assert.Same(established.WorldState, conflict.WorldState);
        Assert.Same(established.WorldGraph, conflict.WorldGraph);
        Assert.NotSame(established.Conflicts, conflict.Conflicts);
        Assert.Single(conflict.Conflicts);

        var revised = kernel.Process(new ObservationProposal(
            new ObservationClaim("ui.observed", "v3"),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("wmp-test", DateTimeOffset.UnixEpoch.AddSeconds(2),
                "artifact:wmp-revise", new[] { "raw" }))).ResultingRevision!;
        Assert.NotSame(conflict.WorldState, revised.WorldState);
        Assert.Same(conflict.WorldGraph, revised.WorldGraph);
        Assert.Same(conflict.Conflicts, revised.Conflicts);
        Assert.Equal("v3", revised.WorldState["ui.observed"].Value);
        Assert.Equal(conflict.WorldState["ui.observed"].EvidenceId,
            revised.WorldState["ui.observed"].SupersededEvidenceIds!.Single());
        Assert.Equal(3,
            metrics.WorldModelPerformance[WorldModelOperation.RevisionIndexBuild].ScannedEntries);
    }

    private static ObservationProposal Proposal(string subject, string value) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation,
        ObservationContext.External,
        new Provenance(
            "wmp-test", DateTimeOffset.UnixEpoch, "artifact:wmp", new[] { "raw" }));

    private void WriteAggregate(string name, WorldModelPerformanceAggregate aggregate) =>
        output.WriteLine("WMP-STAGE " + Format(name, aggregate));

    private static string Format(string name, WorldModelPerformanceAggregate aggregate) =>
        $"{name}:invocations={aggregate.Invocations},scanned={aggregate.ScannedEntries},"
        + $"copied={aggregate.CopiedEntries},outputs={aggregate.OutputEntries},"
        + $"allocatedBytes={aggregate.AllocatedBytes},ticks={aggregate.TotalTicks}";

    private sealed class FixedObservationStrategy(IReadOnlyList<ProposedOccurrence> occurrences)
        : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(
            EvidenceRecord record, WorldBeliefRevision? previous) => occurrences;
    }

    private sealed class AlwaysNewAssociationStrategy : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => new(
            AssociationDispositionKind.New,
            MatchedContainerId: null,
            new[]
            {
                new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()),
            },
            Array.Empty<ProposedRelation>(),
            "wmp-new-container");
    }

    private sealed class AllKnownContainersObservationStrategy(int count) : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(
            EvidenceRecord record, WorldBeliefRevision? previous)
        {
            var owners = (previous?.Containers ?? Array.Empty<ContainerBelief>())
                .Select(c => c.Identity.ContainerId)
                .Append("ctr-" + record.EvidenceId[3..15])
                .ToArray();
            return Enumerable.Range(0, count)
                .Select(i => new ProposedOccurrence(owners[i % owners.Length], "Button", $"item-{i}"))
                .ToArray();
        }
    }

    private sealed class DemandSelectedContinuityStrategy : IContinuityStrategy
    {
        public ContinuityProposal Propose(ContinuityAdjudicationInput input)
        {
            var occurrence = input.Candidates.Single(c =>
                c.Role == input.Demand.Role
                && c.SemanticDescriptor == input.Demand.SemanticDescriptor);
            return new ContinuityProposal(
                ContinuityProposedOutcomeKind.ReferenceEstablished,
                occurrence.OccurrenceId,
                MatchedLogicalItemId: null,
                occurrence.SupportingEvidenceIds,
                Array.Empty<string>(),
                Array.Empty<ProposedTermination>(),
                "wmp-reference-established");
        }
    }
}
