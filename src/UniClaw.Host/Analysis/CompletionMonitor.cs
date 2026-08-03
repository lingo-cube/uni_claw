using System.Collections.Immutable;
using UniClaw.Core.Observability;

namespace UniClaw.Host.Analysis;

/// <summary>
/// CompletionMonitor — Host-side background scheduler that polls registered
/// <see cref="ICompletionAnalyzer"/> implementations on each poll tick, writes an
/// <c>analyze.completion</c> (or <c>analyze.error_loop</c>) span per non-null verdict,
/// and cancels the engine's linked CTS when a termination condition is met.
///
/// The monitor is a composition concern around <c>engine.RunAsync(cts.Token)</c>
/// (trace-span-observability 6.1): it never touches engine internals, and a monitor
/// crash cannot crash the engine — an exception inside a poll tick is logged and the
/// loop continues, so the worst case is that the monitor stops canceling and the
/// engine runs to completion.
///
/// Confidence→action mapping (per spec):
///  <list type="bullet">
///   <item><c>confidence >= 0.9</c> → cancel the linked CTS (Halt/Terminate-class).</item>
///   <item><c>0.7 &lt;= confidence &lt; 0.9</c> → Recommend band: invoke the Recommend
///        callback (true → cancel, false → continue, null → downgrade to Observe).
///        A second consecutive Recommend for the same run escalates to Terminate
///        (anti-nuisance, 6.3).</item>
///   <item><c>confidence &lt; 0.7</c> → continue observing.</item>
///  </list>
/// Stop() only sets a flag the loop checks each tick — it never cancels the linked
/// CTS; only analyzers trigger cancellation.
/// </summary>
public sealed class CompletionMonitor : IDisposable
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly ImmutableArray<ICompletionAnalyzer> _analyzers;
    private readonly ITraceQuery _trace;
    private readonly ITraceRecorder _recorder;
    private readonly CancellationTokenSource _linkedCts;
    private readonly Func<CompletionVerdict, Task<bool?>>? _recommendCallback;
    private readonly TimeSpan _pollInterval;
    private readonly object _gate = new();

    /// <summary>Per-analyzer count of consecutive Recommend-band polls (anti-nuisance).</summary>
    private readonly Dictionary<ICompletionAnalyzer, int> _recommendStreaks = [];

    private volatile bool _stopped;
    private Task? _pollTask;

    /// <summary>Create a monitor over the given analyzers and the run's linked CTS.</summary>
    /// <param name="analyzers">Analyzers evaluated on each poll tick.</param>
    /// <param name="trace">Read-only span-tree surface for the analyzers.</param>
    /// <param name="recorder">Span writer for each poll's analyze.completion span.</param>
    /// <param name="linkedCts">CTS linked to the run (passed to <c>engine.RunAsync</c>);
    /// Cancel() terminates the engine. Owned by the caller — the monitor never cancels
    /// it from Stop()/Dispose().</param>
    /// <param name="recommendCallback">Optional callback consulted for Recommend-band
    /// verdicts (0.7 &lt;= confidence &lt; 0.9): true → cancel, false → continue,
    /// null → downgrade to Observe. When absent, Recommend verdicts are always
    /// downgraded to Observe.</param>
    /// <param name="pollInterval">Poll interval; defaults to 500 ms.</param>
    public CompletionMonitor(
        IEnumerable<ICompletionAnalyzer> analyzers,
        ITraceQuery trace,
        ITraceRecorder recorder,
        CancellationTokenSource linkedCts,
        Func<CompletionVerdict, Task<bool?>>? recommendCallback = null,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(analyzers);
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(linkedCts);

        _analyzers = [.. analyzers];
        _trace = trace;
        _recorder = recorder;
        _linkedCts = linkedCts;
        _recommendCallback = recommendCallback;
        _pollInterval = pollInterval is { } p && p > TimeSpan.Zero
            ? p
            : DefaultPollInterval;
    }

    /// <summary>
    /// Launch the background polling loop and return its task. The monitor is
    /// single-start; a second StartAsync throws.
    /// </summary>
    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_pollTask is not null)
                throw new InvalidOperationException(
                    "CompletionMonitor is already started.");
            _stopped = false;
            _pollTask = Task.Run(PollLoopAsync);
            return _pollTask;
        }
    }

    /// <summary>
    /// Signal the polling loop to stop after the current tick. Does NOT cancel the
    /// linked CTS — only analyzers trigger cancellation.
    /// </summary>
    public void Stop() => _stopped = true;

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    // ── Poll loop ─────────────────────────────────────────

    private async Task PollLoopAsync()
    {
        while (!_stopped)
        {
            var stop = false;
            try
            {
                stop = await PollOnceAsync();
            }
            catch (Exception ex)
            {
                // A crashed monitor must not crash the engine: log and keep polling.
                Console.Error.WriteLine(
                    $"[CompletionMonitor] poll tick failed: {ex}");
            }

            if (stop || _stopped)
                break;

            await Task.Delay(_pollInterval);
        }
    }

    /// <summary>Run one poll: evaluate every analyzer and act on non-null verdicts.</summary>
    /// <returns>true when the linked CTS was cancelled — the polling loop must stop.</returns>
    private async Task<bool> PollOnceAsync()
    {
        foreach (var analyzer in _analyzers)
        {
            var verdict = await analyzer.EvaluateAsync(_trace, _linkedCts.Token);
            if (verdict is null)
            {
                // "No signal" — resets the Recommend streak like any non-Recommend verdict.
                _recommendStreaks.Remove(analyzer);
                continue;
            }

            var spanType = verdict.Reason is not null
                           && verdict.Reason.Contains("error_loop", StringComparison.Ordinal)
                ? SpanTypes.AnalyzeErrorLoop
                : SpanTypes.AnalyzeCompletion;

            await using var scope = await _recorder.BeginSpanAsync(
                spanType,
                "completion poll",
                attributes: new Dictionary<string, object>
                {
                    [TraceFields.PollVerdict] = verdict.Reason ?? "(null)",
                    [TraceFields.PollConfidence] = verdict.Confidence,
                },
                // trace-parent-linkage M2: 轮询 span 的 poll.* 属性全为 Extended（Poll profile）。
                // 无 EntryConfig 注入，level 保持缺省 Detailed（= 现状全量行为）。
                profile: TraceSpanFields.Poll,
                ct: CancellationToken.None);

            var (cancel, finalAttributes) = await DecideActionAsync(analyzer, verdict);

            if (cancel)
                _linkedCts.Cancel();

            await scope.End(
                status: "ok",
                attributes: finalAttributes,
                ct: CancellationToken.None);

            if (cancel)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Apply the confidence→action mapping and compute the span's final attributes.
    /// </summary>
    /// <returns>The cancel decision plus the EndSpan attributes
    /// (<c>poll.action</c>, <c>poll.escalated</c>, and — for the callback path —
    /// <c>poll.callback_outcome</c>).</returns>
    private async Task<(bool Cancel, Dictionary<string, object> FinalAttributes)>
        DecideActionAsync(ICompletionAnalyzer analyzer, CompletionVerdict verdict)
    {
        var finalAttributes = new Dictionary<string, object>();

        // Confidence >= 0.9 → Halt/Terminate-class: cancel the engine.
        if (verdict.Confidence >= 0.9)
        {
            finalAttributes[TraceFields.PollAction] = "cancel";
            finalAttributes[TraceFields.PollEscalated] = false;
            return (true, finalAttributes);
        }

        // 0.7 <= Confidence < 0.9 → Recommend band: consult the callback, with
        // anti-nuisance escalation on a second consecutive Recommend (6.3).
        if (verdict.Confidence >= 0.7)
        {
            var streak = _recommendStreaks.GetValueOrDefault(analyzer) + 1;
            _recommendStreaks[analyzer] = streak;

            if (streak >= 2)
            {
                finalAttributes[TraceFields.PollAction] = "escalate";
                finalAttributes[TraceFields.PollEscalated] = true;
                return (true, finalAttributes);
            }

            bool? outcome = _recommendCallback is null
                ? null
                : await _recommendCallback(verdict);

            finalAttributes[TraceFields.PollAction] = "callback";
            finalAttributes[TraceFields.PollEscalated] = false;
            finalAttributes[TraceFields.PollCallbackOutcome] = outcome switch
            {
                true => "cancel",
                false => "continue",
                _ => "observe",
            };

            return (outcome == true, finalAttributes);
        }

        // Confidence < 0.7 → observe; reset the Recommend streak.
        _recommendStreaks.Remove(analyzer);
        finalAttributes[TraceFields.PollAction] = "continue";
        finalAttributes[TraceFields.PollEscalated] = false;
        return (false, finalAttributes);
    }
}
