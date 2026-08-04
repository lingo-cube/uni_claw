using System.Collections.Immutable;
using UniClaw.Core.Observability;
using UniClaw.Host.Artifacts;
using Xunit;

namespace UniClaw.TraceTool.Tests;

/// <summary>
/// Task 7.4 — DiagnoseEngine rule-engine tests. The failure snapshot's trace carries no
/// error records, its single AI call succeeds, and its steps are back-to-back (no
/// &gt;30s timeline gaps) — so the fixture run itself produces zero evidence. The
/// evidence-collection path is therefore exercised with an in-memory run carrying an
/// error record and a failed AI call, which is exactly what the rule engine consumes.
/// </summary>
public sealed class DiagnoseTests : IClassFixture<TraceRunFixture>
{
    private readonly TraceRunFixture _fixture;

    public DiagnoseTests(TraceRunFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Diagnose_FailureRun_IdentifiesFailureCause()
    {
        var result = await DiagnoseEngine.DiagnoseAsync(_fixture.FailureRun);

        Assert.Equal("failure", result.Status);
        // Cause mirrors result.json completionReason.
        Assert.Equal("target_page_identity_not_verified", result.Verdict.Cause);
        Assert.Equal("Run failed: target_page_identity_not_verified", result.Verdict.Summary);
    }

    [Fact]
    public async Task Diagnose_SuccessRun_ReturnsSuccessVerdict()
    {
        var result = await DiagnoseEngine.DiagnoseAsync(_fixture.SuccessRun);

        Assert.Equal("success", result.Status);
        Assert.Contains("successfully", result.Verdict.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnose_ReturnsEvidence()
    {
        // Fixture runs carry no evidence — build an in-memory run with error + failed
        // AI call records to exercise the evidence rules deterministically.
        var storage = new InMemoryTraceStorage();
        storage.AddError(new ErrorRecord(
            "device.offline",
            "device disconnected mid-run",
            ErrorSeverity.Error,
            new TraceContext(StepNumber: 2)));
        storage.AddAICall(new AICallRecord(
            "analyze_visual",
            "local-vision",
            Success: false,
            LatencyMs: 2500,
            new TraceContext(StepNumber: 3)));
        var service = new InMemoryTraceService(storage);
        var run = new TraceRun("/runs/synthetic", null, null, service, Array.Empty<StepAsset>(), Array.Empty<RunIssue>());

        var result = await DiagnoseEngine.DiagnoseAsync(run);

        Assert.NotEmpty(result.Evidence);
        Assert.Contains(result.Evidence, e => e.Type == "error" && e.StepNumber == "2");
        Assert.Contains(result.Evidence, e => e.Type == "ai_call_failures");
        Assert.Equal("medium", result.Verdict.Confidence);
        Assert.Contains(
            result.Suggestions,
            s => s.Contains("provider credentials", StringComparison.Ordinal));
        Assert.Equal(2, result.Evidence.Count);
    }

    [Fact]
    public async Task Diagnose_ReturnsRunContext()
    {
        var result = await DiagnoseEngine.DiagnoseAsync(_fixture.FailureRun);

        Assert.Equal(_fixture.FailureRun.RunId, result.Run.RunId);
        // Fixtures carry no taskId in manifest.json — the aggregate falls back to
        // "unknown" rather than null.
        Assert.Equal("unknown", result.Run.TaskId);
        Assert.Equal("unknown", result.Run.Purpose);
        Assert.Equal(_fixture.FailureRun.RunId, result.RunId);
    }

    [Fact]
    public async Task Diagnose_EmptyTrace_HandlesGracefully()
    {
        var emptyTrace = new InMemoryTraceService(new InMemoryTraceStorage());
        var run = new TraceRun(
            "/runs/empty", null, null, emptyTrace, Array.Empty<StepAsset>(), Array.Empty<RunIssue>());

        var result = await DiagnoseEngine.DiagnoseAsync(run);

        Assert.Equal("unknown", result.Status);
        Assert.Equal("unknown", result.Verdict.Cause);
        Assert.Null(result.Verdict.FailingStep);
        Assert.Empty(result.Evidence);
        Assert.Equal("low", result.Verdict.Confidence);
        Assert.Equal(
            Path.Combine("/runs/empty", "trace", "trace.jsonl"),
            result.ArtifactPaths.TracePath);
    }

    [Fact]
    public async Task Diagnose_ErrorLoopRun_ReportsErrorLoopStuck()
    {
        // Spec scenario: a stuck error loop — 5+ consecutive engine.step spans whose
        // children are ALL entry.skipped. The detection is delegated to the Host
        // ErrorLoopAnalyzer (design D3); the verdict must surface as cause
        // "error_loop_stuck" with an error_loop evidence entry and the failing step
        // positioned at the last consecutive all-skipped step.
        var storage = new InMemoryTraceStorage();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        for (var i = 0; i < 5; i++)
        {
            storage.OpenSpan(SpanTypes.EngineStep, $"step {i}", $"s{i}", "run",
                start.AddSeconds(i + 1), null, null);
            storage.OpenSpan(SpanTypes.EntrySkipped, $"skip {i}", $"sk{i}", $"s{i}",
                start.AddSeconds(i + 1.1), null, null);
        }
        var service = new InMemoryTraceService(storage);
        var run = new TraceRun("/runs/error-loop", null, null, service, Array.Empty<StepAsset>(), Array.Empty<RunIssue>());

        var result = await DiagnoseEngine.DiagnoseAsync(run);

        Assert.Equal("error_loop_stuck", result.Verdict.Cause);
        Assert.Equal("Step at index 5", result.Verdict.FailingStep);
        var errorLoopEvidence = Assert.Single(result.Evidence);
        Assert.Equal("error_loop", errorLoopEvidence.Type);
        Assert.Contains("5 consecutive all-skipped steps", errorLoopEvidence.Description);
        Assert.Equal("5", errorLoopEvidence.StepNumber);
    }

    [Fact]
    public async Task Diagnose_SkipRateRun_ReportsErrorLoopStuck()
    {
        // skip_rate_too_high branch: no 5-step consecutive all-skipped run, but
        // entry.skipped (5) exceeds entry.visited (1) × 4 → same "error_loop_stuck"
        // cause, evidence carries the skipped/visited ratio, failing step is the
        // last step that skipped entries.
        var storage = new InMemoryTraceStorage();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        storage.OpenSpan(SpanTypes.EngineStep, "step 1", "s1", "run", start.AddSeconds(1), null, null);
        storage.OpenSpan(SpanTypes.EntryVisited, "Network", "v1", "s1", start.AddSeconds(2), null, null);
        for (var i = 0; i < 5; i++)
            storage.OpenSpan(SpanTypes.EntrySkipped, $"skip {i}", $"sk{i}", "s1",
                start.AddSeconds(2.1 + i * 0.01), null, null);
        var service = new InMemoryTraceService(storage);
        var run = new TraceRun("/runs/skip-rate", null, null, service, Array.Empty<StepAsset>(), Array.Empty<RunIssue>());

        var result = await DiagnoseEngine.DiagnoseAsync(run);

        Assert.Equal("error_loop_stuck", result.Verdict.Cause);
        Assert.Equal("Step at index 1", result.Verdict.FailingStep);
        var errorLoopEvidence = Assert.Single(result.Evidence);
        Assert.Equal("error_loop", errorLoopEvidence.Type);
        Assert.Contains("skipped=5 vs visited=1", errorLoopEvidence.Description);
        Assert.Equal("1", errorLoopEvidence.StepNumber);
    }

    [Fact]
    public async Task Diagnose_RunWithIssueFingerprints_AddsFingerprintEvidence()
    {
        // VerificationAnalyzer classification from run artifacts (design D3):
        // non-empty issueFingerprints in result.json are passed through as evidence.
        var service = new InMemoryTraceService(new InMemoryTraceStorage());
        var result = new RunResult(
            "1", "run-fp", "failure", "target_page_identity_not_verified",
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "trace/trace.jsonl",
            ["fp-001", "fp-002"], false, [], DateTimeOffset.UtcNow);
        var run = new TraceRun("/runs/fp", null, result, service, Array.Empty<StepAsset>(), Array.Empty<RunIssue>());

        var diagnosis = await DiagnoseEngine.DiagnoseAsync(run);

        var fingerprintEvidence = Assert.Single(diagnosis.Evidence);
        Assert.Equal("issue_fingerprints", fingerprintEvidence.Type);
        Assert.Contains("fp-001", fingerprintEvidence.Description);
        Assert.Contains("fp-002", fingerprintEvidence.Description);
    }

    [Fact]
    public async Task Diagnose_EmptyResultFingerprints_WithIssues_AddsIssueEvidence()
    {
        // trace-issue-evidence (D-3): the Host writes the real failure reason
        // into issues.jsonl without backfilling result.json fingerprints —
        // diagnose must surface the fingerprint + summary as issue_fingerprints
        // evidence and lift the confidence above the empty-evidence floor.
        var service = new InMemoryTraceService(new InMemoryTraceStorage());
        var result = new RunResult(
            "1", "run-issues", "failure", "target_page_identity_not_verified",
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "trace/trace.jsonl",
            [], false, [], DateTimeOffset.UtcNow);
        var issues = new List<RunIssue>
        {
            new RunIssue(
                "1", "issue-1", "a1b2c3d4e5f6a7b8c9d0",
                "verification", "completion", "error",
                "target_page_identity_not_verified: Post-action page identity '<empty>' did not match the scenario success identities.",
                "run-issues", 4, [], DateTimeOffset.UtcNow, 1, null, "open"),
        };
        var run = new TraceRun(
            "/runs/issues", null, result, service, Array.Empty<StepAsset>(), issues);

        var diagnosis = await DiagnoseEngine.DiagnoseAsync(run);

        var fingerprintEvidence = Assert.Single(diagnosis.Evidence);
        Assert.Equal("issue_fingerprints", fingerprintEvidence.Type);
        Assert.Contains("issues.jsonl", fingerprintEvidence.Description);
        Assert.Contains("a1b2c3d4e5f6a7b8c9d0", fingerprintEvidence.Description);
        Assert.Contains(
            "did not match the scenario success identities",
            fingerprintEvidence.Description);
        Assert.Equal("medium", diagnosis.Verdict.Confidence);
    }

    [Fact]
    public async Task Diagnose_ResultFingerprintsPresent_DoNotDuplicateIssues()
    {
        // trace-issue-evidence (D-3): non-empty result fingerprints win — issues
        // from issues.jsonl must not be duplicated into the evidence.
        var service = new InMemoryTraceService(new InMemoryTraceStorage());
        var result = new RunResult(
            "1", "run-fp", "failure", "target_page_identity_not_verified",
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "trace/trace.jsonl",
            ["fp-001", "fp-002"], false, [], DateTimeOffset.UtcNow);
        var issues = new List<RunIssue>
        {
            new RunIssue(
                "1", "issue-1", "issue-fp",
                "verification", "completion", "error",
                "target_page_identity_not_verified: page identity mismatch.",
                "run-fp", 4, [], DateTimeOffset.UtcNow, 1, null, "open"),
        };
        var run = new TraceRun(
            "/runs/fp-issues", null, result, service, Array.Empty<StepAsset>(), issues);

        var diagnosis = await DiagnoseEngine.DiagnoseAsync(run);

        var fingerprintEvidence = Assert.Single(diagnosis.Evidence);
        Assert.Equal("issue_fingerprints", fingerprintEvidence.Type);
        Assert.Contains("fp-001", fingerprintEvidence.Description);
        Assert.Contains("fp-002", fingerprintEvidence.Description);
        Assert.DoesNotContain("issue-fp", fingerprintEvidence.Description);
        Assert.DoesNotContain("issues.jsonl", fingerprintEvidence.Description);
    }

    [Fact]
    public async Task Diagnose_IssuesWithoutUsableFingerprint_OmitsEvidence()
    {
        // trace-issue-evidence spec scenario 3: issues without a usable
        // fingerprint must not produce an empty-fingerprint evidence entry.
        var service = new InMemoryTraceService(new InMemoryTraceStorage());
        var result = new RunResult(
            "1", "run-fp", "failure", "target_page_identity_not_verified",
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "trace/trace.jsonl",
            [], false, [], DateTimeOffset.UtcNow);
        var issues = new List<RunIssue>
        {
            new RunIssue(
                "1", "issue-1", "   ",
                "verification", "completion", "error",
                "target_page_identity_not_verified: page identity mismatch.",
                "run-fp", 4, [], DateTimeOffset.UtcNow, 1, null, "open"),
        };
        var run = new TraceRun(
            "/runs/no-fp", null, result, service, Array.Empty<StepAsset>(), issues);

        var diagnosis = await DiagnoseEngine.DiagnoseAsync(run);

        Assert.Empty(diagnosis.Evidence);
        Assert.Equal("low", diagnosis.Verdict.Confidence);
    }
}
