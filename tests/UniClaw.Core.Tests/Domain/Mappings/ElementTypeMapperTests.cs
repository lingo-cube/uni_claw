using Xunit;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Domain.Mappings;

namespace UniClaw.Core.Tests.Domain.Mappings;

/// <summary>
/// ElementTypeMapper 单元测试 — PRD §5.4/§7.4: 全表扫描 + 子串匹配 + 回落 + null 防御 + ToTypeHint
/// </summary>
public class ElementTypeMapperTests
{
    // ── Android short-name → 中间字符串 (全表扫描，与 Python ANDROID_CLASS_MAP row-for-row) ──

    [Theory]
    [InlineData("Switch", "switch")]
    [InlineData("CheckBox", "switch")]
    [InlineData("RadioButton", "switch")]
    [InlineData("ToggleButton", "toggle")]         // 独立中间字符串，非 TypeHint.Switch
    [InlineData("Button", "button")]
    [InlineData("ImageButton", "button")]
    [InlineData("TextView", "menu_item")]
    [InlineData("EditText", "input")]
    [InlineData("LinearLayout", "menu_item")]
    [InlineData("RelativeLayout", "menu_item")]
    [InlineData("FrameLayout", "menu_item")]
    [InlineData("ConstraintLayout", "menu_item")]
    [InlineData("SeekBar", "slider")]
    [InlineData("RatingBar", "slider")]
    public void MapAndroidClass_ShortName_AllEntries(string className, string expected)
    {
        Assert.Equal(expected, ElementTypeMapper.MapAndroidClass(className));
    }

    // ── Full class name (android.widget.xxx) 子串匹配 ──

    [Theory]
    [InlineData("android.widget.Switch", "switch")]
    [InlineData("android.widget.Button", "button")]
    [InlineData("android.widget.SeekBar", "slider")]
    [InlineData("android.widget.TextView", "menu_item")]
    public void MapAndroidClass_FullName_SubstringMatch(string fullName, string expected)
    {
        Assert.Equal(expected, ElementTypeMapper.MapAndroidClass(fullName));
    }

    // ── 回落 ──

    [Fact]
    public void MapAndroidClass_UnknownClass_FallsBackToButton()
    {
        Assert.Equal("button", ElementTypeMapper.MapAndroidClass("UnknownWidget"));
    }

    // ── null 防御 ──

    [Fact]
    public void MapAndroidClass_Null_ThrowsDomainValidationException()
    {
        var ex = Assert.Throws<DomainValidationException>(() => ElementTypeMapper.MapAndroidClass(null!));
        Assert.Equal("className", ex.FieldName);
        Assert.Null(ex.IllegalValue);
    }

    // ── ToggleButton 链端到端验证 ──

    [Fact]
    public void MapAndroidClass_ToggleButton_ChainCompletes()
    {
        var intermediate = ElementTypeMapper.MapAndroidClass("ToggleButton");
        Assert.Equal("toggle", intermediate);

        var menuItemType = ElementTypeMapper.ToMenuItemType(intermediate);
        Assert.Equal(MenuItemType.Toggle, menuItemType);

        var expectedAction = ElementTypeMapper.ToExpectedAction(intermediate);
        Assert.Equal(ExpectedAction.Toggle, expectedAction);
    }

    // ── ToTypeHint ──

    [Theory]
    [InlineData("switch", TypeHint.Switch)]
    [InlineData("toggle", TypeHint.Switch)]        // 视觉外观 = Switch
    [InlineData("menu_item", TypeHint.ClickableText)]
    [InlineData("input", TypeHint.InputField)]
    [InlineData("slider", TypeHint.Slider)]
    [InlineData("button", TypeHint.Button)]
    public void ToTypeHint_KnownMappings(string typeString, TypeHint expected)
    {
        Assert.Equal(expected, ElementTypeMapper.ToTypeHint(typeString));
    }

    [Fact]
    public void ToTypeHint_UnknownValue_FallsBackToText()
    {
        Assert.Equal(TypeHint.Text, ElementTypeMapper.ToTypeHint("unknown_type"));
    }

    // ── AndroidClassMap accessor 类型 ──

    [Fact]
    public void AndroidClassMapAccessor_IsStringDictionary()
    {
        var map = ElementTypeMapper.AndroidClassMapAccessor;
        Assert.Equal(14, map.Count);
        Assert.Equal("toggle", map["ToggleButton"]);
    }

    // ── Type-string → MenuItemType (与 Python TYPE_TO_MENU_ITEM row-for-row) ──

    [Theory]
    [InlineData("menu_item", MenuItemType.MenuItem)]
    [InlineData("switch", MenuItemType.Switch)]
    [InlineData("slider", MenuItemType.Button)]
    [InlineData("button", MenuItemType.Button)]
    [InlineData("toggle", MenuItemType.Toggle)]
    [InlineData("text", MenuItemType.Text)]
    [InlineData("readonly", MenuItemType.Readonly)]
    [InlineData("item", MenuItemType.Item)]
    [InlineData("input", MenuItemType.Text)]
    [InlineData("icon", MenuItemType.Icon)]
    [InlineData("link", MenuItemType.Link)]
    [InlineData("tab", MenuItemType.Tab)]
    [InlineData("back_button", MenuItemType.BackButton)]
    public void ToMenuItemType_AllEntries(string typeString, MenuItemType expected)
    {
        Assert.Equal(expected, ElementTypeMapper.ToMenuItemType(typeString));
    }

    [Fact]
    public void ToMenuItemType_UnknownType_FallsBackToItem()
    {
        Assert.Equal(MenuItemType.Item, ElementTypeMapper.ToMenuItemType("unknown_type"));
    }

    // ── Type-string → ExpectedAction (与 Python TYPE_TO_EXPECTED_ACTION row-for-row) ──

    [Theory]
    [InlineData("switch", ExpectedAction.Toggle)]
    [InlineData("toggle", ExpectedAction.Toggle)]
    [InlineData("slider", ExpectedAction.Action)]
    [InlineData("button", ExpectedAction.Action)]
    [InlineData("menu_item", ExpectedAction.Navigate)]
    [InlineData("tab", ExpectedAction.Navigate)]
    [InlineData("text", ExpectedAction.None)]
    [InlineData("readonly", ExpectedAction.None)]
    [InlineData("input", ExpectedAction.Action)]
    [InlineData("icon", ExpectedAction.Action)]
    [InlineData("link", ExpectedAction.Navigate)]
    [InlineData("back_button", ExpectedAction.Navigate)]
    public void ToExpectedAction_AllEntries(string typeString, ExpectedAction expected)
    {
        Assert.Equal(expected, ElementTypeMapper.ToExpectedAction(typeString));
    }

    [Fact]
    public void ToExpectedAction_UnknownType_FallsBackToNone()
    {
        Assert.Equal(ExpectedAction.None, ElementTypeMapper.ToExpectedAction("unknown_type"));
    }

    // ── Validation ──

    [Fact]
    public void IsValidType_KnownAndUnknown()
    {
        Assert.True(ElementTypeMapper.IsValidType("switch"));
        Assert.False(ElementTypeMapper.IsValidType("diagonal"));
    }

    [Fact]
    public void IsValidAndroidClass_KnownAndUnknown()
    {
        Assert.True(ElementTypeMapper.IsValidAndroidClass("android.widget.Switch"));
        Assert.False(ElementTypeMapper.IsValidAndroidClass("com.example.CustomView"));
    }
}
