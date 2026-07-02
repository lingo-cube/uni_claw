using System.Collections.Immutable;

namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 从多模态模型识别的单个UI元素。
/// bbox 可空（PRD §5.1）；confidence ∈ [0,1]；VisualState 为不可变字典。
/// </summary>
public sealed record class FlattenedElement
{
    /// <summary>元素唯一标识</summary>
    public int Id { get; init; }

    /// <summary>可见文本内容</summary>
    public string Text { get; init; }

    /// <summary>视觉类型分类</summary>
    public TypeHint TypeHint { get; init; }

    /// <summary>归一化边界框（可空）</summary>
    public BoundingBox? BoundingBox { get; init; }

    /// <summary>所属区域ID</summary>
    public string? Region { get; init; }

    /// <summary>选择状态</summary>
    public SelectionState SelectionState { get; init; }

    /// <summary>额外视觉状态描述（不可变）</summary>
    public ImmutableDictionary<string, object>? VisualState { get; init; }

    /// <summary>识别置信度 [0,1]</summary>
    public double Confidence { get; init; }

    /// <param name="Id">元素唯一标识</param>
    /// <param name="Text">可见文本内容</param>
    /// <param name="TypeHint">视觉类型分类</param>
    /// <param name="BoundingBox">归一化边界框（可空，默认 null）</param>
    /// <param name="Region">所属区域ID</param>
    /// <param name="SelectionState">选择状态</param>
    /// <param name="VisualState">额外视觉状态（不可变）</param>
    /// <param name="Confidence">识别置信度 [0,1]</param>
    public FlattenedElement(
        int Id,
        string Text,
        TypeHint TypeHint,
        BoundingBox? BoundingBox = null,
        string? Region = null,
        SelectionState SelectionState = SelectionState.Normal,
        ImmutableDictionary<string, object>? VisualState = null,
        double Confidence = 1.0)
    {
        if (Confidence < 0.0 || Confidence > 1.0)
            throw new DomainValidationException(nameof(Confidence), Confidence);

        this.Id = Id;
        this.Text = Text ?? string.Empty;
        this.TypeHint = TypeHint;
        this.BoundingBox = BoundingBox;
        this.Region = Region;
        this.SelectionState = SelectionState;
        this.VisualState = VisualState;
        this.Confidence = Confidence;
    }

    /// <summary>综合类型和状态判断是否可交互</summary>
    public bool IsInteractive => TypeHint.IsInteractive() && SelectionState.IsInteractive();

    /// <summary>获取元素中心点坐标（bbox 存在时）</summary>
    public (double X, double Y)? Center => BoundingBox?.Center();
}
