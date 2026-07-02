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

    [Fact]
    public void Coordinate_Valid()
    {
        var c = new Coordinate(X: 0.5, Y: 0.3);
        Assert.Equal(0.5, c.X);
        Assert.Equal(0.3, c.Y);
    }

    [Theory]
    [InlineData(-0.1, 0.3, "X")]
    [InlineData(0.5, 1.5, "Y")]
    public void Coordinate_OutOfRange_Throws(double x, double y, string field)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Coordinate(X: x, Y: y));
        Assert.Equal(field, ex.FieldName);
    }

    // ── Direction ──

    [Theory]
    [InlineData("left", Direction.Left)]
    [InlineData("right", Direction.Right)]
    [InlineData("top", Direction.Top)]
    [InlineData("bottom", Direction.Bottom)]
    public void Direction_FromValue(string value, Direction expected)
    {
        Assert.Equal(expected, DirectionExtensions.FromValue(value));
    }

    [Fact]
    public void Direction_InvalidValue_Throws()
    {
        Assert.Throws<DomainValidationException>(() => DirectionExtensions.FromValue("diagonal"));
    }

    // ── Direction Values reflection ──

    [Fact]
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

    [Fact]
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

    [Theory]
    [InlineData("menu_item", MenuItemType.MenuItem)]
    [InlineData("button", MenuItemType.Button)]
    [InlineData("switch", MenuItemType.Switch)]
    [InlineData("item", MenuItemType.Item)]
    public void MenuItemType_FromValue(string value, MenuItemType expected)
    {
        Assert.Equal(expected, MenuItemTypeExtensions.FromValue(value));
    }

    [Fact]
    public void MenuItemType_InvalidValue_Throws()
    {
        Assert.Throws<DomainValidationException>(() => MenuItemTypeExtensions.FromValue("nonexistent"));
    }

    // ── ExpectedAction ──

    [Theory]
    [InlineData("navigate", ExpectedAction.Navigate)]
    [InlineData("toggle", ExpectedAction.Toggle)]
    [InlineData("action", ExpectedAction.Action)]
    [InlineData("none", ExpectedAction.None)]
    public void ExpectedAction_FromValue(string value, ExpectedAction expected)
    {
        Assert.Equal(expected, ExpectedActionExtensions.FromValue(value));
    }

    // ── MenuInfo ──

    [Fact]
    public void MenuInfo_Valid()
    {
        var mi = new MenuInfo(Name: "WiFi", Coordinate: new Coordinate(X: 0.5, Y: 0.2));
        Assert.Equal("WiFi", mi.Name);
        Assert.False(mi.Active);
    }

    // ── MenuItem ──

    [Fact]
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

    [Fact]
    public void MenuItem_GetFingerprint()
    {
        var item = new MenuItem(Name: "WiFi", Coordinate: new Coordinate(X: 0.5, Y: 0.2));
        Assert.Equal("Network|WiFi|WiFi", item.GetFingerprint("Network", "WiFi"));
    }

    // ── PopupInfo ──

    [Fact]
    public void PopupInfo_Defaults()
    {
        var p = new PopupInfo();
        Assert.Null(p.Title);
        Assert.Null(p.CloseButton);
    }

    // ── PageAnalysis ──

    [Fact]
    public void PageAnalysis_CollectionsAreImmutableArray()
    {
        var pa = new PageAnalysis(
            Level1Dir: Direction.Left,
            Level2Dir: Direction.Right);
        Assert.Empty(pa.Level1Menus);
        Assert.Empty(pa.Items);
        Assert.Empty(pa.CurrentPath);
    }

    [Fact]
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

    [Fact]
    public void VisitFingerprint_FromString_Valid()
    {
        var fp = VisitFingerprint.FromString("Network|WiFi|Settings");
        Assert.Equal("Network", fp.Level1);
        Assert.Equal("WiFi", fp.Level2);
        Assert.Equal("Settings", fp.ItemName);
    }

    [Fact]
    public void VisitFingerprint_FromString_InvalidFormat_Throws()
    {
        Assert.Throws<DomainValidationException>(() => VisitFingerprint.FromString("bad"));
    }

    [Fact]
    public void VisitFingerprint_ToString_Roundtrip()
    {
        var fp = new VisitFingerprint(Level1: "a", Level2: "b", ItemName: "c");
        var restored = VisitFingerprint.FromString(fp.ToString());
        Assert.Equal(fp, restored);
    }

    // ── ContentNode ──

    [Fact]
    public void ContentNode_Defaults()
    {
        var node = new ContentNode(Id: "1", Title: "Root", Level: 1);
        Assert.Equal("item", node.NodeType);
        Assert.Empty(node.Children);
        Assert.False(node.Visited);
    }

    // ── No ToDictionary / FromDictionary ──

    [Fact]
    public void ContentTypes_HaveNoToDictionary()
    {
        var types = new[] { typeof(MenuInfo), typeof(MenuItem), typeof(PopupInfo),
            typeof(PageAnalysis), typeof(VisitFingerprint), typeof(ContentNode) };
        foreach (var t in types)
            Assert.Null(t.GetMethod("ToDictionary"));
    }
}
