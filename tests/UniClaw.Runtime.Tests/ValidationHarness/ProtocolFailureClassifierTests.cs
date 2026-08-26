using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Classification;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Results;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-EVH-006 7.1 capability tests (tasks 7.1): the protocol failure
/// classifier labels failure EVIDENCE SAMPLES with the correct fixed owner and
/// First Divergence Point, derives the label from existing evidence only (call
/// log entries, projected event kinds, snapshot diagnostics, coverage
/// accounting, terminal reason), treats the BLOCKED_FOR_SPEC marker (S2) as
/// Recovery metadata that is never a runtime failure, returns null for a clean
/// scenario with no failure evidence, and by type design refuses a
/// classification without an owner AND a First Divergence Point — a bare
/// "Runtime failed" conclusion cannot be constructed.
/// Structure is EvidenceFixture → (classifier over the evidence) → Evidence
/// Evaluation; assertions check classification capability, never fixed click
/// counts, coordinates, page text, or UI paths.
/// </summary>
public sealed class ProtocolFailureClassifierTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.UtcNow;

    // ── 7.1#1: admission Reject(code) → StrategyCompilation with the
    //    rejection code as the First Divergence Point ─────────────────────────

    [Fact]
    public void AdmissionReject_ClassifiesStrategyCompilation_RejectionCodeIsFirstDivergencePoint()
    {
        var callLog = EmulatorCallLog.Empty.Append(EmulatorCallLogEntry.RejectedByAdmission(
            EmulatorDriver.StartStrategyMethod, "digest", "duplicateStrategy", Timestamp));
        var result = BuildResult(
            terminalState: RunState.Idle,
            terminalReason: null,
            admission: RejectedAdmission("duplicateStrategy"));

        var classification = new ProtocolFailureClassifier().Classify(result, callLog);

        Assert.NotNull(classification);
        Assert.Equal(FailureOwner.StrategyCompilation, classification!.Owner);
        Assert.Contains("duplicateStrategy", classification.FirstDivergencePoint, StringComparison.Ordinal);
        Assert.True(classification.IsFailure);
        Assert.Contains(classification.EvidenceRefs, reference => reference.Contains("call-log[0]", StringComparison.Ordinal));
    }

    // ── 7.1#2: RunFailed with a recorded failure reason → Execution with the
    //    failure reason as the First Divergence Point ──────────────────────────

    [Fact]
    public void RunFailedWithFailureReason_ClassifiesExecution_ReasonIsFirstDivergencePoint()
    {
        const string failureReason = "run failed: settle state could not be reached within the bound";
        var callLog = AcceptedLog();
        var result = BuildResult(
            terminalState: RunState.Failed,
            terminalReason: failureReason,
            events: FailedRunEvents(failureReason));

        var classification = new ProtocolFailureClassifier().Classify(result, callLog);

        Assert.NotNull(classification);
        Assert.Equal(FailureOwner.Execution, classification!.Owner);
        Assert.Equal(failureReason, classification.FirstDivergencePoint);
        Assert.True(classification.IsFailure);
    }

    // ── 7.1#3: a raised trap / started recovery on the projected stream →
    //    Recovery (structural evidence beats terminal text) ────────────────────

    [Fact]
    public void TrapRaisedOnFailedRun_ClassifiesRecovery()
    {
        var trap = new Trap(TrapKind.UnexpectedPage, TrapScope.Agent, null, null, "fixture-world", "observed page differs from the expected semantic entry", null);
        var result = BuildResult(
            terminalState: RunState.Failed,
            terminalReason: "run failed after the trap",
            events:
            [
                new SurfaceRuntimeEvent("ev-0", "TrapRaised", 0, "B-class", null, null, []),
                new SurfaceRuntimeEvent("ev-1", "RunFailed", 1, "B-class", null, "run failed after the trap", []),
            ],
            activeTrap: trap);

        var classification = new ProtocolFailureClassifier().Classify(result, AcceptedLog());

        Assert.NotNull(classification);
        Assert.Equal(FailureOwner.Recovery, classification!.Owner);
        Assert.False(string.IsNullOrWhiteSpace(classification.FirstDivergencePoint));
        Assert.Contains(classification.EvidenceRefs, reference => reference.Contains("TrapRaised", StringComparison.Ordinal));
        Assert.True(classification.IsFailure);
    }

    // ── 7.1#4: BLOCKED_FOR_SPEC marker (S2) → Recovery metadata with the stop
    //    reason as the divergence point; NEVER a runtime failure ───────────────

    [Fact]
    public void BlockedForSpecMarker_ClassifiesRecoveryMetadata_NotARuntimeFailure()
    {
        var callLog = EmulatorCallLog.Empty;
        var result = BuildResult(
            terminalState: RunState.Idle,
            terminalReason: "BLOCKED_FOR_SPEC: STOPPED_AT_S2_REQUIRES_HUMAN_GATE");

        var classification = new ProtocolFailureClassifier().Classify(result, callLog);

        Assert.NotNull(classification);
        Assert.Equal(FailureOwner.Recovery, classification!.Owner);
        Assert.Contains("STOPPED_AT_S2_REQUIRES_HUMAN_GATE", classification.FirstDivergencePoint, StringComparison.Ordinal);
        Assert.False(classification.IsFailure, "a BLOCKED_FOR_SPEC stop is classification metadata, never a runtime failure");
    }

    // ── 7.1#5: device-environment anomaly diagnostics at terminal →
    //    Environment ───────────────────────────────────────────────────────────

    [Fact]
    public void EnvironmentAnomalyDiagnostics_ClassifiesEnvironment()
    {
        var result = BuildResult(
            terminalState: RunState.Failed,
            terminalReason: "run failed",
            diagnostics: ["unexpected external popup appeared during traversal"]);

        var classification = new ProtocolFailureClassifier().Classify(result, AcceptedLog());

        Assert.NotNull(classification);
        Assert.Equal(FailureOwner.Environment, classification!.Owner);
        Assert.True(classification.IsFailure);
    }

    // ── 7.1#6: unresolved coverage at terminal → Discovery ────────────────────

    [Fact]
    public void UnresolvedCoverageAtTerminal_ClassifiesDiscovery()
    {
        var result = BuildResult(
            terminalState: RunState.Failed,
            terminalReason: "run failed",
            scopes: [new CoverageScopeCounts("fixture-scope", Discovered: 3, Visited: 2, Pending: 0, Unresolved: 1, UnknownFrontier: 1)]);

        var classification = new ProtocolFailureClassifier().Classify(result, AcceptedLog());

        Assert.NotNull(classification);
        Assert.Equal(FailureOwner.Discovery, classification!.Owner);
        Assert.True(classification.IsFailure);
    }

    // ── 7.1#7: a clean scenario carries no failure → null classification ──────

    [Fact]
    public void CleanScenario_NoFailureEvidence_ClassifyReturnsNull()
    {
        var result = BuildResult(
            terminalState: RunState.Completed,
            terminalReason: "completed",
            events:
            [
                new SurfaceRuntimeEvent("ev-0", "GoalEvidenceProduced", 0, "B-class", null, "goal satisfied", []),
                new SurfaceRuntimeEvent("ev-1", "RunCompleted", 1, "B-class", null, "completed", []),
            ]);

        Assert.Null(new ProtocolFailureClassifier().Classify(result, AcceptedLog()));
    }

    // ── 7.1#8: goal-only input (no directive) → TestHarness with the
    //    DIRECTIVE_REQUIRED marker as the divergence point ─────────────────────

    [Fact]
    public void DirectiveRequired_ClassifiesTestHarness()
    {
        var callLog = EmulatorCallLog.Empty.Append(EmulatorCallLogEntry.DirectiveRequired(
            EmulatorDriver.StartStrategyMethod, Timestamp));

        var classification = new ProtocolFailureClassifier().Classify(BuildResult(), callLog);

        Assert.NotNull(classification);
        Assert.Equal(FailureOwner.TestHarness, classification!.Owner);
        Assert.Contains("DIRECTIVE_REQUIRED", classification.FirstDivergencePoint, StringComparison.Ordinal);
        Assert.True(classification.IsFailure);
    }

    // ── 7.1#9: type design — a classification without an owner AND a First
    //    Divergence Point cannot be constructed ("Runtime failed" is
    //    unrepresentable) ─────────────────────────────────────────────────────

    [Fact]
    public void ConstructionWithoutOwnerOrFirstDivergencePoint_IsRejectedByTypeDesign()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProtocolFailureClassification((FailureOwner)999, "some diverging evidence"));
        Assert.Throws<ArgumentException>(() =>
            new ProtocolFailureClassification(FailureOwner.Execution, string.Empty));
        Assert.Throws<ArgumentException>(() =>
            new ProtocolFailureClassification(FailureOwner.Execution, "   "));
        Assert.Throws<ArgumentException>(() =>
            new ProtocolFailureClassification(FailureOwner.Execution, null!));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static EmulatorCallLog AcceptedLog()
        => EmulatorCallLog.Empty.Append(EmulatorCallLogEntry.Accepted(
            EmulatorDriver.StartStrategyMethod, "digest", "run-1", Timestamp));

    private static ImmutableArray<SurfaceRuntimeEvent> FailedRunEvents(string reason)
        => [new SurfaceRuntimeEvent("ev-0", "RunFailed", 0, "B-class", null, reason, [])];

    private static AdmissionSection RejectedAdmission(string rejectionCode)
        => new(
            ResultField<string>.Unavailable("rejected admission — no run"),
            ResultField<string>.Unavailable("rejected admission — no run"),
            ResultField<bool>.Direct(false, "admission receipt"),
            ResultField<string?>.Direct(rejectionCode, "admission receipt"),
            ResultField<string?>.Direct("the transported directive was not admitted", "admission receipt"),
            ResultField<int>.Unavailable("rejected admission — no run"));

    /// <summary>Fully-classified result over controllable evidence (mirror of
    /// the boundary-verifier test builder): every field carries a
    /// classification and truth source; the caller overrides only the failure
    /// evidence under test.</summary>
    private static ValidationResult BuildResult(
        RunState terminalState = RunState.Failed,
        string? terminalReason = null,
        ImmutableArray<SurfaceRuntimeEvent> events = default,
        Trap? activeTrap = null,
        ImmutableArray<string> diagnostics = default,
        AdmissionSection? admission = null,
        CoverageScopeCounts[]? scopes = null)
    {
        var eventArray = events.IsDefault ? ImmutableArray<SurfaceRuntimeEvent>.Empty : events;
        var diagnosticArray = diagnostics.IsDefault ? ImmutableArray<string>.Empty : diagnostics;
        var coverageScopes = scopes is null
            ? ResultField<ImmutableArray<CoverageScopeCounts>>.Unavailable("probe: no ledger on this surface")
            : ResultField<ImmutableArray<CoverageScopeCounts>>.Derived([.. scopes], "probe: ledger accounting");

        return new ValidationResult(
            admission ?? new AdmissionSection(
                ResultField<string>.Direct("run-1", "probe: admission receipt"),
                ResultField<string>.Direct("probe-strategy-1", "probe: admission receipt"),
                ResultField<bool>.Direct(true, "probe: admission receipt"),
                ResultField<string?>.Direct(null, "probe: admission receipt"),
                ResultField<string?>.Direct(null, "probe: admission receipt"),
                ResultField<int>.Direct(2, "probe: admission receipt")),
            Lifecycle: new LifecycleSection(ResultField<ImmutableArray<SurfaceRuntimeEvent>>.Derived(eventArray, "probe: projected stream")),
            Snapshot: new SnapshotSection(
                ResultField<string>.Direct("run-1", "probe: snapshot"),
                ResultField<RunState>.Direct(terminalState, "probe: snapshot"),
                ResultField<string?>.Direct(null, "probe: snapshot"),
                ResultField<Trap?>.Direct(activeTrap, "probe: snapshot"),
                ResultField<GoalSummary?>.Direct(null, "probe: snapshot"),
                ResultField<DecisionSummary?>.Direct(null, "probe: snapshot"),
                ResultField<ActionSummary?>.Direct(null, "probe: snapshot"),
                ResultField<RecoverySummary?>.Direct(null, "probe: snapshot"),
                ResultField<GoalEvidenceSummary?>.Direct(null, "probe: snapshot"),
                ResultField<long?>.Direct(null, "probe: snapshot"),
                ResultField<string?>.Direct(null, "probe: snapshot"),
                ResultField<string?>.Direct(null, "probe: snapshot"),
                ResultField<string?>.Direct(null, "probe: snapshot"),
                ResultField<ImmutableArray<string>>.Direct(diagnosticArray, "probe: snapshot")),
            Trap: new TrapSection(
                ResultField<bool>.Direct(activeTrap is not null, "probe: trap"),
                ResultField<Trap?>.Direct(activeTrap, "probe: trap"),
                ResultField<string?>.Direct(null, "probe: trap")),
            Evidence: new EvidenceSection(ResultField<ImmutableArray<ValidationEvidenceEntry>>.Derived([], "probe: evidence resolutions")),
            Coverage: new CoverageSection(
                ResultField<string>.Direct("probe-tier", "probe: tier composition"),
                ResultField<ExplorationLedgerView?>.Unavailable("probe: ledger not on this surface"),
                coverageScopes,
                ResultField<string?>.Unavailable("probe: ledger not on this surface")),
            Terminal: new TerminalSection(
                ResultField<RunState>.Direct(terminalState, "probe: terminal"),
                ResultField<string?>.Direct(terminalReason, "probe: terminal"),
                ResultField<bool?>.Direct(null, "probe: terminal")),
            Boundary: BoundarySection.Placeholder);
    }
}