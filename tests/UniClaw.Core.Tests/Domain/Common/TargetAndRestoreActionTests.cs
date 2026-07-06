using System.Collections.Immutable;
using System.Reflection;
using Xunit;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;

namespace UniClaw.Core.Tests.Domain.Common;

/// <summary>
/// Target / RestoreAction 单元测试 — PRD §5.3
/// </summary>
public class TargetAndRestoreActionTests
{
    [Fact(DisplayName = "TargetType枚举安全: 不包含ResourceId和ElementType成员")]
    public void TargetType_ShouldNotDefineResourceIdOrElementType()
    {
        var names = Enum.GetNames<TargetType>();
        Assert.DoesNotContain("ResourceId", names);
        Assert.DoesNotContain("ElementType", names);
    }

    [Theory(DisplayName = "Target构造: Text/Coordinate/UiIndex三种By → 成功创建")]
    [InlineData(TargetType.Text)]
    [InlineData(TargetType.Coordinate)]
    [InlineData(TargetType.UiIndex)]
    public void Target_Construction_ShouldAcceptAllowedBy(TargetType by)
    {
        var target = new Target(By: by, Value: "x");
        Assert.Equal(by, target.By);
    }

    [Fact(DisplayName = "Target构造: By越界值999 → 抛异常+FieldName=By")]
    public void Target_Construction_ShouldThrow_WhenByOutOfRange()
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            new Target(By: (TargetType)999, Value: "x"));
        Assert.Equal("By", ex.FieldName);
    }

    [Fact(DisplayName = "Target.Meta: 省略时默认为空不可变字典")]
    public void Target_Meta_ShouldDefaultToEmpty()
    {
        var target = new Target(By: TargetType.Text, Value: "x");
        Assert.NotNull(target.Meta);
        Assert.Empty(target.Meta);
    }

    [Fact(DisplayName = "Target禁止模式: 无ToDictionary和FromDictionary方法")]
    public void Target_NoToDictionaryMethod_ShouldExist()
    {
        Assert.Null(typeof(Target).GetMethod("ToDictionary"));
        Assert.Null(typeof(Target).GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact(DisplayName = "RestoreAction构造: Action越界值999 → 抛异常+FieldName=Action")]
    public void RestoreAction_Construction_ShouldThrow_WhenActionOutOfRange()
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            new RestoreAction(Action: (OperationType)999));
        Assert.Equal("Action", ex.FieldName);
    }

    [Fact(DisplayName = "RestoreAction.Params: 省略时默认为空不可变字典")]
    public void RestoreAction_Params_ShouldDefaultToEmpty()
    {
        var ra = new RestoreAction(Action: OperationType.NoAction);
        Assert.NotNull(ra.Params);
        Assert.Empty(ra.Params);
    }

    [Fact(DisplayName = "RestoreAction禁止模式: 无ToDictionary和FromDictionary方法")]
    public void RestoreAction_NoToDictionaryMethod_ShouldExist()
    {
        Assert.Null(typeof(RestoreAction).GetMethod("ToDictionary"));
        Assert.Null(typeof(RestoreAction).GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static));
    }
}
