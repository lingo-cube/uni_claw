using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// TextTargetResolution — FindMatchingItem/NormalizeTargetText 单元测试。
/// 视觉模型对同一元素跨调用返回的名称不稳定 ("[icon] X" vs "X"、大小写、空白差异),
/// 解析必须容错。精确匹配优先, 归一化/包含匹配兜底。
/// </summary>
public class TextTargetResolutionTests
{
    private static MenuItem Item(string name, double x = 0.4, double y = 0.4)
        => new(name, new Coordinate(x, y), MenuItemType.MenuItem);

    private static PageAnalysis Analysis(params MenuItem[] items)
        => new(
            Direction.Bottom,
            Direction.Bottom,
            Items: items.ToImmutableArray());

    // ── NormalizeTargetText ──

    [Fact(DisplayName = "NormalizeTargetText: 剥离[icon]标记、转小写、折叠空白")]
    public void Normalize_StripsIconMarker_AndCollapsesWhitespace()
    {
        Assert.Equal("network & internet",
            TraversalFSM.NormalizeTargetText("[icon]  Network   &   internet"));
    }

    [Fact(DisplayName = "NormalizeTargetText: 非图标括号文本原样保留")]
    public void Normalize_PreservesNonIconBrackets()
    {
        Assert.Equal("(beta) settings", TraversalFSM.NormalizeTargetText("(beta) Settings"));
    }

    [Fact(DisplayName = "NormalizeTargetText: null/空白返回空串")]
    public void Normalize_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TraversalFSM.NormalizeTargetText(null));
        Assert.Equal(string.Empty, TraversalFSM.NormalizeTargetText("   "));
    }

    // ── FindMatchingItem: 精确匹配 ──

    [Fact(DisplayName = "FindMatchingItem: 精确匹配(大小写不敏感)优先")]
    public void ExactMatch_Wins()
    {
        var analysis = Analysis(
            Item("Network & internet", 0.4, 0.4),
            Item("[icon] Connected devices", 0.4, 0.5));

        var match = TraversalFSM.FindMatchingItem(analysis, "network & INTERNET");
        Assert.NotNull(match);
        Assert.Equal("Network & internet", match.Name);
    }

    // ── FindMatchingItem: 归一化匹配 (图标标记/空白差异) ──

    [Fact(DisplayName = "FindMatchingItem: 目标带[icon]前缀, 当前分析无前缀 → 归一化匹配")]
    public void NormalizedMatch_TargetHasIconPrefix()
    {
        var analysis = Analysis(
            Item("Network & internet", 0.4, 0.4),
            Item("Connected devices", 0.4, 0.5));

        var match = TraversalFSM.FindMatchingItem(analysis, "[icon] Network & internet");
        Assert.NotNull(match);
        Assert.Equal("Network & internet", match.Name);
    }

    [Fact(DisplayName = "FindMatchingItem: 当前分析带[icon]前缀, 目标无前缀 → 归一化匹配")]
    public void NormalizedMatch_AnalysisHasIconPrefix()
    {
        var analysis = Analysis(
            Item("[icon] Network & internet", 0.4, 0.4),
            Item("[icon] Connected devices", 0.4, 0.5));

        var match = TraversalFSM.FindMatchingItem(analysis, "Network & internet");
        Assert.NotNull(match);
        Assert.Equal("[icon] Network & internet", match.Name);
    }

    // ── FindMatchingItem: 包含匹配 (模型改写标签) ──

    [Fact(DisplayName = "FindMatchingItem: 包含匹配兜底 (Bluetooth ⊂ [icon] Bluetooth, pairing)")]
    public void ContainsMatch_FallsBack()
    {
        var analysis = Analysis(
            Item("[icon] Bluetooth, pairing", 0.4, 0.5),
            Item("[icon] Network & internet", 0.4, 0.4));

        var match = TraversalFSM.FindMatchingItem(analysis, "Bluetooth");
        Assert.NotNull(match);
        Assert.Contains("Bluetooth", match.Name);
    }

    [Fact(DisplayName = "FindMatchingItem: 包含匹配选择最具体项")]
    public void ContainsMatch_PrefersMostSpecific()
    {
        var analysis = Analysis(
            Item("Battery", 0.4, 0.4),
            Item("Battery saver", 0.4, 0.5),
            Item("Apps", 0.4, 0.6));

        // "Battery" 精确匹配 "Battery", 不会落到包含匹配
        Assert.Equal("Battery",
            TraversalFSM.FindMatchingItem(analysis, "Battery")!.Name);

        // "Battery saver" 精确匹配 "Battery saver"
        Assert.Equal("Battery saver",
            TraversalFSM.FindMatchingItem(analysis, "Battery saver")!.Name);
    }

    // ── FindMatchingItem: 无匹配 ──

    [Fact(DisplayName = "FindMatchingItem: 无匹配返回 null (保持原失败契约)")]
    public void NoMatch_ReturnsNull()
    {
        var analysis = Analysis(Item("Network & internet", 0.4, 0.4));

        Assert.Null(TraversalFSM.FindMatchingItem(analysis, "Dark mode"));
        Assert.Null(TraversalFSM.FindMatchingItem(null, "Dark mode"));
        Assert.Null(TraversalFSM.FindMatchingItem(analysis, ""));
    }
}
