using System.Collections.Immutable;
using Xunit;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain.Vision;

/// <summary>
/// Region 单元测试 — PRD §5.1: id/bounds/role; role 受限集合(枚举); 无 ToDictionary/FromDictionary
/// </summary>
public class RegionTests
{
    private static BoundingBox ValidBounds => new(X: 0.0, Y: 0.0, Width: 1.0, Height: 1.0);

    [Theory(DisplayName = "Region构造: Menu/Content/Tabs/Overlay四种Role → 成功创建")]
    [InlineData(RegionRole.Menu)]
    [InlineData(RegionRole.Content)]
    [InlineData(RegionRole.Tabs)]
    [InlineData(RegionRole.Overlay)]
    public void Construction_ShouldAcceptAllowedRole(RegionRole role)
    {
        var region = new Region(Id: "r1", Bounds: ValidBounds, Role: role);
        Assert.Equal(role, region.Role);
    }

    [Fact(DisplayName = "Region包含点: 委托给Bounds.ContainsPoint判断")]
    public void ContainsPoint_ShouldDelegateToBounds()
    {
        var region = new Region(Id: "r1", Bounds: ValidBounds, Role: RegionRole.Content);
        Assert.True(region.ContainsPoint(0.5, 0.5));
        Assert.False(region.ContainsPoint(1.5, 1.5));
    }

    [Fact(DisplayName = "Region禁止模式: 无ToDictionary方法")]
    public void NoToDictionaryMethod_ShouldExist()
    {
        var method = typeof(Region).GetMethod("ToDictionary");
        Assert.Null(method);
    }
}
