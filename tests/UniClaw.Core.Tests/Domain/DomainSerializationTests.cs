using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain;

/// <summary>
/// Domain 序列化基线测试（PRD §6/§7.5）：仅对象→JSON（camelCase）；
/// JSON→对象往返为已知限制（R-6），不作为成功断言。
/// </summary>
public class DomainSerializationTests
{
    [Fact(DisplayName = "BoundingBox序列化: JSON键名为camelCase(x/width)")]
    public void BoundingBox_ShouldSerializeWithCamelCaseKeys()
    {
        var bbox = new BoundingBox(X: 0.1, Y: 0.2, Width: 0.3, Height: 0.4);
        var json = JsonSerializer.Serialize(bbox, DomainJsonOptions.Default);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("x", out var x));
        Assert.True(doc.RootElement.TryGetProperty("width", out var w));
        Assert.Equal(0.1, x.GetDouble());
        Assert.Equal(0.3, w.GetDouble());
    }

    [Fact(DisplayName = "FlattenedScreen序列化: Elements数组正常序列化")]
    public void FlattenedScreen_ShouldSerializeImmutableArrayElements()
    {
        var el = new FlattenedElement(Id: 1, Text: "ok", TypeHint: TypeHint.Button,
            BoundingBox: new BoundingBox(X: 0.1, Y: 0.1, Width: 0.1, Height: 0.1));
        var screen = new FlattenedScreen(ImmutableArray.Create(el));

        var json = JsonSerializer.Serialize(screen, DomainJsonOptions.Default);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("elements", out var elements));
        Assert.Equal(1, elements.GetArrayLength());
    }
}
