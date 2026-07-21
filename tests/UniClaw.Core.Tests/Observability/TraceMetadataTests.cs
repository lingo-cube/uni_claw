using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// C-9/C-10: TraceMetadata + TraceHandlerAttribute 基本单元测试。
/// </summary>
public class TraceMetadataTests
{
    // ── TraceMetadata.Build ─────────────────────────────────

    [Fact(DisplayName = "C-9: TraceMetadata.Build 链式 API 生成字典")]
    public void Build_Chain_ProducesDictionary()
    {
        var dict = TraceMetadata.Build()
            .Add("key1", "value1")
            .Add("key2", 42)
            .Add("key3", (double?)3.14)
            .Add("key4", (bool?)true)
            .ToDict();

        Assert.Equal(4, dict.Count);
        Assert.Equal("value1", dict["key1"]);
        Assert.Equal(42, dict["key2"]);
        Assert.Equal(3.14, dict["key3"]);
        Assert.Equal(true, dict["key4"]);
    }

    [Fact(DisplayName = "C-9: TraceMetadata.Build null skip")]
    public void Build_SkipsNullValues()
    {
        var dict = TraceMetadata.Build()
            .Add("present", "value")
            .Add("null_string", (string?)null)
            .Add("null_enum", (SpanType?)null)
            .Add("null_int", (int?)null)
            .Add("null_double", (double?)null)
            .Add("null_bool", (bool?)null)
            .ToDict();

        Assert.Single(dict);
        Assert.Equal("value", dict["present"]);
    }

    [Fact(DisplayName = "C-9: TraceMetadata.Build enum→string")]
    public void Build_EnumToString()
    {
        var dict = TraceMetadata.Build()
            .Add("span_type", (SpanType?)SpanType.PopupHandling)
            .Add("severity", (ErrorSeverity?)ErrorSeverity.Warning)
            .ToDict();

        Assert.Equal("PopupHandling", dict["span_type"]);
        Assert.Equal("Warning", dict["severity"]);
    }

    [Fact(DisplayName = "C-9: TraceMetadata.Build empty returns empty dict")]
    public void Build_Empty_ReturnsEmpty()
    {
        var dict = TraceMetadata.Build().ToDict();
        Assert.Empty(dict);
    }

    // ── TraceHandlerAttribute ───────────────────────────────

    [Fact(DisplayName = "C-9: TraceHandlerAttribute stores properties")]
    public void TraceHandlerAttribute_StoresProperties()
    {
        var attr = new TraceHandlerAttribute(SpanType.PopupHandling, "handle_popup");

        Assert.Equal(SpanType.PopupHandling, attr.SpanType);
        Assert.Equal("handle_popup", attr.Action);
    }

    [Fact(DisplayName = "C-9: TraceHandlerAttribute decorates methods only")]
    public void TraceHandlerAttribute_MethodTargetOnly()
    {
        var attrType = typeof(TraceHandlerAttribute);
        var usage = (AttributeUsageAttribute)attrType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];

        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Method));
    }
}
