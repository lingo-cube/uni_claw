namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 从多模态模型识别的单个UI元素
/// </summary>
/// <param name="Id">元素唯一标识</param>
/// <param name="Text">可见文本内容</param>
/// <param name="TypeHint">视觉类型分类</param>
/// <param name="BoundingBox">归一化边界框</param>
/// <param name="Region">所属区域ID</param>
/// <param name="SelectionState">选择状态</param>
/// <param name="VisualState">额外视觉状态描述</param>
/// <param name="Confidence">识别置信度 (0-1)</param>
public sealed record class FlattenedElement(
    int Id,
    string Text,
    TypeHint TypeHint,
    BoundingBox BoundingBox,
    string? Region = null,
    SelectionState SelectionState = SelectionState.Normal,
    Dictionary<string, object>? VisualState = null,
    double Confidence = 1.0)
{
    /// <summary>
    /// 综合类型和状态判断是否可交互
    /// </summary>
    public bool IsInteractive => TypeHint.IsInteractive() && SelectionState.IsInteractive();

    /// <summary>
    /// 获取元素中心点坐标
    /// </summary>
    public (double X, double Y) Center => BoundingBox.Center();

    /// <summary>
    /// 转换为字典
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            ["id"] = Id,
            ["text"] = Text,
            ["type_hint"] = TypeHint.ToString().ToLowerInvariant(),
            ["bbox"] = new
            {
                x = BoundingBox.X,
                y = BoundingBox.Y,
                w = BoundingBox.Width,
                h = BoundingBox.Height
            },
            ["selection_state"] = SelectionState.ToString().ToLowerInvariant(),
            ["confidence"] = Confidence
        };

        if (Region != null)
            dict["region"] = Region;

        if (VisualState != null && VisualState.Count > 0)
            dict["visual_state"] = VisualState;

        return dict;
    }

    /// <summary>
    /// 从字典创建
    /// </summary>
    public static FlattenedElement? FromDictionary(Dictionary<string, object> data)
    {
        try
        {
            var bboxData = data["bbox"] as dynamic;
            if (bboxData == null) return null;

            var visualState = data.TryGetValue("visual_state", out var vs) && vs is Dictionary<string, object> vsDict
                ? vsDict
                : null;

            return new FlattenedElement(
                Id: Convert.ToInt32(data["id"]),
                Text: data["text"] as string ?? "",
                TypeHint: TypeHintExtensions.FromString(data["type_hint"] as string ?? ""),
                BoundingBox: new BoundingBox(
                    X: Convert.ToDouble(bboxData.x),
                    Y: Convert.ToDouble(bboxData.y),
                    Width: Convert.ToDouble(bboxData.w),
                    Height: Convert.ToDouble(bboxData.h)
                ),
                Region: data.TryGetValue("region", out var r) ? r as string : null,
                SelectionState: SelectionStateExtensions.FromString(data["selection_state"] as string ?? ""),
                VisualState: visualState,
                Confidence: data.TryGetValue("confidence", out var c) ? Convert.ToDouble(c) : 1.0
            );
        }
        catch
        {
            return null;
        }
    }
}
