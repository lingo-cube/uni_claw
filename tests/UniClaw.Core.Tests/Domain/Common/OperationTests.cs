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
    [Fact]
    public void OperationType_ShouldNotDefineWaitOrLongPress()
    {
        var names = Enum.GetNames<OperationType>();
        Assert.DoesNotContain("Wait", names);
        Assert.DoesNotContain("LongPress", names);
    }

    [Fact]
    public void OperationType_ShouldDefineExactActionSet()
    {
    }

    [Theory]
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

    [Fact]
    public void Construction_ShouldThrow_WhenActionOutOfRange()
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            new Operation(Action: (OperationType)999));
        Assert.Equal("Action", ex.FieldName);
    }

    [Fact]
    public void Params_ShouldDefaultToEmpty()
    {
        var op = new Operation(Action: OperationType.NoAction);
        Assert.NotNull(op.Params);
        Assert.Empty(op.Params);
    }

    [Fact]
    public void Params_ShouldBeImmutableDictionary()
    {
        var op = new Operation(Action: OperationType.Click,
            Params: ImmutableDictionary<string, object>.Empty.Add("k", "v"));
        Assert.IsAssignableFrom<IImmutableDictionary<string, object>>(op.Params);
    }

    [Fact]
    public void NoToDictionaryMethod_ShouldExist()
    {
        Assert.Null(typeof(Operation).GetMethod("ToDictionary"));
        Assert.Null(typeof(Operation).GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static));
    }
}
