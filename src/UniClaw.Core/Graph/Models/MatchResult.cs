namespace UniClaw.Core.Graph.Models;

/// <summary>
/// 匹配结果
/// </summary>
/// <param name="Matched">是否匹配</param>
/// <param name="MatchRuleId">匹配的规则ID</param>
/// <param name="MatchedItem">匹配的项</param>
/// <param name="Action">匹配后的操作</param>
public sealed record class MatchResult(
    bool Matched,
    string MatchRuleId,
    MatchableItem MatchedItem,
    MatchAction Action);
