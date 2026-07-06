using Xunit;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain.Vision;

/// <summary>
/// TypeHint 单元测试 — PRD §5.1: 删 Unknown; FromString 精确别名字典+回落 Text;
/// 补 Values/IsValid(TypeHint)/IsValid(string); 新增 scrollable/click/check/dropdown/seekbar/BUTTON/空格容错
/// </summary>
public class TypeHintTests
{
    [Theory(DisplayName = "TypeHint交互性: 交互/非交互类型 → IsInteractive返回对应bool值")]
    [InlineData(TypeHint.ClickableText, true)]
    [InlineData(TypeHint.Switch, true)]
    [InlineData(TypeHint.Slider, true)]
    [InlineData(TypeHint.Button, true)]
    [InlineData(TypeHint.InputField, true)]
    [InlineData(TypeHint.Icon, false)]
    [InlineData(TypeHint.Text, false)]
    [InlineData(TypeHint.Image, false)]
    public void IsInteractive_ShouldReturnExpectedValue(TypeHint type, bool expected)
    {
        Assert.Equal(expected, type.IsInteractive());
    }

    // ── FromString: 精确枚举值 ──

    [Theory(DisplayName = "FromString精确枚举值: 输入canonical名 → 返回对应TypeHint")]
    [InlineData("button", TypeHint.Button)]
    [InlineData("switch", TypeHint.Switch)]
    [InlineData("clickable_text", TypeHint.ClickableText)]
    [InlineData("slider", TypeHint.Slider)]
    [InlineData("icon", TypeHint.Icon)]
    [InlineData("input_field", TypeHint.InputField)]
    [InlineData("text", TypeHint.Text)]
    [InlineData("image", TypeHint.Image)]
    public void FromString_ExactEnumValues(string input, TypeHint expected)
    {
        Assert.Equal(expected, TypeHintExtensions.FromString(input));
    }

    // ── FromString: Python 别名 ──

    [Theory(DisplayName = "FromString Python别名: 别名映射 → 返回对应TypeHint")]
    [InlineData("clickable", TypeHint.ClickableText)]
    [InlineData("click", TypeHint.ClickableText)]      // previously missing
    [InlineData("toggle", TypeHint.Switch)]
    [InlineData("checkbox", TypeHint.Switch)]
    [InlineData("check", TypeHint.Switch)]              // previously missing
    [InlineData("btn", TypeHint.Button)]
    [InlineData("input", TypeHint.InputField)]
    [InlineData("field", TypeHint.InputField)]
    [InlineData("img", TypeHint.Image)]
    [InlineData("picture", TypeHint.Image)]
    public void FromString_PythonAliases(string input, TypeHint expected)
    {
        Assert.Equal(expected, TypeHintExtensions.FromString(input));
    }

    // ── FromString: C# 扩展别名 ──

    [Theory(DisplayName = "FromString C#扩展别名: seekbar/edit/textbox → 映射到对应TypeHint")]
    [InlineData("seekbar", TypeHint.Slider)]            // Android SeekBar
    [InlineData("edit", TypeHint.InputField)]           // EditText
    [InlineData("textbox", TypeHint.InputField)]        // AI variant
    public void FromString_CsExtensionAliases(string input, TypeHint expected)
    {
        Assert.Equal(expected, TypeHintExtensions.FromString(input));
    }

    // ── FromString: 大小写容错 ──

    [Theory(DisplayName = "FromString大小写容错: 大小写混合输入 → 仍返回正确TypeHint")]
    [InlineData("BUTTON", TypeHint.Button)]
    [InlineData("Switch", TypeHint.Switch)]
    [InlineData("CLICKABLE_TEXT", TypeHint.ClickableText)]
    public void FromString_CaseInsensitive(string input, TypeHint expected)
    {
        Assert.Equal(expected, TypeHintExtensions.FromString(input));
    }

    // ── FromString: 空格容错 ──

    [Theory(DisplayName = "FromString空格容错: 带前后空格输入 → 去空格后返回正确TypeHint")]
    [InlineData(" button ", TypeHint.Button)]
    [InlineData("  switch  ", TypeHint.Switch)]
    public void FromString_WhitespaceTolerant(string input, TypeHint expected)
    {
        Assert.Equal(expected, TypeHintExtensions.FromString(input));
    }

    // ── FromString: 未知值回落 Text ──

    [Theory(DisplayName = "FromString未知回落: 无法识别的字符串 → 回落为TypeHint.Text")]
    [InlineData("scrollable", TypeHint.Text)]           // previously mis-hit Slider via "scroll"
    [InlineData("dropdown", TypeHint.Text)]             // unknown
    [InlineData("something-unknown", TypeHint.Text)]
    [InlineData("", TypeHint.Text)]
    [InlineData("nonsense", TypeHint.Text)]
    [InlineData("xyz123", TypeHint.Text)]
    public void FromString_ShouldFallBackToText_WhenUnrecognized(string input, TypeHint expected)
    {
        Assert.Equal(expected, TypeHintExtensions.FromString(input));
    }

    [Fact(DisplayName = "TypeHint枚举安全: 不包含Unknown成员")]
    public void Enum_ShouldNotDefineUnknown()
    {
        Assert.DoesNotContain("Unknown", Enum.GetNames<TypeHint>());
    }

    [Fact(DisplayName = "TypeHint.Values: 返回全部8个canonical枚举成员")]
    public void Values_ShouldExposeAllCanonicalMembers()
    {
        Assert.Equal(Enum.GetValues<TypeHint>(), TypeHintExtensions.Values);
    }

    [Theory(DisplayName = "TypeHint.IsValid: 越界值999→false, 合法值→true")]
    [InlineData(TypeHint.Button, true)]
    [InlineData(TypeHint.Text, true)]
    [InlineData((TypeHint)999, false)]
    public void IsValid_ShouldRejectOutOfRangeValues(TypeHint value, bool expected)
    {
        Assert.Equal(expected, TypeHintExtensions.IsValid(value));
    }

    // ── IsValid(string) ──

    [Theory(DisplayName = "IsValid(string): canonical名和别名 → 返回true")]
    [InlineData("button", true)]
    [InlineData("btn", true)]                           // alias is parseable
    [InlineData("click", true)]                         // alias
    [InlineData("seekbar", true)]                       // C# extension alias
    public void IsValid_String_ReturnsTrueForKnownAndAlias(string value, bool expected)
    {
        Assert.Equal(expected, TypeHintExtensions.IsValid(value));
    }

    [Theory(DisplayName = "IsValid(string): 未知字符串和空串 → 返回false")]
    [InlineData("scrollable", false)]                   // not parseable
    [InlineData("", false)]                             // empty
    [InlineData("activated", false)]                    // unknown
    public void IsValid_String_ReturnsFalseForUnknown(string value, bool expected)
    {
        Assert.Equal(expected, TypeHintExtensions.IsValid(value));
    }
}
