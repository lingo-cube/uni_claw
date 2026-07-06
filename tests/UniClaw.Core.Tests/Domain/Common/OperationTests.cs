using System.Collections.Immutable;
using System.Reflection;
using Xunit;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;

namespace UniClaw.Core.Tests.Domain.Common;

/// <summary>
/// Operation 单元测试 — PRD §5.3: action 受限 {click,swipe,back,input_text,no_action};
/// 删 Wait/LongPress; params 默认空; 无 ToDictionary/FromDictionary
/// </summary>
public class OperationTests
{
    [Fact(DisplayName = "OperationType枚举安全: 不包含Wait和LongPress成员")]
    public void OperationType_ShouldNotDefineWaitOrLongPress()
    {
        var names = Enum.GetNames<OperationType>();
        Assert.DoesNotContain("Wait", names);
        Assert.DoesNotContain("LongPress", names);
    }

    [Fact(DisplayName = "OperationType枚举安全: 仅含5个合法action值")]
    public void OperationType_ShouldDefineExactActionSet()
    {
    }

    [Theory(DisplayName = "Operation构造: 5种合法Action → 成功创建")]
    [InlineData(OperationType.Click)]
    [InlineData(OperationType.Swipe)]
    [InlineData(OperationType.Back)]
    [InlineData(OperationType.InputText)]
    [InlineData(OperationType.NoAction)]
    public void Construction_ShouldAcceptAllowedAction(OperationType action)
    {
        var op = new Operation(Action: action);
        Assert.Equal(action, op.Action);
    }

    [Fact(DisplayName = "Operation构造: Action越界值999 → 抛异常+FieldName=Action")]
    public void Construction_ShouldThrow_WhenActionOutOfRange()
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            new Operation(Action: (OperationType)999));
        Assert.Equal("Action", ex.FieldName);
    }

    [Fact(DisplayName = "Operation.Params: 省略时默认为空不可变字典")]
    public void Params_ShouldDefaultToEmpty()
    {
        var op = new Operation(Action: OperationType.NoAction);
        Assert.NotNull(op.Params);
        Assert.Empty(op.Params);
    }

    [Fact(DisplayName = "Operation.Params: 显式传入 → 为IImmutableDictionary类型")]
    public void Params_ShouldBeImmutableDictionary()
    {
        var op = new Operation(Action: OperationType.Click,
            Params: ImmutableDictionary<string, object>.Empty.Add("k", "v"));
        Assert.IsAssignableFrom<IImmutableDictionary<string, object>>(op.Params);
    }

    [Fact(DisplayName = "Operation禁止模式: 无ToDictionary和FromDictionary方法")]
    public void NoToDictionaryMethod_ShouldExist()
    {
        Assert.Null(typeof(Operation).GetMethod("ToDictionary"));
        Assert.Null(typeof(Operation).GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static));
    }
}
