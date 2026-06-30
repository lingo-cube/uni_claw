namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 视觉元素的激活/选择状态
/// </summary>
public enum SelectionState
{
    /// <summary>
    /// 当前选中/高亮状态
    /// </summary>
    Selected,

    /// <summary>
    /// 普通未选中状态
    /// </summary>
    Normal,

    /// <summary>
    /// 禁用状态（灰色，不可交互）
    /// </summary>
    Disabled
}

/// <summary>
/// SelectionState 扩展方法
/// </summary>
public static class SelectionStateExtensions
{
    /// <summary>
    /// 判断是否可交互（非禁用）
    /// </summary>
    public static bool IsInteractive(this SelectionState state) => state != SelectionState.Disabled;

    /// <summary>
    /// 判断是否为选中状态
    /// </summary>
    public static bool IsActive(this SelectionState state) => state == SelectionState.Selected;

    /// <summary>
    /// 从字符串模糊匹配创建 SelectionState
    /// </summary>
    public static SelectionState FromString(string value)
    {
        return value.ToLowerInvariant() switch
        {
            var v when v.Contains("selected") || v.Contains("active") || v.Contains("highlighted") => SelectionState.Selected,
            var v when v.Contains("disabled") || v.Contains("gray") || v.Contains("dimmed") => SelectionState.Disabled,
            _ => SelectionState.Normal
        };
    }
}
