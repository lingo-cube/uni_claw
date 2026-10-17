namespace UniClaw.Host.SettingsCoverage;

/// <summary>
/// TRAJECTORY-001 D5 — 像素矩形（uiAutomator bounds 语义：闭区间，
/// X2/Y2 含于矩形内）。
/// </summary>
public sealed record Rect(long X1, long Y1, long X2, long Y2)
{
    /// <summary>点是否在闭区间矩形内（含边界）。</summary>
    public bool Contains((long X, long Y) point) =>
        point.X >= X1 && point.X <= X2 && point.Y >= Y1 && point.Y <= Y2;

    /// <summary>与另一矩形存在正面积交叠（仅边界相触不算）。</summary>
    public bool Overlaps(Rect other) => IntersectionArea(other) > 0;

    /// <summary>交叠面积；不相交为 0。</summary>
    public long IntersectionArea(Rect other)
    {
        var width = Math.Min(X2, other.X2) - Math.Max(X1, other.X1);
        var height = Math.Min(Y2, other.Y2) - Math.Max(Y1, other.Y1);
        return width > 0 && height > 0 ? width * height : 0;
    }

    /// <summary>几何中心（整数中点；与中心点遮挡判据共用同一坐标语义）。</summary>
    public (long X, long Y) Center => ((X1 + X2) / 2, (Y1 + Y2) / 2);
}

/// <summary>
/// TRAJECTORY-001 D5 — 几何遮挡判定与 uiAutomator bounds 解析。裁决边界：
/// 几何第一阶段只做信号/诊断/慢感知输入，不改 Coverage Ledger；参与覆盖
/// 判定另立 Promotion 决策。
/// </summary>
public static class GeometryOps
{
    /// <summary>D5 遮挡判据：target 的中心点落在 overlay 内 = 被遮挡
    /// （中心点单点测试，不做面积占比推断）。</summary>
    public static bool IsOccluded(Rect target, Rect overlay) =>
        overlay.Contains(target.Center);

    /// <summary>
    /// 解析 uiAutomator bounds 属性文本 "[x1,y1][x2,y2]" → Rect；
    /// 格式非法（缺失/多余/非数字）→ null（不猜测）。
    /// </summary>
    public static Rect? ParseBounds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var parts = raw.Split("][", StringSplitOptions.TrimEntries);
        // "[x1,y1][x2,y2]" → "[x1,y1" + "x2,y2]"
        if (parts.Length != 2
            || parts[0].Length < 2 || parts[0][0] != '[' || parts[0][^1] == ']'
            || parts[1].Length < 2 || parts[1][0] == '[' || parts[1][^1] != ']')
            return null;
        var first = parts[0][1..].Split(',');
        var second = parts[1][..^1].Split(',');
        if (first.Length != 2 || second.Length != 2
            || !long.TryParse(first[0], out var x1) || !long.TryParse(first[1], out var y1)
            || !long.TryParse(second[0], out var x2) || !long.TryParse(second[1], out var y2))
            return null;
        return new Rect(x1, y1, x2, y2);
    }
}
