using Xunit;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain.Vision;

/// <summary>
/// TypeHint 单元测试
/// </summary>
public class TypeHintTests
{
    [Theory]
    [InlineData(TypeHint.ClickableText, true)]
    [InlineData(TypeHint.Switch, true)]
    [InlineData(TypeHint.Slider, true)]
    [InlineData(TypeHint.Button, true)]
    [InlineData(TypeHint.InputField, true)]
    [InlineData(TypeHint.Icon, false)]
    [InlineData(TypeHint.Text, false)]
    [InlineData(TypeHint.Image, false)]
    [InlineData(TypeHint.Unknown, false)]
    public void IsInteractive_ShouldReturnExpectedValue(TypeHint type, bool expected)
    {
        // Act
        var result = type.IsInteractive();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("button", TypeHint.Button)]
    [InlineData("Button", TypeHint.Button)]
    [InlineData("btn", TypeHint.Button)]
    [InlineData("switch", TypeHint.Switch)]
    [InlineData("toggle", TypeHint.Switch)]
    [InlineData("clickable", TypeHint.ClickableText)]
    [InlineData("text", TypeHint.Text)]
    [InlineData("icon", TypeHint.Icon)]
    [InlineData("slider", TypeHint.Slider)]
    [InlineData("input", TypeHint.InputField)]
    [InlineData("edit", TypeHint.InputField)]
    [InlineData("image", TypeHint.Image)]
    [InlineData("unknown", TypeHint.Unknown)]
    public void FromString_ShouldParseCorrectly(string input, TypeHint expected)
    {
        // Act
        var result = TypeHintExtensions.FromString(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
