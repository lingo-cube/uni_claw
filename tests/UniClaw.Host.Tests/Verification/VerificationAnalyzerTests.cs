using System.Collections.Immutable;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Host.Safety;
using UniClaw.Host.Verification;
using Xunit;

namespace UniClaw.Host.Tests.Verification;

public class VerificationAnalyzerTests
{
    private const string RunId = "run-test-001";

    private sealed class Fixture
    {
        public InMemoryTraceStorage Storage { get; } = new();
        public InMemoryTraceRecorder Recorder { get; }
        public InMemoryTraceService Trace { get; }
        public SafetyDecisionJournal Journal { get; } = new();

        public Fixture()
        {
            Recorder = new InMemoryTraceRecorder(Storage);
            Trace = new InMemoryTraceService(Storage);
        }
    }

    private static TraversalResult Result(
        string reason,
        bool success = false,
        int steps = 5,
        Exception? error = null) =>
        new(
            success,
            reason,
            steps,
            1.5,
            ImmutableArray<ActionRecord>.Empty,
            ImmutableArray<string>.Empty,
            ImmutableArray<TraceRecord>.Empty,
            RunId,
            TraversalState.FrameComplete,
            error);

    private static VerificationAnalyzer Analyzer(Fixture f) =>
        new(f.Trace, f.Journal, RunId);

    [Fact(DisplayName = "Analyzer: verify.fail → failure/verification_mismatch with failing step")]
    public async Task VerifyFail_ClassifiesVerificationMismatch()
    {
        var f = new Fixture();
        await f.Recorder.RecordExecutionAsync(
            new ExecutionRecord(
                Action: "verify.fail",
                Status: "fail",
                SpanType: SpanType.StateDecision,
                Context: new TraceContext(StepNumber: 3, TraceId: RunId),
                PageId: "wifi_settings"));

        var outcome = Analyzer(f).Analyze(Result(TraversalResult.Reasons.MaxSteps));

        Assert.Equal("failure", outcome.Status);
        Assert.Equal(3, outcome.FailingStep);
        Assert.Equal("verification_mismatch", outcome.FailureCause);
        Assert.Contains("wifi_settings", outcome.IssueFingerprints);
    }

    [Fact(DisplayName = "Analyzer: safety deny → failure/safety_denial classified from journal")]
    public async Task SafetyDeny_ClassifiesSafetyDenial()
    {
        var f = new Fixture();
        await f.Recorder.RecordExecutionAsync(
            new ExecutionRecord(
                Action: "safety.click",
                Status: "deny",
                SpanType: SpanType.SkipDangerous,
                Context: new TraceContext(StepNumber: 2, TraceId: RunId),
                PageId: "settings_home"));
        await f.Journal.RecordAsync(new SafetyDecision(
            "1",
            "settings-v1",
            "1",
            "hash",
            "deny",
            "deny.default",
            "No explicit safe-navigation rule allowed the candidate.",
            "click",
            "wifi",
            null,
            "Settings",
            null,
            0.99,
            RunId,
            2,
            "fp-2",
            "engine_hook",
            DateTimeOffset.UtcNow));

        var outcome = Analyzer(f).Analyze(Result(TraversalResult.Reasons.MaxSteps));

        Assert.Equal("failure", outcome.Status);
        Assert.Equal(2, outcome.FailingStep);
        Assert.Equal("safety_denial", outcome.FailureCause);
        Assert.Equal(1, outcome.SafetyDenied);
        Assert.Contains("settings_home", outcome.IssueFingerprints);
    }

    [Fact(DisplayName = "Analyzer: error record → failure/execution_failure")]
    public async Task ErrorRecord_ClassifiesExecutionFailure()
    {
        var f = new Fixture();
        await f.Recorder.RecordErrorAsync(
            new ErrorRecord(
                "TapFailure",
                "Action executor failed to tap.",
                ErrorSeverity.Error,
                new TraceContext(StepNumber: 1, TraceId: RunId)));

        var outcome = Analyzer(f).Analyze(Result(
            TraversalResult.Reasons.Error,
            error: new InvalidOperationException("engine error")));

        Assert.Equal("failure", outcome.Status);
        Assert.Equal(1, outcome.FailingStep);
        Assert.Equal("execution_failure", outcome.FailureCause);
        Assert.Contains("TapFailure", outcome.FailureDetail);
    }

    [Fact(DisplayName = "Analyzer: no signals + all_visited → success")]
    public void AllVisited_IsSuccess()
    {
        var f = new Fixture();
        var outcome = Analyzer(f).Analyze(Result(TraversalResult.Reasons.AllVisited, success: true));

        Assert.Equal("success", outcome.Status);
        Assert.Null(outcome.FailingStep);
        Assert.Null(outcome.FailureCause);
    }

    [Fact(DisplayName = "Analyzer: no signals + max_steps → incomplete")]
    public void MaxSteps_WithoutFailureSignals_IsIncomplete()
    {
        var f = new Fixture();
        var outcome = Analyzer(f).Analyze(Result(TraversalResult.Reasons.MaxSteps));

        Assert.Equal("incomplete", outcome.Status);
        Assert.Null(outcome.FailingStep);
        Assert.Null(outcome.FailureCause);
    }
}
