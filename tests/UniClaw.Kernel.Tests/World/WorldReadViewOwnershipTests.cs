using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class WorldReadViewOwnershipTests
{
    [Fact]
    public void PublishedHistoryCannotClearCurrentBeliefAndStillTracksOwnerAppends()
    {
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
        var history = world.RevisionHistory;
        Assert.Empty(history);

        kernel.Process(Observation("visible", 0));
        var first = world.Current;
        Assert.NotNull(first);
        Assert.Single(history);
        AssertReadOnly(history);
        Assert.Same(first, world.Current);

        kernel.Process(Observation("hidden", 1));
        Assert.Equal(2, history.Count);
        Assert.Same(first, history[0]);
        Assert.NotSame(first, world.Current);
        Assert.Same(history[1], world.Current);
    }

    [Fact]
    public void EveryPublishedOwnerLogRejectsExternalWrites()
    {
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        AssertReadOnly(world.RelevanceLog);
        AssertReadOnly(world.AssociationLog);
        AssertReadOnly(world.ContinuityDemands);
        AssertReadOnly(world.ContinuityLog);
        AssertReadOnly(world.ClaimEvolutionLog);
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> view)
    {
        if (view is ICollection<T> collection)
        {
            Assert.True(collection.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => collection.Clear());
        }
    }

    private static ObservationProposal Observation(string value, int second) =>
        new(new ObservationClaim("screen.home", value), IngressKind.Observation, ObservationContext.External,
            new Provenance("read-view-test", new DateTimeOffset(2026, 10, 21, 12, 0, second, TimeSpan.Zero),
                "screen.home", new[] { "capture:read-view" }));
}
