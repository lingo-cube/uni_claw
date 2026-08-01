using System.Collections.Immutable;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Host.Safety;

namespace UniClaw.Host.Verification;

/// <summary>
/// Post-hoc scenario analysis. Runs strictly after <c>TraversalEngine.RunAsync()</c>
/// returns and consumes only the persisted trace (<see cref="ITraceService"/>) and
/// the <see cref="SafetyDecisionJournal"/> — no real-time coupling, no engine
/// internals. Produces the <see cref="ScenarioRunOutcome"/> with a level-2
/// step-level traceback: the failing step and its cause classification
/// (verification mismatch / safety denial / execution failure).
/// </summary>
public sealed class VerificationAnalyzer
{
    private const string VerifyFailAction = "verify.fail";
    private const string SafetyActionPrefix = "safety.";

    private readonly ITraceService _trace;
    private readonly SafetyDecisionJournal _journal;
    private readonly string _runId;

    public VerificationAnalyzer(
        ITraceService trace,
        SafetyDecisionJournal journal,
        string runId)
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _runId = runId;
    }

    public ScenarioRunOutcome Analyze(TraversalResult engineResult)
    {
        var executions = _trace.GetExecutions();
        var errors = _trace.GetErrors();

        var verifyFails = executions
            .Where(e => string.Equals(e.Action, VerifyFailAction, StringComparison.Ordinal)
                        || (e.Action?.StartsWith(
                                "boundary.",
                                StringComparison.Ordinal) == true
                            && string.Equals(
                                e.Status,
                                "violation",
                                StringComparison.Ordinal)))
            .ToList();
        var denied = executions.Where(IsSafetyDenied).ToList();
        var stepErrors = errors
            .Where(e => e.Context?.StepNumber is not null)
            .ToList();

        var failingStep = MaxStep(
            verifyFails.Select(StepOf),
            denied.Select(StepOf),
            stepErrors.Select(e => e.Context!.StepNumber!.Value));

        string? cause = null;
        string? detail = null;
        if (failingStep is int failing)
        {
            var decision = _journal.GetLatest(_runId, failing);
            if (decision is { Allowed: false })
            {
                cause = "safety_denial";
                detail = $"Step {failing} denied by rule '{decision.RuleId}': {decision.Reason}";
            }
            else if (verifyFails.Any(e => StepOf(e) == failing))
            {
                cause = "verification_mismatch";
                detail = $"Step {failing} did not meet its expected change.";
            }
            else
            {
                cause = "execution_failure";
                detail = stepErrors
                    .Where(e => e.Context?.StepNumber == failing)
                    .Select(e => $"{e.ErrorType}: {e.ErrorMessage}")
                    .FirstOrDefault() ?? $"Step {failing} failed to execute.";
            }
        }
        else if (engineResult.CompletionReason == TraversalResult.Reasons.Error)
        {
            cause = "execution_failure";
            detail = engineResult.Error?.Message ?? "engine error";
        }

        var reason = engineResult.CompletionReason;
        var status = failingStep is not null
                     || reason == TraversalResult.Reasons.Error
            ? "failure"
            : reason is TraversalResult.Reasons.AllVisited
                    or TraversalResult.Reasons.TargetFound
                ? "success"
                : "incomplete";

        var actionHistory = engineResult.ActionHistory;
        var safetyExecutions = executions
            .Where(e => e.Action?.StartsWith(SafetyActionPrefix, StringComparison.Ordinal) == true)
            .ToList();
        var safetyAllowed = safetyExecutions.Count(
            e => string.Equals(e.Status, "allow", StringComparison.Ordinal));
        var safetyDenied = safetyExecutions.Count(
            e => string.Equals(e.Status, "deny", StringComparison.Ordinal));

        var issues = ImmutableArray.CreateBuilder<string>();
        foreach (var page in verifyFails.Select(PageOf).Concat(denied.Select(PageOf)))
        {
            if (!string.IsNullOrEmpty(page) && !issues.Contains(page))
                issues.Add(page);
        }

        return new ScenarioRunOutcome(
            _runId,
            status,
            reason,
            engineResult.TotalSteps,
            actionHistory.Count(a => IsScroll(a.Action)),
            actionHistory.Length,
            actionHistory.Count(a => a.Success),
            safetyAllowed,
            safetyDenied,
            issues.ToImmutable(),
            failingStep,
            cause,
            detail);
    }

    private static bool IsSafetyDenied(ExecutionRecord record) =>
        record.SpanType == SpanType.SkipDangerous
        || (record.Action?.StartsWith(SafetyActionPrefix, StringComparison.Ordinal) == true
            && string.Equals(record.Status, "deny", StringComparison.Ordinal));

    private static int? StepOf(ExecutionRecord record) => record.Context?.StepNumber;

    private static string? PageOf(ExecutionRecord record) => record.PageId;

    private static bool IsScroll(string action) =>
        string.Equals(action, "scroll", StringComparison.OrdinalIgnoreCase)
        || string.Equals(action, "swipe", StringComparison.OrdinalIgnoreCase);

    private static int? MaxStep(
        IEnumerable<int?> first,
        IEnumerable<int?> second,
        IEnumerable<int> third)
    {
        var max = -1;
        foreach (var step in first.Concat(second).Concat(third.Select(v => (int?)v)))
        {
            if (step is int value && value > max)
                max = value;
        }

        return max >= 0 ? max : null;
    }
}
