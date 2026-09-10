using System.Collections.Frozen;
using System.Diagnostics;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// WMP-002 lazy canonical materialization investigation probe. Deterministic
/// allocation evidence (GC.GetAllocatedBytesForCurrentThread deltas) for the
/// PersistentRevisionDictionary / PersistentRevisionSet publication: reconcile
/// cost, FIRST enumeration of Current.WorldState / Current.EvidenceBasis
/// (does it materialize the whole ancestor chain?), repeated enumeration, and
/// full RevisionHistory replay/export. Ticks are auxiliary facts only — no
/// wall-time thresholds anywhere. Content/order of every enumeration is
/// pinned to the test-side frozen chain oracle (CanonicalOracle).
/// </summary>
public sealed class WorldModelMaterializationProbeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(512)]
    public void FirstEnumeration_Replay_AndRetention_Probe(int size)
    {
        // warm up JIT paths (kernel, canonical views, and the fresh frozen
        // reference construction) on disposable data before any measurement
        BuildWorld(Math.Min(size, 8), enumerate: true);
        _ = Measure(() =>
        {
            var pairs = Enumerable.Range(0, 8)
                .Select(i => new KeyValuePair<string, int>($"claim-{i}", i));
            _ = pairs.ToFrozenDictionary(StringComparer.Ordinal);
            _ = Enumerable.Range(0, 8).Select(i => $"claim-{i}").ToFrozenSet(StringComparer.Ordinal);
        });

        var world = BuildWorld(size, enumerate: false);

        // reconcile itself must stay lazy: no eager canonical-view builds per
        // revision (deterministic guard against reintroducing per-reconcile
        // frozen materialization — the WMP-001 baseline cost this design
        // removed; bound ≈ 24KB/revision, ~4.5× the measured steady state).
        var reconcileKernel = default(UniKernel)!;
        var reconcileWorldRef = default(WorldModel)!;
        long reconcileAlloc = 0;
        Measure(() =>
        {
            var subjects = Enumerable.Range(0, size).Select(i => $"claim-{i}").ToHashSet();
            reconcileWorldRef = new WorldModel(subjects);
            reconcileKernel = new UniKernel(
                new EvidenceLedger(), reconcileWorldRef, DisabledRunTrace.Instance);
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < size; i++)
                reconcileKernel.Process(Proposal($"claim-{i}", $"value-{i}", i));
            reconcileAlloc = GC.GetAllocatedBytesForCurrentThread() - allocated;
        });
        Assert.True(reconcileAlloc < size * 24_000L,
            $"reconcile allocated {reconcileAlloc} bytes for {size} revisions — eager "
            + "canonical-view materialization may have been reintroduced "
            + "(WMP-001 measured ~5.3KB/rev; the eager baseline measured ~85KB/rev at 512)");

        // reference: one fresh frozen build over the final key count
        var referenceAlloc = Measure(() =>
        {
            var pairs = Enumerable.Range(0, size)
                .Select(i => new KeyValuePair<string, int>($"claim-{i}", i));
            _ = pairs.ToFrozenDictionary(StringComparer.Ordinal);
        });

        // FIRST enumeration of the chain head (no enumeration during build)
        var current = world.Current!;
        long firstStateAlloc;
        var firstTicks = Measure(
            () =>
            {
                var sink = 0;
                foreach (var kv in current.WorldState)
                    sink += kv.Value.Value.Length;
                _ = sink;
            },
            out firstStateAlloc);
        long firstBasisAlloc;
        Measure(
            () =>
            {
                var sink = 0;
                foreach (var id in current.EvidenceBasis)
                    sink += id.Length;
                _ = sink;
            },
            out firstBasisAlloc);
        var materializedStateViews = Ratio(firstStateAlloc, referenceAlloc);
        var materializedBasisViews = Ratio(firstBasisAlloc, referenceAlloc);
        output.WriteLine(
            $"WMP-MAT size={size} reconcileAlloc={reconcileAlloc} "
            + $"firstStateAlloc={firstStateAlloc} firstBasisAlloc={firstBasisAlloc} "
            + $"referenceSingleBuild={referenceAlloc} "
            + $"derivedMaterializedStateViews={materializedStateViews:0.0} "
            + $"derivedMaterializedBasisViews={materializedBasisViews:0.0} firstTicks={firstTicks}");

        // repeated enumeration: cached, near-zero allocation (deterministic bound)
        long repeatAlloc;
        Measure(
            () =>
            {
                var sink = 0;
                foreach (var kv in current.WorldState)
                    sink += kv.Value.Value.Length;
                foreach (var id in current.EvidenceBasis)
                    sink += id.Length;
                _ = sink;
            },
            out repeatAlloc);
        Assert.True(repeatAlloc <= 64 + 16L * (current.WorldState.Count + current.EvidenceBasis.Count),
            $"repeated enumeration must be cached (allocated {repeatAlloc} bytes)");

        // full RevisionHistory replay/export AFTER touching the head
        long replayAlloc;
        var replayTicks = Measure(
            () =>
            {
                var sink = 0;
                foreach (var revision in world.RevisionHistory)
                foreach (var kv in revision.WorldState)
                    sink += kv.Value.Value.Length;
                _ = sink;
            },
            out replayAlloc);
        output.WriteLine(
            $"WMP-MAT size={size} replayAlloc={replayAlloc} replayTicks={replayTicks} "
            + $"replayDerivedViews={Ratio(replayAlloc, referenceAlloc):0.0}");

        // content+order of the head still matches the test-side frozen chain
        var expectedOrder = FrozenSet<string>.Empty;
        for (var i = 0; i < size; i++)
            expectedOrder = expectedOrder.Append($"claim-{i}").ToFrozenSet(StringComparer.Ordinal);
        var expectedState = Enumerable.Range(0, size)
            .Select(i => ($"claim-{i}", world.RevisionHistory[i].WorldState[$"claim-{i}"].Value,
                world.RevisionHistory[i].WorldState[$"claim-{i}"].EvidenceId))
            .ToArray();
        var basisOrder = FrozenSet<string>.Empty;
        for (var i = 0; i < size; i++)
            basisOrder = basisOrder.Append(world.RevisionHistory[i].EvidenceBasis
                    .Except(i == 0 ? Array.Empty<string>() : world.RevisionHistory[i - 1].EvidenceBasis)
                    .Single())
                .ToFrozenSet(StringComparer.Ordinal);
        CanonicalOracle.EqualPublicState(
            current, expectedState, expectedOrder.ToArray(), basisOrder.ToArray(),
            Enumerable.Range(0, size).Select(i => $"claim-{i}").ToArray(),
            $"materialization probe head size={size}");
    }

    [Fact]
    public void RetainedMemory_AfterHeadTouch_IsReported()
    {
        // Informational only (forced-GC totals carry several-MB noise; the
        // deterministic guards live in FirstEnumeration_Replay_AndRetention_Probe).
        const int size = 512;
        BuildWorld(Math.Min(size, 8), enumerate: true); // warm-up

        var world = BuildWorld(size, enumerate: false);
        GC.Collect();
        var preTouchMemory = GC.GetTotalMemory(forceFullCollection: true);

        var sink = 0;
        foreach (var kv in world.Current!.WorldState)
            sink += kv.Value.Value.Length;
        _ = sink;
        GC.Collect();
        var postTouchMemory = GC.GetTotalMemory(forceFullCollection: true);

        output.WriteLine(
            "WMP-MAT retained size=512 "
            + $"headTouchRetainedBytes={postTouchMemory - preTouchMemory} "
            + $"stateEntries={world.Current!.WorldState.Count} revisions={world.RevisionHistory.Count}");
        Assert.Equal(size, world.Current!.WorldState.Count);
    }

    // ---- helpers ---------------------------------------------------------

    private static WorldModel BuildWorld(int size, bool enumerate)
    {
        var subjects = Enumerable.Range(0, size).Select(i => $"claim-{i}").ToHashSet();
        var world = new WorldModel(subjects);
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
        for (var i = 0; i < size; i++)
            kernel.Process(Proposal($"claim-{i}", $"value-{i}", i));
        if (enumerate)
        {
            var sink = 0;
            foreach (var kv in world.Current!.WorldState)
                sink += kv.Value.Value.Length;
            foreach (var id in world.Current!.EvidenceBasis)
                sink += id.Length;
            _ = sink;
        }
        return world;
    }

    private static long Measure(Action action, out long allocatedBytes)
    {
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        action();
        var ticks = Stopwatch.GetTimestamp() - start;
        allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        return ticks;
    }

    private static long Measure(Action action) => Measure(action, out _);

    private static double Ratio(long value, long reference) =>
        reference <= 0 ? 0 : (double)value / reference;

    private static ObservationProposal Proposal(string subject, string value, int ordinal) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation,
        ObservationContext.External,
        new Provenance(
            "wmp-probe", DateTimeOffset.UnixEpoch.AddSeconds(ordinal),
            $"artifact:wmp-{ordinal}", new[] { "raw" }));
}
