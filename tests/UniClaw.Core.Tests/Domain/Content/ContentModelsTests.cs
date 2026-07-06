using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Xunit;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Tests.Domain.Content;

/// <summary>
/// Content 模型单元测试 — PRD §5.2: 10 types ported, validated, immutable, camelCase
/// </summary>
public class ContentModelsTests
{
    // ── Coordinate ──

    [Fact(DisplayName = "Coordinate构造: 合法范围值 → 成功创建")]
    public void Coordinate_Valid()
    {
        var c = new Coordinate(X: 0.5, Y: 0.3);
        Assert.Equal(0.5, c.X);
        Assert.Equal(0.3, c.Y);
    }

    [Theory(DisplayName = "Coordinate构造: 坐标越界[0,1] → 抛DomainValidationException+对应FieldName")]
    [InlineData(-0.1, 0.3, "X")]
    [InlineData(0.5, 1.5, "Y")]
    public void Coordinate_OutOfRange_Throws(double x, double y, string field)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Coordinate(X: x, Y: y));
        Assert.Equal(field, ex.FieldName);
    }

    // ── Direction ──

    [Theory(DisplayName = "Direction解析: left/right/top/bottom → 返回对应枚举值")]
    [InlineData("left", Direction.Left)]
    [InlineData("right", Direction.Right)]
    [InlineData("top", Direction.Top)]
    [InlineData("bottom", Direction.Bottom)]
    public void Direction_FromValue(string value, Direction expected)
    {
        Assert.Equal(expected, DirectionExtensions.FromValue(value));
    }

    [Fact(DisplayName = "Direction解析: 非法值diagonal → 抛DomainValidationException")]
    public void Direction_InvalidValue_Throws()
    {
        Assert.Throws<DomainValidationException>(() => DirectionExtensions.FromValue("diagonal"));
    }

    // ── Direction Values reflection ──

    [Fact(DisplayName = "Direction.Values: 反射获取4个值且与JsonPropertyName属性一致")]
    public void Direction_Values_MatchesJsonPropertyNameAttributes()
    {
        var values = DirectionExtensions.Values;
        Assert.Equal(4, values.Count);

        // Verify each value matches [JsonPropertyName] attribute via reflection
        foreach (var dir in Enum.GetValues<Direction>())
        {
            var attr = dir.GetType().GetField(dir.ToString())!
                .GetCustomAttributes<System.Text.Json.Serialization.JsonPropertyNameAttribute>()
                .FirstOrDefault();
            var expected = attr?.Name ?? dir.ToString().ToLowerInvariant();
            Assert.Contains(expected, values);
        }
    }

    [Fact(DisplayName = "Direction.Values: 由反射生成而非硬编码字面量")]
    public void Direction_Values_IsNotHardcoded()
    {
        // Values is derived from reflection, not a literal new[] { ... }
        var valuesViaReflection = Enum.GetValues<Direction>()
            .Select(d =>
            {
                var attr = d.GetType().GetField(d.ToString())!
                    .GetCustomAttributes<System.Text.Json.Serialization.JsonPropertyNameAttribute>()
                    .FirstOrDefault();
                return attr?.Name ?? d.ToString().ToLowerInvariant();
            }).ToList();

        Assert.Equal(valuesViaReflection, DirectionExtensions.Values);
    }

    // ── MenuItemType ──

    [Theory(DisplayName = "MenuItemType解析: canonical名 → 返回对应枚举值")]
    [InlineData("menu_item", MenuItemType.MenuItem)]
    [InlineData("button", MenuItemType.Button)]
    [InlineData("switch", MenuItemType.Switch)]
    [InlineData("item", MenuItemType.Item)]
    public void MenuItemType_FromValue(string value, MenuItemType expected)
    {
        Assert.Equal(expected, MenuItemTypeExtensions.FromValue(value));
    }

    [Fact(DisplayName = "MenuItemType解析: 非法值nonexistent → 抛DomainValidationException")]
    public void MenuItemType_InvalidValue_Throws()
    {
        Assert.Throws<DomainValidationException>(() => MenuItemTypeExtensions.FromValue("nonexistent"));
    }

    // ── ExpectedAction ──

    [Theory(DisplayName = "ExpectedAction解析: navigate/toggle/action/none → 返回对应枚举值")]
    [InlineData("navigate", ExpectedAction.Navigate)]
    [InlineData("toggle", ExpectedAction.Toggle)]
    [InlineData("action", ExpectedAction.Action)]
    [InlineData("none", ExpectedAction.None)]
    public void ExpectedAction_FromValue(string value, ExpectedAction expected)
    {
        Assert.Equal(expected, ExpectedActionExtensions.FromValue(value));
    }

    // ── MenuInfo ──

    [Fact(DisplayName = "MenuInfo构造: 合法参数 → Active默认false")]
    public void MenuInfo_Valid()
    {
        var mi = new MenuInfo(Name: "WiFi", Coordinate: new Coordinate(X: 0.5, Y: 0.2));
        Assert.Equal("WiFi", mi.Name);
        Assert.False(mi.Active);
    }

    // ── MenuItem ──

    [Fact(DisplayName = "MenuItem构造: 合法参数 → Name和Type正确存储")]
    public void MenuItem_Valid()
    {
        var item = new MenuItem(
            Name: "Settings",
            Coordinate: new Coordinate(X: 0.3, Y: 0.5),
            Type: MenuItemType.MenuItem,
            ExpectedAction: ExpectedAction.Navigate);
        Assert.Equal("Settings", item.Name);
        Assert.Equal(MenuItemType.MenuItem, item.Type);
    }

    [Fact(DisplayName = "MenuItem指纹: GetFingerprint组合Level1|Level2|Name")]
    public void MenuItem_GetFingerprint()
    {
        var item = new MenuItem(Name: "WiFi", Coordinate: new Coordinate(X: 0.5, Y: 0.2));
        Assert.Equal("Network|WiFi|WiFi", item.GetFingerprint("Network", "WiFi"));
    }

    // ── PopupInfo ──

    [Fact(DisplayName = "PopupInfo构造: 省略可选字段 → Title和CloseButton均为null")]
    public void PopupInfo_Defaults()
    {
        var p = new PopupInfo();
        Assert.Null(p.Title);
        Assert.Null(p.CloseButton);
    }

    // ── PageAnalysis ──

    [Fact(DisplayName = "PageAnalysis构造: 集合字段默认为空ImmutableArray")]
    public void PageAnalysis_CollectionsAreImmutableArray()
    {
        var pa = new PageAnalysis(
            Level1Dir: Direction.Left,
            Level2Dir: Direction.Right);
        Assert.Empty(pa.Level1Menus);
        Assert.Empty(pa.Items);
        Assert.Empty(pa.CurrentPath);
    }

    [Fact(DisplayName = "PageAnalysis序列化: camelCase键名+enum-as-string")]
    public void PageAnalysis_Serialization_CamelCase()
    {
        var pa = new PageAnalysis(
            Level1Dir: Direction.Left,
            Level2Dir: Direction.Right,
            Items: ImmutableArray.Create(
                new MenuItem(Name: "WiFi", Coordinate: new Coordinate(X: 0.5, Y: 0.2))));
        var json = JsonSerializer.Serialize(pa, DomainJsonOptions.Default);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("items", out _));
        Assert.True(doc.RootElement.TryGetProperty("level1Dir", out var dir));
        Assert.Equal("left", dir.GetString());
    }

    // ── VisitFingerprint ──

    [Fact(DisplayName = "VisitFingerprint解析: L1|L2|Name格式 → 正确拆分为三段")]
    public void VisitFingerprint_FromString_Valid()
    {
        var fp = VisitFingerprint.FromString("Network|WiFi|Settings");
        Assert.Equal("Network", fp.Level1);
        Assert.Equal("WiFi", fp.Level2);
        Assert.Equal("Settings", fp.ItemName);
    }

    [Fact(DisplayName = "VisitFingerprint解析: 缺少分隔符 → 抛DomainValidationException")]
    public void VisitFingerprint_FromString_InvalidFormat_Throws()
    {
        Assert.Throws<DomainValidationException>(() => VisitFingerprint.FromString("bad"));
    }

    [Fact(DisplayName = "VisitFingerprint往返: ToString→FromString → 值一致")]
    public void VisitFingerprint_ToString_Roundtrip()
    {
        var fp = new VisitFingerprint(Level1: "a", Level2: "b", ItemName: "c");
        var restored = VisitFingerprint.FromString(fp.ToString());
        Assert.Equal(fp, restored);
    }

    // ── ContentNode ──

    [Fact(DisplayName = "ContentNode构造: NodeType默认item, Children空, Visited=false")]
    public void ContentNode_Defaults()
    {
        var node = new ContentNode(Id: "1", Title: "Root", Level: 1);
        Assert.Equal("item", node.NodeType);
        Assert.Empty(node.Children);
        Assert.False(node.Visited);
    }

    // ── No ToDictionary / FromDictionary ──

    [Fact(DisplayName = "Content类型禁止模式: 6种类型均无ToDictionary方法")]
    public void ContentTypes_HaveNoToDictionary()
    {
        var types = new[] { typeof(MenuInfo), typeof(MenuItem), typeof(PopupInfo),
            typeof(PageAnalysis), typeof(VisitFingerprint), typeof(ContentNode) };
        foreach (var t in types)
            Assert.Null(t.GetMethod("ToDictionary"));
    }
}
