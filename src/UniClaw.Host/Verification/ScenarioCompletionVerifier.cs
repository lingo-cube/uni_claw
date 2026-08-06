using System.Collections.Immutable;
using System.Text.RegularExpressions;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;

namespace UniClaw.Host.Verification;

/// <summary>
/// Applies scenario-specific success criteria after the Core engine completes.
/// V2: only the enumerate branch is judged in Host (not yet migrated to
/// TraceTool); locate_one_item and other modes produce pending_verification
/// runs judged by the TraceTool verify command.
/// </summary>
public static class ScenarioCompletionVerifier
{
    public static async Task<ScenarioRunOutcome> Verify(
        AndroidSettingsScenario scenario,
        TraversalResult engineResult,
        PageAnalysis? finalAnalysis,
        ScenarioRunOutcome outcome,
        ITraceService? trace = null,
        SafetyDecisionJournal? safetyJournal = null,
        bool screenEndOfList = false,
        Func<string, string, string, string, int?, Task>? issueSink = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(engineResult);
        ArgumentNullException.ThrowIfNull(outcome);

        if (string.Equals(
                scenario.Mode,
                "enumerate_first_level",
                StringComparison.Ordinal))
        {
            return await VerifyEnumerate(
                scenario,
                engineResult,
                finalAnalysis,
                outcome,
                trace,
                safetyJournal,
                screenEndOfList,
                issueSink);
        }

        // locate_one_item and other modes: Host no longer judges.
        // TraceTool verify command produces the final verdict.
        return outcome;
    }

    private static async Task<ScenarioRunOutcome> VerifyEnumerate(
        AndroidSettingsScenario scenario,
        TraversalResult engineResult,
        PageAnalysis? finalAnalysis,
        ScenarioRunOutcome outcome,
        ITraceService? trace,
        SafetyDecisionJournal? safetyJournal,
        bool screenEndOfList,
        Func<string, string, string, string, int?, Task>? issueSink)
    {
        if (trace is null || safetyJournal is null)
        {
            if (issueSink is not null)
            {
                await issueSink(
                    "verification",
                    "completion",
                    "error",
                    "enumeration_evidence_unavailable: trace/safety journals missing.",
                    null);
            }
            return outcome with
            {
                Status = "failure",
                CompletionReason = "enumeration_evidence_unavailable",
                FailureCause = "verification_mismatch",
                FailureDetail = "Enumerate verification requires trace and safety journals.",
            };
        }

        var executions = trace.GetExecutions();
        var discovered = executions
            .Where(record => string.Equals(record.Action, "generate", StringComparison.Ordinal)
                             && string.Equals(record.ParentNodeId, "root", StringComparison.Ordinal))
            .Select(record => ExtractRootEntry(record.ChildNodeId))
            .Where(name => name is not null)
            .Select(name => Normalize(name!))
            .ToHashSet(StringComparer.Ordinal);
        var clickDecisions = safetyJournal.GetRun(outcome.RunId)
            .Where(decision => string.Equals(
                decision.Action,
                "click",
                StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(decision.NormalizedTarget))
            .ToArray();
        var rootClicks = clickDecisions
            .Where(decision => discovered.Contains(
                Normalize(decision.NormalizedTarget!)))
            .ToArray();
        var outOfScopeClicks = clickDecisions
            .Except(rootClicks)
            .ToArray();
        var skipped = rootClicks
            .Where(decision => !decision.Allowed)
            .Select(decision => Normalize(decision.NormalizedTarget!))
            .ToHashSet(StringComparer.Ordinal);
        var failedTargets = executions
            .Where(record => string.Equals(record.Action, "click", StringComparison.Ordinal)
                             && string.Equals(record.Status, "fail", StringComparison.Ordinal)
                             && !string.IsNullOrWhiteSpace(record.TargetValue))
            .Select(record => Normalize(record.TargetValue!))
            .Where(target => discovered.Contains(target) && !skipped.Contains(target))
            .ToHashSet(StringComparer.Ordinal);
        var visited = rootClicks
            .Where(decision => decision.Allowed)
            .Select(decision => Normalize(decision.NormalizedTarget!))
            .Where(target => !failedTargets.Contains(target))
            .ToHashSet(StringComparer.Ordinal);
        var accounted = visited.Concat(skipped).ToHashSet(StringComparer.Ordinal);
        var unaccounted = discovered.Count(target => !accounted.Contains(target));
        var traceEndProof = executions.Any(record =>
            string.Equals(record.Action, "scroll_roi_end_reached", StringComparison.Ordinal)
            || string.Equals(record.Action, "scroll_roi_content_guard", StringComparison.Ordinal)
            // Legacy signal kept for simulation environments (D7).
            || string.Equals(record.Action, "scroll_no_new_elements_end_reached", StringComparison.Ordinal));
        var endProven = !scenario.SuccessCriteria.RequireEndOfList
                        || traceEndProof
                        || screenEndOfList;
        var finalIdentity = finalAnalysis?.CurrentPath.LastOrDefault();
        var homeRestored = !string.IsNullOrWhiteSpace(finalIdentity)
                           && scenario.SuccessCriteria.ExpectedPageIdentities.Any(
                               expected => IdentityMatches(finalIdentity, expected));
        var engineCompleted = string.Equals(
            engineResult.CompletionReason,
            TraversalResult.Reasons.AllVisited,
            StringComparison.Ordinal);

        var counts = outcome with
        {
            DiscoveredEntries = discovered.Count,
            VisitedEntries = visited.Count,
            SkippedEntries = skipped.Count,
            FailedEntries = failedTargets.Count + outOfScopeClicks.Length,
        };
        if (engineCompleted
            && discovered.Count > 0
            && unaccounted == 0
            && failedTargets.Count == 0
            && outOfScopeClicks.Length == 0
            && endProven
            && homeRestored)
        {
            return counts with
            {
                Status = "success",
                CompletionReason = "enumerated_all_first_level",
                FailingStep = null,
                FailureCause = null,
                FailureDetail = null,
                SuccessCriteriaSatisfied = true,
                SuccessEvidence =
                [
                    $"first_level_discovered:{discovered.Count}",
                    $"first_level_visited:{visited.Count}",
                    $"first_level_skipped:{skipped.Count}",
                    "end_of_list:verified",
                    $"return_page_identity:{finalIdentity}",
                ],
            };
        }

        var (status, reason, detail) = outOfScopeClicks.Length > 0
            ? ("failure", "child_control_execution_detected",
                $"Observed {outOfScopeClicks.Length} click decision(s) outside discovered first-level entries.")
            : failedTargets.Count > 0
                ? ("failure", "first_level_action_failed",
                    $"{failedTargets.Count} first-level action(s) failed.")
                : !homeRestored
                    ? ("failure", "settings_home_not_restored",
                        $"Final page identity '{finalIdentity ?? "<empty>"}' was not Settings.")
                    : !endProven
                        ? ("incomplete", "end_of_list_unproven",
                            "Neither screen state nor traversal trace proved the end of the first-level list.")
                        : ("incomplete", "enumeration_accounting_incomplete",
                            $"Discovered={discovered.Count}, accounted={accounted.Count}, engine={engineResult.CompletionReason}.");
        if (issueSink is not null && status != "success")
        {
            await issueSink(
                "verification",
                "completion",
                status == "failure" ? "error" : "warning",
                $"{reason}: {detail}",
                outcome.Steps > 0 ? outcome.Steps : null);
        }
        return counts with
        {
            Status = status,
            CompletionReason = reason,
            FailureCause = status == "failure"
                ? "verification_mismatch"
                : null,
            FailureDetail = detail,
            SuccessCriteriaSatisfied = false,
            SuccessEvidence = ImmutableArray<string>.Empty,
        };
    }

    private static string? ExtractRootEntry(string? nodeId)
    {
        const string prefix = "dyn_menu_container_";
        const string suffix = "_root";
        if (string.IsNullOrWhiteSpace(nodeId)
            || !nodeId.StartsWith(prefix, StringComparison.Ordinal)
            || !nodeId.EndsWith(suffix, StringComparison.Ordinal))
        {
            return null;
        }

        return nodeId[prefix.Length..^suffix.Length];
    }

    private static bool IdentityMatches(string actual, string expected)
    {
        var normalizedActual = Normalize(actual);
        var normalizedExpected = Normalize(expected);
        return string.Equals(
                   normalizedActual,
                   normalizedExpected,
                   StringComparison.Ordinal)
               || normalizedActual.Contains(
                   normalizedExpected,
                   StringComparison.Ordinal)
               || normalizedExpected.Contains(
                   normalizedActual,
                   StringComparison.Ordinal);
    }

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            Regex.Replace(
                    value.Trim().ToLowerInvariant(),
                    @"\s*,\s*",
                    ", ")
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
