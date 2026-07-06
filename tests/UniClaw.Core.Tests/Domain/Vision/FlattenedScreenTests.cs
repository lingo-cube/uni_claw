using System.Collections.Immutable;
using Xunit;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain.Vision;

/// <summary>
/// FlattenedScreen 单元测试 — PRD §5.1: elements 为 ImmutableArray, 构造即按 (y,x) 排序, with 副本独立
/// </summary>
public class FlattenedScreenTests
{
    private static FlattenedElement Element(int id, double x, double y, string text = "t") =>
        new(Id: id, Text: text, TypeHint: TypeHint.Text,
            BoundingBox: new BoundingBox(X: x, Y: y, Width: 0.1, Height: 0.1));

    [Fact(DisplayName = "FlattenedScreen.Elements: 类型为ImmutableArray而非可变List")]
    public void Elements_ShouldBeImmutableArray()
    {
        var screen = new FlattenedScreen(ImmutableArray<FlattenedElement>.Empty);
        Assert.IsAssignableFrom<ImmutableArray<FlattenedElement>>(screen.Elements);
        // structural check: it is not backed by a mutable List
        Assert.False(screen.Elements.GetType().GetGenericTypeDefinition() == typeof(List<>));
    }

    [Fact(DisplayName = "FlattenedScreen构造: 元素按(y,x)升序自动排列")]
    public void Construction_ShouldSortElementsByYThenX()
    {
        // given out of (y,x) order: (y=0.5,x=0.1), (y=0.1,x=0.9), (y=0.1,x=0.2)
        var unsorted = ImmutableArray.Create(
            Element(1, x: 0.1, y: 0.5),
            Element(2, x: 0.9, y: 0.1),
            Element(3, x: 0.2, y: 0.1));

        var screen = new FlattenedScreen(unsorted);

        // expected: (0.1,0.2)=id3, (0.9,0.1)=id2, (0.1,0.5)=id1
        Assert.Equal(3, screen.Elements[0].Id);
        Assert.Equal(2, screen.Elements[1].Id);
        Assert.Equal(1, screen.Elements[2].Id);
    }

    [Fact(DisplayName = "FlattenedScreen构造: 空元素集合 → 合法接受")]
    public void Construction_ShouldAcceptEmptyElements()
    {
        var screen = new FlattenedScreen(ImmutableArray<FlattenedElement>.Empty);
        Assert.Empty(screen.Elements);
    }

    [Fact(DisplayName = "FlattenedScreen with副本: 替换Elements后原实例不变")]
    public void With_ShouldProduceIndependentCollection()
    {
        var original = new FlattenedScreen(ImmutableArray.Create(Element(1, 0.1, 0.1)));
        var replacement = ImmutableArray.Create(Element(2, 0.2, 0.2), Element(3, 0.3, 0.3));

        var copy = original with { Elements = replacement };

        Assert.Single(original.Elements);
        Assert.Equal(1, original.Elements[0].Id);
        Assert.Equal(2, copy.Elements[0].Id);
        Assert.Equal(3, copy.Elements[1].Id);
    }

    [Fact(DisplayName = "FlattenedScreen构造: default ImmutableArray → 抛DomainValidationException")]
    public void Construction_ShouldThrow_WhenElementsDefault()
    {
        // ImmutableArray<T> default (Uninitialized) must be rejected to avoid silent empty
        Assert.Throws<DomainValidationException>(() => new FlattenedScreen(default(ImmutableArray<FlattenedElement>)));
    }
}
