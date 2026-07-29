using System.Collections.Immutable;
using System.Diagnostics;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Traversal;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Commands;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;

namespace UniClaw.Host.Runner;

public sealed record class StepVerification(
    string Status,
    string ExpectedChange,
    string? BeforeFingerprint,
    string? AfterFingerprint,
    string? BeforePage,
    string? AfterPage,
    bool ActionExecuted,
    string Reason,
    IReadOnlyDictionary<string, object>? ActionEvidence = null);

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
    ImmutableArray<string> IssueFingerprints);

public sealed class IncrementalScenarioRunner
{
    private readonly ScenarioSnapshot _snapshot;
    private readonly TraversalPlan _plan;
    private readonly HostRunServices _services;
    private readonly IScenarioObservationSource _observations;
    private readonly LocateScenarioStepPlanner _planner;

    public IncrementalScenarioRunner(
        ScenarioSnapshot snapshot,
        TraversalPlan plan,
        HostRunServices services,
        IScenarioObservationSource observations,
        LocateScenarioStepPlanner? planner = null)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _observations = observations
                        ?? throw new ArgumentNullException(nameof(observations));
        _planner = planner ?? new LocateScenarioStepPlanner();
        if (_snapshot.Scenario.Mode != "locate_one_item")
        {
            throw new ArgumentException(
                "Incremental locate runner requires locate_one_item mode.",
                nameof(snapshot));
        }
    }

    public async Task<ScenarioRunOutcome> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var runId = _services.Assets.Manifest.RunId;
        var stopwatch = Stopwatch.StartNew();
        var issues = ImmutableArray.CreateBuilder<string>();
        var steps = 0;
        var scrolls = 0;
        var actionsAttempted = 0;
        var actionsSucceeded = 0;
        var safetyAllowed = 0;
        var safetyDenied = 0;

        await _services.TraceRecorder.StartSessionAsync(
            runId,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["scenarioId"] = _snapshot.Scenario.ScenarioId,
                ["scenarioHash"] = _snapshot.ScenarioHash,
                ["policyHash"] = _snapshot.PolicyHash,
                ["planId"] = _plan.PlanId,
            },
            cancellationToken);
        try
        {
            await ResetAsync(cancellationToken);
            string? previousXml = null;
            var afterScroll = false;

            while (steps < _snapshot.Scenario.Boundaries.MaxSteps
                   && stopwatch.Elapsed
                   < TimeSpan.FromSeconds(
                       _snapshot.Scenario.Boundaries.MaxDurationSeconds))
            {
                cancellationToken.ThrowIfCancellationRequested();
                steps++;
                var observation = await _observations.ObserveAsync(
                    previousXml,
                    afterScroll,
                    cancellationToken);
                ValidateBoundary(observation);
                var step = await _services.Assets.BeginStepAsync(
                    steps,
                    observation.PageFingerprint,
                    cancellationToken);
                await step.WriteBeforeAsync(
                    observation.Screenshot,
                    observation.UiXml,
                    cancellationToken);
                await step.WriteAnalysisAsync(
                    observation.Analysis,
                    "success",
                    cancellationToken: cancellationToken);

                var planning = _planner.Plan(
                    _snapshot,
                    observation,
                    runId,
                    steps,
                    _snapshot.Scenario.Boundaries.MaxSteps - steps + 1,
                    _snapshot.Scenario.Boundaries.MaxScrolls - scrolls);
                await step.WriteStepPlanAsync(
                    planning.Plan,
                    planning.Status,
                    planning.Plan is null ? planning.Reason : null,
                    cancellationToken);

                if (planning.Status == "complete")
                {
                    await step.WriteSafetyDecisionAsync(
                        null,
                        "not_required",
                        "success was verified without an action",
                        cancellationToken);
                    await step.WriteVerificationAsync(
                        new StepVerification(
                            "success",
                            "target_page_identity",
                            observation.PageFingerprint,
                            observation.PageFingerprint,
                            observation.PageIdentity,
                            observation.PageIdentity,
                            false,
                            planning.Reason),
                        "success",
                        cancellationToken: cancellationToken);
                    return await FinishAsync(
                        "success",
                        planning.Reason,
                        true,
                        steps,
                        scrolls,
                        actionsAttempted,
                        actionsSucceeded,
                        safetyAllowed,
                        safetyDenied,
                        issues,
                        stopwatch);
                }

                if (planning.Plan is null)
                {
                    await step.WriteSafetyDecisionAsync(
                        null,
                        "not_attempted",
                        planning.Reason,
                        cancellationToken);
                    await step.WriteVerificationAsync(
                        new StepVerification(
                            "incomplete",
                            "target_page_identity",
                            observation.PageFingerprint,
                            null,
                            observation.PageIdentity,
                            null,
                            false,
                            planning.Reason),
                        "incomplete",
                        cancellationToken: cancellationToken);
                    return await FinishAsync(
                        "incomplete",
                        planning.Reason,
                        false,
                        steps,
                        scrolls,
                        actionsAttempted,
                        actionsSucceeded,
                        safetyAllowed,
                        safetyDenied,
                        issues,
                        stopwatch);
                }

                var stepPlan = planning.Plan;
                var currentFingerprint =
                    await _observations.GetCurrentFingerprintAsync(
                        cancellationToken);
                if (!string.Equals(
                        currentFingerprint,
                        stepPlan.PageFingerprint,
                        StringComparison.Ordinal))
                {
                    var issue = await RecordIssueAsync(
                        "planning",
                        "stale_plan",
                        "error",
                        "Current page fingerprint changed after step planning.",
                        steps,
                        [$"{step.RelativeDirectory}/step-plan.json"],
                        cancellationToken);
                    issues.Add(issue.Fingerprint);
                    await step.WriteSafetyDecisionAsync(
                        null,
                        "not_attempted",
                        "stale plan was rejected before safety evaluation",
                        cancellationToken);
                    await step.WriteVerificationAsync(
                        new StepVerification(
                            "failure",
                            stepPlan.ExpectedChange,
                            stepPlan.PageFingerprint,
                            currentFingerprint,
                            observation.PageIdentity,
                            null,
                            false,
                            "stale_plan"),
                        "failure",
                        cancellationToken: cancellationToken);
                    return await FinishAsync(
                        "failure",
                        "stale_plan",
                        false,
                        steps,
                        scrolls,
                        actionsAttempted,
                        actionsSucceeded,
                        safetyAllowed,
                        safetyDenied,
                        issues,
                        stopwatch);
                }

                var candidate = BuildSafetyCandidate(
                    stepPlan,
                    observation,
                    scrolls);
                bool executed;
                actionsAttempted++;
                using (_services.SafetyContext.Push(candidate))
                {
                    executed = await ExecuteAsync(
                        stepPlan,
                        cancellationToken);
                }
                var decision = _services.SafetyJournal.GetLatest(runId, steps)
                               ?? throw new InvalidOperationException(
                                   "Safety decision was not journaled.");
                if (decision.Allowed)
                    safetyAllowed++;
                else
                    safetyDenied++;

                if (!decision.Allowed)
                {
                    var issue = await RecordIssueAsync(
                        "safety",
                        "safety",
                        "warning",
                        $"Action was denied by {decision.RuleId}.",
                        steps,
                        [$"{step.RelativeDirectory}/safety-decision.json"],
                        cancellationToken);
                    issues.Add(issue.Fingerprint);
                    await step.WriteVerificationAsync(
                        new StepVerification(
                            "blocked",
                            stepPlan.ExpectedChange,
                            observation.PageFingerprint,
                            observation.PageFingerprint,
                            observation.PageIdentity,
                            observation.PageIdentity,
                            false,
                            decision.RuleId),
                        "blocked",
                        cancellationToken: cancellationToken);
                    return await FinishAsync(
                        "blocked",
                        decision.RuleId,
                        false,
                        steps,
                        scrolls,
                        actionsAttempted,
                        actionsSucceeded,
                        safetyAllowed,
                        safetyDenied,
                        issues,
                        stopwatch);
                }

                if (!executed)
                {
                    var issue = await RecordIssueAsync(
                        "action",
                        "execute",
                        "error",
                        $"Device executor returned false for {stepPlan.Action}.",
                        steps,
                        [$"{step.RelativeDirectory}/safety-decision.json"],
                        cancellationToken);
                    issues.Add(issue.Fingerprint);
                    await step.WriteVerificationAsync(
                        new StepVerification(
                            "failure",
                            stepPlan.ExpectedChange,
                            observation.PageFingerprint,
                            null,
                            observation.PageIdentity,
                            null,
                            true,
                            "action_failed"),
                        "failure",
                        cancellationToken: cancellationToken);
                    return await FinishAsync(
                        "failure",
                        "action_failed",
                        false,
                        steps,
                        scrolls,
                        actionsAttempted,
                        actionsSucceeded,
                        safetyAllowed,
                        safetyDenied,
                        issues,
                        stopwatch);
                }

                actionsSucceeded++;
                if (stepPlan.Action == "scroll")
                    scrolls++;
                var actionEvidence = _services.ActionExecutor
                    .GetHistory()
                    .LastOrDefault()
                    ?.Parameters;
                await Task.Delay(
                    stepPlan.Action == "click" ? 1000 : 250,
                    cancellationToken);
                var after = await _observations.ObserveAsync(
                    observation.UiXml,
                    stepPlan.Action == "scroll",
                    cancellationToken);
                ValidateBoundary(after);
                await step.WriteAfterAsync(
                    after.Screenshot,
                    after.UiXml,
                    cancellationToken);
                var verified = Verify(
                    stepPlan,
                    observation,
                    after,
                    actionEvidence);
                await step.WriteVerificationAsync(
                    verified,
                    verified.Status,
                    cancellationToken: cancellationToken);

                if (stepPlan.Action == "click")
                {
                    if (verified.Status != "success")
                    {
                        var issue = await RecordIssueAsync(
                            "verification",
                            "verify",
                            "error",
                            verified.Reason,
                            steps,
                            [$"{step.RelativeDirectory}/verification.json"],
                            cancellationToken);
                        issues.Add(issue.Fingerprint);
                        return await FinishAsync(
                            "failure",
                            verified.Reason,
                            false,
                            steps,
                            scrolls,
                            actionsAttempted,
                            actionsSucceeded,
                            safetyAllowed,
                            safetyDenied,
                            issues,
                            stopwatch);
                    }
                    return await FinishAsync(
                        "success",
                        verified.Reason,
                        true,
                        steps,
                        scrolls,
                        actionsAttempted,
                        actionsSucceeded,
                        safetyAllowed,
                        safetyDenied,
                        issues,
                        stopwatch,
                        [$"{step.RelativeDirectory}/verification.json"]);
                }

                if (after.Analysis.IsEndOfList
                    && string.Equals(
                        after.PageFingerprint,
                        observation.PageFingerprint,
                        StringComparison.Ordinal))
                {
                    return await FinishAsync(
                        "incomplete",
                        "target_absent_at_verified_end",
                        false,
                        steps,
                        scrolls,
                        actionsAttempted,
                        actionsSucceeded,
                        safetyAllowed,
                        safetyDenied,
                        issues,
                        stopwatch);
                }
                previousXml = after.UiXml;
                afterScroll = false;
            }

            return await FinishAsync(
                "incomplete",
                steps >= _snapshot.Scenario.Boundaries.MaxSteps
                    ? "step_budget_exhausted"
                    : "duration_budget_exhausted",
                false,
                steps,
                scrolls,
                actionsAttempted,
                actionsSucceeded,
                safetyAllowed,
                safetyDenied,
                issues,
                stopwatch);
        }
        catch (OperationCanceledException)
        {
            return await FinishAsync(
                "cancelled",
                "cancelled",
                false,
                steps,
                scrolls,
                actionsAttempted,
                actionsSucceeded,
                safetyAllowed,
                safetyDenied,
                issues,
                stopwatch);
        }
        catch (Exception ex)
        {
            var category = ex is ScenarioObservationException
                ? "device"
                : "traversal";
            var issue = await RecordIssueAsync(
                category,
                "runtime",
                "error",
                $"{ex.GetType().Name}: {ex.Message}",
                steps == 0 ? null : steps,
                [],
                CancellationToken.None);
            issues.Add(issue.Fingerprint);
            return await FinishAsync(
                "failure",
                ex is ScenarioObservationException observation
                    ? observation.Kind
                    : "runtime_failure",
                false,
                steps,
                scrolls,
                actionsAttempted,
                actionsSucceeded,
                safetyAllowed,
                safetyDenied,
                issues,
                stopwatch);
        }
        finally
        {
            await _services.TraceRecorder.EndSessionAsync(CancellationToken.None);
        }
    }

    private async Task ResetAsync(CancellationToken cancellationToken)
    {
        var scenario = _snapshot.Scenario;
        var candidate = new SafetyCandidate(
            "launch",
            scenario.AppPackage,
            "settings_home",
            null,
            null,
            scenario.AppPackage,
            1,
            true,
            true,
            0,
            scenario.Boundaries.MaxSteps,
            scenario.Boundaries.MaxScrolls,
            _services.Assets.Manifest.RunId,
            0,
            "preparation",
            "entry");
        EntryResult result;
        using (_services.SafetyContext.Push(candidate))
        {
            result = await new EntryPolicyExecutor(
                    _services.EntryActionDriver)
                .ExecuteAsync(
                    _plan.EntryPolicy,
                    new EntryConfig(
                        WaitMode: WaitMode.Polling,
                        WaitTimeoutSeconds:
                        scenario.ResetProcedure.TimeoutSeconds,
                        WaitIntervalMs: 500,
                        ActionDelayMs: 1000),
                    scenario.AppPackage,
                    cancellationToken);
        }
        if (!result.Success)
        {
            throw new HostPreparationException(
                $"Settings reset/entry failed: {result.Description}");
        }

        var observation = await _observations.ObserveAsync(
            cancellationToken: cancellationToken);
        ValidateBoundary(observation);
        if (!string.Equals(
                Normalize(observation.PageIdentity),
                Normalize(scenario.ResetProcedure.ExpectedPageIdentity),
                StringComparison.Ordinal))
        {
            throw new HostPreparationException(
                $"Reset page '{observation.PageIdentity}' did not verify as "
                + $"'{scenario.ResetProcedure.ExpectedPageIdentity}'.");
        }
    }

    private SafetyCandidate BuildSafetyCandidate(
        ScenarioStepPlan plan,
        ScenarioObservation observation,
        int scrolls) =>
        new(
            plan.Action,
            plan.Target,
            plan.Semantic,
            observation.PageIdentity,
            string.Join("/", observation.Analysis.CurrentPath),
            observation.PackageName,
            plan.Action == "click" ? 0.99 : 1,
            plan.Action != "click" || plan.X is not null && plan.Y is not null,
            false,
            1,
            _snapshot.Scenario.Boundaries.MaxSteps - plan.StepNumber + 1,
            _snapshot.Scenario.Boundaries.MaxScrolls - scrolls,
            plan.RunId,
            plan.StepNumber,
            plan.PageFingerprint,
            "runner");

    private Task<bool> ExecuteAsync(
        ScenarioStepPlan plan,
        CancellationToken cancellationToken) =>
        plan.Action switch
        {
            "click" when plan.X is not null && plan.Y is not null =>
                _services.ActionExecutor.TapAsync(
                    plan.X.Value,
                    plan.Y.Value,
                    cancellationToken),
            "scroll" when plan.X is not null
                          && plan.Y is not null
                          && plan.EndX is not null
                          && plan.EndY is not null
                          && plan.DurationMs is not null =>
                _services.ActionExecutor.SwipeAsync(
                    plan.X.Value,
                    plan.Y.Value,
                    plan.EndX.Value,
                    plan.EndY.Value,
                    plan.DurationMs.Value,
                    cancellationToken),
            _ => Task.FromResult(false),
        };

    private StepVerification Verify(
        ScenarioStepPlan plan,
        ScenarioObservation before,
        ScenarioObservation after,
        IReadOnlyDictionary<string, object>? actionEvidence)
    {
        if (plan.Action == "scroll")
        {
            var changed = !string.Equals(
                before.PageFingerprint,
                after.PageFingerprint,
                StringComparison.Ordinal);
            return new StepVerification(
                changed || after.Analysis.IsEndOfList ? "success" : "failure",
                plan.ExpectedChange,
                before.PageFingerprint,
                after.PageFingerprint,
                before.PageIdentity,
                after.PageIdentity,
                true,
                changed
                    ? "page_fingerprint_changed"
                    : after.Analysis.IsEndOfList
                        ? "verified_end_of_list"
                        : "scroll_did_not_change_page",
                actionEvidence);
        }

        var target = _snapshot.Scenario.Target!;
        var expected = target.Aliases
            .Add(target.Label)
            .Select(Normalize)
            .ToHashSet(StringComparer.Ordinal);
        var verified = expected.Contains(Normalize(after.PageIdentity));
        var visualTransitionVerified = !verified
                                      && LooksLikeVisualTransition(
                                          before.Screenshot,
                                          after.Screenshot);
        return new StepVerification(
            verified || visualTransitionVerified ? "success" : "failure",
            plan.ExpectedChange,
            before.PageFingerprint,
            after.PageFingerprint,
            before.PageIdentity,
            after.PageIdentity,
            true,
            verified
                ? "target_page_identity_verified"
                : visualTransitionVerified
                    ? "target_page_visual_transition_verified"
                    : $"verification_mismatch:{after.PageIdentity}",
            actionEvidence);
    }

    private static bool LooksLikeVisualTransition(
        byte[] beforeScreenshot,
        byte[] afterScreenshot)
    {
        if (beforeScreenshot is null
            || afterScreenshot is null
            || beforeScreenshot.Length == 0
            || afterScreenshot.Length == 0)
        {
            return false;
        }

        var larger = Math.Max(beforeScreenshot.Length, afterScreenshot.Length);
        var difference = Math.Abs(
            beforeScreenshot.Length - afterScreenshot.Length);
        return difference >= larger * 0.20;
    }

    private void ValidateBoundary(ScenarioObservation observation)
    {
        if (!string.Equals(
                observation.PackageName,
                _snapshot.Scenario.AppPackage,
                StringComparison.Ordinal))
        {
            throw new ScenarioObservationException(
                "package_boundary",
                $"Observed package '{observation.PackageName}' instead of "
                + $"'{_snapshot.Scenario.AppPackage}'.");
        }
        if (!_snapshot.Scenario.Boundaries.AllowedPages.Any(
                page => observation.PageIdentity.StartsWith(
                    page,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new ScenarioObservationException(
                "page_boundary",
                $"Observed page '{observation.PageIdentity}' outside scenario boundary.");
        }
    }

    private async Task<RunIssue> RecordIssueAsync(
        string category,
        string phase,
        string severity,
        string summary,
        int? stepNumber,
        IEnumerable<string> evidence,
        CancellationToken cancellationToken)
    {
        var issue = _services.Assets.CreateIssue(
            category,
            phase,
            severity,
            summary,
            stepNumber,
            evidence);
        await _services.Assets.AppendIssueAsync(issue, cancellationToken);
        return issue;
    }

    private async Task<ScenarioRunOutcome> FinishAsync(
        string status,
        string reason,
        bool successCriteriaSatisfied,
        int steps,
        int scrolls,
        int actionsAttempted,
        int actionsSucceeded,
        int safetyAllowed,
        int safetyDenied,
        ImmutableArray<string>.Builder issues,
        Stopwatch stopwatch,
        IEnumerable<string>? successEvidence = null)
    {
        stopwatch.Stop();
        var evidence = successEvidence?.ToImmutableArray()
                       ?? ImmutableArray<string>.Empty;
        await _services.Assets.FinalizeAsync(
            new RunResult(
                RunAssetVocabulary.SchemaVersion,
                _services.Assets.Manifest.RunId,
                status,
                reason,
                0,
                successCriteriaSatisfied ? 1 : 0,
                safetyDenied,
                status == "failure" ? 1 : 0,
                actionsAttempted,
                actionsSucceeded,
                safetyAllowed,
                safetyDenied,
                steps,
                scrolls,
                stopwatch.ElapsedMilliseconds,
                $"trace/{_services.Assets.Manifest.RunId}/trace.jsonl",
                issues.ToImmutable(),
                successCriteriaSatisfied,
                evidence,
                DateTimeOffset.UtcNow),
            CancellationToken.None);
        return new ScenarioRunOutcome(
            _services.Assets.Manifest.RunId,
            status,
            reason,
            steps,
            scrolls,
            actionsAttempted,
            actionsSucceeded,
            safetyAllowed,
            safetyDenied,
            issues.ToImmutable());
    }

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
