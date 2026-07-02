using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Domain.Mappings;

/// <summary>
/// 领域映射器（PRD §5.4）：Android 控件类 → 中间字符串 → MenuItemType / ExpectedAction。
/// 依赖仅 Models.Content + Models.Vision（不依赖上层）。全映射表与 Python source row-for-row 对齐。
/// </summary>
public static class ElementTypeMapper
{
    // ── Android short-name → 中间字符串 (与 Python ANDROID_CLASS_MAP row-for-row) ──
    // 精确匹配 → 子串匹配 → 回落 "button"
    private static readonly Dictionary<string, string> AndroidClassMap = new()
    {
        // Toggles/Switches
        ["Switch"] = "switch",
        ["CheckBox"] = "switch",
        ["RadioButton"] = "switch",
        ["ToggleButton"] = "toggle",          // Python: toggle ≠ switch; 下游独立 MenuItemType.Toggle

        // Buttons
        ["Button"] = "button",
        ["ImageButton"] = "button",

        // Text/Labels → menu_item or input
        ["TextView"] = "menu_item",
        ["EditText"] = "input",

        // Layouts → menu_item (menu_item containers)
        ["LinearLayout"] = "menu_item",
        ["RelativeLayout"] = "menu_item",
        ["FrameLayout"] = "menu_item",
        ["ConstraintLayout"] = "menu_item",

        // Seekable → slider
        ["SeekBar"] = "slider",
        ["RatingBar"] = "slider",
    };

    // ── Element type string → MenuItemType ──
    private static readonly Dictionary<string, MenuItemType> TypeToMenuItemTypeMap = new()
    {
        ["menu_item"] = MenuItemType.MenuItem,
        ["switch"] = MenuItemType.Switch,
        ["slider"] = MenuItemType.Button,    // Python: sliders map to BUTTON (action type)
        ["button"] = MenuItemType.Button,
        ["toggle"] = MenuItemType.Toggle,
        ["text"] = MenuItemType.Text,
        ["readonly"] = MenuItemType.Readonly,
        ["item"] = MenuItemType.Item,
        ["input"] = MenuItemType.Text,
        ["icon"] = MenuItemType.Icon,
        ["link"] = MenuItemType.Link,
        ["tab"] = MenuItemType.Tab,
        ["back_button"] = MenuItemType.BackButton,
    };

    // ── Element type string → ExpectedAction ──
    private static readonly Dictionary<string, ExpectedAction> TypeToExpectedActionMap = new()
    {
        // Toggles change state
        ["switch"] = ExpectedAction.Toggle,
        ["toggle"] = ExpectedAction.Toggle,

        // Sliders adjust values
        ["slider"] = ExpectedAction.Action,

        // Buttons trigger actions
        ["button"] = ExpectedAction.Action,

        // Menu items navigate
        ["menu_item"] = ExpectedAction.Navigate,
        ["tab"] = ExpectedAction.Navigate,

        // Text is read-only
        ["text"] = ExpectedAction.None,
        ["readonly"] = ExpectedAction.None,

        // Inputs trigger input
        ["input"] = ExpectedAction.Action,

        // Icons can be various actions
        ["icon"] = ExpectedAction.Action,
        ["link"] = ExpectedAction.Navigate,

        // Back buttons navigate back
        ["back_button"] = ExpectedAction.Navigate,
    };

    // ── 中间字符串 → TypeHint 视觉分类 ──
    private static readonly Dictionary<string, TypeHint> TypeStringToTypeHintMap = new()
    {
        ["switch"] = TypeHint.Switch,
        ["toggle"] = TypeHint.Switch,         // ToggleButton 视觉外观 = Switch
        ["menu_item"] = TypeHint.ClickableText,
        ["input"] = TypeHint.InputField,
        ["slider"] = TypeHint.Slider,
        ["button"] = TypeHint.Button,
    };

    /// <summary>
    /// 将 Android 控件类名映射为中间字符串。支持完整类名或短名；
    /// 精确匹配 → 子串匹配 → 回落 "button"。
    /// </summary>
    public static string MapAndroidClass(string className)
    {
        if (className is null)
            throw new DomainValidationException(nameof(className), className);

        // 精确短名匹配
        if (AndroidClassMap.TryGetValue(className, out var exact))
            return exact;

        // 子串匹配（className 包含 short-name）
        foreach (var (key, value) in AndroidClassMap)
            if (className.Contains(key))
                return value;

        // 回落
        return "button";
    }

    /// <summary>
    /// 将中间字符串映射为视觉分类 TypeHint。未知值回落 Text。
    /// </summary>
    public static TypeHint ToTypeHint(string typeString)
    {
        return TypeStringToTypeHintMap.GetValueOrDefault(typeString, TypeHint.Text);
    }

    /// <summary>
    /// 将元素类型字符串映射为 MenuItemType。回落为 Item。
    /// </summary>
    public static MenuItemType ToMenuItemType(string typeString)
    {
        return TypeToMenuItemTypeMap.GetValueOrDefault(typeString, MenuItemType.Item);
    }

    /// <summary>
    /// 将元素类型字符串映射为 ExpectedAction。回落为 None。
    /// </summary>
    public static ExpectedAction ToExpectedAction(string typeString)
    {
        return TypeToExpectedActionMap.GetValueOrDefault(typeString, ExpectedAction.None);
    }

    /// <summary>判断类型字符串是否为已知合法类型</summary>
    public static bool IsValidType(string typeString) => TypeToMenuItemTypeMap.ContainsKey(typeString);

    /// <summary>判断类名是否包含已知 Android 控件类</summary>
    public static bool IsValidAndroidClass(string className)
    {
        if (className is null) return false;
        if (AndroidClassMap.ContainsKey(className)) return true;
        foreach (var key in AndroidClassMap.Keys)
            if (className.Contains(key)) return true;
        return false;
    }

    // ── Full-table accessor for test scan ──

    /// <summary>Android 控件类→中间字符串映射表（全 14 行）</summary>
    public static IReadOnlyDictionary<string, string> AndroidClassMapAccessor => AndroidClassMap;
    /// <summary>中间字符串→MenuItemType 映射表</summary>
    public static IReadOnlyDictionary<string, MenuItemType> MenuItemTypeMap => TypeToMenuItemTypeMap;
    /// <summary>中间字符串→ExpectedAction 映射表</summary>
    public static IReadOnlyDictionary<string, ExpectedAction> ExpectedActionMap => TypeToExpectedActionMap;
}
