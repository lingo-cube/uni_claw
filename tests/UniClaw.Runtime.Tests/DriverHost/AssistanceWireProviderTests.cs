using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Brain;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// AssistanceWireProvider + pending-registry gate (dsh-assistance-provider-adapter A1):
/// request lifecycle PENDING→RESOLVED / PENDING→EXPIRED; requestId correlation;
/// world-version staleness; recommendation whitelist; bounded timeout/capacity;
/// duplicate/late resolve cannot resurrect; repeated polls harmless; fail-closed
/// everywhere. The provider owns NO intelligence (asserted by construction).
/// </summary>
public sealed class AssistanceWireProviderTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(80);

    private static AssistanceContext Context(string requestId = "req-1", long worldVersion = 7)
        => new(
            requestId,
            "run-1",
            "Settings",
            SemanticBeliefState.Contradicted,
            worldVersion,
            new Observation([new ObservedElement("Wi‑Fi", false, 0)], "settings", worldVersion));

    private static AssistanceResolveRequest Resolve(string requestId, long worldVersion, string? recommendation = "re-observe")
        => new(requestId, worldVersion, recommendation, null, "test");

    [Fact]
    public async Task CorrelatedResolve_CompletesConsultWithAdvice()
    {
        var registry = new AssistancePendingRegistry();
        var provider = new AssistanceWireProvider(registry);

        var consult = provider.ConsultAsync(Context("req-1", 7), CancellationToken.None);

        var pending = registry.Pending();
        var digest = Assert.Single(pending);
        Assert.Equal("req-1", digest.RequestId);
        Assert.Equal(7, digest.WorldVersion);
        Assert.Equal(SemanticBeliefState.Contradicted, digest.BeliefState);
        Assert.Equal("Settings", digest.SemanticPage);
        Assert.Equal(1, digest.ElementCount);

        var result = registry.Resolve(Resolve("req-1", 7, "re-observe"));
        Assert.True(result.Resolved);

        var advice = await consult;
        Assert.NotNull(advice);
        Assert.Equal("req-1", advice!.RequestId);
        Assert.Equal(7, advice.WorldVersion);
        Assert.Equal("re-observe", advice.Recommendation);
        // Entry consumed: poll no longer returns it; resolve finds nothing.
        Assert.Empty(registry.Pending());
        Assert.Equal(0, registry.PendingCount);
    }

    [Fact]
    public async Task MismatchedRequestId_Rejected_ConsultTimesOutNull()
    {
        var registry = new AssistancePendingRegistry();
        var provider = new AssistanceWireProvider(registry, ShortTimeout);

        var consult = provider.ConsultAsync(Context("req-1", 7), CancellationToken.None);
        var result = registry.Resolve(Resolve("wrong-request", 7, "re-observe"));
        Assert.False(result.Resolved);
        Assert.Contains("unknown or already-terminal", result.Diagnostic, StringComparison.Ordinal);

        Assert.Null(await consult); // bounded timeout → fail closed
        Assert.Equal(0, registry.PendingCount); // entry removed
    }

    [Fact]
    public async Task StaleWorldVersion_Rejected_ConsultTimesOutNull()
    {
        var registry = new AssistancePendingRegistry();
        var provider = new AssistanceWireProvider(registry, ShortTimeout);

        var consult = provider.ConsultAsync(Context("req-1", 7), CancellationToken.None);
        var result = registry.Resolve(Resolve("req-1", 8, "re-observe"));
        Assert.False(result.Resolved);
        Assert.Contains("stale", result.Diagnostic, StringComparison.OrdinalIgnoreCase);

        Assert.Null(await consult);
        Assert.Equal(0, registry.PendingCount);
    }

    [Fact]
    public async Task InvalidRecommendation_Rejected_ConsultTimesOutNull()
    {
        var registry = new AssistancePendingRegistry();
        var provider = new AssistanceWireProvider(registry, ShortTimeout);

        var consult = provider.ConsultAsync(Context("req-1", 7), CancellationToken.None);
        var result = registry.Resolve(Resolve("req-1", 7, "not-a-real-recommendation"));
        Assert.False(result.Resolved);
        Assert.Contains("invalid recommendation", result.Diagnostic, StringComparison.Ordinal);

        Assert.Null(await consult);
        Assert.Equal(0, registry.PendingCount);
    }

    [Fact]
    public async Task AbandonResolve_NullRecommendation_CompletesWithNullAdvice()
    {
        var registry = new AssistancePendingRegistry();
        var provider = new AssistanceWireProvider(registry);

        var consult = provider.ConsultAsync(Context("req-1", 7), CancellationToken.None);
        var result = registry.Resolve(Resolve("req-1", 7, null)); // abandon
        Assert.True(result.Resolved);

        var advice = await consult;
        Assert.Null(advice!.Recommendation); // Agent fails closed on no actionable advice
    }

    [Fact]
    public async Task Timeout_FailsClosed_NoHang_NoFabricatedAdvice()
    {
        var registry = new AssistancePendingRegistry();
        var provider = new AssistanceWireProvider(registry, ShortTimeout);

        var consult = provider.ConsultAsync(Context("req-1", 7), CancellationToken.None);
        Assert.Null(await consult); // no resolve arrives → bounded timeout → null
        Assert.Equal(0, registry.PendingCount); // entry eventually removed
    }

    [Fact]
    public async Task Cancellation_FailsClosed_EntryRemoved()
    {
        var registry = new AssistancePendingRegistry();
        var provider = new AssistanceWireProvider(registry);

        using var cts = new CancellationTokenSource();
        var consult = provider.ConsultAsync(Context("req-1", 7), cts.Token);
        cts.Cancel();

        Assert.Null(await consult);
        Assert.Equal(0, registry.PendingCount);
    }

    [Fact]
    public async Task CapacityExhausted_RejectsNewConsult_FailsClosed()
    {
        var registry = new AssistancePendingRegistry(capacity: 2);
        var provider = new AssistanceWireProvider(registry, ShortTimeout);

        var c1 = provider.ConsultAsync(Context("req-1", 7), CancellationToken.None);
        var c2 = provider.ConsultAsync(Context("req-2", 8), CancellationToken.None);
        // Third consult is rejected immediately (capacity exhausted).
        Assert.Null(await provider.ConsultAsync(Context("req-3", 9), CancellationToken.None));

        Assert.Equal(2, registry.PendingCount);
        registry.Resolve(Resolve("req-1", 7, "rebind"));
        registry.Resolve(Resolve("req-2", 8, "re-observe"));
        Assert.NotNull(await c1);
        Assert.NotNull(await c2);
    }

    [Fact]
    public async Task DuplicateAndLateResolve_CannotResurrectRequest()
    {
        var registry = new AssistancePendingRegistry();
        var provider = new AssistanceWireProvider(registry, ShortTimeout);

        var consult = provider.ConsultAsync(Context("req-1", 7), CancellationToken.None);
        Assert.True(registry.Resolve(Resolve("req-1", 7, "re-observe")).Resolved);
        var advice = await consult;
        Assert.NotNull(advice);

        // Duplicate resolve after terminal: rejected, no side effect.
        var dup = registry.Resolve(Resolve("req-1", 7, "re-observe"));
        Assert.False(dup.Resolved);
        Assert.Contains("unknown or already-terminal", dup.Diagnostic, StringComparison.Ordinal);

        // Late resolve after timeout: rejected (entry already removed).
        var consult2 = provider.ConsultAsync(Context("req-2", 9), CancellationToken.None);
        Assert.Null(await consult2); // times out
        var late = registry.Resolve(Resolve("req-2", 9, "re-observe"));
        Assert.False(late.Resolved);
        Assert.Equal(0, registry.PendingCount);
    }

    [Fact]
    public async Task RepeatedPendingPoll_IsHarmless()
    {
        var registry = new AssistancePendingRegistry();
        var provider = new AssistanceWireProvider(registry, ShortTimeout);

        var consult = provider.ConsultAsync(Context("req-1", 7), CancellationToken.None);
        // Many read-only polls: same request, never consumed by polling.
        for (var i = 0; i < 5; i++)
        {
            var digest = Assert.Single(registry.Pending());
            Assert.Equal("req-1", digest.RequestId);
        }

        registry.Resolve(Resolve("req-1", 7, "re-observe"));
        Assert.NotNull(await consult);
        Assert.Empty(registry.Pending());
    }
}
