using System.Collections.Immutable;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// WMP-003 — pins the invariant that protects the container-index count-stable
/// fast path in WorldRevisionIndex.Create (equal container count ⇒ reuse the
/// parent's ContainerPositions wholesale).
///
/// The invariant itself is checkable through the PUBLIC interface: every
/// revision transition reachable from public flows either appends a container,
/// leaves the container list untouched (continuity), or updates an existing
/// container's evidence basis in place (Matched) — a count-stable transition
/// NEVER permutes or replaces container identities.
///
/// The canary below additionally demonstrates, at the internal seam (existing
/// InternalsVisibleTo; no interface is widened), what happens if that
/// invariant is ever violated: the fast path silently serves stale positions.
/// It asserts today's behaviour deliberately — if someone hardens the fast
/// path or introduces container replacement/termination flows, this test
/// fails loudly and forces a conscious decision.
/// </summary>
public sealed class WorldModelIndexInvariantTests(ITestOutputHelper output)
{
    [Fact]
    public void PublicFlows_CountStableTransitions_NeverPermuteContainerIdentities()
    {
        // New/Matched reconcile transitions AND a continuity-only revision are
        // both interleaved, so the pairwise invariant sweep covers every
        // reachable count-stable transition kind.
        var world = new WorldModel(
            new HashSet<string> { "ui.establish", "ui.observed" },
            new AlternatingAssociationStrategy(matchAfterNew: 3),
            new FixedOccurrencesForInvariant(),
            new ScriptedContinuityStrategyForInvariant());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);

        WorldBeliefRevision? previous = null;
        var transitions = 0;
        var countStableTransitions = 0;
        var continuityTransitions = 0;
        for (var step = 0; step < 30; step++)
        {
            if (step == 10)
            {
                // interleave a continuity-only revision (containers reused by
                // reference, LogicalItems minted) into the sweep
                var handle = world.RegisterContinuityDemand(new ContinuityDemand(
                    $"demand-{step}", ContinuityDemandSourceKind.EffectTargetCommitment,
                    OwningContainerId: null, Role: "Button", SemanticDescriptor: "save",
                    AnchorOccurrenceId: null, AnchorRevisionId: null, LogicalItemId: null));
                world.ResolveContinuity(handle);
                continuityTransitions++;
            }
            var revision = kernel.Process(Proposal("ui.establish", $"frame-{step}", step))
                .ResultingRevision!;
            if (previous is not null)
            {
                transitions++;
                var before = ContainerIds(previous);
                var after = ContainerIds(revision);
                if (before.Length == after.Length)
                {
                    countStableTransitions++;
                    // THE INVARIANT: count-stable transitions keep the container
                    // identity SEQUENCE identical (order included) — this is what
                    // licenses the wholesale reuse in the count-stable fast path.
                    Assert.Equal(before, after);
                }
                else
                {
                    // reachable non-stable transitions are pure appends only
                    Assert.Equal(before.Length + 1, after.Length);
                    Assert.Equal(before, after[..^1]);
                }
            }
            previous = revision;
        }

        Assert.True(transitions > 0);
        Assert.True(countStableTransitions > 0);
        Assert.Equal(1, continuityTransitions);
        Assert.NotNull(world.Current!.LogicalItems); // the interleaved continuity mint landed
        output.WriteLine(
            $"WMP-INVARIANT transitions={transitions} countStable={countStableTransitions} "
            + $"continuity={continuityTransitions} containers={world.Current!.Containers!.Count}");

        // the whole optimized surface stays equal to the naive canonical scans
        WorldModelCanonicalOracleTests.Battery(world, "index-invariant-final");
    }

    [Fact]
    public void ContinuityRevisions_ReuseContainerCollection_PositionsUnchanged()
    {
        // ReferenceEquals count-stable path: a continuity-only revision shares
        // the parent's container collection object; positions must be reused
        // untouched and every consumer view stays canonical.
        var strategy = new ScriptedContinuityStrategyForInvariant();
        var world = new WorldModel(
            new HashSet<string> { "ui.observed" },
            new AlternatingAssociationStrategy(matchAfterNew: 1),
            new FixedOccurrencesForInvariant(),
            strategy);
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);

        var seeded = kernel.Process(Proposal("ui.observed", "frame-1", 1)).ResultingRevision!;
        Assert.Null(seeded.LogicalItems);
        var handle = world.RegisterContinuityDemand(new ContinuityDemand(
            "demand-1", ContinuityDemandSourceKind.EffectTargetCommitment,
            OwningContainerId: null, Role: "Button", SemanticDescriptor: "save",
            AnchorOccurrenceId: null, AnchorRevisionId: null, LogicalItemId: null));
        var minted = world.ResolveContinuity(handle);

        var continuity = world.Current!;
        Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, minted.Outcome.Kind);
        Assert.Equal(seeded.RevisionId, continuity.ParentRevisionId);
        // the reference-sharing claim in the test name, asserted for real:
        // a continuity-only revision reuses the parent container collection
        // object (ReferenceEquals count-stable path), so positions cannot move
        Assert.Same(seeded.Containers, continuity.Containers);
        WorldModelCanonicalOracleTests.Battery(world, "continuity-reuse");
    }

    [Fact]
    public void InternalSeamCanary_CountStablePermutation_ServesStalePositions()
    {
        // CANARY at the internal seam — NOT public-interface evidence. It
        // documents that WorldRevisionIndex.Create's count-stable fast path
        // (equal count ⇒ reuse parent ContainerPositions wholesale) has NO
        // structural defence against identity permutation: it relies entirely
        // on the public-flow invariant pinned by the test above. If this test
        // ever fails, someone changed that contract — update the canary and
        // the invariant test CONSCIOUSLY (e.g. container termination flows).
        var parent = RevisionWithContainers("rev-canary-parent", "ctr-a", "ctr-b", "ctr-c");
        var permuted = RevisionWithContainers("rev-canary-permuted", "ctr-b", "ctr-a", "ctr-c"); // same count, swapped 0/1

        // parent index built normally (full rebuild, correct positions)
        var parentIndex = WorldRevisionIndex.Create(parent, null, null, metrics: null);
        // permuted revision hits the count-stable fast path (count equal, not
        // reference-equal) against that parent index
        var index = WorldRevisionIndex.Create(permuted, parent, parentIndex, metrics: null);

        // Today's behaviour, pinned deliberately: positions come from the
        // PARENT's layout, so the swapped identities are mis-served.
        Assert.Equal(0, index.ContainerPositions["ctr-a"]); // parent slot, not 1
        Assert.Equal(1, index.ContainerPositions["ctr-b"]); // parent slot, not 0
        Assert.Equal(2, index.ContainerPositions["ctr-c"]);
    }

    // ---- helpers ---------------------------------------------------------

    private static string[] ContainerIds(WorldBeliefRevision revision) =>
        (revision.Containers ?? Array.Empty<ContainerBelief>())
            .Select(c => c.Identity.ContainerId).ToArray();

    private static WorldBeliefRevision RevisionWithContainers(string revisionId, params string[] containerIds)
    {
        var containers = containerIds
            .Select(id => new ContainerBelief(new ContainerIdentity(id), ImmutableList<string>.Empty))
            .ToArray();
        return new WorldBeliefRevision(
            RevisionId: revisionId,
            ParentRevisionId: null,
            RevisionNumber: 1,
            WorldState: new Dictionary<string, WorldClaim>(),
            WorldGraph: Array.Empty<string>(),
            EvidenceBasis: new HashSet<string>(),
            FreshnessBasis: new FreshnessBasis(DateTimeOffset.UnixEpoch),
            Uncertainty: new Uncertainty(0),
            Conflicts: Array.Empty<Conflict>(),
            Containers: containers);
    }

    private static ObservationProposal Proposal(string subject, string value, int ordinal) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation,
        ObservationContext.External,
        new Provenance(
            "wmp-invariant", DateTimeOffset.UnixEpoch.AddSeconds(ordinal),
            $"artifact:wmp-inv-{ordinal}", new[] { "raw" }));

    /// <summary>New ×N then Matched-first forever (deterministic interleave).</summary>
    private sealed class AlternatingAssociationStrategy(int matchAfterNew) : IAssociationStrategy
    {
        private int _frame;

        public AssociationProposal Propose(AssociationInput input)
        {
            if (_frame++ < matchAfterNew || input.Previous?.Containers is not { Count: > 0 } containers)
                return new AssociationProposal(
                    AssociationDispositionKind.New, MatchedContainerId: null,
                    new[] { new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
                    Array.Empty<ProposedRelation>(), "invariant-new-container");
            return new AssociationProposal(
                AssociationDispositionKind.Matched, containers[0].Identity.ContainerId,
                new[] { new AssociationCandidate(
                    containers[0].Identity.ContainerId,
                    new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
                Array.Empty<ProposedRelation>(), "invariant-matched-first");
        }
    }

    private sealed class FixedOccurrencesForInvariant : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            new[]
            {
                new ProposedOccurrence(null, "Button", "save"),
                new ProposedOccurrence(null, "Button", "submit"),
            };
    }

    private sealed class ScriptedContinuityStrategyForInvariant : IContinuityStrategy
    {
        public ContinuityProposal Propose(ContinuityAdjudicationInput input)
        {
            var occurrence = input.Candidates.Single(c => c.SemanticDescriptor == "save");
            return new ContinuityProposal(
                ContinuityProposedOutcomeKind.ReferenceEstablished,
                occurrence.OccurrenceId, MatchedLogicalItemId: null,
                occurrence.SupportingEvidenceIds.ToArray(), Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "invariant-mint");
        }
    }
}
