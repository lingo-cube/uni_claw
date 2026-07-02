namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 视觉元素的激活/选择状态
/// </summary>
public enum SelectionState
{
    /// <summary>当前选中/高亮状态</summary>
    Selected,

    /// <summary>普通未选中状态</summary>
    Normal,

    /// <summary>禁用状态（灰色，不可交互）</summary>
    Disabled
}

/// <summary>
/// SelectionState 扩展方法
/// </summary>
public static class SelectionStateExtensions
{
    // ── Selected aliases (5 values, Python + C# extension) ──
    private static readonly HashSet<string> SelectedAliases = new()
    {
        "selected",      // exact enum value
        "active",        // Python alias
        "checked",       // Python alias
        "highlight",     // Python alias (previously missing)
        "highlighted",   // Python alias (previously missing)
    };

    // ── Disabled aliases (6 values, Python + C# extension) ──
    private static readonly HashSet<string> DisabledAliases = new()
    {
        "disabled",      // C# extension (semantic: disabled → Disabled is intuitive)
        "inactive",      // Python alias
        "hidden",        // Python alias
        "gray",          // Python alias
        "grayed",        // Python alias
        "dimmed",        // Python alias
    };

    /// <summary>判断是否可交互（非禁用）</summary>
    public static bool IsInteractive(this SelectionState state) => state != SelectionState.Disabled;

    /// <summary>判断是否为选中状态</summary>
    public static bool IsActive(this SelectionState state) => state == SelectionState.Selected;

    /// <summary>
    /// 从字符串创建 SelectionState：精确别名 HashSet 匹配。
    /// DisabledAliases 先查（避免 "inactive" 误落入 "active" 子串匹配）；
    /// SelectedAliases 后查；未命中回落 Normal。
    /// 大小写不敏感。
    /// </summary>
    public static SelectionState FromString(string value)
    {
        var key = value.ToLowerInvariant().Trim();

        // Disabled 先查：防止 "inactive" 被误判为 Selected (含 "active" 子串)
        if (DisabledAliases.Contains(key))
            return SelectionState.Disabled;

        if (SelectedAliases.Contains(key))
            return SelectionState.Selected;

        return SelectionState.Normal;
    }

    /// <summary>
    /// 判断字符串能否被 FromString 成功解析为已知状态（非回落 Normal）。
    /// 含别名 + "normal" 精确值；未知值返回 false。
    /// </summary>
    public static bool IsValid(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var key = value.ToLowerInvariant().Trim();
        return SelectedAliases.Contains(key) || DisabledAliases.Contains(key) || key == "normal";
    }
}
