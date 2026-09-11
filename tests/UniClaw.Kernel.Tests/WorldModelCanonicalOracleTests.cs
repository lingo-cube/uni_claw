using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Tests.Perception;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// WMP-002 canonical safety diagnostics. Expected values are naive full-table
/// scans of the same revision's public canonical collections; actual values
/// come from the production optimized paths (revision-bound indexes + COW
/// persistent collections) behind the public WorldModel interface. Every
/// mismatch fails at the FIRST divergence with a stable machine-parseable
/// report (see CanonicalOracle). These tests are the deterministic evidence
/// source for the DBG-001 world-model consistency route.
/// </summary>
public sealed class WorldModelCanonicalOracleTests(ITestOutputHelper output)
{
    // WMP-001 evidence §3 canonical hashes (baseline-verified at base 687b6e7
    // before any WMP-002 change), pinned whole. Single source for the scale
    // switch and the GoldenHashes scenario.
    private static readonly (int Size, string Golden)[] GoldenScale =
    {
        (8, "f3e6b003901f2a51bbb541019bd0e5fcefafbbd2ae220dae1045c25ea6a753a5"),
        (64, "c1dc8ce2f24417d7de8c9fd867a7786c8c62f7bb750163e6a4aa3bb50731f4b1"),
        (512, "ac95fb0265df78170509c308965b87098368db5d3b9cf21011d788ab0e448bde"),
    };
    private const string GoldenScopedClaims64 = "c2a5df1f7e0b419048f004282a346bf7ad44698a01986dbdf604f9a30e169c81";
    private const string GoldenRealAssets = "acd285433ab1ae910754da73e8e130932a6d9bd2fab6c1ea747022d6b35c17a0";

    // ------------------------------------------------------------------
    // Scenario 1 — ordinary revision advance, S/M/L tiers, driver-known
    // deltas, golden hashes, history invariance, deterministic replay.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(512)]
    public void ScaleReconcile_OrdinaryAdvance_CanonicalOracle(int size)
    {
        var subjects = Enumerable.Range(0, size).Select(i => $"claim-{i}").ToHashSet();
        var world = new WorldModel(subjects);
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);

        var expectedState = new List<(string Subject, string Value, string EvidenceId)>();
        var stateOrder = FrozenSet<string>.Empty;
        var basisOrder = FrozenSet<string>.Empty;
        var expectedGraph = new List<string>();

        for (var i = 0; i < size; i++)
        {
            var parent = world.Current;
            var revision = kernel.Process(Proposal($"claim-{i}", $"value-{i}", i)).ResultingRevision!;
            var deltaEvidence = revision.EvidenceBasis
                .Except(parent is null ? Enumerable.Empty<string>() : parent.EvidenceBasis)
                .Single();
            stateOrder = stateOrder.Append($"claim-{i}").ToFrozenSet(StringComparer.Ordinal);
            basisOrder = basisOrder.Append(deltaEvidence).ToFrozenSet(StringComparer.Ordinal);
            expectedState.Add(($"claim-{i}", $"value-{i}", deltaEvidence));
            expectedGraph.Add($"claim-{i}");

            CanonicalOracle.EqualPublicState(
                revision, expectedState, stateOrder.ToArray(), basisOrder.ToArray(),
                expectedGraph, $"ordinary advance step {i}");
            if (i % 64 == 0 || i == size - 1)
            {
                CanonicalOracle.EqualScalar(
                    "DeriveBindingView.HasTargetSubjectClaim", revision, $"subject=claim-{i}",
                    bool.TrueString, world.DeriveBindingView($"claim-{i}").HasTargetSubjectClaim.ToString());
                CanonicalOracle.EqualScalar(
                    "DeriveBindingView.HasTargetSubjectClaim", revision, "subject=claim-missing",
                    bool.FalseString, world.DeriveBindingView("claim-missing").HasTargetSubjectClaim.ToString());
            }
        }

        Battery(world, "scale-final");
        AssertInvariance(world, CaptureRenderings(world), "scale-final");

        var golden = GoldenScale.Single(g => g.Size == size).Golden;
        CanonicalOracle.EqualScalar("GoldenCanonicalHash", world.Current!, $"scale={size}",
            golden, Hash(RenderHistory(world)));

        // deterministic replay: identical proposals into a fresh model
        var replayWorld = new WorldModel(subjects);
        var replayKernel = new UniKernel(new EvidenceLedger(), replayWorld, DisabledRunTrace.Instance);
        for (var i = 0; i < size; i++)
            replayKernel.Process(Proposal($"claim-{i}", $"value-{i}", i));
        EqualHistories(world, replayWorld, "scale replay");
        output.WriteLine($"WMP-ORACLE scale={size} revisions={size} golden={Hash(RenderHistory(world))[..16]}…");
    }

    // ------------------------------------------------------------------
    // Scenario 2 — container append + matched replace + claim-before-
    // container + stale slice validity (driver-known derived claims).
    // ------------------------------------------------------------------

    [Fact]
    public void ContainerAppendAndMatchedReplace_ClaimBeforeContainer_Oracle()
    {
        var world = new WorldModel(
            new HashSet<string> { "ui.establish" },
            new SequencedAssociationStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);

        // three New-association frames -> three appended containers
        for (var frame = 0; frame < 3; frame++)
        {
            var revision = kernel.Process(Proposal("ui.establish", $"frame-{frame}", frame)).ResultingRevision!;
            Assert.Equal(frame + 1, revision.Containers!.Count);
            Battery(world, $"container-append-{frame}");
        }

        var containers = world.Current!.Containers!.Select(c => c.Identity.ContainerId).ToArray();
        var beforeBasis = world.Current.Containers![0].EvidenceBasis.ToHashSet();

        // matched replace: evidence basis of container #0 grows, identity and
        // position unchanged (matched changes basis, never position)
        var parent = world.Current;
        var revision4 = kernel.Process(Proposal("ui.establish", "frame-3", 3)).ResultingRevision!;
        var frame3Evidence = revision4.EvidenceBasis.Except(parent!.EvidenceBasis).Single();
        var after = world.Current!.Containers!;
        Assert.Equal(3, after.Count);
        Assert.Equal(containers, after.Select(c => c.Identity.ContainerId).ToArray());
        Assert.Contains(frame3Evidence, after[0].EvidenceBasis);
        Assert.True(after[0].EvidenceBasis.ToHashSet().IsSupersetOf(beforeBasis),
            "matched replace must only grow the container evidence basis");
        Battery(world, "container-matched-replace");

        // claim-before-container: the SAME establish proposal instance mints
        // the SAME container id in the second world, so a claim accepted
        // before the container exists must enter its Slice once established.
        var establish = Proposal("ui.establish", "container", 0);
        var seed = new WorldModel(
            new HashSet<string> { "ui.establish", "ctr-seed.title" },
            new SequencedAssociationStrategy());
        var seedKernel = new UniKernel(new EvidenceLedger(), seed, DisabledRunTrace.Instance);
        seedKernel.Process(establish);
        var futureRoot = seed.Current!.Containers!.Single().Identity.ContainerId;

        var claimWorld = new WorldModel(
            new HashSet<string> { "ui.establish", $"{futureRoot}.title" },
            new SequencedAssociationStrategy());
        var claimKernel = new UniKernel(new EvidenceLedger(), claimWorld, DisabledRunTrace.Instance);
        claimKernel.Process(Proposal($"{futureRoot}.title", "accepted-before-container", 1));
        claimKernel.Process(establish);

        Battery(claimWorld, "claim-before-container");
        var slice = claimWorld.DeriveSlice(futureRoot);
        CanonicalOracle.EqualScalar(
            "DeriveSlice.ScopedClaims.DelayedContainer", claimWorld.Current!,
            $"root={futureRoot}|subject={futureRoot}.title",
            "accepted-before-container", slice.ScopedClaims[$"{futureRoot}.title"]);
        CanonicalOracle.EqualSequence(
            "DeriveSlice.ScopedClaims", claimWorld.Current!, $"root={futureRoot}",
            CanonicalOracle.ExpectedScopedClaims(claimWorld.Current!, new[] { futureRoot }),
            slice.ScopedClaims.Select(kv => (kv.Key, kv.Value)).ToArray(),
            CanonicalOracle.ClaimIdentity, CanonicalOracle.ClaimValue, "claim-before-container");

        // stale slice: advance the revision, the previously derived slice is
        // no longer valid but its content is frozen
        var sourceRevision = claimWorld.Current!;
        var frozenOccurrences = slice.Occurrences.Select(o => o.OccurrenceId).ToArray();
        claimKernel.Process(Proposal("ui.establish", "later-frame", 2));
        Assert.False(claimWorld.IsSliceValid(slice));
        CanonicalOracle.EqualSequence(
            "StaleSlice.OccurrencesFrozen", claimWorld.Current!, $"root={futureRoot}",
            CanonicalOracle.ExpectedSliceOccurrences(sourceRevision, new[] { futureRoot })
                .Select(o => new OccurrenceFact(
                    o.OccurrenceId, o.OwningContainerId, o.Role, o.SemanticDescriptor,
                    o.State, o.Locator, o.Native)).ToArray(),
            slice.Occurrences,
            CanonicalOracle.FactIdentity, CanonicalOracle.FactValue,
            "historical slice content is frozen");
        Assert.Equal(frozenOccurrences, slice.Occurrences.Select(o => o.OccurrenceId).ToArray());

        Battery(claimWorld, "claim-before-container-advanced");
        AssertInvariance(claimWorld, CaptureRenderings(claimWorld), "claim-before-container");
        AssertInvariance(world, CaptureRenderings(world), "container-append");
    }

    // ------------------------------------------------------------------
    // Scenario 3 — occurrence replacement, stale occurrence/binding,
    // missing keys, repeated queries.
    // ------------------------------------------------------------------

    [Fact]
    public void OccurrenceReplacement_StaleReferences_MissingKeys_Oracle()
    {
        var world = new WorldModel(
            new HashSet<string> { "ui.observed" },
            associationStrategy: null,
            new RichRotatingObservationStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);

        var first = kernel.Process(Proposal("ui.observed", "frame-1", 1)).ResultingRevision!;
        Battery(world, "occurrence-first");
        var firstIds = first.Occurrences!.Select(o => o.OccurrenceId).ToArray();
        Assert.Equal(3, firstIds.Length);

        var second = kernel.Process(Proposal("ui.observed", "frame-2", 2)).ResultingRevision!;
        Assert.NotEqual(first.Occurrences, second.Occurrences);

        // stale occurrence: fresh ids visible, old ids invisible everywhere
        var secondIds = second.Occurrences!.Select(o => o.OccurrenceId).ToArray();
        Assert.All(firstIds, stale =>
        {
            CanonicalOracle.EqualScalar(
                "DeriveBindingView.HasTargetOccurrence", second, $"occurrence={Short(stale)}",
                bool.FalseString, world.DeriveBindingView(null, stale).HasTargetOccurrence.ToString());
        });
        Assert.All(secondIds, fresh =>
        {
            CanonicalOracle.EqualScalar(
                "DeriveBindingView.HasTargetOccurrence", second, $"occurrence={Short(fresh)}",
                bool.TrueString, world.DeriveBindingView(null, fresh).HasTargetOccurrence.ToString());
        });

        // missing keys fail-closed / empty on every lookup path
        Assert.Throws<InvalidOperationException>(() => world.DeriveSlice("ctr-nonexistent"));
        var noCandidate = world.ResolveCurrent(new TargetDescriptor("role-missing"));
        CanonicalOracle.EqualScalar(
            "ResolveCurrent.Result", world.Current!, "role=role-missing",
            nameof(CurrentCandidateSetResultKind.NoCandidate), noCandidate.Result.ToString());
        CanonicalOracle.EqualScalar(
            "DeriveBindingView.HasTargetOccurrence", world.Current!, "occurrence=occ-missing",
            bool.FalseString, world.DeriveBindingView(null, "occ-missing").HasTargetOccurrence.ToString());

        kernel.Process(Proposal("ui.observed", "frame-3", 3));
        Battery(world, "occurrence-final");
        AssertInvariance(world, CaptureRenderings(world), "occurrence-final");
    }

    // ------------------------------------------------------------------
    // Scenario 4 — LogicalItem append + termination, continuity-only
    // revisions (reference sharing), demand registry mutations.
    // ------------------------------------------------------------------

    [Fact]
    public void Continuity_ItemAppendTermination_DemandRegistry_Oracle()
    {
        var strategy = new ScriptedContinuityStrategy();
        var world = new WorldModel(
            new HashSet<string> { "ui.observed" },
            associationStrategy: null,
            new RotatingObservationStrategy(),
            strategy);
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);

        var rev1 = kernel.Process(Proposal("ui.observed", "frame-1", 1)).ResultingRevision!;
        var saveOccurrence = rev1.Occurrences!.Single(o => o.SemanticDescriptor == "save");

        // append: demand without prior identity -> ReferenceEstablished mint
        var demand = new ContinuityDemand(
            "demand-save", ContinuityDemandSourceKind.EffectTargetCommitment,
            OwningContainerId: null, Role: "Button", SemanticDescriptor: "save",
            AnchorOccurrenceId: null, AnchorRevisionId: null, LogicalItemId: null);
        var handle = world.RegisterContinuityDemand(demand);
        strategy.Enqueue(new ContinuityProposal(
            ContinuityProposedOutcomeKind.ReferenceEstablished,
            saveOccurrence.OccurrenceId, MatchedLogicalItemId: null,
            saveOccurrence.EvidenceBasis.ToArray(), Array.Empty<string>(),
            Array.Empty<ProposedTermination>(), "oracle-mint"));
        var minted = world.ResolveContinuity(handle);
        Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, minted.Outcome.Kind);
        var itemId = minted.LogicalItemId!;

        // continuity-only revision shares every collection except LogicalItems
        var rev2 = world.Current!;
        Assert.Equal(rev1.RevisionId, rev2.ParentRevisionId);
        Assert.Same(rev1.WorldState, rev2.WorldState);
        Assert.Same(rev1.WorldGraph, rev2.WorldGraph);
        Assert.Same(rev1.EvidenceBasis, rev2.EvidenceBasis);
        Assert.Same(rev1.Conflicts, rev2.Conflicts);
        Assert.Same(rev1.Containers, rev2.Containers);
        Assert.Same(rev1.Occurrences, rev2.Occurrences);
        Assert.Single(rev2.LogicalItems!);
        Battery(world, "continuity-mint");

        // demand binding update is observable through the public registry
        var bound = Assert.Single(world.ContinuityDemands);
        Assert.Equal(itemId, bound.LogicalItemId);
        CheckHotItem(world, itemId);
        CheckHotItem(world, "item-unknown");

        // re-register same demand id is idempotent (update path)
        var sameHandle = world.RegisterContinuityDemand(demand);
        Assert.Equal(handle.DemandId, sameHandle.DemandId);
        Assert.Single(world.ContinuityDemands);

        // ordinary advance replaces occurrences, then SameReferent extends the
        // item basis while a valid termination ends its lifecycle
        var rev3 = kernel.Process(Proposal("ui.observed", "frame-2", 2)).ResultingRevision!;
        var freshSave = rev3.Occurrences!.Single(o => o.SemanticDescriptor == "save");
        Assert.NotEqual(saveOccurrence.OccurrenceId, freshSave.OccurrenceId);
        strategy.Enqueue(new ContinuityProposal(
            ContinuityProposedOutcomeKind.SameReferent,
            freshSave.OccurrenceId, itemId,
            freshSave.EvidenceBasis.ToArray(), Array.Empty<string>(),
            new[] { new ProposedTermination(itemId, freshSave.EvidenceBasis.ToArray(), "oracle-end") },
            "oracle-same-referent-end"));
        var adjudicated = world.ResolveContinuity(handle);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.SameReferent, adjudicated.Outcome.AdjudicationKind);
        var ended = world.Current!.LogicalItems!.Single(i => i.LogicalItemId == itemId);
        Assert.Equal(LogicalItemLifecycle.Ended, ended.Lifecycle);
        Assert.Equal("referent-terminated", ended.EndedReason);
        Assert.Contains(freshSave.EvidenceBasis.Single(), ended.EvidenceBasis);

        // registry mutations: middle and tail deletes preserve order; hot
        // lookups follow the naive public scan
        for (var i = 0; i < 6; i++)
            world.RegisterContinuityDemand(new ContinuityDemand(
                $"demand-{i}", ContinuityDemandSourceKind.EffectTargetCommitment,
                OwningContainerId: null, Role: "Button", SemanticDescriptor: null,
                AnchorOccurrenceId: null, AnchorRevisionId: null, LogicalItemId: $"item-{i}"));
        var expectedOrder = new[]
        {
            "demand-save", "demand-0", "demand-1", "demand-2", "demand-3", "demand-4", "demand-5",
        };
        CanonicalOracle.EqualSequence(
            "DemandRegistry.Order", world.Current!, "registry",
            expectedOrder.Select(id => new CanonicalOracle.OracleEntry(id)).ToArray(),
            world.ContinuityDemands.Select(d => new CanonicalOracle.OracleEntry(d.DemandId)).ToArray(),
            entry => entry.Id, _ => "-");
        CheckHotItem(world, "item-3");

        world.RevokeContinuityDemand("demand-3"); // middle delete
        CanonicalOracle.EqualSequence(
            "DemandRegistry.OrderAfterMiddleDelete", world.Current!, "registry",
            expectedOrder.Where(id => id != "demand-3").Select(id => new CanonicalOracle.OracleEntry(id)).ToArray(),
            world.ContinuityDemands.Select(d => new CanonicalOracle.OracleEntry(d.DemandId)).ToArray(),
            entry => entry.Id, _ => "-");
        CheckHotItem(world, "item-3");

        world.RevokeContinuityDemand("demand-5"); // tail delete
        CanonicalOracle.EqualSequence(
            "DemandRegistry.OrderAfterTailDelete", world.Current!, "registry",
            expectedOrder.Where(id => id is not "demand-3" and not "demand-5").Select(id => new CanonicalOracle.OracleEntry(id)).ToArray(),
            world.ContinuityDemands.Select(d => new CanonicalOracle.OracleEntry(d.DemandId)).ToArray(),
            entry => entry.Id, _ => "-");
        Assert.Equal("demand-4", world.ContinuityDemands[^1].DemandId);

        // stale anchor occurrence is fail-closed
        Assert.Throws<InvalidOperationException>(() => world.RegisterContinuityDemand(new ContinuityDemand(
            "demand-stale", ContinuityDemandSourceKind.EffectTargetCommitment,
            OwningContainerId: null, Role: "Button", SemanticDescriptor: null,
            AnchorOccurrenceId: saveOccurrence.OccurrenceId, AnchorRevisionId: null, LogicalItemId: null)));
        // missing demand resolution is fail-closed
        Assert.Throws<InvalidOperationException>(() => world.ResolveContinuity(new DemandHandle("demand-missing")));

        Battery(world, "continuity-final");
        AssertInvariance(world, CaptureRenderings(world), "continuity-final");
    }

    // ------------------------------------------------------------------
    // Scenario 5 — conflict append + revise, consumer views.
    // ------------------------------------------------------------------

    [Fact]
    public void ConflictAppendAndRevise_ConsumerViews_Oracle()
    {
        var world = new WorldModel(new HashSet<string> { "ui.a", "ui.b", "ui.c" });
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);

        // same producer + same scope + different values -> explicit conflicts
        kernel.Process(ConflictProposal("ui.a", "a-v1", 1));
        kernel.Process(ConflictProposal("ui.b", "b-v1", 2));
        var firstConflict = kernel.Process(ConflictProposal("ui.a", "a-v2", 3)).ResultingRevision!;
        Assert.Single(firstConflict.Conflicts);
        kernel.Process(ConflictProposal("ui.b", "b-v2", 4));
        var thirdConflict = kernel.Process(ConflictProposal("ui.b", "b-v3", 5)).ResultingRevision!;
        Assert.Equal(3, thirdConflict.Conflicts.Count);
        Battery(world, "conflicts-appended");

        // revise path (same producer, different scope) replaces value + chain
        kernel.Process(new ObservationProposal(
            new ObservationClaim("ui.c", "c-v1"),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("wmp-test", DateTimeOffset.UnixEpoch.AddSeconds(6),
                "artifact:wmp-c-1", new[] { "raw" })));
        var revised = kernel.Process(new ObservationProposal(
            new ObservationClaim("ui.c", "c-v2"),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("wmp-test", DateTimeOffset.UnixEpoch.AddSeconds(7),
                "artifact:wmp-c-2", new[] { "raw" }))).ResultingRevision!;
        CanonicalOracle.EqualScalar(
            "RevisedClaim.Value", revised, "subject=ui.c", "c-v2", revised.WorldState["ui.c"].Value);
        Assert.Single(revised.WorldState["ui.c"].SupersededEvidenceIds!);
        Assert.Equal(3, revised.Conflicts.Count);
        Battery(world, "conflicts-final");

        var subjects = new HashSet<string> { "ui.a", "ui.b" };
        CanonicalOracle.EqualSequence(
            "OutcomeConflicts.Order", revised, "scope=ui.a|ui.b",
            CanonicalOracle.ExpectedConflicts(revised, subjects)
                .Select(c => (c.Subject, c.ChallengingValue)).ToArray(),
            world.DeriveOutcomeAssuranceView(subjects).Conflicts
                .Select(c => (c.Subject, c.ChallengingValue)).ToArray(),
            t => t.Subject, t => t.ChallengingValue);
        Assert.True(world.DeriveActionAssuranceView("ui.a").HasConflictOnTarget);
        Assert.True(world.DeriveActionAssuranceView("ui.b").HasConflictOnTarget);
        Assert.False(world.DeriveActionAssuranceView("ui.c").HasConflictOnTarget);
        AssertInvariance(world, CaptureRenderings(world), "conflicts-final");
    }

    // ------------------------------------------------------------------
    // Scenario 6 — real-asset cold/warm/partial/grounding battery.
    // ------------------------------------------------------------------

    [Fact]
    public void RealAssetColdWarmPartialGrounding_Oracle()
    {
        foreach (var scenario in new[] { "golden-case-a-before", "scroll01-v1-partial", "nav03-parent" })
        {
            var (world, kernel) = NewCorpusWorld(owned: scenario == "nav03-parent");
            var rounds = scenario == "golden-case-a-before" ? 3 : 2; // cold + warm rounds
            var captured = new List<string>();
            for (var round = 0; round < rounds; round++)
            {
                Observe(kernel, scenario);
                Battery(world, $"{scenario}-round-{round}");
                captured.AddRange(world.RevisionHistory
                    .Skip(captured.Count)
                    .Select(CanonicalOracle.RenderRevision));
            }

            // replay: identical corpus input into a fresh model reproduces
            // the same revision rendering item-by-item
            var (replayWorld, replayKernel) = NewCorpusWorld(owned: scenario == "nav03-parent");
            for (var round = 0; round < rounds; round++)
                Observe(replayKernel, scenario);
            EqualHistories(world, replayWorld, $"{scenario} replay");

            // grounding chain: battery + invariance off the final revision
            Battery(world, $"{scenario}-final");
            AssertInvariance(world, captured, $"{scenario}-final");
            output.WriteLine(
                $"WMP-ORACLE real-asset scenario={scenario} revisions={world.RevisionHistory.Count}");
        }
    }

    // ------------------------------------------------------------------
    // Scenario 7 — WMP-001 golden hashes pinned (probe scenarios mirrored).
    // ------------------------------------------------------------------

    [Fact]
    public void GoldenHashes_Wmp001Evidence_Pinned()
    {
        // scale 8/64/512 (mirrors WMP-001 evidence §3 reconcile rows)
        foreach (var (size, golden) in GoldenScale)
        {
            var world = new WorldModel(Enumerable.Range(0, size).Select(i => $"claim-{i}").ToHashSet());
            var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
            for (var i = 0; i < size; i++)
                kernel.Process(Proposal($"claim-{i}", $"value-{i}", i));
            CanonicalOracle.EqualScalar("GoldenCanonicalHash", world.Current!, $"scale={size}",
                golden, Hash(RenderHistory(world)));
        }

        // 64 container-scoped claims slice (mirrors evidence §3 scoped-claim row)
        {
            var firstProposal = Proposal("ui.observed", "frame-0", 0);
            var seed = new WorldModel(
                new HashSet<string> { "ui.observed" }, new ProbeNewAssociationStrategy());
            var seedKernel = new UniKernel(new EvidenceLedger(), seed, DisabledRunTrace.Instance);
            seedKernel.Process(firstProposal);
            var root = seed.Current!.Containers!.Single().Identity.ContainerId;
            var claimSubjects = Enumerable.Range(0, 64).Select(i => $"{root}.claim-{i}").ToArray();
            var world = new WorldModel(
                claimSubjects.Append("ui.observed").ToHashSet(),
                new ProbeNewAssociationStrategy());
            var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
            kernel.Process(firstProposal);
            for (var i = 0; i < claimSubjects.Length; i++)
                kernel.Process(Proposal(claimSubjects[i], $"value-{i}", i + 1));
            var slice = world.DeriveSlice(root);
            var projection = string.Join(";", slice.ScopedClaims.Select(kv => $"{kv.Key}={kv.Value}"));
            CanonicalOracle.EqualScalar("GoldenCanonicalHash", world.Current!, "scoped-claims-64",
                GoldenScopedClaims64, Hash(projection));
            Battery(world, "scoped-claims-64");
        }

        // real assets cold/warm/partial/grounding (mirrors evidence §3 row 4)
        {
            var projections = new List<string>();
            WorldModel? last = null;
            foreach (var scenario in new[]
                     { "golden-case-a-before", "scroll01-v1-partial", "nav03-parent" })
            {
                var (world, kernel) = NewCorpusWorld(owned: scenario == "nav03-parent");
                Observe(kernel, scenario, rounds: scenario == "golden-case-a-before" ? 2 : 1);
                var extra = string.Empty;
                if (scenario == "nav03-parent")
                {
                    var root = world.Current!.Containers!.Single().Identity.ContainerId;
                    var slice = world.DeriveSlice(root);
                    var grounding = world.ResolveCurrent(new TargetDescriptor("Button", "CHILD A"));
                    extra = "|slice=" + string.Join(",", slice.Occurrences.Select(o => o.OccurrenceId))
                        + "|ground=" + string.Join(",", grounding.Candidates.Select(c => c.OccurrenceId));
                }
                projections.Add(scenario + ":" + RenderHistory(world) + extra);
                last = world;
            }

            var canonical = string.Join("\n---\n", projections);
            CanonicalOracle.EqualScalar("GoldenCanonicalHash", last!.Current!, "real-assets",
                GoldenRealAssets, Hash(canonical));
            Battery(last, "real-assets-golden");
        }
    }

    [Fact]
    public void Oracle_SelfCheck_ReportsParseableFirstDivergence()
    {
        // The oracle itself must stay red-capable and machine-parseable: feed
        // it a deliberately divergent pair and require the exact report shape
        // (re-verifies the sabotage-matrix property on every full run without
        // breaking production).
        static string Identity((int Id, string Tag) entry) => "entry-" + entry.Id;
        static string Value((int Id, string Tag) entry) => entry.Tag;
        var revision = new WorldBeliefRevision(
            "rev-selfcheck", null, 1,
            new Dictionary<string, WorldClaim>(), Array.Empty<string>(),
            new HashSet<string>(), new FreshnessBasis(DateTimeOffset.UnixEpoch),
            new Uncertainty(0), Array.Empty<Conflict>());
        var expected = new[] { (1, "a"), (2, "b"), (3, "c") };
        var actual = new[] { (1, "a"), (9, "x"), (3, "c") };
        var ex = Assert.Throws<XunitException>(() => CanonicalOracle.EqualSequence(
            "SelfCheck", revision, "identity-divergence", expected, actual, Identity, Value));
        var line = ex.Message.Split('\n')[0];
        Assert.StartsWith("WMP-DIVERGENCE schema=wmp-canonical-oracle/1", line);
        foreach (var token in new[]
                 {
                     " op=SelfCheck", " owner=", " rev=", " key=", " expectedCount=3",
                     " actualCount=3", " firstDivergentPosition=1",
                     " expected=\"entry-2|b\"", " actual=\"entry-9|x\"",
                 })
            Assert.Contains(token, line);

        var missing = Assert.Throws<XunitException>(() => CanonicalOracle.EqualSequence(
            "SelfCheck", revision, "omission-divergence",
            expected, new[] { (1, "a"), (2, "b") }, Identity, Value));
        var missingLine = missing.Message.Split('\n')[0];
        Assert.Contains("expectedCount=3 actualCount=2 firstDivergentPosition=2", missingLine);
        Assert.Contains("expected=\"entry-3|c\"", missingLine);
        Assert.Contains("actual=\"<none>\"", missingLine);
    }

    // ==================================================================
    // Battery — expected (naive canonical scan) vs actual (optimized
    // production path) through the public interface only.
    // ==================================================================

    internal static void Battery(WorldModel world, string context)
    {
        var current = world.Current!;
        var occurrences = current.Occurrences ?? Array.Empty<OccurrenceBelief>();

        // ResolveCurrent over every role / container / descriptor combination
        if (current.Occurrences is null)
        {
            var unavailable = world.ResolveCurrent(new TargetDescriptor("any"));
            CanonicalOracle.EqualScalar(
                "ResolveCurrent.Result", current, "role=any|no-observation-seam",
                nameof(CurrentCandidateSetResultKind.ScopeProjectionUnavailable),
                unavailable.Result.ToString(), context);
        }
        else
        {
            foreach (var role in occurrences.Select(o => o.Role).Distinct().ToArray())
            {
                CheckResolveCurrent(world, new TargetDescriptor(role), context);
                foreach (var container in occurrences.Where(o => o.Role == role)
                             .Select(o => o.OwningContainerId).OfType<string>().Distinct())
                    CheckResolveCurrent(world, new TargetDescriptor(role, OwningContainerId: container), context);
                foreach (var descriptor in occurrences.Where(o => o.Role == role)
                             .Select(o => o.SemanticDescriptor).OfType<string>().Distinct())
                    CheckResolveCurrent(world, new TargetDescriptor(role, descriptor), context);
            }
            CheckResolveCurrent(world, new TargetDescriptor("role-oracle-missing"), context);
        }

        // DeriveSlice for every container and multi-container scopes
        var containers = (current.Containers ?? Array.Empty<ContainerBelief>())
            .Select(c => c.Identity.ContainerId).ToArray();
        foreach (var container in containers)
        {
            CheckSlice(world, container, null, context);
            if (containers.Length >= 2)
                CheckSlice(world, containers[0], containers, context);
        }

        // BindingView: every occurrence id + dotted subjects + missing keys
        foreach (var occurrence in occurrences)
            CheckBinding(world, null, occurrence.OccurrenceId, context);
        CheckBinding(world, null, "occ-oracle-missing", context);
        foreach (var subject in current.WorldState.Keys.Where(k => k.Contains('.')).Take(12))
            CheckBinding(world, subject, null, context);
        CheckBinding(world, "subject-oracle-missing", null, context);

        // Consumer views: conflicts, claims, entity obligations
        var conflictSubjects = current.Conflicts.Select(c => c.Subject).ToHashSet();
        foreach (var subject in current.Conflicts.Select(c => c.Subject).Distinct().Take(8))
        {
            var view = world.DeriveActionAssuranceView(subject);
            CanonicalOracle.EqualScalar(
                "ActionAssuranceView.HasConflictOnTarget", current, $"subject={Short(subject)}",
                CanonicalOracle.ExpectedHasConflict(current, subject).ToString(),
                view.HasConflictOnTarget.ToString(), context);
        }
        var cleanSubject = current.WorldState.Keys.FirstOrDefault(k => !conflictSubjects.Contains(k))
            ?? "subject-clean";
        CanonicalOracle.EqualScalar(
            "ActionAssuranceView.HasConflictOnTarget", current, $"subject={Short(cleanSubject)}",
            CanonicalOracle.ExpectedHasConflict(current, cleanSubject).ToString(),
            world.DeriveActionAssuranceView(cleanSubject).HasConflictOnTarget.ToString(), context);

        CheckOutcome(world, context);

        // demand hot lookups follow the naive public registry scan
        foreach (var itemId in world.ContinuityDemands.Select(d => d.LogicalItemId)
                     .OfType<string>().Distinct().ToArray())
            CheckHotItem(world, itemId);
        CheckHotItem(world, "item-oracle-missing");
    }

    private static void CheckResolveCurrent(WorldModel world, TargetDescriptor descriptor, string context)
    {
        var current = world.Current!;
        var key = $"role={descriptor.Role}|desc={descriptor.SemanticDescriptor ?? "*"}"
            + $"|container={descriptor.OwningContainerId ?? "*"}";
        var expected = CanonicalOracle.ExpectedResolveCurrent(current, descriptor);
        var expectedFacts = expected.Select(o => new CandidateOccurrenceFact(
            o.OccurrenceId, o.OwningContainerId, o.Role, o.SemanticDescriptor, current.RevisionId)).ToArray();
        var view = world.ResolveCurrent(descriptor);
        CanonicalOracle.EqualSequence(
            "ResolveCurrent", current, key, expectedFacts, view.Candidates.ToArray(),
            CanonicalOracle.CandidateIdentity, CanonicalOracle.CandidateValue, context);
        var expectedResult = expected.Count switch
        {
            0 => nameof(CurrentCandidateSetResultKind.NoCandidate),
            1 => nameof(CurrentCandidateSetResultKind.UniqueCandidate),
            _ => nameof(CurrentCandidateSetResultKind.MultipleCandidates),
        };
        CanonicalOracle.EqualScalar(
            "ResolveCurrent.Result", current, key, expectedResult, view.Result.ToString(), context);
        var expectedOwner = expected.Select(o => o.OwningContainerId).OfType<string>().Distinct().ToArray();
        CanonicalOracle.EqualScalar(
            "ResolveCurrent.DerivedOwner", current, key,
            expectedOwner.Length == 1 ? expectedOwner[0] : "<null>",
            view.OwningContainerId ?? "<null>", context);

        // repeated query determinism
        var again = world.ResolveCurrent(descriptor);
        CanonicalOracle.EqualSequence(
            "ResolveCurrent.Repeat", current, key, view.Candidates.ToArray(), again.Candidates.ToArray(),
            CanonicalOracle.CandidateIdentity, CanonicalOracle.CandidateValue, context);
    }

    private static void CheckSlice(WorldModel world, string root, string[]? scope, string context)
    {
        var current = world.Current!;
        var inScope = scope ?? new[] { root };
        var distinct = inScope.Distinct().ToArray();
        var key = $"root={Short(root)}|scope={string.Join(",", distinct.Select(Short))}";
        var slice = world.DeriveSlice(root, inScope);

        var expectedOccurrences = CanonicalOracle.ExpectedSliceOccurrences(current, distinct);
        CanonicalOracle.EqualSequence(
            "DeriveSlice.Occurrences", current, key,
            expectedOccurrences.Select(o => new OccurrenceFact(
                o.OccurrenceId, o.OwningContainerId, o.Role, o.SemanticDescriptor,
                o.State, o.Locator, o.Native)).ToArray(),
            slice.Occurrences,
            CanonicalOracle.FactIdentity, CanonicalOracle.FactValue, context);

        var expectedClaims = CanonicalOracle.ExpectedScopedClaims(current, distinct);
        CanonicalOracle.EqualSequence(
            "DeriveSlice.ScopedClaims", current, key,
            expectedClaims, slice.ScopedClaims.Select(kv => (kv.Key, kv.Value)).ToArray(),
            CanonicalOracle.ClaimIdentity, CanonicalOracle.ClaimValue, context);

        CanonicalOracle.EqualScalar(
            "Slice.SourceRevisionId", current, key, current.RevisionId, slice.SourceRevisionId, context);
        CanonicalOracle.EqualScalar(
            "Slice.RootContainerId", current, key, root, slice.RootContainerId, context);
        CanonicalOracle.EqualSequence(
            "Slice.InScopeEcho", current, key,
            inScope.Select(id => new CanonicalOracle.OracleEntry(id)).ToArray(),
            slice.InScopeContainerIds.Select(id => new CanonicalOracle.OracleEntry(id)).ToArray(),
            entry => entry.Id, _ => "-", context);
        Assert.Equal(current.FreshnessBasis, slice.FreshnessBasis);
        Assert.True(world.IsSliceValid(slice));

        var again = world.DeriveSlice(root, inScope);
        CanonicalOracle.EqualSequence(
            "DeriveSlice.Repeat.Occurrences", current, key,
            slice.Occurrences, again.Occurrences,
            CanonicalOracle.FactIdentity, CanonicalOracle.FactValue, context);
        CanonicalOracle.EqualSequence(
            "DeriveSlice.Repeat.ScopedClaims", current, key,
            slice.ScopedClaims.Select(kv => (kv.Key, kv.Value)).ToArray(),
            again.ScopedClaims.Select(kv => (kv.Key, kv.Value)).ToArray(),
            CanonicalOracle.ClaimIdentity, CanonicalOracle.ClaimValue, context);
    }

    private static void CheckBinding(WorldModel world, string? subject, string? occurrenceId, string context)
    {
        var current = world.Current!;
        var expectedOccurrence = occurrenceId is null
            ? null
            : CanonicalOracle.ExpectedOccurrence(current, occurrenceId);
        var key = $"subject={Short(subject) ?? "-"}|occurrence={Short(occurrenceId) ?? "-"}";
        var view = world.DeriveBindingView(subject, occurrenceId);
        CanonicalOracle.EqualScalar(
            "BindingView.HasTargetSubjectClaim", current, key,
            (subject is not null && current.WorldState.ContainsKey(subject)).ToString(),
            view.HasTargetSubjectClaim.ToString(), context);
        CanonicalOracle.EqualScalar(
            "BindingView.HasTargetOccurrence", current, key,
            (expectedOccurrence is not null).ToString(), view.HasTargetOccurrence.ToString(), context);
        CanonicalOracle.EqualScalar(
            "BindingView.Locator", current, key,
            CanonicalOracle.Describe(expectedOccurrence?.Locator),
            CanonicalOracle.Describe(view.TargetOccurrenceLocator), context);
        CanonicalOracle.EqualScalar(
            "BindingView.Native", current, key,
            CanonicalOracle.Describe(expectedOccurrence?.Native),
            CanonicalOracle.Describe(view.TargetOccurrenceNative), context);
        CanonicalOracle.EqualScalar(
            "BindingView.RevisionId", current, key, current.RevisionId, view.RevisionId, context);
        CanonicalOracle.EqualScalar(
            "BindingView.RevisionNumber", current, key,
            current.RevisionNumber.ToString(), view.RevisionNumber.ToString(), context);
    }

    private static void CheckOutcome(WorldModel world, string context)
    {
        var current = world.Current!;
        var dotted = current.WorldState.Keys.Where(k => k.Contains('.')).ToArray();
        var scope = dotted
            .Append(current.Conflicts.Select(c => c.Subject).FirstOrDefault() ?? "ui.none")
            .ToHashSet();
        var key = $"scope={scope.Count}-subjects";
        var view = world.DeriveOutcomeAssuranceView(scope);
        CanonicalOracle.EqualSequence(
            "OutcomeView.Claims", current, key,
            CanonicalOracle.ExpectedOutcomeClaims(current, scope),
            view.Claims.Select(kv => (kv.Key, kv.Value.Value, kv.Value.EvidenceId)).ToArray(),
            CanonicalOracle.OutcomeClaimIdentity, CanonicalOracle.OutcomeClaimValue, context);
        CanonicalOracle.EqualSequence(
            "OutcomeView.Conflicts", current, key,
            CanonicalOracle.ExpectedConflicts(current, scope),
            view.Conflicts,
            CanonicalOracle.ConflictIdentity, CanonicalOracle.ConflictValue, context);
        CanonicalOracle.EqualScalar(
            "OutcomeView.ConflictingClaimCount", current, key,
            current.Conflicts.Count.ToString(), view.ConflictingClaimCount.ToString(), context);

        // entity obligations: tri-state over occurrence state (CDS-001 semantics)
        var obligations = (current.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Where(o => o.State is not null)
            .Take(8)
            .Select(o => (ObligationId: "obl-" + o.OccurrenceId,
                Scope: new TargetDescriptor(o.Role, o.SemanticDescriptor, o.OwningContainerId),
                Required: o.State!))
            .ToArray();
        if (obligations.Length > 0)
        {
            var expected = obligations.Select(obl =>
            {
                var candidates = (current.Occurrences ?? Array.Empty<OccurrenceBelief>())
                    .Where(o => o.Role == obl.Scope.Role
                        && (obl.Scope.SemanticDescriptor is null
                            || o.SemanticDescriptor == obl.Scope.SemanticDescriptor)
                        && (obl.Scope.OwningContainerId is null
                            || o.OwningContainerId == obl.Scope.OwningContainerId))
                    .ToArray();
                if (candidates.Length != 1)
                    return nameof(EntityObligationFactKind.Unknown);
                var state = candidates[0].State;
                return state is null
                    ? nameof(EntityObligationFactKind.Unknown)
                    : state == obl.Required
                        ? nameof(EntityObligationFactKind.Satisfied)
                        : nameof(EntityObligationFactKind.Unsatisfied);
            }).ToArray();
            var facts = world.DeriveOutcomeAssuranceView(
                scope, obligations.Select(o => (o.ObligationId, o.Scope, o.Required)));
            CanonicalOracle.EqualSequence(
                "OutcomeView.EntityFacts", current, "entity-obligations",
                obligations.Select((o, i) => (Id: o.ObligationId, Kind: expected[i])).ToArray(),
                facts.EntityFacts!.Select(f => (Id: f.ObligationId, Kind: f.Kind.ToString())).ToArray(),
                entry => entry.Id, entry => entry.Kind, context);
        }

        var again = world.DeriveOutcomeAssuranceView(scope);
        CanonicalOracle.EqualSequence(
            "OutcomeView.Repeat.Conflicts", current, key,
            view.Conflicts, again.Conflicts,
            CanonicalOracle.ConflictIdentity, CanonicalOracle.ConflictValue, context);
    }

    private static void CheckHotItem(WorldModel world, string logicalItemId)
    {
        CanonicalOracle.EqualScalar(
            "IsHotItem", world.Current!, $"item={logicalItemId}",
            CanonicalOracle.ExpectedIsHotItem(world, logicalItemId).ToString(),
            world.IsHotItem(logicalItemId).ToString());
    }

    // ---- history helpers ------------------------------------------------

    private static string RenderHistory(WorldModel world) =>
        string.Join("\n", world.RevisionHistory.Select(CanonicalOracle.RenderRevision));

    private static IReadOnlyList<string> CaptureRenderings(WorldModel world) =>
        world.RevisionHistory.Select(CanonicalOracle.RenderRevision).ToArray();

    private static void AssertInvariance(WorldModel world, IReadOnlyList<string> captured, string context)
    {
        var history = world.RevisionHistory;
        if (captured.Count != history.Count)
            throw new XunitException(CanonicalOracle.Divergence(
                "HistoryInvariance", history[^1].RevisionId, "history-length",
                captured.Count, history.Count, 0,
                new CanonicalOracle.DivergenceFact($"captured:{captured.Count}", null),
                new CanonicalOracle.DivergenceFact($"actual:{history.Count}", null), context));
        for (var i = 0; i < captured.Count; i++)
        {
            var before = ShortHash(captured[i]);
            var after = ShortHash(CanonicalOracle.RenderRevision(history[i]));
            if (before != after)
                throw new XunitException(CanonicalOracle.Divergence(
                    "HistoryInvariance", history[i].RevisionId, $"position={i}",
                    1, 1, i,
                    new CanonicalOracle.DivergenceFact(before, null),
                    new CanonicalOracle.DivergenceFact(after, null),
                    $"{context}: historical revision mutated after later reconciliation"));
        }
    }

    private static void EqualHistories(WorldModel expected, WorldModel actual, string context)
    {
        var expectedHistory = expected.RevisionHistory;
        var actualHistory = actual.RevisionHistory;
        var shared = Math.Min(expectedHistory.Count, actualHistory.Count);
        for (var i = 0; i < shared; i++)
        {
            var left = ShortHash(CanonicalOracle.RenderRevision(expectedHistory[i]));
            var right = ShortHash(CanonicalOracle.RenderRevision(actualHistory[i]));
            if (left != right)
                throw new XunitException(CanonicalOracle.Divergence(
                    "HistoryReplay", actualHistory[i].RevisionId, $"position={i}",
                    expectedHistory.Count, actualHistory.Count, i,
                    new CanonicalOracle.DivergenceFact(left, null),
                    new CanonicalOracle.DivergenceFact(right, null),
                    $"{context}: replay diverged at revision {i}"));
        }
        if (expectedHistory.Count != actualHistory.Count)
            throw new XunitException(CanonicalOracle.Divergence(
                "HistoryReplay", actualHistory.Count > 0 ? actualHistory[^1].RevisionId : "<empty>",
                "history-length", expectedHistory.Count, actualHistory.Count, shared,
                shared < expectedHistory.Count
                    ? new CanonicalOracle.DivergenceFact(expectedHistory[shared].RevisionId, null)
                    : null,
                shared < actualHistory.Count
                    ? new CanonicalOracle.DivergenceFact(actualHistory[shared].RevisionId, null)
                    : null,
                context));
    }

    private static string ShortHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)).Take(8).ToArray()).ToLowerInvariant();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Short(string? value) =>
        string.IsNullOrEmpty(value) ? "-" : (value.Length <= 24 ? value : value[..24] + "…");

    // ---- evidence / strategies ------------------------------------------

    private static ObservationProposal Proposal(string subject, string value, int ordinal) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation,
        ObservationContext.External,
        new Provenance(
            "wmp-probe", DateTimeOffset.UnixEpoch.AddSeconds(ordinal),
            $"artifact:wmp-{ordinal}", new[] { "raw" }));

    /// <summary>Same producer + same scope + different value ⇒ explicit conflict path.</summary>
    private static ObservationProposal ConflictProposal(string subject, string value, int ordinal) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation,
        ObservationContext.External,
        new Provenance(
            "wmp-oracle", DateTimeOffset.UnixEpoch.AddSeconds(ordinal),
            "artifact:wmp-conflict", new[] { "raw" }));

    private (WorldModel World, UniKernel Kernel) NewCorpusWorld(bool owned)
    {
        var corpus = CorpusManifest.Load();
        var signatureScopes = corpus.Scenarios
            .Where(s => s.Observations.Any(o => o.Subject == CorpusAssociationStrategy.PageSignatureSubject))
            .Select(s => "artifact:" + corpus.Artifact(s.ScenarioId).ArtifactId)
            .ToHashSet();
        var dialogScopes = corpus.Scenarios
            .Where(s => s.Observations.Any(o => o.Subject == CorpusAssociationStrategy.DialogTitleSubject))
            .Select(s => "artifact:" + corpus.Artifact(s.ScenarioId).ArtifactId)
            .ToHashSet();
        var world = new WorldModel(
            corpus.SubjectScope,
            new CorpusAssociationStrategy(signatureScopes, dialogScopes),
            owned ? new OwnedCorpusObservationStrategy() : new CorpusObservationStrategy(),
            new CorpusContinuityStrategy());
        return (world, new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance));
    }

    private static void Observe(UniKernel kernel, string scenarioId, int rounds = 1)
    {
        var corpus = CorpusManifest.Load();
        var artifact = corpus.Artifact(scenarioId);
        for (var round = 0; round < rounds; round++)
        {
            var perception = new FastPerception(
                "perception.corpus", new CorpusFastPerception(corpus, scenarioId),
                DisabledRunTrace.Instance, metrics: null);
            foreach (var proposal in perception.Observe(artifact))
                kernel.Process(proposal);
        }
    }

    /// <summary>New ×3 then Matched-first (deterministic; deltas driver-known).</summary>
    private sealed class SequencedAssociationStrategy : IAssociationStrategy
    {
        private int _frame;

        public AssociationProposal Propose(AssociationInput input)
        {
            if (_frame++ < 3 || input.Previous?.Containers is not { Count: > 0 } containers)
                return new AssociationProposal(
                    AssociationDispositionKind.New, MatchedContainerId: null,
                    new[] { new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
                    Array.Empty<ProposedRelation>(), "oracle-new-container");
            return new AssociationProposal(
                AssociationDispositionKind.Matched, containers[0].Identity.ContainerId,
                new[] { new AssociationCandidate(
                    containers[0].Identity.ContainerId,
                    new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
                Array.Empty<ProposedRelation>(), "oracle-matched-first");
        }
    }

    /// <summary>frame-1: save/cancel/submit; frame-2+: save/submit (fresh ids per revision).</summary>
    private sealed class RotatingObservationStrategy : IUiObservationStrategy
    {
        private int _frame;

        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            _frame++ switch
            {
                0 => new[]
                {
                    new ProposedOccurrence(null, "Button", "save"),
                    new ProposedOccurrence(null, "Button", "cancel"),
                    new ProposedOccurrence(null, "Button", "submit"),
                },
                _ => new[]
                {
                    new ProposedOccurrence(null, "Button", "save"),
                    new ProposedOccurrence(null, "Button", "submit", State: "ready"),
                },
            };
    }

    /// <summary>
    /// Occurrences with per-occurrence distinguishing owner/locator/native/state
    /// facts, so every occurrence-id lookup path (BindingView echo, container
    /// filters, entity obligations) observes occurrence-specific values — an
    /// identity mix-up in the occurrence index cannot hide behind null facts.
    /// </summary>
    private sealed class RichRotatingObservationStrategy : IUiObservationStrategy
    {
        private int _frame;

        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            _frame++ switch
            {
                0 => new[]
                {
                    new ProposedOccurrence(
                        "ctr-alpha", "Button", "save",
                        Locator: new SpatialLocator(0.10, 0.10, 0.20, 0.20, "device-viewport"),
                        Native: new NativeLocator("android", "btn-save")),
                    new ProposedOccurrence(
                        "ctr-alpha", "Button", "cancel",
                        Locator: new SpatialLocator(0.30, 0.10, 0.40, 0.20, "device-viewport"),
                        Native: new NativeLocator("android", "btn-cancel")),
                    new ProposedOccurrence(
                        "ctr-beta", "Button", "submit",
                        State: "ready",
                        Locator: new SpatialLocator(0.50, 0.10, 0.60, 0.20, "device-viewport"),
                        Native: new NativeLocator("android", "btn-submit")),
                },
                _ => new[]
                {
                    new ProposedOccurrence(
                        "ctr-alpha", "Button", "save",
                        Locator: new SpatialLocator(0.12, 0.12, 0.22, 0.22, "device-viewport"),
                        Native: new NativeLocator("android", "btn-save-2")),
                    new ProposedOccurrence(
                        "ctr-beta", "Button", "submit",
                        State: "armed",
                        Locator: new SpatialLocator(0.52, 0.12, 0.62, 0.22, "device-viewport"),
                        Native: new NativeLocator("android", "btn-submit-2")),
                },
            };
    }

    private sealed class ScriptedContinuityStrategy : IContinuityStrategy
    {
        private readonly Queue<ContinuityProposal> _proposals = new();

        internal void Enqueue(ContinuityProposal proposal) => _proposals.Enqueue(proposal);

        public ContinuityProposal Propose(ContinuityAdjudicationInput input) =>
            _proposals.Dequeue();
    }

    private sealed class ProbeNewAssociationStrategy : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => new(
            AssociationDispositionKind.New,
            MatchedContainerId: null,
            new[] { new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
            Array.Empty<ProposedRelation>(),
            "oracle-probe-new-container");
    }
}
