using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Graph.Abstractions;

/// <summary>
/// 动态匹配器接口 — 匹配页面对象与 DynamicRule 条件。
/// 5 个条件维度 conjunctive logic: MenuItemType, ExpectedAction, text_pattern, index range, custom dict。
/// </summary>
public interface IDynamicMatcher
{
    /// <summary>
    /// 匹配单个条件与单个项。
    /// 所有条件必须通过 (conjunctive logic)。
    /// </summary>
    MatchResult Match(MatchCondition condition, MatchableItem item);

    /// <summary>
    /// 对所有项批量匹配所有规则。
    /// </summary>
    List<MatchResult> MatchAll(List<DynamicRule> rules, List<MatchableItem> items);
}
