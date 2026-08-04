using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using UniClaw.Host.Verification;
using Xunit;

namespace UniClaw.Host.Tests.Verification;

public sealed class ScenarioCompletionVerifierTests
{
    private static readonly AndroidSettingsScenario Scenario =
        new ScenarioCatalog().LoadSnapshot(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "locate-one-item.v1.json"))
            .Scenario;
    private static readonly AndroidSettingsScenario EnumerateScenario =
        new ScenarioCatalog().LoadSnapshot(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "enumerate-settings-safely.v1.json"))
            .Scenario;

    [Fact]
    public async Task Locate_ReturnsOutcomeUnchanged_HostNoLongerJudges()
    {
        // V2: locate verification moved to TraceTool VerifyEngine.
        // ScenarioCompletionVerifier returns the outcome unchanged for locate mode.
        var result = Result(withSuccessfulAction: true);
        var analysis = Analysis("About emulated device");

        var verified = await ScenarioCompletionVerifier.Verify(
            Scenario,
            result,
            analysis,
            Outcome());

        Assert.Equal("success", verified.Status);
        Assert.False(verified.SuccessCriteriaSatisfied);  // Host no longer sets this
    }

    [Fact]
    public async Task Locate_ReturnsOutcomeUnchanged_RegardlessOfIdentity()
    {
        // V2: Host passes through the outcome for locate — TraceTool judges identity.
        var verified = await ScenarioCompletionVerifier.Verify(
            Scenario,
            Result(withSuccessfulAction: true),
            Analysis("Settings"),
            Outcome());

        Assert.Equal("success", verified.Status);  // Outcome is passed through unchanged
        Assert.False(verified.SuccessCriteriaSatisfied);
    }

    [Fact]
    public async Task Locate_ReturnsOutcomeUnchanged_RegardlessOfActionSuccess()
    {
        // V2: Host passes through the outcome for locate — TraceTool judges action success.
        var verified = await ScenarioCompletionVerifier.Verify(
            Scenario,
            Result(withSuccessfulAction: false),
            Analysis("About phone"),
            Outcome());

        Assert.Equal("success", verified.Status);  // Outcome is passed through unchanged
        Assert.False(verified.SuccessCriteriaSatisfied);
    }

    [Fact]
    public async Task Enumerate_DiscoveryVisitSkipEndAndReturnEvidence_Passes()
    {
        var (trace, recorder) = TraceFixture();
        await RecordAsync(
            recorder,
            new ExecutionRecord(
                "generate",
                "ok",
                SpanType.DfsForward,
                ParentNodeId: "root",
                ChildNodeId: "dyn_menu_container_Network & internet_root"),
            new ExecutionRecord(
                "generate",
                "ok",
                SpanType.DfsForward,
                ParentNodeId: "root",
                ChildNodeId: "dyn_menu_container_Reset options_root"),
            new ExecutionRecord(
                "click",
                "success",
                SpanType.StateDecision,
                TargetValue: "Network & internet"),
            new ExecutionRecord(
                "scroll_no_new_elements_end_reached",
                "ok",
                SpanType.StateDecision));
        var journal = new SafetyDecisionJournal();
        await journal.RecordAsync(Decision(1, "Network & internet", allowed: true));
        await journal.RecordAsync(Decision(2, "Reset options", allowed: false));

        var verified = await ScenarioCompletionVerifier.Verify(
            EnumerateScenario,
            EnumerateResult(),
            Analysis("Settings"),
            EnumerateOutcome(),
            trace,
            journal);

        Assert.Equal("success", verified.Status);
        Assert.Equal("enumerated_all_first_level", verified.CompletionReason);
        Assert.True(verified.SuccessCriteriaSatisfied);
        Assert.Equal(2, verified.DiscoveredEntries);
        Assert.Equal(1, verified.VisitedEntries);
        Assert.Equal(1, verified.SkippedEntries);
        Assert.Equal(0, verified.FailedEntries);
        Assert.Contains("end_of_list:verified", verified.SuccessEvidence);
    }

    [Fact]
    public async Task Enumerate_ChildControlClick_IsRejected()
    {
        var (trace, recorder) = TraceFixture();
        await RecordAsync(
            recorder,
            new ExecutionRecord(
                "generate",
                "ok",
                SpanType.DfsForward,
                ParentNodeId: "root",
                ChildNodeId: "dyn_menu_container_Network & internet_root"),
            new ExecutionRecord(
                "scroll_no_new_elements_end_reached",
                "ok",
                SpanType.StateDecision));
        var journal = new SafetyDecisionJournal();
        await journal.RecordAsync(Decision(1, "Network & internet", allowed: true));
        await journal.RecordAsync(Decision(2, "Wi-Fi toggle", allowed: true));

        var verified = await ScenarioCompletionVerifier.Verify(
            EnumerateScenario,
            EnumerateResult(),
            Analysis("Settings"),
            EnumerateOutcome(),
            trace,
            journal);

        Assert.Equal("failure", verified.Status);
        Assert.Equal("child_control_execution_detected", verified.CompletionReason);
        Assert.Equal(1, verified.FailedEntries);
    }

    [Fact]
    public async Task Enumerate_WithoutEndProof_IsIncomplete()
    {
        var (trace, recorder) = TraceFixture();
        await RecordAsync(
            recorder,
            new ExecutionRecord(
                "generate",
                "ok",
                SpanType.DfsForward,
                ParentNodeId: "root",
                ChildNodeId: "dyn_menu_container_Network & internet_root"));
        var journal = new SafetyDecisionJournal();
        await journal.RecordAsync(Decision(1, "Network & internet", allowed: true));

        var verified = await ScenarioCompletionVerifier.Verify(
            EnumerateScenario,
            EnumerateResult(),
            Analysis("Settings"),
            EnumerateOutcome(),
            trace,
            journal);

        Assert.Equal("incomplete", verified.Status);
        Assert.Equal("end_of_list_unproven", verified.CompletionReason);
    }

    private static PageAnalysis Analysis(string identity) =>
        new(
            Direction.Left,
            Direction.Left,
            CurrentPath: [identity]);

    private static TraversalResult Result(bool withSuccessfulAction) =>
        new(
            Success: true,
            CompletionReason: TraversalResult.Reasons.TargetFound,
            TotalSteps: 7,
            ElapsedSeconds: 1,
            ActionHistory: withSuccessfulAction
                ? [new ActionRecord(
                    "tap",
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>(),
                    true)]
                : ImmutableArray<ActionRecord>.Empty,
            VisitedPages: ImmutableArray<string>.Empty,
            Trace: ImmutableArray<TraceRecord>.Empty,
            TraceId: "trace",
            FinalState: TraversalState.ResultVerify);

    private static ScenarioRunOutcome Outcome() =>
        new(
            "run",
            "success",
            TraversalResult.Reasons.TargetFound,
            7,
            0,
            1,
            1,
            1,
            0,
            ImmutableArray<string>.Empty);

    private static TraversalResult EnumerateResult() =>
        new(
            Success: true,
            CompletionReason: TraversalResult.Reasons.AllVisited,
            TotalSteps: 20,
            ElapsedSeconds: 2,
            ActionHistory: ImmutableArray<ActionRecord>.Empty,
            VisitedPages: ImmutableArray<string>.Empty,
            Trace: ImmutableArray<TraceRecord>.Empty,
            TraceId: "trace",
            FinalState: TraversalState.FrameComplete);

    private static ScenarioRunOutcome EnumerateOutcome() =>
        new(
            "run-enumerate",
            "success",
            TraversalResult.Reasons.AllVisited,
            20,
            2,
            2,
            2,
            2,
            0,
            ImmutableArray<string>.Empty);

    private static SafetyDecision Decision(
        int step,
        string target,
        bool allowed) =>
        new(
            "1",
            "settings-read-only-v1",
            "1.0.0",
            "hash",
            allowed ? "allow" : "deny",
            allowed ? "allow.navigation_row" : "deny.dangerous_text",
            allowed ? "allowed" : "denied",
            "click",
            target.ToLowerInvariant(),
            "navigation_row",
            "Settings",
            "Settings",
            0.99,
            "run-enumerate",
            step,
            "fingerprint",
            "engine_hook",
            DateTimeOffset.UtcNow);

    private static (ITraceService Service, ITraceRecorder Recorder) TraceFixture()
    {
        var storage = new InMemoryTraceStorage();
        return (new InMemoryTraceService(storage), new InMemoryTraceRecorder(storage));
    }

    private static async Task RecordAsync(
        ITraceRecorder recorder,
        params ExecutionRecord[] records)
    {
        foreach (var record in records)
            await recorder.RecordExecutionAsync(record);
    }
}
