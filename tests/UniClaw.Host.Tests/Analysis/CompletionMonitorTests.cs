using UniClaw.Core.Observability;
using UniClaw.Host.Analysis;
using Xunit;

namespace UniClaw.Host.Tests.Analysis;

/// <summary>
/// P4 acceptance tests (tasks.md §7.3 / §9.16 / §9.18): CompletionMonitor cancels the
/// linked CTS on confidence &gt;= 0.9 verdicts, keeps polling on Observe-band and null
/// verdicts, escalates a second consecutive Recommend to Terminate (anti-nuisance), and
/// Halt survives a missing baseline (cold-start suppression never swallows Halt).
/// </summary>
public sealed class CompletionMonitorTests
{
    // 50 ms, not the 500 ms default — tests wait on real wall clock.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>Build a monitor over one analyzer against a fresh in-memory trace.</summary>
    private static CompletionMonitor CreateMonitor(
        InMemoryTraceStorage storage,
        ICompletionAnalyzer analyzer,
        CancellationTokenSource linkedCts,
        ITraceRecorder? recorder = null)
    {
        recorder ??= new InMemoryTraceRecorder(storage);
        return new CompletionMonitor(
            new[] { analyzer },
            new InMemoryTraceService(storage),
            recorder,
            linkedCts,
            pollInterval: PollInterval);
    }

    /// <summary>Await the token's cancellation; returns false when the timeout elapses first.</summary>
    private static async Task<bool> WaitForCancellationAsync(CancellationToken ct, TimeSpan timeout)
    {
        try
        {
            await Task.Delay(timeout, ct);
            return false; // not cancelled
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    [Fact]
    public async Task HighConfidenceVerdict_CancelsLinkedCts()
    {
        // Arrange: confidence 0.95 + ShouldTerminate=true → Halt/Terminate-class, cancel.
        var storage = new InMemoryTraceStorage();
        var analyzer = new MockAnalyzer(new CompletionVerdict(true, "halt", 0.95));

        using var linkedCts = new CancellationTokenSource();
        using var monitor = CreateMonitor(storage, analyzer, linkedCts);

        // Act
        var pollTask = monitor.StartAsync();

        // Assert: the linked CTS is cancelled on the first poll tick.
        var cancelled = await WaitForCancellationAsync(linkedCts.Token, TimeSpan.FromSeconds(3));
        Assert.True(cancelled, "Linked CTS should be cancelled on high-confidence verdict");
        await pollTask; // poll loop exits by itself after cancelling
    }

    [Fact]
    public async Task LowConfidenceVerdict_DoesNotCancel()
    {
        // Arrange: confidence 0.5 → Observe band, keep polling.
        var storage = new InMemoryTraceStorage();
        var analyzer = new MockAnalyzer(new CompletionVerdict(false, "low confidence", 0.5));

        using var linkedCts = new CancellationTokenSource();
        using var monitor = CreateMonitor(storage, analyzer, linkedCts);

        // Act
        var pollTask = monitor.StartAsync();

        // Assert: after well more than 2 poll intervals the CTS is still alive.
        await Task.Delay(TimeSpan.FromSeconds(1)); // 20 polls at 50 ms
        Assert.False(linkedCts.IsCancellationRequested,
            "Observe-band verdict must not cancel the linked CTS");

        monitor.Dispose(); // stop the loop so the poll task exits
        await pollTask;
    }

    [Fact]
    public async Task NullVerdict_DoesNotCancel()
    {
        // Arrange: null verdict = "no signal", monitor keeps polling.
        var storage = new InMemoryTraceStorage();
        var analyzer = new MockAnalyzer(null);

        using var linkedCts = new CancellationTokenSource();
        using var monitor = CreateMonitor(storage, analyzer, linkedCts);

        // Act
        var pollTask = monitor.StartAsync();

        // Assert: no-signal polls never cancel the linked CTS.
        await Task.Delay(TimeSpan.FromSeconds(1)); // 20 polls at 50 ms
        Assert.False(linkedCts.IsCancellationRequested,
            "Null verdict must not cancel the linked CTS");

        monitor.Dispose(); // stop the loop so the poll task exits
        await pollTask;
    }

    [Fact]
    public async Task MissingBaseline_OnlyHaltCanTerminate()
    {
        // Arrange: span tree with nothing pending and end-of-list reached. With a null
        // baseline the EnumerateCompletionAnalyzer is in cold-start (Terminate/Recommend
        // suppressed to Observe) but Halt (confidence 1.0) still fires — by design §5.4.
        var storage = new InMemoryTraceStorage();
        var start = DateTimeOffset.UtcNow.AddMinutes(-1);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        storage.OpenSpan(SpanTypes.EngineStep, "step 1", "s1", "run", start.AddSeconds(1),
            null, new Dictionary<string, object> { ["end_of_list"] = true });

        var recorder = new InMemoryTraceRecorder(storage);
        var analyzer = new EnumerateCompletionAnalyzer(recorder, baselineProfile: null);

        using var linkedCts = new CancellationTokenSource();
        using var monitor = CreateMonitor(storage, analyzer, linkedCts, recorder);

        // Act
        var pollTask = monitor.StartAsync();

        // Assert: Halt cancels even without a baseline profile.
        var cancelled = await WaitForCancellationAsync(linkedCts.Token, TimeSpan.FromSeconds(3));
        Assert.True(cancelled, "Halt verdict must cancel the linked CTS even with a null baseline");
        await pollTask;
    }

    [Fact]
    public async Task SecondRecommend_EscalatesToTerminate()
    {
        // Arrange: an analyzer that ALWAYS returns Recommend (0.7, ShouldTerminate=false).
        // First poll → callback (absent) → observe; a second consecutive Recommend for the
        // same analyzer escalates to Terminate and cancels (anti-nuisance, 6.3).
        var storage = new InMemoryTraceStorage();
        var analyzer = new MockAnalyzer(CompletionVerdict.Recommend());

        using var linkedCts = new CancellationTokenSource();
        using var monitor = CreateMonitor(storage, analyzer, linkedCts);

        // Act
        var pollTask = monitor.StartAsync();

        // Assert: the second consecutive Recommend cancels the linked CTS.
        var cancelled = await WaitForCancellationAsync(linkedCts.Token, TimeSpan.FromSeconds(3));
        Assert.True(cancelled,
            "A second consecutive Recommend must escalate to Terminate and cancel the linked CTS");
        await pollTask;
    }

    /// <summary>Deterministic analyzer stub — returns a fixed verdict (or null) each poll.</summary>
    private sealed class MockAnalyzer : ICompletionAnalyzer
    {
        private readonly CompletionVerdict? _verdict;

        public MockAnalyzer(CompletionVerdict? verdict) => _verdict = verdict;

        public Task<CompletionVerdict?> EvaluateAsync(ITraceQuery trace, CancellationToken ct)
            => Task.FromResult(_verdict);
    }
}
