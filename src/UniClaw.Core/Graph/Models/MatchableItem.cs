using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Graph.Models;

/// <summary>
/// 可匹配的 UI 元素项 — DynamicMatcher 的输入。
/// </summary>
public sealed record class MatchableItem(
    string? Text = null,
    MenuItemType MenuItemType = MenuItemType.Item,
    ExpectedAction ExpectedAction = ExpectedAction.Action,
    int Index = 0,
    Dictionary<string, string>? Metadata = null);
