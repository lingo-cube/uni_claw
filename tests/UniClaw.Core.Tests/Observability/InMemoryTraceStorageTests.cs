using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// InMemoryTraceStorage tests — Add+Get, index correctness (byNodeId via Context?.NodeId,
/// bySpanType), null Context not indexed, session lifecycle.
/// </summary>
public class InMemoryTraceStorageTests
{
    // ── Add + Get ────────────────────────────────────────

    [Fact(DisplayName = "Storage: AddExecution + GetExecutions")]
    public void AddExecution_GetExecutions()
    {
        var storage = new InMemoryTraceStorage();
        var r = new ExecutionRecord("click", "success",
            Context: new TraceContext(NodeId: "wifi_node"));
        storage.AddExecution(r);

        var executions = storage.GetExecutions();
        Assert.Single(executions);
        Assert.Equal("click", executions[0].Action);
        Assert.Equal("wifi_node", executions[0].Context?.NodeId);
    }

    [Fact(DisplayName = "Storage: AddTransition + GetTransitions")]
    public void AddTransition_GetTransitions()
    {
        var storage = new InMemoryTraceStorage();
        var t = new StateTransition("NodeSelect", "Execute",
            Context: new TraceContext(NodeId: "n1"), FsmType: "TraversalFSM");
        storage.AddTransition(t);

        var transitions = storage.GetTransitions();
        Assert.Single(transitions);
        Assert.Equal("NodeSelect", transitions[0].FromState);
    }

    [Fact(DisplayName = "Storage: AddError + GetErrors")]
    public void AddError_GetErrors()
    {
        var storage = new InMemoryTraceStorage();
        var e = new ErrorRecord("type", "msg", ErrorSeverity.Warning,
            Context: new TraceContext(NodeId: "err_node"));
        storage.AddError(e);

        var errors = storage.GetErrors();
        Assert.Single(errors);
        Assert.Equal("type", errors[0].ErrorType);
    }

    [Fact(DisplayName = "Storage: AddPageTransition + GetPageTransitions")]
    public void AddPageTransition_GetPageTransitions()
    {
        var storage = new InMemoryTraceStorage();
        var pt = new PageTransition("home", "wifi", "forward",
            Context: new TraceContext(NodeId: "home_node"));
        storage.AddPageTransition(pt);

        var pts = storage.GetPageTransitions();
        Assert.Single(pts);
        Assert.Equal("home", pts[0].FromPage);
    }

    [Fact(DisplayName = "Storage: AddAICall + GetAICalls")]
    public void AddAICall_GetAICalls()
    {
        var storage = new InMemoryTraceStorage();
        var ai = new AICallRecord("vision", "provider", true, 230.5,
            Context: new TraceContext(NodeId: "n2"));
        storage.AddAICall(ai);

        var calls = storage.GetAICalls();
        Assert.Single(calls);
        Assert.Equal("vision", calls[0].Capability);
    }

    // ── Index correctness ─────────────────────────────────

    [Fact(DisplayName = "Storage: _byNodeId index groups by Context.NodeId")]
    public void ByNodeId_IndexGroupsByContextNodeId()
    {
        var storage = new InMemoryTraceStorage();
        storage.AddExecution(new ExecutionRecord("click", "ok", Context: new TraceContext(NodeId: "wifi_node")));
        storage.AddExecution(new ExecutionRecord("toggle", "ok", Context: new TraceContext(NodeId: "wifi_node")));
        storage.AddExecution(new ExecutionRecord("back", "ok", Context: new TraceContext(NodeId: "home_node")));

        var wifiRecords = storage.GetByNodeId("wifi_node");
        Assert.Equal(2, wifiRecords.Count);
        Assert.All(wifiRecords, r => Assert.Equal("wifi_node", r.Context?.NodeId));

        var homeRecords = storage.GetByNodeId("home_node");
        Assert.Single(homeRecords);
    }

    [Fact(DisplayName = "Storage: _bySpanType index groups by SpanType")]
    public void BySpanType_IndexGroupsBySpanType()
    {
        var storage = new InMemoryTraceStorage();
        storage.AddExecution(new ExecutionRecord("forward", "ok", SpanType: SpanType.DfsForward));
        storage.AddExecution(new ExecutionRecord("forward2", "ok", SpanType: SpanType.DfsForward));
        storage.AddExecution(new ExecutionRecord("analysis", "ok", SpanType: SpanType.PageAnalysis));

        var dfsForward = storage.GetBySpanType(SpanType.DfsForward);
        Assert.Equal(2, dfsForward.Count);
        Assert.All(dfsForward, r => Assert.Equal(SpanType.DfsForward, r.SpanType));

        var pageAnalysis = storage.GetBySpanType(SpanType.PageAnalysis);
        Assert.Single(pageAnalysis);
    }

    // ── Null Context not indexed ──────────────────────────

    [Fact(DisplayName = "Storage: null Context not indexed by _byNodeId")]
    public void NullContext_NotIndexedByNodeId()
    {
        var storage = new InMemoryTraceStorage();
        storage.AddExecution(new ExecutionRecord("click", "ok")); // Context=null

        var result = storage.GetByNodeId("any");
        Assert.Empty(result);

        // Still accessible via flat list
        Assert.Single(storage.GetExecutions());
    }

    [Fact(DisplayName = "Storage: null Context.NodeId not indexed by _byNodeId")]
    public void NullContextNodeId_NotIndexedByNodeId()
    {
        var storage = new InMemoryTraceStorage();
        storage.AddExecution(new ExecutionRecord("click", "ok", Context: new TraceContext(NodeId: null)));

        var result = storage.GetByNodeId("any");
        Assert.Empty(result);

        // Still accessible via flat list
        Assert.Single(storage.GetExecutions());
    }

    [Fact(DisplayName = "Storage: null SpanType not indexed by _bySpanType")]
    public void NullSpanType_NotIndexedBySpanType()
    {
        var storage = new InMemoryTraceStorage();
        storage.AddExecution(new ExecutionRecord("click", "ok")); // SpanType=null

        var result = storage.GetBySpanType(SpanType.DfsForward);
        Assert.Empty(result);

        Assert.Single(storage.GetExecutions());
    }

    // ── Session lifecycle ─────────────────────────────────

    [Fact(DisplayName = "Storage: session lifecycle (SetSession + EndSession)")]
    public void SessionLifecycle()
    {
        var storage = new InMemoryTraceStorage();

        // Before session: null
        Assert.Null(storage.CurrentSession);

        // Set session
        var session = new TraceSession("abc", DateTimeOffset.UtcNow);
        storage.SetSession(session);
        Assert.Equal("abc", storage.CurrentSession?.TraceId);
        Assert.Null(storage.CurrentSession?.EndTime);

        // End session
        storage.EndSession();
        Assert.NotNull(storage.CurrentSession?.EndTime);
        Assert.True(storage.CurrentSession?.IsCompleted);
    }

    // ── Export ────────────────────────────────────────────

    [Fact(DisplayName = "Storage: Export produces JSON string")]
    public void ExportProducesJson()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("test", DateTimeOffset.UtcNow));
        storage.AddExecution(new ExecutionRecord("click", "ok"));

        var json = storage.Export();
        Assert.NotEmpty(json);
        Assert.Contains("click", json);
    }
}
