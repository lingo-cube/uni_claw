using System.Collections.Immutable;
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
        plan = ApplyTargetNarrowing(plan, scenario);
        plan = ApplyExcludePatterns(plan, scenario);
        return plan;
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
                        // D-200: 视觉场景下 YOLO 对屏幕边缘列表项常只检出 text 框
                        // (menuItem 框被边缘裁剪/低置信度过滤)，type=menu_item 严格
                        // 匹配会漏掉目标 (实测: Settings 列表底部 "About emulated
                        // device" 仅 4 个重叠 text 框 → 引擎不生成点击节点 → 滚动
                        // 6 次耗尽仍失败)。locate 模式目标规则已用 textPattern
                        // Exact 收窄到目标名，放开 type 约束 (null=任意) 安全。
                        Type = null,
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

    /// <summary>
    /// Apply excludePatterns from the scenario to all DynamicRules.
    /// If the scenario has no exclude patterns, the plan is returned unchanged.
    /// </summary>
    private static TraversalPlan ApplyExcludePatterns(TraversalPlan plan, AndroidSettingsScenario scenario)
    {
        if (scenario.ExcludePatterns.IsDefault || scenario.ExcludePatterns.Length == 0)
            return plan;

        var excludePatterns = System.Collections.Immutable.ImmutableArray.CreateRange(
            scenario.ExcludePatterns.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (excludePatterns.Length == 0)
            return plan;

        var rootNode = plan.RootNode;
        if (rootNode?.ChildrenStrategy.DynamicRules is null)
            return plan;

        var updatedRules = new Dictionary<string, DynamicRule>(StringComparer.Ordinal);
        foreach (var (key, rule) in rootNode.ChildrenStrategy.DynamicRules)
        {
            updatedRules[key] = rule with
            {
                MatchCondition = rule.MatchCondition with
                {
                    ExcludeTextPatterns = excludePatterns,
                },
            };
        }

        return plan with
        {
            RootNode = rootNode with
            {
                ChildrenStrategy = rootNode.ChildrenStrategy with
                {
                    DynamicRules = updatedRules,
                },
            },
        };
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
