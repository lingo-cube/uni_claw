using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Tests.Perception;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// Public-interface-only WMP probe. The same source is runnable at the locked
/// b7f430d1 baseline and at WMP-001 HEAD, so canonical hashes and external
/// allocation/timing observations are comparable without a product toggle.
/// </summary>
public sealed class WorldModelBenchmarkProbeTests(ITestOutputHelper output)
{
    private static readonly CorpusManifest Corpus = CorpusManifest.Load();

    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(512)]
    public void ReconcileScaleProbe(int size)
    {
        var subjects = Enumerable.Range(0, size).Select(i => $"claim-{i}").ToHashSet();
        _ = RunScale(Math.Min(size, 8), subjects, metricsEnabled: false);
        var runtimeSamples = Enumerable.Range(0, 5)
            .Select(_ => RunScale(size, subjects, metricsEnabled: false))
            .ToArray();
        var instrumentedSamples = Enumerable.Range(0, 5)
            .Select(_ => RunScale(size, subjects, metricsEnabled: true))
            .ToArray();
        var final = instrumentedSamples[^1];
        var world = final.World;
        var canonical = Canonical(world);

        Assert.Equal(size, world.RevisionHistory.Count);
        Assert.Equal(size, world.Current!.WorldState.Count);
        output.WriteLine(
            $"WMP-PROBE reconcile size={size} revisions={world.RevisionHistory.Count} "
            + $"runtimeAllocatedBytes={Median(runtimeSamples.Select(s => s.AllocatedBytes))} "
            + $"runtimeWallTicks={Median(runtimeSamples.Select(s => s.WallTicks))} "
            + $"instrumentedAllocatedBytes={Median(instrumentedSamples.Select(s => s.AllocatedBytes))} "
            + $"instrumentedWallTicks={Median(instrumentedSamples.Select(s => s.WallTicks))} "
            + $"stageTicks={Median(instrumentedSamples.Select(s => s.StageTicks))} "
            + $"canonical={Hash(canonical)}");
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(8, 1)]
    [InlineData(8, 8)]
    [InlineData(8, 64)]
    [InlineData(64, 0)]
    [InlineData(64, 1)]
    [InlineData(64, 8)]
    [InlineData(64, 64)]
    [InlineData(512, 0)]
    [InlineData(512, 1)]
    [InlineData(512, 8)]
    [InlineData(512, 64)]
    public void OccurrenceIndexReadCrossoverProbe(int size, int reads)
    {
        _ = RunOccurrenceReads(Math.Min(size, 8), reads: 1);
        var samples = Enumerable.Range(0, 5)
            .Select(_ => RunOccurrenceReads(size, reads))
            .ToArray();
        var final = samples[^1];

        Assert.Equal(size, final.World.Current!.Occurrences!.Count);
        output.WriteLine(
            $"WMP-PROBE occurrence-read size={size} reads={reads} "
            + $"runtimeAllocatedBytes={Median(samples.Select(s => s.AllocatedBytes))} "
            + $"runtimeWallTicks={Median(samples.Select(s => s.WallTicks))} "
            + $"canonical={Hash(final.Projection)}");
    }

    [Fact]
    public void ScopedClaimSliceCanonicalProbe()
    {
        _ = RunScopedClaimSlice(8);
        var samples = Enumerable.Range(0, 5).Select(_ => RunScopedClaimSlice(64)).ToArray();
        var final = samples[^1];

        output.WriteLine(
            "WMP-PROBE scoped-claim-slice size=64 "
            + $"runtimeAllocatedBytes={Median(samples.Select(s => s.AllocatedBytes))} "
            + $"runtimeWallTicks={Median(samples.Select(s => s.WallTicks))} "
            + $"canonical={Hash(final.Projection)}");
    }

    [Fact]
    public void RealAssetColdWarmPartialGroundingCanonicalProbe()
    {
        var projections = new List<string>();
        foreach (var scenario in new[]
                 {
                     "golden-case-a-before", "scroll01-v1-partial", "nav03-parent",
                 })
        {
            var world = NewCorpusWorld(owned: scenario == "nav03-parent");
            var metrics = new RuntimeStageMetrics();
            var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);
            Observe(kernel, scenario, rounds: scenario == "golden-case-a-before" ? 2 : 1, metrics);
            var extra = string.Empty;
            if (scenario == "nav03-parent")
            {
                var root = world.Current!.Containers!.Single().Identity.ContainerId;
                var slice = world.DeriveSlice(root);
                var grounding = world.ResolveCurrent(new TargetDescriptor("Button", "CHILD A"));
                extra = "|slice=" + string.Join(",", slice.Occurrences.Select(o => o.OccurrenceId))
                    + "|ground=" + string.Join(",", grounding.Candidates.Select(c => c.OccurrenceId));
            }
            projections.Add(scenario + ":" + Canonical(world) + extra);
        }

        var canonical = string.Join("\n---\n", projections);
        output.WriteLine($"WMP-PROBE real-assets canonical={Hash(canonical)} chars={canonical.Length}");
        Assert.NotEmpty(canonical);
    }

    private static WorldModel NewCorpusWorld(bool owned)
    {
        var signatureScopes = Corpus.Scenarios
            .Where(s => s.Observations.Any(o => o.Subject == CorpusAssociationStrategy.PageSignatureSubject))
            .Select(s => "artifact:" + Corpus.Artifact(s.ScenarioId).ArtifactId)
            .ToHashSet();
        var dialogScopes = Corpus.Scenarios
            .Where(s => s.Observations.Any(o => o.Subject == CorpusAssociationStrategy.DialogTitleSubject))
            .Select(s => "artifact:" + Corpus.Artifact(s.ScenarioId).ArtifactId)
            .ToHashSet();
        return new WorldModel(
            Corpus.SubjectScope,
            new CorpusAssociationStrategy(signatureScopes, dialogScopes),
            owned ? new OwnedCorpusObservationStrategy() : new CorpusObservationStrategy(),
            new CorpusContinuityStrategy());
    }

    private static void Observe(
        UniKernel kernel, string scenarioId, int rounds, RuntimeStageMetrics metrics)
    {
        var artifact = Corpus.Artifact(scenarioId);
        for (var round = 0; round < rounds; round++)
        {
            var perception = new FastPerception(
                "perception.corpus", new CorpusFastPerception(Corpus, scenarioId),
                DisabledRunTrace.Instance, metrics);
            foreach (var proposal in perception.Observe(artifact))
                kernel.Process(proposal);
        }
    }

    private static ObservationProposal Proposal(string subject, string value, int ordinal) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation,
        ObservationContext.External,
        new Provenance(
            "wmp-probe", DateTimeOffset.UnixEpoch.AddSeconds(ordinal),
            $"artifact:wmp-{ordinal}", new[] { "raw" }));

    private static ScaleSample RunScale(
        int size,
        IReadOnlySet<string> subjects,
        bool metricsEnabled)
    {
        var metrics = metricsEnabled ? new RuntimeStageMetrics() : null;
        var world = new WorldModel(subjects);
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics);
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < size; i++)
            kernel.Process(Proposal($"claim-{i}", $"value-{i}", i));
        var ticks = Stopwatch.GetTimestamp() - start;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        var stageTicks = metrics?.Stages[RuntimeStage.WorldReconciliation].TotalTicks ?? 0;
        return new ScaleSample(world, allocated, ticks, stageTicks);
    }

    private static OccurrenceReadSample RunOccurrenceReads(int size, int reads)
    {
        var observations = Enumerable.Range(0, size)
            .Select(i => new ProposedOccurrence(null, $"role-{i % 8}", $"item-{i}"))
            .ToArray();
        var world = new WorldModel(
            new HashSet<string> { "ui.observed" },
            associationStrategy: null,
            new ProbeObservationStrategy(observations));
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
        var projections = new List<string>();
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        kernel.Process(Proposal("ui.observed", "frame", 0));
        for (var i = 0; i < reads; i++)
        {
            var view = world.ResolveCurrent(new TargetDescriptor("role-3"));
            projections.Add(string.Join(",", view.Candidates.Select(c => c.OccurrenceId)));
        }
        var ticks = Stopwatch.GetTimestamp() - start;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        return new OccurrenceReadSample(world, allocated, ticks, string.Join(";", projections));
    }

    private static ProjectionSample RunScopedClaimSlice(int size)
    {
        var firstProposal = Proposal("ui.observed", "frame-0", 0);
        var seed = new WorldModel(
            new HashSet<string> { "ui.observed" }, new ProbeNewAssociationStrategy());
        var seedKernel = new UniKernel(new EvidenceLedger(), seed, DisabledRunTrace.Instance);
        seedKernel.Process(firstProposal);
        var root = seed.Current!.Containers!.Single().Identity.ContainerId;
        var claimSubjects = Enumerable.Range(0, size).Select(i => $"{root}.claim-{i}").ToArray();
        var world = new WorldModel(
            claimSubjects.Append("ui.observed").ToHashSet(),
            new ProbeNewAssociationStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
        kernel.Process(firstProposal);
        for (var i = 0; i < size; i++)
            kernel.Process(Proposal(claimSubjects[i], $"value-{i}", i + 1));

        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        var slice = world.DeriveSlice(root);
        var ticks = Stopwatch.GetTimestamp() - start;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        var projection = string.Join(";", slice.ScopedClaims.Select(kv => $"{kv.Key}={kv.Value}"));
        return new ProjectionSample(allocated, ticks, projection);
    }

    private static long Median(IEnumerable<long> values)
    {
        var ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private static string Canonical(WorldModel world) =>
        string.Join("\n", world.RevisionHistory.Select(CanonicalRevision));

    private static string CanonicalRevision(WorldBeliefRevision r) =>
        $"{r.RevisionId}|{r.ParentRevisionId}|{r.RevisionNumber}"
        + "|state=" + string.Join(";", r.WorldState.Select(kv =>
            $"{kv.Key}={kv.Value.Value}:{kv.Value.EvidenceId}:{kv.Value.EstablishingProducer}:{kv.Value.EstablishingScope}:"
            + string.Join(",", kv.Value.SupersededEvidenceIds ?? Array.Empty<string>())))
        + "|graph=" + string.Join(";", r.WorldGraph)
        + "|basis=" + string.Join(";", r.EvidenceBasis)
        + "|conflicts=" + string.Join(";", r.Conflicts.Select(c =>
            $"{c.Subject}:{c.EstablishedValue}:{c.ChallengingValue}:{c.EstablishedEvidenceId}:{c.ChallengingEvidenceId}"))
        + "|containers=" + string.Join(";", (r.Containers ?? Array.Empty<ContainerBelief>()).Select(c =>
            c.Identity.ContainerId + ":" + string.Join(",", c.EvidenceBasis)))
        + "|relations=" + string.Join(";", (r.Relations ?? Array.Empty<ContainerRelation>()).Select(x =>
            $"{x.Kind}:{x.SourceContainerId}:{x.TargetContainerId}:" + string.Join(",", x.EvidenceBasis)))
        + "|occurrences=" + string.Join(";", (r.Occurrences ?? Array.Empty<OccurrenceBelief>()).Select(o =>
            $"{o.OccurrenceId}:{o.OwningContainerId}:{o.Role}:{o.SemanticDescriptor}:{o.State}"))
        + "|items=" + string.Join(";", (r.LogicalItems ?? Array.Empty<LogicalItemBelief>()).Select(i =>
            $"{i.LogicalItemId}:{i.OwningContainerId}:{i.Role}:{i.SemanticDescriptor}:{i.Lifecycle}:{i.EndedReason}:"
            + string.Join(",", i.EvidenceBasis)));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record ScaleSample(
        WorldModel World,
        long AllocatedBytes,
        long WallTicks,
        long StageTicks);

    private sealed record OccurrenceReadSample(
        WorldModel World,
        long AllocatedBytes,
        long WallTicks,
        string Projection);

    private sealed record ProjectionSample(long AllocatedBytes, long WallTicks, string Projection);

    private sealed class ProbeObservationStrategy(IReadOnlyList<ProposedOccurrence> occurrences)
        : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(
            EvidenceRecord record,
            WorldBeliefRevision? previous) => occurrences;
    }

    private sealed class ProbeNewAssociationStrategy : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => new(
            AssociationDispositionKind.New,
            MatchedContainerId: null,
            new[]
            {
                new AssociationCandidate(
                    "(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()),
            },
            Array.Empty<ProposedRelation>(),
            "wmp-probe-new-container");
    }
}
