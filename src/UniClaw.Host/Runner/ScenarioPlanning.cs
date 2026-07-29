using UniClaw.Core.Graph.Models;
using UniClaw.Core.Graph.Services;
using UniClaw.Host.Scenarios;

namespace UniClaw.Host.Runner;

public sealed class ScenarioPlanCompiler
{
    public TraversalPlan Compile(ScenarioSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var scenario = snapshot.Scenario;
        var scope = scenario.Mode == "locate_one_item"
            ? "target_only"
            : "full";
        var slots = new IntentSlots(
            scenario.AppPackage,
            scope,
            scenario.Target?.Label,
            scenario.Boundaries.MaxDepth,
            ElementHandling: "menu_only",
            Navigation: "bounded_settings",
            Restore: true,
            Entry: scenario.ResetProcedure.ExpectedPageIdentity);
        var compiled = new PlanCompiler().Compile(slots);
        var entryStrategy = scenario.EntryStrategy switch
        {
            "cold_launch" => EntryStrategy.ColdLaunch,
            "bind_current_screen" => EntryStrategy.BindCurrentScreen,
            "direct_deeplink" => EntryStrategy.DirectDeeplink,
            _ => throw new ScenarioValidationException(
                "entryStrategy",
                scenario.EntryStrategy,
                "cannot be compiled"),
        };
        return compiled with
        {
            PlanName = $"Android Settings: {scenario.ScenarioId}",
            PlanId = $"{scenario.ScenarioId}-{snapshot.ScenarioHash[..12]}",
            EntryPolicy = new EntryPolicy(
                entryStrategy,
                WaitCondition: new Dictionary<string, object>
                {
                    ["package"] = scenario.AppPackage,
                },
                TimeoutSeconds: scenario.ResetProcedure.TimeoutSeconds),
            Meta = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = ScenarioVocabulary.SchemaVersion,
                ["scenarioId"] = scenario.ScenarioId,
                ["scenarioHash"] = snapshot.ScenarioHash,
                ["policyHash"] = snapshot.PolicyHash,
                ["mode"] = scenario.Mode,
                ["maxSteps"] = scenario.Boundaries.MaxSteps,
                ["maxScrolls"] = scenario.Boundaries.MaxScrolls,
                ["maxDurationSeconds"] = scenario.Boundaries.MaxDurationSeconds,
            },
        };
    }
}

public sealed record class ScenarioStepPlan(
    string SchemaVersion,
    string RunId,
    int StepNumber,
    string PageFingerprint,
    string Action,
    string? Target,
    string? Semantic,
    double? X,
    double? Y,
    double? EndX,
    double? EndY,
    int? DurationMs,
    string ExpectedChange,
    string Reason);

public sealed record class StepPlanningResult(
    string Status,
    ScenarioStepPlan? Plan,
    string Reason);

public sealed class LocateScenarioStepPlanner
{
    public StepPlanningResult Plan(
        ScenarioSnapshot snapshot,
        ScenarioObservation observation,
        string runId,
        int stepNumber,
        int remainingSteps,
        int remainingScrolls)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(observation);
        var target = snapshot.Scenario.Target
                     ?? throw new InvalidOperationException(
                         "Locate scenario requires a target.");
        var names = target.Aliases
            .Add(target.Label)
            .Select(Normalize)
            .ToHashSet(StringComparer.Ordinal);
        if (names.Contains(Normalize(observation.PageIdentity)))
        {
            return new StepPlanningResult(
                "complete",
                null,
                "target_page_identity_verified");
        }
        if (remainingSteps <= 0)
            return new StepPlanningResult("incomplete", null, "step_budget_exhausted");

        var match = observation.Analysis.Items.FirstOrDefault(
            item => names.Contains(Normalize(item.Name)));
        if (match is not null)
        {
            return new StepPlanningResult(
                "action",
                new ScenarioStepPlan(
                    "1",
                    runId,
                    stepNumber,
                    observation.PageFingerprint,
                    "click",
                    match.Name,
                    "navigation_row",
                    match.Coordinate.X,
                    match.Coordinate.Y,
                    null,
                    null,
                    null,
                    "target_page_identity",
                    "trusted_target_row_visible"),
                "target_visible");
        }

        if (observation.Analysis.HasScroll
            && !observation.Analysis.IsEndOfList
            && remainingScrolls > 0)
        {
            return new StepPlanningResult(
                "action",
                new ScenarioStepPlan(
                    "1",
                    runId,
                    stepNumber,
                    observation.PageFingerprint,
                    "scroll",
                    null,
                    "settings_home",
                    0.5,
                    0.8,
                    0.5,
                    0.2,
                    350,
                    "new_page_fingerprint",
                    "target_not_visible_scroll_within_budget"),
                "scroll_for_target");
        }

        return new StepPlanningResult(
            "incomplete",
            null,
            observation.Analysis.IsEndOfList
                ? "target_absent_at_verified_end"
                : "target_absent_without_completion_proof");
    }

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
