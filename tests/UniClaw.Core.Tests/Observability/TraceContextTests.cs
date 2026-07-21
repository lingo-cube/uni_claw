using System.Text.Json;
using UniClaw.Core.Domain;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// Phase 3-A: TraceContext 6-field serialization, backward compat, and null-omit tests.
/// </summary>
public class TraceContextTests
{
    [Fact(DisplayName = "Phase 3-A: 6-field TraceContext construction")]
    public void TraceContext_All6Fields_Accessible()
    {
        var ctx = new TraceContext(
            NodeId: "wifi_node",
            StepSpanId: "abc-000005",
            StepNumber: 5,
            TraceId: "abc",
            VisitSpanId: "abc-000003",
            ParentSpanId: "abc-000010");

        Assert.Equal("wifi_node", ctx.NodeId);
        Assert.Equal("abc-000005", ctx.StepSpanId);
        Assert.Equal(5, ctx.StepNumber);
        Assert.Equal("abc", ctx.TraceId);
        Assert.Equal("abc-000003", ctx.VisitSpanId);
        Assert.Equal("abc-000010", ctx.ParentSpanId);
    }

    [Fact(DisplayName = "Phase 3-A: 6-field TraceContext serialization round-trip")]
    public void TraceContext_SerializationRoundTrip()
    {
        var ctx = new TraceContext(
            NodeId: "wifi_node",
            StepSpanId: "abc-000005",
            StepNumber: 5,
            TraceId: "abc",
            VisitSpanId: "abc-000003",
            ParentSpanId: "abc-000010");

        var json = JsonSerializer.Serialize(ctx, DomainJsonOptions.Default);
        Assert.Contains("visitSpanId", json);
        Assert.Contains("parentSpanId", json);

        var deserialized = JsonSerializer.Deserialize<TraceContext>(json, DomainJsonOptions.Default);
        Assert.NotNull(deserialized);
        Assert.Equal(ctx.NodeId, deserialized.NodeId);
        Assert.Equal(ctx.StepSpanId, deserialized.StepSpanId);
        Assert.Equal(ctx.StepNumber, deserialized.StepNumber);
        Assert.Equal(ctx.TraceId, deserialized.TraceId);
        Assert.Equal(ctx.VisitSpanId, deserialized.VisitSpanId);
        Assert.Equal(ctx.ParentSpanId, deserialized.ParentSpanId);
    }

    [Fact(DisplayName = "Phase 3-A: null VisitSpanId/ParentSpanId omitted from JSON")]
    public void TraceContext_NullFields_OmittedFromJson()
    {
        var ctx = new TraceContext(
            NodeId: "wifi_node",
            StepSpanId: "abc-000005",
            StepNumber: 5,
            TraceId: "abc");

        var json = JsonSerializer.Serialize(ctx, DomainJsonOptions.Default);
        Assert.DoesNotContain("visitSpanId", json);
        Assert.DoesNotContain("parentSpanId", json);
    }

    [Fact(DisplayName = "Phase 3-A: old 4-field JSONL deserializes with VisitSpanId/ParentSpanId null")]
    public void TraceContext_Old4FieldJson_DeserializesWithNulls()
    {
        var oldJson = """{"nodeId":"wifi_node","stepSpanId":"abc-000005","stepNumber":5,"traceId":"abc"}""";
        var ctx = JsonSerializer.Deserialize<TraceContext>(oldJson, DomainJsonOptions.Default);

        Assert.NotNull(ctx);
        Assert.Equal("wifi_node", ctx.NodeId);
        Assert.Equal("abc-000005", ctx.StepSpanId);
        Assert.Equal(5, ctx.StepNumber);
        Assert.Equal("abc", ctx.TraceId);
        Assert.Null(ctx.VisitSpanId);
        Assert.Null(ctx.ParentSpanId);
    }

    [Fact(DisplayName = "Phase 3-A: 6-field JSONL round-trip with DomainJsonOptions")]
    public void TraceContext_SixFieldRoundTrip_WithDomainJsonOptions()
    {
        var ctx = new TraceContext(
            NodeId: "node_1",
            StepSpanId: "trace-000001",
            StepNumber: 1,
            TraceId: "trace",
            VisitSpanId: "trace-000002",
            ParentSpanId: "trace-000001");

        var json = JsonSerializer.Serialize(ctx, DomainJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<TraceContext>(json, DomainJsonOptions.Default);

        Assert.NotNull(deserialized);
        Assert.Equal("node_1", deserialized.NodeId);
        Assert.Equal("trace-000002", deserialized.VisitSpanId);
        Assert.Equal("trace-000001", deserialized.ParentSpanId);
    }
}
