using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain.Vision;

/// <summary>
/// ScreenHints 单元测试 — PRD §5.1: extra 独立嵌套字段; regions 为 ImmutableArray; 无 ToDictionary/FromDictionary
/// </summary>
public class ScreenHintsTests
{
    [Fact(DisplayName = "ScreenHints构造: 省略可选字段 → TopBarText=null, Regions=空, Extra=null")]
    public void Construction_ShouldDefaultOptionalFields()
    {
        var hints = new ScreenHints();
        Assert.Null(hints.TopBarText);
        Assert.Empty(hints.Regions);
        Assert.False(hints.OverlayDetected);
        Assert.False(hints.ScrollDetected);
        Assert.Null(hints.Extra);
    }

    [Fact(DisplayName = "ScreenHints.Regions: ImmutableArray类型, 单元素可读取")]
    public void Regions_ShouldBeImmutableArray()
    {
        var region = new Region(Id: "r1",
            Bounds: new BoundingBox(X: 0, Y: 0, Width: 1, Height: 1),
            Role: RegionRole.Content);
        var hints = new ScreenHints(Regions: ImmutableArray.Create(region));
        Assert.IsAssignableFrom<ImmutableArray<Region>>(hints.Regions);
        Assert.Single(hints.Regions);
    }

    [Fact(DisplayName = "ScreenHints序列化: Extra字段序列化为嵌套JSON对象")]
    public void Extra_ShouldSerializeAsNestedField()
    {
        var hints = new ScreenHints(
            TopBarText: "Settings",
            Extra: ImmutableDictionary<string, object>.Empty.Add("pageType", "list"));

        var json = JsonSerializer.Serialize(hints, DomainJsonOptions.Default);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("extra", out var extra));
        Assert.True(extra.TryGetProperty("pageType", out var pageType));
        Assert.Equal("list", pageType.GetString());
    }

    [Fact(DisplayName = "ScreenHints禁止模式: 无ToDictionary方法")]
    public void NoToDictionaryMethod_ShouldExist()
    {
        Assert.Null(typeof(ScreenHints).GetMethod("ToDictionary"));
    }
}
