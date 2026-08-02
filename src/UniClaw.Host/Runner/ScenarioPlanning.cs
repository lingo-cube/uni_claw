using UniClaw.Core.Graph.Models;
using UniClaw.Core.Graph.Services;
using UniClaw.Core.UniBrain;
using UniClaw.Host.Scenarios;

namespace UniClaw.Host.Runner;

/// <summary>
/// Host orchestration: converts a scenario into <see cref="IntentSlots"/>,
/// delegates to <see cref="PlanCompiler"/> (Core), and returns the plan.
/// Only minimal orchestration-level transformations (target narrowing for
/// locate mode) are applied; PlanCompiler is the single source of truth.
/// </summary>
public sealed class ScenarioPlanCompiler
{
    private readonly IIntentExtractor? _intentExtractor;

    public ScenarioPlanCompiler(IIntentExtractor? intentExtractor = null)
    {
        _intentExtractor = intentExtractor;
    }

    public TraversalPlan Compile(ScenarioSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var scenario = snapshot.Scenario;
        var slots = ResolveIntentSlots(scenario);
        var plan = new PlanCompiler().Compile(slots);
        return ApplyTargetNarrowing(plan, scenario);
    }

    /// <summary>
    /// For locate_one_item: narrow root DynamicRules to exact-match the
    /// target names and add aliases to CompletionPolicy.  This is an
    /// orchestration concern — adapting a generic plan to a specific target.
    /// </summary>
    private static TraversalPlan ApplyTargetNarrowing(TraversalPlan plan, AndroidSettingsScenario scenario)
    {
        if (scenario.Mode != "locate_one_item" || scenario.Target is null)
            return plan;

        var target = scenario.Target;
        var rootNode = plan.RootNode;
        if (rootNode?.ChildrenStrategy.DynamicRules is null)
            return plan;

        var targetNames = target.Aliases
            .Add(target.Label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var targetRules = new Dictionary<string, DynamicRule>(StringComparer.Ordinal);
        foreach (var pair in rootNode.ChildrenStrategy.DynamicRules)
        {
            for (var index = 0; index < targetNames.Length; index++)
            {
                var ruleId = $"{pair.Key}_target_{index:D2}";
                targetRules[ruleId] = pair.Value with
                {
                    RuleId = ruleId,
                    MatchCondition = pair.Value.MatchCondition with
                    {
                        TextPattern = targetNames[index],
                        TextMatchMode = TextMatchMode.Exact,
                    },
                };
            }
        }

        var narrowedRoot = rootNode with
        {
            ChildrenStrategy = rootNode.ChildrenStrategy with
            {
                DynamicRules = targetRules,
            },
        };

        return plan with
        {
            RootNode = narrowedRoot,
            CompletionPolicy = plan.CompletionPolicy is not null
                ? plan.CompletionPolicy with { TargetAliases = target.Aliases }
                : null,
        };
    }

    private IntentSlots ResolveIntentSlots(AndroidSettingsScenario scenario)
    {
        if (_intentExtractor is not null)
        {
            try
            {
                return _intentExtractor.ExtractAsync(
                    scenario.Description,
                    scenario.AppPackage,
                    scenario.Target?.Label,
                    scenario.Boundaries.MaxDepth,
                    scenario.ResetProcedure.ExpectedPageIdentity).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ScenarioPlanCompiler] AI intent extraction failed, "
                    + $"falling back to mechanical mapping: {ex.Message}");
            }
        }

        return BuildMechanicalSlots(scenario);
    }

    private static IntentSlots BuildMechanicalSlots(AndroidSettingsScenario scenario)
    {
        var scope = scenario.Mode == "locate_one_item"
            ? "target_only"
            : "full";
        return new IntentSlots(
            scenario.AppPackage,
            scope,
            scenario.Target?.Label,
            scenario.Boundaries.MaxDepth,
            ElementHandling: "full_interaction",
            Navigation: "bounded_settings",
            Restore: true,
            Entry: scenario.ResetProcedure.ExpectedPageIdentity);
    }
}
