using System.Collections.Immutable;
using System.Diagnostics;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Traversal;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Commands;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using UniClaw.Host.Verification;

namespace UniClaw.Host.Runner;

/// <summary>
/// Template-method base for incremental scenario runners. Owns the shared
/// step loop, reset/execute/verify helpers, and the asset bookkeeping. Mode
/// specific behavior is supplied through virtual hooks whose defaults encode
/// the <c>locate_one_item</c> semantics; <see cref="EnumerateScenarioRunner"/>
/// overrides them for first-level enumeration.
/// </summary>
public abstract class ScenarioRunnerBase
{
    private readonly ScenarioSnapshot _snapshot;
    private readonly TraversalPlan _plan;
    private readonly HostRunServices _services;
    private readonly IScenarioObservationSource _observations;

    protected ScenarioRunnerBase(
        ScenarioSnapshot snapshot,
        TraversalPlan plan,
        HostRunServices services,
        IScenarioObservationSource observations)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _observations = observations
                        ?? throw new ArgumentNullException(nameof(observations));
    }

    protected ScenarioSnapshot Snapshot => _snapshot;
    protected TraversalPlan Plan => _plan;
    protected HostRunServices Services => _services;
    protected IScenarioObservationSource Observations => _observations;

    /// <summary>
    /// The expected-change label the verifier writes for the success criterion
    /// the run is pursuing. Locate runs look for a target page identity; enumerate
    /// runs look for a verified end-of-list.
    /// </summary>
    protected virtual string CompletionExpectedChange => "target_page_identity";

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

                var planning = PlanStep(
                    observation,
                    runId,
                    steps,
                    ref scrolls,
                    cancellationToken);
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
                            CompletionExpectedChange,
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
                            CompletionExpectedChange,
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
                    var denial = await OnSafetyDenied(
                        step,
                        stepPlan,
                        observation,
                        decision,
                        steps,
                        scrolls,
                        actionsAttempted,
                        actionsSucceeded,
                        safetyAllowed,
                        safetyDenied,
                        issues,
                        stopwatch,
                        cancellationToken);
                    if (denial.Finish is not null)
                        return await denial.Finish;
                    continue;
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

                var postClick = await OnActionVerified(
                    stepPlan,
                    step,
                    verified,
                    after,
                    steps,
                    scrolls,
                    actionsAttempted,
                    actionsSucceeded,
                    safetyAllowed,
                    safetyDenied,
                    issues,
                    stopwatch);
                if (postClick.Finish is not null)
                    return await postClick.Finish;

                if (after.Analysis.IsEndOfList
                    && string.Equals(
                        after.PageFingerprint,
                        observation.PageFingerprint,
                        StringComparison.Ordinal)
                    && stepPlan.Action != "click")
                {
                    var scrollEnd = await OnScrollEndOfList(
                        step,
                        steps,
                        scrolls,
                        actionsAttempted,
                        actionsSucceeded,
                        safetyAllowed,
                        safetyDenied,
                        issues,
                        stopwatch,
                        cancellationToken);
                    if (scrollEnd.Finish is not null)
                        return await scrollEnd.Finish;
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

    /// <summary>
    /// Plan the next step. Delegates to the mode-specific planner held by the
    /// subclass. The planner is constructed once per run (subclass ctor) so
    /// stateful planners (e.g. <see cref="EnumerateScenarioStepPlanner"/>) keep
    /// their discovery/sampling state across steps.
    /// </summary>
    protected abstract StepPlanningResult PlanStep(
        ScenarioObservation observation,
        string runId,
        int stepNumber,
        int remainingSteps,
        int remainingScrolls);

    private StepPlanningResult PlanStep(
        ScenarioObservation observation,
        string runId,
        int stepNumber,
        ref int scrolls,
        CancellationToken cancellationToken) =>
        PlanStep(
            observation,
            runId,
            stepNumber,
            _snapshot.Scenario.Boundaries.MaxSteps - stepNumber + 1,
            _snapshot.Scenario.Boundaries.MaxScrolls - scrolls);

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
            result = await _services.EntryPolicyExecutor
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

    protected virtual SafetyCandidate BuildSafetyCandidate(
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
            "back" => _services.ActionExecutor.PressBackAsync(cancellationToken),
            _ => Task.FromResult(false),
        };

    /// <summary>
    /// Verify a step. Dispatches scroll to <see cref="VerifyScroll"/> (shared),
    /// click to <see cref="VerifyClick"/> (mode-specific), and back to
    /// <see cref="VerifyBack"/> (mode-specific).
    /// </summary>
    private StepVerification Verify(
        ScenarioStepPlan plan,
        ScenarioObservation before,
        ScenarioObservation after,
        IReadOnlyDictionary<string, object>? actionEvidence)
    {
        if (plan.Action == "scroll")
            return VerifyScroll(plan, before, after, actionEvidence);
        if (plan.Action == "back")
            return VerifyBack(plan, before, after, actionEvidence);
        return VerifyClick(plan, before, after, actionEvidence);
    }

    /// <summary>
    /// Shared scroll verification: success when the page fingerprint changed
    /// or the end-of-list was reached.
    /// </summary>
    protected static StepVerification VerifyScroll(
        ScenarioStepPlan plan,
        ScenarioObservation before,
        ScenarioObservation after,
        IReadOnlyDictionary<string, object>? actionEvidence)
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

    /// <summary>
    /// Locate-mode click verification: success when the post-click page identity
    /// matches any target alias, or a large visual transition was observed.
    /// Enumerate overrides to detect leaving the Settings home page.
    /// </summary>
    protected virtual StepVerification VerifyClick(
        ScenarioStepPlan plan,
        ScenarioObservation before,
        ScenarioObservation after,
        IReadOnlyDictionary<string, object>? actionEvidence)
    {
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

    /// <summary>
    /// Back-action verification. Locate never backs and throws by default;
    /// enumerate overrides to verify the return to the Settings home page.
    /// </summary>
    protected virtual StepVerification VerifyBack(
        ScenarioStepPlan plan,
        ScenarioObservation before,
        ScenarioObservation after,
        IReadOnlyDictionary<string, object>? actionEvidence) =>
        throw new NotSupportedException(
            "Back action is not supported by this runner mode.");

    /// <summary>
    /// Hook invoked when the safety gate denies a candidate action. Locate
    /// records an issue and finishes <c>blocked</c>; enumerate records a
    /// per-step <c>skipped</c> verification and continues.
    /// </summary>
    protected virtual async Task<LoopControl> OnSafetyDenied(
        StepAssetWriter step,
        ScenarioStepPlan plan,
        ScenarioObservation observation,
        SafetyDecision decision,
        int steps,
        int scrolls,
        int actionsAttempted,
        int actionsSucceeded,
        int safetyAllowed,
        int safetyDenied,
        ImmutableArray<string>.Builder issues,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
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
                plan.ExpectedChange,
                observation.PageFingerprint,
                observation.PageFingerprint,
                observation.PageIdentity,
                observation.PageIdentity,
                false,
                decision.RuleId),
            "blocked",
            cancellationToken: cancellationToken);
        return LoopControl.Terminate(FinishAsync(
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
            stopwatch));
    }

    /// <summary>
    /// Hook invoked after a click/back verification is written. Locate finishes
    /// on success or failure; enumerate continues on success to sample the next
    /// entry and only finishes on a verification failure.
    /// </summary>
    protected virtual async Task<LoopControl> OnActionVerified(
        ScenarioStepPlan plan,
        StepAssetWriter step,
        StepVerification verified,
        ScenarioObservation after,
        int steps,
        int scrolls,
        int actionsAttempted,
        int actionsSucceeded,
        int safetyAllowed,
        int safetyDenied,
        ImmutableArray<string>.Builder issues,
        Stopwatch stopwatch)
    {
        if (plan.Action != "click")
            return LoopControl.Continue();

        if (verified.Status != "success")
        {
            var issue = await RecordIssueAsync(
                "verification",
                "verify",
                "error",
                verified.Reason,
                steps,
                [$"{step.RelativeDirectory}/verification.json"],
                CancellationToken.None);
            issues.Add(issue.Fingerprint);
            return LoopControl.Terminate(FinishAsync(
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
                stopwatch));
        }
        return LoopControl.Terminate(FinishAsync(
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
            [$"{step.RelativeDirectory}/verification.json"]));
    }

    /// <summary>
    /// Hook invoked when the scroll verifier reports a verified end-of-list
    /// with no page change. Locate finishes <c>incomplete</c> (target absent);
    /// enumerate finishes <c>success</c> if every discovered entry was processed.
    /// </summary>
    protected virtual Task<LoopControl> OnScrollEndOfList(
        StepAssetWriter step,
        int steps,
        int scrolls,
        int actionsAttempted,
        int actionsSucceeded,
        int safetyAllowed,
        int safetyDenied,
        ImmutableArray<string>.Builder issues,
        Stopwatch stopwatch,
        CancellationToken cancellationToken) =>
        Task.FromResult(LoopControl.Terminate(FinishAsync(
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
            stopwatch)));

    protected static bool LooksLikeVisualTransition(
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

    /// <summary>
    /// Boundary guard invoked on every observation. Enforces the package
    /// boundary unconditionally, then defers the page-prefix boundary to
    /// <see cref="ValidatePageBoundary"/>. Locate keeps the strict page-prefix
    /// check (home + known target pages); enumerate overrides it to allow
    /// intentionally-diverse child pages whose names aren't known up front —
    /// the safety gate remains the guard against dangerous navigation.
    /// </summary>
    protected void ValidateBoundary(ScenarioObservation observation)
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
        ValidatePageBoundary(observation);
    }

    /// <summary>
    /// Page-prefix boundary check. Locate default: the page identity must start
    /// with one of the scenario's allowed page prefixes. Enumerate overrides to
    /// no-op (child pages are intentionally diverse; the package boundary and
    /// the safety gate are the real guards).
    /// </summary>
    protected virtual void ValidatePageBoundary(ScenarioObservation observation)
    {
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

    protected async Task<RunIssue> RecordIssueAsync(
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

    protected async Task<ScenarioRunOutcome> FinishAsync(
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

    protected static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>
/// Control flow signal returned by the mode-specific hooks. <c>Continue</c>
/// proceeds to the next loop iteration; <c>Terminate</c> terminates the run
/// with the supplied outcome.
/// </summary>
public sealed record class LoopControl(Task<ScenarioRunOutcome>? Finish)
{
    public static LoopControl Continue() => new((Task<ScenarioRunOutcome>?)null);

    public static LoopControl Terminate(Task<ScenarioRunOutcome> finish) =>
        new(finish);
}