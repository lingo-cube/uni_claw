namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 粗粒度的视觉元素分类枚举
/// </summary>
public enum TypeHint
{
    /// <summary>
    /// 可点击文本区域（如菜单项）
    /// </summary>
    ClickableText,

    /// <summary>
    /// 开关/切换控件
    /// </summary>
    Switch,

    /// <summary>
    /// 滑块控件
    /// </summary>
    Slider,

    /// <summary>
    /// 按钮控件
    /// </summary>
    Button,

    /// <summary>
    /// 图标元素（无文本）
    /// </summary>
    Icon,

    /// <summary>
    /// 文本输入框
    /// </summary>
    InputField,

    /// <summary>
    /// 纯文本（非交互）
    /// </summary>
    Text,

    /// <summary>
    /// 图片元素
    /// </summary>
    Image,

    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown
}

/// <summary>
/// TypeHint 扩展方法
/// </summary>
public static class TypeHintExtensions
{
    /// <summary>
    /// 判断类型是否可交互
    /// </summary>
    public static bool IsInteractive(this TypeHint type) => type switch
    {
        TypeHint.ClickableText or TypeHint.Switch or TypeHint.Slider or
        TypeHint.Button or TypeHint.InputField => true,
        _ => false
    };

    /// <summary>
    /// 判断类型是否仅为视觉元素（不可交互）
    /// </summary>
    public static bool IsVisualOnly(this TypeHint type) => !IsInteractive(type);

    /// <summary>
    /// 从字符串模糊匹配创建 TypeHint
    /// </summary>
    public static TypeHint FromString(string value)
    {
        return value.ToLowerInvariant() switch
        {
            // 可点击文本（仅 "clickable" 触发，纯 "text" 归 Text）
            var v when v.Contains("clickable") => TypeHint.ClickableText,

            // 开关
            var v when v.Contains("switch") || v.Contains("toggle") || v.Contains("checkbox") => TypeHint.Switch,

            // 滑块
            var v when v.Contains("slider") || v.Contains("seekbar") => TypeHint.Slider,

            // 按钮
            var v when v.Contains("button") || v.Contains("btn") => TypeHint.Button,

            // 图标（仅 "icon" 触发，"image" 归 Image）
            var v when v.Contains("icon") => TypeHint.Icon,

            // 输入框
            var v when v.Contains("input") || v.Contains("edit") || v.Contains("field") || v.Contains("textbox") => TypeHint.InputField,

            // 图片
            var v when v.Contains("image") || v.Contains("img") || v.Contains("picture") => TypeHint.Image,

            // 纯文本
            var v when v.Contains("text") => TypeHint.Text,

            _ => TypeHint.Unknown
        };
    }
}
