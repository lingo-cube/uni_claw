using System.Collections.Immutable;
using Xunit;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain.Vision;

/// <summary>
/// FlattenedElement 单元测试 — PRD §5.1: bbox 可空; confidence∈[0,1]; 无 ToDictionary/FromDictionary
/// </summary>
public class FlattenedElementTests
{
    private static BoundingBox ValidBox => new(X: 0.1, Y: 0.2, Width: 0.3, Height: 0.4);

    [Fact(DisplayName = "FlattenedElement构造: 合法confidence → 成功创建")]
    public void Construction_ShouldSucceed_WithValidConfidence()
    {
        var el = new FlattenedElement(Id: 1, Text: "ok", TypeHint: TypeHint.Button,
            BoundingBox: ValidBox, Confidence: 0.87);
        Assert.Equal(0.87, el.Confidence);
    }

    [Fact(DisplayName = "FlattenedElement构造: 省略BoundingBox → 默认null")]
    public void Construction_ShouldDefaultBoundingBoxToNull()
    {
        var el = new FlattenedElement(Id: 1, Text: "ok", TypeHint: TypeHint.Text);
        Assert.Null(el.BoundingBox);
    }

    [Theory(DisplayName = "FlattenedElement构造: confidence越界[0,1] → 抛异常+FieldName=Confidence")]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Construction_ShouldThrow_WhenConfidenceOutOfRange(double confidence)
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            new FlattenedElement(Id: 1, Text: "ok", TypeHint: TypeHint.Button,
                BoundingBox: ValidBox, Confidence: confidence));
        Assert.Equal("Confidence", ex.FieldName);
    }

    [Theory(DisplayName = "FlattenedElement构造: confidence边界值0.0和1.0 → 合法接受")]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void Construction_ShouldAcceptBoundaryConfidence(double confidence)
    {
        var el = new FlattenedElement(Id: 1, Text: "ok", TypeHint: TypeHint.Button,
            BoundingBox: ValidBox, Confidence: confidence);
        Assert.Equal(confidence, el.Confidence);
    }

    [Fact(DisplayName = "FlattenedElement.VisualState: 不可变字典, 键值可读取")]
    public void VisualState_ShouldBeImmutableDictionary()
    {
        var el = new FlattenedElement(Id: 1, Text: "ok", TypeHint: TypeHint.Button,
            BoundingBox: ValidBox,
            VisualState: ImmutableDictionary<string, object>.Empty.Add("k", "v"));
        Assert.NotNull(el.VisualState);
        Assert.IsAssignableFrom<IImmutableDictionary<string, object>>(el.VisualState);
        Assert.Equal("v", el.VisualState!["k"]);
    }
}
