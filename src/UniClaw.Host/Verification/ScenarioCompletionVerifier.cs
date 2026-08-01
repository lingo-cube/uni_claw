using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;

namespace UniClaw.Host.Verification;

/// <summary>
/// Applies scenario-specific success criteria after the Core engine completes.
/// Core decides traversal completion; Host proves product-level page identity.
/// </summary>
public static class ScenarioCompletionVerifier
{
    public static ScenarioRunOutcome Verify(
        AndroidSettingsScenario scenario,
        TraversalResult engineResult,
        PageAnalysis? finalAnalysis,
        ScenarioRunOutcome outcome,
        ITraceService? trace = null,
        SafetyDecisionJournal? safetyJournal = null,
        bool screenEndOfList = false)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(engineResult);
        ArgumentNullException.ThrowIfNull(outcome);

        if (string.Equals(
                scenario.Mode,
                "enumerate_first_level",
                StringComparison.Ordinal))
        {
            return VerifyEnumerate(
                scenario,
                engineResult,
                finalAnalysis,
                outcome,
                trace,
                safetyJournal,
                screenEndOfList);
        }

        if (!string.Equals(scenario.Mode, "locate_one_item", StringComparison.Ordinal))
        {
            return outcome;
        }

        var finalIdentity = finalAnalysis?.CurrentPath.LastOrDefault();
        var expected = scenario.SuccessCriteria.ExpectedPageIdentities;
        var identityMatched = !string.IsNullOrWhiteSpace(finalIdentity)
            && expected.Any(candidate => IdentityMatches(finalIdentity, candidate));
        var targetActionExecuted =
            engineResult.CompletionReason == TraversalResult.Reasons.TargetFound
            && engineResult.ActionHistory.Any(action => action.Success);

        if (outcome.Status == "success"
            && targetActionExecuted
            && identityMatched)
        {
            return outcome with
            {
                SuccessCriteriaSatisfied = true,
                SuccessEvidence =
                [
                    $"target_action_executed:{engineResult.ActionHistory.Length}",
                    $"target_page_identity:{finalIdentity}",
                    $"steps/{outcome.Steps:D4}/after.png",
                ],
            };
        }

        var detail = !targetActionExecuted
            ? "The target action did not execute successfully before target_found."
            : $"Post-action page identity '{finalIdentity ?? "<empty>"}' did not match the scenario success identities.";
        return outcome with
        {
            Status = "failure",
            CompletionReason = "target_page_identity_not_verified",
            FailingStep = outcome.Steps > 0 ? outcome.Steps : null,
            FailureCause = "verification_mismatch",
            FailureDetail = detail,
            SuccessCriteriaSatisfied = false,
            SuccessEvidence = ImmutableArray<string>.Empty,
        };
    }

    private static ScenarioRunOutcome VerifyEnumerate(
        AndroidSettingsScenario scenario,
        TraversalResult engineResult,
        PageAnalysis? finalAnalysis,
        ScenarioRunOutcome outcome,
        ITraceService? trace,
        SafetyDecisionJournal? safetyJournal,
        bool screenEndOfList)
    {
        if (trace is null || safetyJournal is null)
        {
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
        var traceEndProof = executions.Any(record => string.Equals(
            record.Action,
            "scroll_no_new_elements_end_reached",
            StringComparison.Ordinal));
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
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
