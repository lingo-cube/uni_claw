using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 粗粒度的视觉元素分类枚举。无 Unknown（PRD §5.1）：未识别输入回落为 Text。
/// [JsonPropertyName] 锁定 snake_case 序列化名（P3：对齐 Python，与其他 3 个 Domain enum 一致）。
/// </summary>
public enum TypeHint
{
    /// <summary>可点击文本区域（如菜单项）</summary>
    [JsonPropertyName("clickable_text")] ClickableText,

    /// <summary>开关/切换控件</summary>
    [JsonPropertyName("switch")] Switch,

    /// <summary>滑块控件</summary>
    [JsonPropertyName("slider")] Slider,

    /// <summary>按钮控件</summary>
    [JsonPropertyName("button")] Button,

    /// <summary>图标元素（无文本）</summary>
    [JsonPropertyName("icon")] Icon,

    /// <summary>文本输入框</summary>
    [JsonPropertyName("input_field")] InputField,

    /// <summary>纯文本（非交互）</summary>
    [JsonPropertyName("text")] Text,

    /// <summary>图片元素</summary>
    [JsonPropertyName("image")] Image
}

/// <summary>
/// TypeHint 扩展方法
/// </summary>
public static class TypeHintExtensions
{
    /// <summary>所有合法枚举值</summary>
    public static IReadOnlyList<TypeHint> Values { get; } =
        (IReadOnlyList<TypeHint>)Enum.GetValues<TypeHint>();

    // ── 别名字典：8 精确枚举值 + 10 Python 别名 + 3 C# 扩展别名 ──
    private static readonly Dictionary<string, TypeHint> AliasMap = new()
    {
        // 8 精确枚举值
        ["clickable_text"] = TypeHint.ClickableText,
        ["switch"] = TypeHint.Switch,
        ["slider"] = TypeHint.Slider,
        ["button"] = TypeHint.Button,
        ["icon"] = TypeHint.Icon,
        ["input_field"] = TypeHint.InputField,
        ["text"] = TypeHint.Text,
        ["image"] = TypeHint.Image,

        // Python 别名
        ["clickable"] = TypeHint.ClickableText,   // Python alias
        ["click"] = TypeHint.ClickableText,       // Python alias (previously missing)
        ["toggle"] = TypeHint.Switch,             // Python alias
        ["checkbox"] = TypeHint.Switch,           // Python alias
        ["check"] = TypeHint.Switch,              // Python alias (previously missing)
        ["btn"] = TypeHint.Button,                // Python alias
        ["input"] = TypeHint.InputField,          // Python alias
        ["field"] = TypeHint.InputField,          // Python alias
        ["img"] = TypeHint.Image,                 // Python alias
        ["picture"] = TypeHint.Image,             // Python alias

        // C# 扩展别名（Python mapping dict 不含，但 C# 遍历场景实际遇到）
        ["seekbar"] = TypeHint.Slider,            // Android SeekBar
        ["edit"] = TypeHint.InputField,           // EditText
        ["textbox"] = TypeHint.InputField,        // AI variant
    };

    /// <summary>判断类型是否可交互</summary>
    public static bool IsInteractive(this TypeHint type) => type switch
    {
        TypeHint.ClickableText or TypeHint.Switch or TypeHint.Slider or
        TypeHint.Button or TypeHint.InputField => true,
        _ => false
    };

    /// <summary>判断类型是否仅为视觉元素（不可交互）</summary>
    public static bool IsVisualOnly(this TypeHint type) => !IsInteractive(type);

    /// <summary>判断枚举值是否在合法范围内</summary>
    public static bool IsValid(TypeHint value) => Enum.IsDefined(value);

    /// <summary>
    /// 从字符串创建 TypeHint：精确别名字典匹配 → 未识别回落 Text。
    /// 不返回 Unknown（已删除）。大小写不敏感，空格容错。
    /// </summary>
    public static TypeHint FromString(string value)
    {
        var key = value.ToLowerInvariant().Trim();
        return AliasMap.TryGetValue(key, out var result) ? result : TypeHint.Text;
    }

    /// <summary>
    /// 判断字符串能否被 FromString 成功解析（含别名），未识别值返回 false。
    /// </summary>
    public static bool IsValid(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return AliasMap.ContainsKey(value.ToLowerInvariant().Trim());
    }

    /// <summary>
    /// 8 个 TypeHint 枚举值的精确规范名（snake_case，与 [JsonPropertyName] 一致）。
    /// </summary>
    private static readonly HashSet<string> CanonicalNames = new()
    {
        "clickable_text", "switch", "slider", "button",
        "icon", "input_field", "text", "image"
    };

    /// <summary>
    /// 判断字符串是否为 TypeHint 的精确规范名（8 个枚举值之一），区别于别名。
    /// IsValid(string) 含别名，IsCanonical(string) 仅精确值。
    /// </summary>
    public static bool IsCanonical(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return CanonicalNames.Contains(value.ToLowerInvariant().Trim());
    }
}
