using System.Collections.Immutable;

namespace UniClaw.Host.Verification;

/// <summary>
/// Post-run outcome produced by <see cref="VerificationAnalyzer"/> after
/// <c>TraversalEngine.RunAsync()</c> completes. Status distinguishes
/// <c>success</c> / <c>failure</c> / <c>incomplete</c>; the traceback fields
/// (FailingStep + FailureCause) carry the level-2 classification: which step
/// failed and why — <c>verification_mismatch</c> / <c>safety_denial</c> /
/// <c>execution_failure</c>.
/// </summary>
public sealed record class ScenarioRunOutcome(
    string RunId,
    string Status,
    string CompletionReason,
    int Steps,
    int Scrolls,
    int ActionsAttempted,
    int ActionsSucceeded,
    int SafetyAllowed,
    int SafetyDenied,
    ImmutableArray<string> IssueFingerprints,
    int? FailingStep = null,
    string? FailureCause = null,
    string? FailureDetail = null,
    bool SuccessCriteriaSatisfied = false,
    ImmutableArray<string> SuccessEvidence = default,
    int DiscoveredEntries = 0,
    int VisitedEntries = 0,
    int SkippedEntries = 0,
    int FailedEntries = 0);
