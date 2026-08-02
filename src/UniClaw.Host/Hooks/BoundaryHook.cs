using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Host.Hooks;

/// <summary>
/// Records package/page-prefix boundary violations instead of silently ignoring
/// them. The engine's step loop is Log-and-Continue — hooks cannot fail a step —
/// so this hook observes and records violations into the trace; the
/// <see cref="Verification.VerificationAnalyzer"/> classifies them post-run.
/// The package is read from the device (the engine context carries no package
/// name); a delegate is injected so tests can supply a fake.
/// </summary>
public sealed class BoundaryHook : TraversalHookBase
{
    private readonly Func<Task<string>> _getCurrentPackage;
    private readonly string _expectedPackage;
    private readonly IReadOnlyList<string> _allowedPagePrefixes;
    private readonly ITraceRecorder _traceRecorder;
    private readonly string _traceId;
    private readonly bool _allowFirstLevelChildPages;
    private readonly int _checkInterval;
    private int _stepsSinceCheck;

    public BoundaryHook(
        Func<Task<string>> getCurrentPackage,
        string expectedPackage,
        IEnumerable<string> allowedPagePrefixes,
        ITraceRecorder traceRecorder,
        string traceId,
        bool allowFirstLevelChildPages = false,
        int checkInterval = 5)
    {
        _getCurrentPackage = getCurrentPackage
                             ?? throw new ArgumentNullException(nameof(getCurrentPackage));
        _expectedPackage = expectedPackage
                           ?? throw new ArgumentNullException(nameof(expectedPackage));
        _allowedPagePrefixes = (allowedPagePrefixes
                                ?? throw new ArgumentNullException(nameof(allowedPagePrefixes)))
            .ToList();
        _traceRecorder = traceRecorder
                         ?? throw new ArgumentNullException(nameof(traceRecorder));
        _traceId = traceId;
        _allowFirstLevelChildPages = allowFirstLevelChildPages;
        _checkInterval = checkInterval > 0
            ? checkInterval
            : throw new ArgumentOutOfRangeException(nameof(checkInterval));
        // First check always runs: seed the counter at the interval.
        _stepsSinceCheck = _checkInterval;
    }

    /// <inheritdoc/>
    public override Task OnAfterStepAsync(ITraversalContext context)
    {
        // The foreground package is stable within a page; the ADB dumpsys call
        // is expensive (~200-500ms), so it runs every N steps. (Fingerprint-
        // triggered checks arrive with the deterministic-first layer.)
        if (++_stepsSinceCheck < _checkInterval)
            return Task.CompletedTask;
        _stepsSinceCheck = 0;
        return OnAfterStepCheckedAsync(context);
    }

    private async Task OnAfterStepCheckedAsync(ITraversalContext context)
    {
        var package = await _getCurrentPackage();
        if (!string.Equals(package, _expectedPackage, StringComparison.Ordinal))
        {
            await RecordViolationAsync(
                context,
                "package_boundary",
                $"Observed package '{package}' instead of '{_expectedPackage}'.");
            return;
        }

        var analyzedPath = (context as TraversalRuntimeContext)
            ?.CurrentPageAnalysis
            ?.CurrentPath;
        var page = analyzedPath?.LastOrDefault()
                   ?? context.CurrentPath.LastOrDefault();
        var isFirstLevelChild = _allowFirstLevelChildPages
                                && context.NodeStack.Depth is >= 2 and <= 3;
        if (_allowedPagePrefixes.Count > 0
            && !string.IsNullOrEmpty(page)
            && !isFirstLevelChild
            && !_allowedPagePrefixes.Any(
                prefix => page.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await RecordViolationAsync(
                context,
                "page_boundary",
                $"Observed page '{page}' outside scenario boundary.");
        }
    }

    private async Task RecordViolationAsync(
        ITraversalContext context,
        string kind,
        string message)
    {
        await _traceRecorder.RecordExecutionAsync(
            new ExecutionRecord(
                Action: $"boundary.{kind}",
                Status: "violation",
                SpanType: SpanType.ErrorHandling,
                Context: new TraceContext(
                    NodeId: context.CurrentFrame?.NodeId,
                    StepNumber: context.StepCount,
                    TraceId: _traceId),
                PageId: (context as TraversalRuntimeContext)
                            ?.CurrentPageAnalysis
                            ?.CurrentPath
                            .LastOrDefault()
                        ?? context.CurrentPath.LastOrDefault(),
                Timestamp: DateTimeOffset.UtcNow,
                Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["kind"] = kind,
                    ["message"] = message,
                }));
    }
}
