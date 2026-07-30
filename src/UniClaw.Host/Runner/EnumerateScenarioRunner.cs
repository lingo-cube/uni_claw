using System.Collections.Immutable;
using System.Diagnostics;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Commands;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;

namespace UniClaw.Host.Runner;

/// <summary>
/// Runner for <c>enumerate_first_level</c> scenarios. Samples each first-level
/// Settings entry by clicking in, capturing the child page identity, and
/// backing out. Dangerous entries are denied by the safety gate and recorded
/// as per-step <c>skipped</c> (not failures). The run succeeds when the
/// verified end-of-list is reached with every discovered entry processed.
/// </summary>
public sealed class EnumerateScenarioRunner : ScenarioRunnerBase
{
    private readonly EnumerateScenarioStepPlanner _planner;

    public EnumerateScenarioRunner(
        ScenarioSnapshot snapshot,
        TraversalPlan plan,
        HostRunServices services,
        IScenarioObservationSource observations,
        EnumerateScenarioStepPlanner? planner = null)
        : base(snapshot, plan, services, observations)
    {
        _planner = planner ?? new EnumerateScenarioStepPlanner();
        if (snapshot.Scenario.Mode != "enumerate_first_level")
        {
            throw new ArgumentException(
                "Enumerate runner requires enumerate_first_level mode.",
                nameof(snapshot));
        }
    }

    /// <summary>
    /// The enumerate success criterion is reaching a verified end-of-list, not
    /// a target page identity.
    /// </summary>
    protected override string CompletionExpectedChange => "verified_end_of_list";

    /// <summary>
    /// Enumerate intentionally navigates to diverse child pages whose names
    /// aren't known up front (the scenario JSON lists only the Settings home).
    /// The package boundary (still enforced by <see cref="ScenarioRunnerBase.ValidateBoundary"/>)
    /// and the safety gate remain the real guards; the page-prefix check is
    /// inappropriate here.
    /// </summary>
    protected override void ValidatePageBoundary(ScenarioObservation observation)
    {
        // No-op: child pages are intentionally diverse. The package boundary is
        // still enforced by the base ValidateBoundary.
    }

    protected override StepPlanningResult PlanStep(
        ScenarioObservation observation,
        string runId,
        int stepNumber,
        int remainingSteps,
        int remainingScrolls) =>
        _planner.Plan(
            Snapshot,
            observation,
            runId,
            stepNumber,
            remainingSteps,
            remainingScrolls);

    /// <summary>
    /// Click verification for enumeration: success when the page identity
    /// changed away from the Settings home page (we entered a child page to
    /// sample it); failure when we are still on Settings (the click did not
    /// navigate).
    /// </summary>
    protected override StepVerification VerifyClick(
        ScenarioStepPlan plan,
        ScenarioObservation before,
        ScenarioObservation after,
        IReadOnlyDictionary<string, object>? actionEvidence)
    {
        var home = Normalize(
            Snapshot.Scenario.ResetProcedure.ExpectedPageIdentity);
        var leftHome = Normalize(after.PageIdentity) != home;
        return new StepVerification(
            leftHome ? "success" : "failure",
            plan.ExpectedChange,
            before.PageFingerprint,
            after.PageFingerprint,
            before.PageIdentity,
            after.PageIdentity,
            true,
            leftHome
                ? "entered_child_page"
                : $"click_did_not_leave_home:{after.PageIdentity}",
            actionEvidence);
    }

    /// <summary>
    /// Back verification for enumeration: success when we returned to the
    /// Settings home page; failure when we are still off Settings (the back
    /// did not return home, or landed on an unexpected page).
    /// </summary>
    protected override StepVerification VerifyBack(
        ScenarioStepPlan plan,
        ScenarioObservation before,
        ScenarioObservation after,
        IReadOnlyDictionary<string, object>? actionEvidence)
    {
        var home = Normalize(
            Snapshot.Scenario.ResetProcedure.ExpectedPageIdentity);
        var returned = Normalize(after.PageIdentity) == home;
        return new StepVerification(
            returned ? "success" : "failure",
            plan.ExpectedChange,
            before.PageFingerprint,
            after.PageFingerprint,
            before.PageIdentity,
            after.PageIdentity,
            true,
            returned
                ? "returned_to_settings_home"
                : "return_verification_failed",
            actionEvidence);
    }

    /// <summary>
    /// When the safety gate denies a dangerous entry, record a per-step
    /// <c>skipped</c> verification with the rule id, mark the entry skipped in
    /// the planner, and continue to the next entry. Skips are expected
    /// (exploratory decision 3) — no issue is recorded and the run is never
    /// <c>blocked</c>.
    /// </summary>
    protected override async Task<LoopControl> OnSafetyDenied(
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
        await step.WriteVerificationAsync(
            new StepVerification(
                "skipped",
                plan.ExpectedChange,
                observation.PageFingerprint,
                observation.PageFingerprint,
                observation.PageIdentity,
                observation.PageIdentity,
                false,
                decision.RuleId),
            "skipped",
            cancellationToken: cancellationToken);
        _planner.MarkSkipped(plan.Target ?? string.Empty, decision.RuleId);
        await Task.Yield();
        return LoopControl.Continue();
    }

    /// <summary>
    /// After a click or back is verified: on success continue to the next step
    /// (the planner will mark the entry sampled on the next home observation,
    /// or plan the next entry); on failure finish <c>failure</c> with the
    /// verification reason. For a back failure the reason is
    /// <c>return_verification_failed</c>.
    /// </summary>
    protected override async Task<LoopControl> OnActionVerified(
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
        if (verified.Status == "success")
            return LoopControl.Continue();

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

    /// <summary>
    /// When the scroll verifier reports a verified end-of-list with no page
    /// change: if every discovered entry was processed (sampled or skipped),
    /// finish <c>success</c> with <c>enumerated_all_first_level</c> (exploratory
    /// decision 4 — all-dangerous + verified end-of-list is still success);
    /// otherwise finish <c>incomplete</c> with <c>end_of_list_with_unprocessed</c>.
    /// </summary>
    protected override Task<LoopControl> OnScrollEndOfList(
        StepAssetWriter step,
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
        var allProcessed = _planner.AreAllDiscoveredProcessed();
        var reason = allProcessed
            ? "enumerated_all_first_level"
            : "end_of_list_with_unprocessed";
        return Task.FromResult(LoopControl.Terminate(FinishAsync(
            allProcessed ? "success" : "incomplete",
            reason,
            allProcessed,
            steps,
            scrolls,
            actionsAttempted,
            actionsSucceeded,
            safetyAllowed,
            safetyDenied,
            issues,
            stopwatch)));
    }
}