using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Host.Hooks;

/// <summary>
/// Plan-mode expected-change verification on <c>OnAfterStep</c> (D3). Reads the
/// step's before/after page identity and matches against the expected change
/// carried in the plan node's <c>Meta["expected_change"]</c>, then records the
/// pass/fail into the trace for the post-run <see cref="Verification.VerificationAnalyzer"/>.
/// Never mutates engine state. Intent mode and plan steps without an
/// expected-change are no-ops (the engine's structural ResultVerify check covers
/// them).
/// </summary>
public sealed class VerifyHook : TraversalHookBase
{
    private const string ExpectedChangeKey = "expected_change";
    private const string ExpectedPageChange = "change";

    private readonly ITraceRecorder _traceRecorder;
    private readonly string _runId;
    private TraversalNode? _beforeFrame;
    private string? _beforePage;
    private int _beforeDepth;

    public VerifyHook(
        ITraceRecorder traceRecorder,
        string runId)
    {
        _traceRecorder = traceRecorder
                         ?? throw new ArgumentNullException(nameof(traceRecorder));
        _runId = runId;
    }

    /// <inheritdoc/>
    public override Task OnBeforeStepAsync(ITraversalContext context)
    {
        // Capture the step's frame at the start: by OnAfterStep the engine has
        // already re-synced CurrentFrame to the stack top (a leaf child is popped
        // right after it executes), so the live frame would be the parent, never
        // the node whose operation just ran.
        _beforeFrame = context.CurrentFrame as TraversalNode;
        _beforePage = CurrentPage(context);
        _beforeDepth = context.NodeStack.Depth;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task OnAfterStepAsync(ITraversalContext context)
    {
        if (_beforeFrame?.Meta is not { } meta
            || !meta.TryGetValue(ExpectedChangeKey, out var expectedObj))
        {
            return;
        }

        var expected = expectedObj?.ToString();
        if (string.IsNullOrWhiteSpace(expected))
            return;

        // Only the step on which the node's operation ran gets verified: the engine
        // pops a leaf frame and re-syncs CurrentFrame to the parent right after its
        // Execute, so a depth decrease here means this step executed the captured
        // node. Without this, the hook would fire on every step the leaf is the
        // current frame (NodeSelect/PreconditionCheck) and record duplicate verifies.
        if (context.NodeStack.Depth >= _beforeDepth)
            return;

        var afterPage = CurrentPage(context);
        var pass = ExpectedPageChange.Equals(expected, StringComparison.OrdinalIgnoreCase)
            ? !string.Equals(afterPage, _beforePage, StringComparison.Ordinal)
            : afterPage?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;

        await _traceRecorder.RecordExecutionAsync(
            new ExecutionRecord(
                Action: pass ? "verify.pass" : "verify.fail",
                Status: pass ? "pass" : "fail",
                SpanType: SpanType.StateDecision,
                Context: new TraceContext(
                    NodeId: _beforeFrame.NodeId,
                    StepNumber: context.StepCount,
                    TraceId: _runId),
                PageId: afterPage,
                TargetValue: expected,
                Timestamp: DateTimeOffset.UtcNow,
                Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [ExpectedChangeKey] = expected,
                    ["before_page"] = _beforePage ?? string.Empty,
                    ["after_page"] = afterPage ?? string.Empty,
                }));
    }

    private static string? CurrentPage(ITraversalContext context) =>
        context is TraversalRuntimeContext runtime
            ? runtime.CurrentPageAnalysis?.CurrentPath.LastOrDefault()
            : context.CurrentPath.LastOrDefault();
}
