using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Trace;


/// <summary>
/// Tests for TraceCoordinator — active state, no-op behavior, trace-level filtering.
/// Extracted from Phase2StepOrchestratorTests.cs.
/// </summary>
public class TraceCoordinatorTests
{
    [Fact]
    public async Task Active_NullRecorder_False()
        => Assert.False(new TraceCoordinator(null, null).Active);

    [Fact]
    public async Task Active_EmptyTraceId_False()
        => Assert.False(new TraceCoordinator(null, "").Active);

    [Fact]
    public async Task AllMethods_NoOpWhenInactive()
    {
        var coord = new TraceCoordinator(null, null);
        // All 16+ methods silently do nothing — no exceptions
        await coord.RecordStateTransitionAsync("A", "B");
        await coord.RecordRootNodePushedAsync("node-1");
        await coord.RecordPageAnalysisAsync(null);
        await coord.RecordActionExecutionAsync("click", "btn", true);
        await coord.RecordMetricsAsSpansAsync(null);
        await coord.RecordErrorSpanAsync("type", "msg", ErrorSeverity.Warning);
        await coord.RecordDecisionAsync("skip", new TraversalRuntimeContext("test"));
        await coord.RecordPageTransitionAsync("/a", "/b", "nav");
        await coord.RecordDynamicLifecycleAsync("generate", "n1", "p1", "r1", "");
        await coord.RecordStateDecisionAsync("continue", "n1", null);
        await coord.RecordStepStartAsync("n1", "");
        await coord.RecordStepEndAsync("n1", "ok");
    }

    [Fact]
    public async Task ShouldRecordEntryAttempt_Basic_True()
        => Assert.True(new TraceCoordinator(null, null).ShouldRecordEntryAttempt(TraceLevel.Basic));

    [Fact]
    public async Task ShouldRecordEntryAttempt_None_False()
        => Assert.False(new TraceCoordinator(null, null).ShouldRecordEntryAttempt(TraceLevel.None));

    [Fact]
    public async Task ShouldRecordVisionCall_Detailed_True()
        => Assert.True(new TraceCoordinator(null, null).ShouldRecordVisionCall(TraceLevel.Detailed));

    [Fact]
    public async Task ShouldRecordVisionCall_Basic_False()
        => Assert.False(new TraceCoordinator(null, null).ShouldRecordVisionCall(TraceLevel.Basic));
}

// ===== NodeType Tests =====

public class NodeTypeTests
{
    [Fact]
    public async Task NodeType_HasExactly8Values()
        => Assert.Equal(8, Enum.GetValues<NodeType>().Length);

    [Fact]
    public async Task NodeType_ValuesMatchExpected()
        => Assert.Equal(new[] {
            NodeType.Container, NodeType.LeafSwitch, NodeType.LeafSlider,
            NodeType.LeafAction, NodeType.LeafInfo, NodeType.Screen,
            NodeType.Action, NodeType.Target
        }, Enum.GetValues<NodeType>());

    [Fact]
    public async Task NodeTypeExtensions_FromValue_ResolvesAll()
    {
        Assert.Equal(NodeType.Container, NodeTypeExtensions.FromValue("container"));
        Assert.Equal(NodeType.LeafSwitch, NodeTypeExtensions.FromValue("leaf_switch"));
        Assert.Equal(NodeType.LeafSlider, NodeTypeExtensions.FromValue("leaf_slider"));
        Assert.Equal(NodeType.LeafAction, NodeTypeExtensions.FromValue("leaf_action"));
        Assert.Equal(NodeType.LeafInfo, NodeTypeExtensions.FromValue("leaf_info"));
        Assert.Equal(NodeType.Screen, NodeTypeExtensions.FromValue("screen"));
        Assert.Equal(NodeType.Action, NodeTypeExtensions.FromValue("action"));
        Assert.Equal(NodeType.Target, NodeTypeExtensions.FromValue("target"));
    }

    [Fact]
    public async Task NodeTypeExtensions_IsValid_Works()
    {
        Assert.True(NodeTypeExtensions.IsValid("container"));
        Assert.True(NodeTypeExtensions.IsValid("leaf_switch"));
        Assert.False(NodeTypeExtensions.IsValid("unknown_type"));
    }

    [Fact]
    public async Task NodeType_InDomainModelsContent()
        => Assert.Equal("UniClaw.Core.Domain.Models.Content", typeof(NodeType).Namespace);
}

/// <summary>
/// In-memory ITraceRecorder implementation for testing — delegates to InMemoryTraceStorage.
/// Implements all 7 ITraceRecorder methods (slimmed from 13 → pure write contract).
/// </summary>
public sealed class InMemoryTraceRecorder : ITraceRecorder
{
    private readonly InMemoryTraceStorage _storage = new();

    public Task<TraceSession> StartSessionAsync(string traceId, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
    {
        var session = new TraceSession(traceId, DateTimeOffset.UtcNow, null, metadata);
        _storage.SetSession(session);
        return Task.FromResult(session);
    }

    public Task EndSessionAsync(CancellationToken cancellationToken = default)
    {
        _storage.EndSession();
        return Task.CompletedTask;
    }

    public Task RecordTransitionAsync(StateTransition transition, CancellationToken cancellationToken = default)
    {
        _storage.AddTransition(transition);
        return Task.CompletedTask;
    }

    public Task RecordAICallAsync(AICallRecord record, CancellationToken cancellationToken = default)
    {
        _storage.AddAICall(record);
        return Task.CompletedTask;
    }

    public Task RecordExecutionAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        _storage.AddExecution(record);
        return Task.CompletedTask;
    }

    public Task RecordErrorAsync(ErrorRecord record, CancellationToken cancellationToken = default)
    {
        _storage.AddError(record);
        return Task.CompletedTask;
    }

    public Task RecordPageTransitionAsync(PageTransition transition, CancellationToken cancellationToken = default)
    {
        _storage.AddPageTransition(transition);
        return Task.CompletedTask;
    }

    public Task<string> StartSpanAsync(string spanType, string spanName,
        string? parentSpanId = null, Dictionary<string, object>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var spanId = _storage.NextSpanId(_storage.CurrentSession?.TraceId);
        _storage.OpenSpan(spanType, spanName, spanId, parentSpanId,
            DateTimeOffset.UtcNow, context: null, attributes);
        return Task.FromResult(spanId);
    }

    public Task EndSpanAsync(string spanId, string status = "ok",
        Dictionary<string, object>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        _storage.CloseSpan(spanId, DateTimeOffset.UtcNow, status, attributes);
        return Task.CompletedTask;
    }

    // ── Direct storage access for test assertions ──────────
    public InMemoryTraceStorage Storage => _storage;
}

/// <summary>
/// Tests for SpanType, PageTransition, and InMemoryTraceRecorder (Phase 2.2 trace minimal).
/// </summary>
public class TraceMinimalTests
{
    [Fact(DisplayName = "SpanType: 11 个值覆盖 operation_rules + trace_integrity")]
    public async Task SpanType_HasExpectedValues()
    {
        var values = Enum.GetValues<SpanType>();
        Assert.Equal(11, values.Length);
        Assert.Contains(SpanType.RestoreOp, values);      // operation_rules
        Assert.Contains(SpanType.SkipDangerous, values);   // operation_rules
        Assert.Contains(SpanType.DfsForward, values);      // trace_integrity
        Assert.Contains(SpanType.DfsBacktrack, values);    // trace_integrity
        Assert.Contains(SpanType.PageAnalysis, values);    // trace_integrity
        Assert.Contains(SpanType.StateDecision, values);   // trace_integrity
    }

    [Fact(DisplayName = "PageTransition: record structure with TraceContext")]
    public async Task PageTransition_RecordStructure()
    {
        var pt = new PageTransition("home", "wifi", "forward",
            Context: new TraceContext(NodeId: "node-1"), DurationMs: 150.0, Timestamp: DateTimeOffset.UtcNow);

        Assert.Equal("home", pt.FromPage);
        Assert.Equal("wifi", pt.ToPage);
        Assert.Equal("forward", pt.TransitionType);
        Assert.Equal("node-1", pt.Context?.NodeId);
        Assert.Equal(150.0, pt.DurationMs);
        Assert.Null(pt.Metadata);
    }

    [Fact(DisplayName = "ExecutionRecord: SpanType + Context backward compatible")]
    public async Task ExecutionRecord_SpanType_BackwardCompatible()
    {
        // Without SpanType (default null) — backward compatible
        var r1 = new ExecutionRecord("click", "success");
        Assert.Null(r1.SpanType);
        Assert.Null(r1.Context);
        Assert.Null(r1.TargetType);
        Assert.Null(r1.TargetValue);

        // With SpanType — new functionality
        var r2 = new ExecutionRecord("toggle", "success", SpanType: SpanType.RestoreOp);
        Assert.Equal(SpanType.RestoreOp, r2.SpanType);
    }

    [Fact(DisplayName = "InMemoryTraceRecorder: RecordPageTransitionAsync delegates to storage")]
    public async Task InMemoryTraceRecorder_PageTransitionMethods()
    {
        var recorder = new InMemoryTraceRecorder();
        var pt = new PageTransition("home", "wifi", "forward");
        await recorder.RecordPageTransitionAsync(pt);

        var transitions = recorder.Storage.GetPageTransitions();
        Assert.Single(transitions);
        Assert.Equal("home", transitions[0].FromPage);
        Assert.Equal("wifi", transitions[0].ToPage);
    }

    [Fact(DisplayName = "InMemoryTraceRecorder: RecordExecutionAsync with SpanType delegates to storage")]
    public async Task InMemoryTraceRecorder_ExecutionRecordWithSpanType()
    {
        var recorder = new InMemoryTraceRecorder();
        var r = new ExecutionRecord("restore", "success", SpanType: SpanType.RestoreOp);
        await recorder.RecordExecutionAsync(r);

        var executions = recorder.Storage.GetExecutions();
        Assert.Single(executions);
        Assert.Equal(SpanType.RestoreOp, executions[0].SpanType);
    }
}
