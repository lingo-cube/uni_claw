using System.Collections.Immutable;
using System.Linq;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// InMemoryTraceService tests — ReconstructTree, GetNodeSpans, GetNodeVisitTimeline,
/// GetStepTimeline, GetStepSpanGroup, GetBySpanType — all using Context access pattern.
/// </summary>
public class InMemoryTraceServiceTests
{
    private InMemoryTraceStorage CreateStorageWithTestData()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("abc", DateTimeOffset.UtcNow));

        // DfsForward edges for tree reconstruction
        storage.AddExecution(new ExecutionRecord("dfs_forward", "ok",
            SpanType: SpanType.DfsForward,
            Context: new TraceContext(NodeId: "root", StepSpanId: "abc-000001", StepNumber: 1, TraceId: "abc"),
            SpanId: "abc-000001",
            ChildNodeId: "wifi_node",
            Depth: 1));
        storage.AddExecution(new ExecutionRecord("dfs_forward", "ok",
            SpanType: SpanType.DfsForward,
            Context: new TraceContext(NodeId: "wifi_node", StepSpanId: "abc-000003", StepNumber: 3, TraceId: "abc"),
            SpanId: "abc-000003",
            ChildNodeId: "toggle_node",
            Depth: 2));

        // DfsBacktrack
        storage.AddExecution(new ExecutionRecord("dfs_backtrack", "ok",
            SpanType: SpanType.DfsBacktrack,
            Context: new TraceContext(NodeId: "wifi_node", StepSpanId: "abc-000005", StepNumber: 5, TraceId: "abc"),
            SpanId: "abc-000005",
            Depth: 2));

        // PageAnalysis
        storage.AddExecution(new ExecutionRecord("page_analysis", "ok",
            SpanType: SpanType.PageAnalysis,
            Context: new TraceContext(NodeId: "wifi_node", StepSpanId: "abc-000002", StepNumber: 2, TraceId: "abc")));

        // State transition
        storage.AddTransition(new StateTransition("NodeSelect", "Execute",
            Context: new TraceContext(NodeId: "wifi_node", StepSpanId: "abc-000002", StepNumber: 2, TraceId: "abc"),
            FsmType: "TraversalFSM"));

        // Error at wifi_node
        storage.AddError(new ErrorRecord("popup", "unexpected popup", ErrorSeverity.Warning,
            Context: new TraceContext(NodeId: "wifi_node", StepSpanId: "abc-000003", StepNumber: 3, TraceId: "abc")));

        // Page transition
        storage.AddPageTransition(new PageTransition("home", "wifi", "forward",
            Context: new TraceContext(NodeId: "wifi_node", StepSpanId: "abc-000001", StepNumber: 1, TraceId: "abc")));

        // AI call
        storage.AddAICall(new AICallRecord("vision", "provider", true, 230.5,
            Context: new TraceContext(NodeId: "wifi_node", StepSpanId: "abc-000002", StepNumber: 2, TraceId: "abc"),
            Tokens: 1500));

        return storage;
    }

    // ── ReconstructTree ───────────────────────────────────

    [Fact(DisplayName = "Service: ReconstructTree from DfsForward edges via Context.NodeId")]
    public void ReconstructTree_FromDfsForwardEdges()
    {
        var storage = CreateStorageWithTestData();
        var service = new InMemoryTraceService(storage);
        var tree = service.ReconstructTree();

        Assert.Equal(2, tree.Edges.Length);
        Assert.Equal("root", tree.Edges[0].Parent);
        Assert.Equal("wifi_node", tree.Edges[0].Child);
        Assert.Equal("wifi_node", tree.Edges[1].Parent);
        Assert.Equal("toggle_node", tree.Edges[1].Child);
    }

    // ── GetNodeSpans ──────────────────────────────────────

    [Fact(DisplayName = "Service: GetNodeSpans aggregates all 5 record types by Context?.NodeId")]
    public void GetNodeSpans_AggregatesAll5Types()
    {
        var storage = CreateStorageWithTestData();
        var service = new InMemoryTraceService(storage);
        var spans = service.GetNodeSpans("wifi_node");

        Assert.Equal("wifi_node", spans.NodeId);
        Assert.True(spans.Executions.Length >= 2); // DfsForward + DfsBacktrack + PageAnalysis
        Assert.Single(spans.Errors);
        Assert.Single(spans.PageTransitions);
        Assert.Single(spans.Transitions);
        Assert.Single(spans.AICalls);
    }

    // ── GetStepTimeline ───────────────────────────────────

    [Fact(DisplayName = "Service: GetStepTimeline aggregates by Context?.StepNumber")]
    public void GetStepTimeline_AggregatesByStepNumber()
    {
        var storage = CreateStorageWithTestData();
        var service = new InMemoryTraceService(storage);
        var timeline = service.GetStepTimeline(2);

        Assert.Equal(2, timeline.StepNumber);
        // Step 2 has: PageAnalysis, StateTransition, AICall
        Assert.True(timeline.Executions.Length >= 1);
        Assert.Single(timeline.Transitions);
        Assert.Single(timeline.AICalls);
    }

    // ── GetStepSpanGroup ──────────────────────────────────

    [Fact(DisplayName = "Service: GetStepSpanGroup aggregates by Context?.StepSpanId")]
    public void GetStepSpanGroup_AggregatesByStepSpanId()
    {
        var storage = CreateStorageWithTestData();
        var service = new InMemoryTraceService(storage);
        var group = service.GetStepSpanGroup("abc-000002");

        Assert.Equal("abc-000002", group.StepSpanId);
        Assert.True(group.Executions.Length >= 1); // PageAnalysis
        Assert.Single(group.Transitions);
        Assert.Single(group.AICalls);
    }

    // ── GetNodeVisitTimeline ──────────────────────────────

    [Fact(DisplayName = "Service: GetNodeVisitTimeline from DfsForward/DfsBacktrack")]
    public void GetNodeVisitTimeline_EntryAndExit()
    {
        var storage = CreateStorageWithTestData();
        var service = new InMemoryTraceService(storage);
        var timeline = service.GetNodeVisitTimeline("wifi_node");

        Assert.Equal("wifi_node", timeline.NodeId);
        Assert.Equal(3, timeline.EntryStep); // DfsForward at wifi_node at step 3
        Assert.Equal(5, timeline.ExitStep);  // DfsBacktrack at step 5
    }

    // ── GetBySpanType ─────────────────────────────────────

    [Fact(DisplayName = "Service: GetBySpanType returns DfsForward records")]
    public void GetBySpanType_ReturnsDfsForward()
    {
        var storage = CreateStorageWithTestData();
        var service = new InMemoryTraceService(storage);
        var dfsForward = service.GetBySpanType(SpanType.DfsForward);

        Assert.Equal(2, dfsForward.Count);
        Assert.All(dfsForward, r => Assert.Equal(SpanType.DfsForward, r.SpanType));
    }

    // ── Flat read methods ─────────────────────────────────

    [Fact(DisplayName = "Service: flat read methods delegate to storage")]
    public void FlatRead_DelegatesToStorage()
    {
        var storage = CreateStorageWithTestData();
        var service = new InMemoryTraceService(storage);

        Assert.True(service.GetExecutions().Count >= 4);
        Assert.Single(service.GetTransitions());
        Assert.Single(service.GetErrors());
        Assert.Single(service.GetPageTransitions());
        Assert.Single(service.GetAICalls());
    }

    // ── CurrentSession ────────────────────────────────────

    [Fact(DisplayName = "Service: CurrentSession from storage")]
    public void CurrentSession_FromStorage()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("abc", DateTimeOffset.UtcNow));
        var service = new InMemoryTraceService(storage);

        Assert.Equal("abc", service.CurrentSession?.TraceId);
    }

    // ── ExportTrace ────────────────────────────────────────

    [Fact(DisplayName = "Service: ExportTrace delegates to storage")]
    public void ExportTrace_DelegatesToStorage()
    {
        var storage = CreateStorageWithTestData();
        var service = new InMemoryTraceService(storage);

        var json = service.ExportTrace();
        Assert.NotEmpty(json);
    }
}
