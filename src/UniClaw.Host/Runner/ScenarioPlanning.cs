using UniClaw.Core.Domain.Models.Content;
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

/// <summary>
/// Stateful step planner for <c>enumerate_first_level</c> scenarios. Constructed
/// once per run; tracks discovered first-level entries (by normalized name),
/// entries already sampled, entries skipped by the safety gate, the entry
/// currently being sampled (<c>Pending</c>), and whether a back is awaited to
/// return from a sampled child page. Coordinates are never used as identity —
/// only the normalized entry name is.
/// </summary>
public sealed class EnumerateScenarioStepPlanner
{
    private readonly Dictionary<string, MenuInfo> _discovered =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _sampled = new(StringComparer.Ordinal);
    private readonly HashSet<string> _skipped = new(StringComparer.Ordinal);
    private string? _pending;
    private bool _awaitingReturn;

    /// <summary>Names of entries denied by the safety gate (read by the runner for reporting).</summary>
    public IReadOnlyCollection<string> Skipped => _skipped;

    /// <summary>Names of entries successfully sampled (read by the runner for reporting).</summary>
    public IReadOnlyCollection<string> Sampled => _sampled;

    /// <summary>
    /// True when every discovered first-level entry has been sampled or skipped.
    /// Read by <see cref="EnumerateScenarioRunner.OnScrollEndOfList"/> to decide
    /// success vs. incomplete at a verified end-of-list.
    /// </summary>
    public bool AreAllDiscoveredProcessed() =>
        _discovered.Count > 0
        && _discovered.Keys.All(
            key => _sampled.Contains(key) || _skipped.Contains(key));

    /// <summary>
    /// Mark the currently-pending entry as skipped by the safety gate. Called
    /// by <see cref="EnumerateScenarioRunner.OnSafetyDenied"/> when the gate
    /// denies a dangerous click. Clears <see cref="Pending"/> so the next plan
    /// iteration moves on to the next entry.
    /// </summary>
    public void MarkSkipped(string name, string ruleId)
    {
        if (string.IsNullOrEmpty(name))
            return;
        _skipped.Add(Normalize(name));
        _pending = null;
    }

    /// <summary>
    /// Plan the next step. Implements the enumerate state machine:
    /// <list type="bullet">
    /// <item>If off the Settings home page, plan a back to return after sampling.</item>
    /// <item>If home with a pending entry and a return awaited, mark it sampled and continue discovery.</item>
    /// <item>Discover any new <see cref="PageAnalysis.Level1Menus"/> entries by normalized name.</item>
    /// <item>Click the next visible unprocessed entry; advance the cursor.</item>
    /// <item>If all discovered entries are processed and the end-of-list is verified, complete with <c>enumerated_all_first_level</c>.</item>
    /// <item>Scroll to reveal more entries when the visible set is exhausted but the end is not verified.</item>
    /// <item>If the visible set is exhausted with no scroll budget (or no scroll capability) and no end proof, report <c>end_of_list_unproven</c>.</item>
    /// <item>If the end-of-list is verified but some discovered entries remain unprocessed, report <c>end_of_list_with_unprocessed</c>.</item>
    /// </list>
    /// </summary>
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
        var expectedHome = Normalize(
            snapshot.Scenario.ResetProcedure.ExpectedPageIdentity);
        var isHome = Normalize(observation.PageIdentity) == expectedHome;

        // Inside a sampled child page: plan back to return home.
        if (!isHome)
        {
            _awaitingReturn = true;
            return new StepPlanningResult(
                "action",
                new ScenarioStepPlan(
                    "1",
                    runId,
                    stepNumber,
                    observation.PageFingerprint,
                    "back",
                    null,
                    "return_after_sampling",
                    null,
                    null,
                    null,
                    null,
                    null,
                    "verified_end_of_list",
                    "return_after_sampling"),
                "return_after_sampling");
        }

        // Home after a back: the pending entry is now sampled.
        if (_awaitingReturn && _pending is not null)
        {
            _sampled.Add(_pending);
            _pending = null;
            _awaitingReturn = false;
            // fall through to discovery / next entry
        }

        // Discovery: dedup-add any new Level1Menus entries by normalized name.
        foreach (var menu in observation.Analysis.Level1Menus)
        {
            var key = Normalize(menu.Name);
            if (!_discovered.ContainsKey(key))
                _discovered[key] = menu;
        }

        // Click the next visible, unprocessed entry.
        foreach (var menu in observation.Analysis.Level1Menus)
        {
            var key = Normalize(menu.Name);
            if (_sampled.Contains(key) || _skipped.Contains(key))
                continue;
            _pending = key;
            return new StepPlanningResult(
                "action",
                new ScenarioStepPlan(
                    "1",
                    runId,
                    stepNumber,
                    observation.PageFingerprint,
                    "click",
                    menu.Name,
                    "navigation_row",
                    menu.Coordinate.X,
                    menu.Coordinate.Y,
                    null,
                    null,
                    null,
                    "verified_end_of_list",
                    "unprocessed_first_level_entry_visible"),
                "click_unprocessed_entry");
        }

        // All discovered entries processed and end-of-list verified -> success.
        if (observation.Analysis.IsEndOfList
            && AllDiscoveredProcessed())
        {
            return new StepPlanningResult(
                "complete",
                null,
                "enumerated_all_first_level");
        }

        // End-of-list verified but some discovered entries still unprocessed.
        if (observation.Analysis.IsEndOfList && !AllDiscoveredProcessed())
        {
            return new StepPlanningResult(
                "incomplete",
                null,
                "end_of_list_with_unprocessed");
        }

        // Visible set exhausted, end not verified, scroll budget remains and page scrolls -> scroll.
        if (!observation.Analysis.IsEndOfList
            && observation.Analysis.HasScroll
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
                    "verified_end_of_list",
                    "more_entries_may_exist_scroll_within_budget"),
                "scroll_for_more_entries");
        }

        // Visible set exhausted, end not verified, no scroll budget / capability -> unproven.
        return new StepPlanningResult(
            "incomplete",
            null,
            "end_of_list_unproven");
    }

    private bool AllDiscoveredProcessed() =>
        _discovered.Count > 0
        && _discovered.Keys.All(
            key => _sampled.Contains(key) || _skipped.Contains(key));

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
