using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Abstractions;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Graph.Services;

/// <summary>
/// DynamicMatcher — 匹配页面对象与 DynamicRule 条件。
/// 5 个条件维度 conjunctive logic: MenuItemType, ExpectedAction, text_pattern, index range, custom dict。
/// </summary>
public sealed class DynamicMatcher : IDynamicMatcher
{
    /// <summary>
    /// 匹配单个条件与单个项。
    /// 所有条件必须通过 (conjunctive logic)。
    /// </summary>
    public MatchResult Match(MatchCondition condition, MatchableItem item)
    {
        // Empty condition matches everything (leaf_info semantics)
        if (condition is null || IsEmptyCondition(condition))
            return new MatchResult(true, condition?.Type ?? "any", item, MatchAction.GenerateChild);

        // 1. MenuItemType match
        if (!MatchType(condition, item))
            return new MatchResult(false, condition.Type ?? "", item, MatchAction.Skip);

        // 2. ExpectedAction match
        if (!MatchExpectedAction(condition, item))
            return new MatchResult(false, condition.Type ?? "", item, MatchAction.Skip);

        // 3. Text pattern match
        if (!MatchTextPattern(condition, item))
            return new MatchResult(false, condition.Type ?? "", item, MatchAction.Skip);

        // 4. Index range match
        if (!MatchIndexRange(condition, item))
            return new MatchResult(false, condition.Type ?? "", item, MatchAction.Skip);

        // 5. Custom dict match
        if (!MatchCustomDict(condition, item))
            return new MatchResult(false, condition.Type ?? "", item, MatchAction.Skip);

        // 6. Exclude text patterns — item text matches any exclude pattern → skip
        if (!MatchExcludeTextPatterns(condition, item))
            return new MatchResult(false, condition.Type ?? "", item, MatchAction.Skip);

        // All conditions passed
        var action = DetermineAction(condition);
        return new MatchResult(true, condition.Type ?? "any", item, action);
    }

    /// <summary>
    /// 对所有项批量匹配所有规则。
    /// </summary>
    public List<MatchResult> MatchAll(List<DynamicRule> rules, List<MatchableItem> items)
    {
        var results = new List<MatchResult>();

        foreach (var item in items)
        {
            foreach (var rule in rules)
            {
                var result = Match(rule.MatchCondition, item);
                if (result.Matched)
                {
                    // Fix: set MatchRuleId to the DynamicRule's RuleId (e.g. "menu_rule"),
                    // not the condition's Type string (e.g. "button").
                    // Generate() looks up the rule by RuleId to find ChildTemplate and Action.
                    results.Add(new MatchResult(true, rule.RuleId, result.MatchedItem, result.Action));
                    break; // First matching rule wins
                }
            }
        }

        return results;
    }

    private static bool IsEmptyCondition(MatchCondition condition)
    {
        return string.IsNullOrWhiteSpace(condition.Type)
            && string.IsNullOrWhiteSpace(condition.ExpectedAction)
            && string.IsNullOrWhiteSpace(condition.TextPattern)
            && condition.MinIndex == null
            && condition.MaxIndex == null
            && (condition.ExcludeTextPatterns.IsDefault || condition.ExcludeTextPatterns.Length == 0)
            && (condition.Custom == null || condition.Custom.Count == 0);
    }

    private static bool MatchExcludeTextPatterns(MatchCondition condition, MatchableItem item)
    {
        if (condition.ExcludeTextPatterns.IsDefault || condition.ExcludeTextPatterns.Length == 0)
            return true; // No exclude patterns = pass

        var itemText = item.Text ?? "";
        return !condition.ExcludeTextPatterns.Any(
            p => itemText.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchType(MatchCondition condition, MatchableItem item)
    {
        if (string.IsNullOrWhiteSpace(condition.Type))
            return true; // No type constraint = pass

        // Resolve MenuItemType from string
        if (!MenuItemTypeExtensions.IsValid(condition.Type))
            return false; // Invalid type string = no match

        var expectedType = MenuItemTypeExtensions.FromValue(condition.Type);
        return item.MenuItemType == expectedType;
    }

    private bool MatchExpectedAction(MatchCondition condition, MatchableItem item)
    {
        if (string.IsNullOrWhiteSpace(condition.ExpectedAction))
            return true; // No action constraint = pass

        if (!ExpectedActionExtensions.IsValid(condition.ExpectedAction))
            return false;

        var expectedAction = ExpectedActionExtensions.FromValue(condition.ExpectedAction);
        return item.ExpectedAction == expectedAction;
    }

    private bool MatchTextPattern(MatchCondition condition, MatchableItem item)
    {
        if (string.IsNullOrWhiteSpace(condition.TextPattern))
            return true; // No text constraint = pass

        // M-9: TextMatchMode determines match semantics
        // Exact: string equality (case-insensitive)
        // Contains: substring match (case-insensitive, default for backward compat)
        var pattern = condition.TextPattern;
        var itemText = item.Text ?? "";

        return condition.TextMatchMode == TextMatchMode.Exact
            ? string.Equals(itemText, pattern, StringComparison.OrdinalIgnoreCase)
            : itemText.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchIndexRange(MatchCondition condition, MatchableItem item)
    {
        if (condition.MinIndex == null && condition.MaxIndex == null)
            return true; // No index constraint = pass

        var index = item.Index;

        if (condition.MinIndex.HasValue && index < condition.MinIndex.Value)
            return false;

        if (condition.MaxIndex.HasValue && index > condition.MaxIndex.Value)
            return false;

        return true;
    }

    private bool MatchCustomDict(MatchCondition condition, MatchableItem item)
    {
        if (condition.Custom == null || condition.Custom.Count == 0)
            return true; // No custom constraint = pass

        foreach (var (key, expectedValue) in condition.Custom)
        {
            if (!item.Metadata.TryGetValue(key, out var actualValueStr) || actualValueStr == null)
                return false; // Key not present

            var expectedStr = expectedValue?.ToString() ?? "";
            if (!string.Equals(expectedStr, actualValueStr, StringComparison.OrdinalIgnoreCase))
                return false; // Value mismatch
        }

        return true;
    }

    private MatchAction DetermineAction(MatchCondition condition)
    {
        // Default action for matching items is GenerateChild
        // Skip and ExecuteInline would be specified in the DynamicRule, not here
        return MatchAction.GenerateChild;
    }
}
