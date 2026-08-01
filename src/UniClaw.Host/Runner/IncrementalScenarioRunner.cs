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

/// <summary>
/// Incremental runner for <c>locate_one_item</c> scenarios. A thin sealed
/// subclass of <see cref="ScenarioRunnerBase"/> that supplies a
/// <see cref="LocateScenarioStepPlanner"/> and inherits all locate-mode hook
/// defaults (target-alias click verification, safety-denied finishes blocked,
/// end-of-list finishes incomplete, success criterion is target page identity).
/// </summary>
public sealed class IncrementalScenarioRunner : ScenarioRunnerBase
{
    private readonly LocateScenarioStepPlanner _planner;

    public IncrementalScenarioRunner(
        ScenarioSnapshot snapshot,
        TraversalPlan plan,
        HostRunServices services,
        IScenarioObservationSource observations,
        LocateScenarioStepPlanner? planner = null)
        : base(snapshot, plan, services, observations)
    {
        _planner = planner ?? new LocateScenarioStepPlanner();
        if (snapshot.Scenario.Mode != "locate_one_item")
        {
            throw new ArgumentException(
                "Incremental locate runner requires locate_one_item mode.",
                nameof(snapshot));
        }
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
}