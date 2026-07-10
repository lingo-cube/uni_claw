using System.Collections.Immutable;
using UniClaw.Core.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Trace;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Trace;

/// <summary>
/// Tests for TraceNode hierarchy — SessionNode, StepNode, SpanNode.
/// Extracted from Phase2CoreTests.cs.
/// </summary>
public class TraceNodeTests
{
    [Fact]
    public void TraceNode_BaseRecord_Has4Fields()
    {
        // TraceNode is abstract — test via SessionNode
        var now = DateTimeOffset.UtcNow;
        var session = new SessionNode("span-1", "parent-1", now,
            ImmutableDictionary<string, string>.Empty,
            "session-1", "pixel-6", "settings-app", "active");

        Assert.Equal("span-1", session.SpanId);
        Assert.Equal("parent-1", session.ParentSpanId);
        Assert.Equal(now, session.Timestamp);
        // Metadata was provided as Empty, not null
        Assert.NotNull(session.Metadata);
        Assert.Equal("session-1", session.SessionId);
    }

    [Fact]
    public void SessionNode_IsSealedRecordClass_InheritsTraceNode()
    {
        var node = new SessionNode("s1", null, DateTimeOffset.UtcNow);
        Assert.True(node is TraceNode);
        Assert.True(typeof(SessionNode).IsSealed);
    }

    [Fact]
    public void StepNode_Has8Fields_InheritsTraceNode()
    {
        var now = DateTimeOffset.UtcNow;
        var step = new StepNode("span-2", "span-1", now,
            ImmutableDictionary<string, string>.Empty,
            "execute", "node-42", "click", "success");

        Assert.Equal("span-2", step.SpanId);
        Assert.Equal("span-1", step.ParentSpanId);
        Assert.Equal("execute", step.StepType);
        Assert.Equal("node-42", step.NodeId);
        Assert.Equal("click", step.Action);
        Assert.Equal("success", step.Result);
        Assert.True(step is TraceNode);
        Assert.True(typeof(StepNode).IsSealed);
    }

    [Fact]
    public void SpanNode_Has7Fields_InheritsTraceNode()
    {
        var now = DateTimeOffset.UtcNow;
        var span = new SpanNode("span-3", "span-2", now,
            ImmutableDictionary<string, string>.Empty,
            "ai_call", 150.0, "completed");

        Assert.Equal("span-3", span.SpanId);
        Assert.Equal("span-2", span.ParentSpanId);
        Assert.Equal("ai_call", span.SpanType);
        Assert.Equal(150.0, span.DurationMs);
        Assert.Equal("completed", span.Status);
        Assert.True(span is TraceNode);
        Assert.True(typeof(SpanNode).IsSealed);
    }

    [Fact]
    public void TraceNodeHierarchy_ExactlyThreeSubtypes()
    {
        var subtypes = typeof(TraceNode).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(TraceNode)) && !t.IsAbstract)
            .ToList();

        Assert.Equal(3, subtypes.Count);
        Assert.Contains(typeof(SessionNode), subtypes);
        Assert.Contains(typeof(StepNode), subtypes);
        Assert.Contains(typeof(SpanNode), subtypes);
    }
}

/// <summary>
/// Tests for UlidGenerator — ULID format, Crockford Base32, monotonicity.
/// Extracted from Phase2CoreTests.cs.
/// </summary>
public class UlidGeneratorTests
{
    [Fact]
    public void Ulid_IsExactly26Characters()
    {
        var ulid = UlidGenerator.Generate();
        Assert.Equal(26, ulid.Length);
    }

    [Fact]
    public void Ulid_UsesOnlyCrockfordBase32Characters()
    {
        var validChars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        for (int i = 0; i < 10; i++)
        {
            var ulid = UlidGenerator.Generate();
            foreach (var c in ulid)
            {
                Assert.Contains(char.ToUpperInvariant(c), validChars);
            }
        }
    }

    [Fact]
    public void Ulid_TimestampPortion_IsFirst10Chars()
    {
        var knownTs = 1700000000000L;
        var ulid = UlidGenerator.Generate(timestamp: knownTs);
        Assert.Equal(26, ulid.Length);

        var timestampPart = ulid.Substring(0, 10);
        Assert.True(timestampPart.Length == 10);
        foreach (var c in timestampPart)
        {
            Assert.Contains(char.ToUpperInvariant(c), "0123456789ABCDEFGHJKMNPQRSTVWXYZ");
        }
    }

    [Fact]
    public void Ulids_SameMillisecond_AreDifferent()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ulid1 = UlidGenerator.Generate(timestamp: ts);
        var ulid2 = UlidGenerator.Generate(timestamp: ts);

        Assert.NotEqual(ulid1, ulid2); // Different random portions
    }

    [Fact]
    public void Ulid_IsValid_Works()
    {
        var ulid = UlidGenerator.Generate();
        Assert.True(UlidGenerator.IsValid(ulid));
        Assert.False(UlidGenerator.IsValid("")); // Too short
        Assert.False(UlidGenerator.IsValid(null!)); // Null
    }

    [Fact]
    public void Ulid_DifferentTimestamps_SortCorrectly()
    {
        var ulid1 = UlidGenerator.Generate(timestamp: 1000);
        var ulid2 = UlidGenerator.Generate(timestamp: 2000);

        Assert.True(string.Compare(ulid1, ulid2, StringComparison.Ordinal) < 0);
    }
}

/// <summary>
/// Tests for TraceCoordinator — active state, no-op behavior, trace-level filtering.
/// Extracted from Phase2StepOrchestratorTests.cs.
/// </summary>
public class TraceCoordinatorTests
{
    [Fact]
    public void Active_NullRecorder_False()
        => Assert.False(new TraceCoordinator(null, null).Active);

    [Fact]
    public void Active_EmptyTraceId_False()
        => Assert.False(new TraceCoordinator(null, "").Active);

    [Fact]
    public void AllMethods_NoOpWhenInactive()
    {
        var coord = new TraceCoordinator(null, null);
        // All 16+ methods silently do nothing — no exceptions
        coord.RecordStateTransition("A", "B");
        coord.RecordRootNodePushed("node-1");
        coord.RecordPageAnalysis(null);
        coord.RecordActionExecution("click", "btn", true);
        coord.RecordMetricsAsSpans(null);
        coord.RecordErrorSpan("type", "msg", ErrorSeverity.Warning);
        coord.RecordDecision("skip", new TraversalRuntimeContext("test"));
        coord.RecordPageTransition("/a", "/b", "nav");
        coord.RecordDynamicLifecycle("generate", "n1", "p1", "r1", "");
        coord.RecordStateDecision("continue", "n1", null);
        coord.RecordStepStart("n1", "");
        coord.RecordStepEnd("n1", "ok");
    }

    [Fact]
    public void ShouldRecordEntryAttempt_Basic_True()
        => Assert.True(new TraceCoordinator(null, null).ShouldRecordEntryAttempt(TraceLevel.Basic));

    [Fact]
    public void ShouldRecordEntryAttempt_None_False()
        => Assert.False(new TraceCoordinator(null, null).ShouldRecordEntryAttempt(TraceLevel.None));

    [Fact]
    public void ShouldRecordVisionCall_Detailed_True()
        => Assert.True(new TraceCoordinator(null, null).ShouldRecordVisionCall(TraceLevel.Detailed));

    [Fact]
    public void ShouldRecordVisionCall_Basic_False()
        => Assert.False(new TraceCoordinator(null, null).ShouldRecordVisionCall(TraceLevel.Basic));
}

// ===== NodeType Tests =====

public class NodeTypeTests
{
    [Fact]
    public void NodeType_HasExactly8Values()
        => Assert.Equal(8, Enum.GetValues<NodeType>().Length);

    [Fact]
    public void NodeType_ValuesMatchExpected()
        => Assert.Equal(new[] {
            NodeType.Container, NodeType.LeafSwitch, NodeType.LeafSlider,
            NodeType.LeafAction, NodeType.LeafInfo, NodeType.Screen,
            NodeType.Action, NodeType.Target
        }, Enum.GetValues<NodeType>());

    [Fact]
    public void NodeTypeExtensions_FromValue_ResolvesAll()
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
    public void NodeTypeExtensions_IsValid_Works()
    {
        Assert.True(NodeTypeExtensions.IsValid("container"));
        Assert.True(NodeTypeExtensions.IsValid("leaf_switch"));
        Assert.False(NodeTypeExtensions.IsValid("unknown_type"));
    }

    [Fact]
    public void NodeType_InDomainModelsContent()
        => Assert.Equal("UniClaw.Core.Domain.Models.Content", typeof(NodeType).Namespace);
}

/// <summary>
/// In-memory ITraceRecorder implementation for testing.
/// Implements all ITraceRecorder methods including the 2 new PageTransition methods.
/// </summary>
public sealed class InMemoryTraceRecorder : ITraceRecorder
{
    private TraceSession? _session;
    private readonly List<StateTransition> _transitions = new();
    private readonly List<AICallRecord> _aiCalls = new();
    private readonly List<ExecutionRecord> _executions = new();
    private readonly List<ErrorRecord> _errors = new();
    private readonly List<PageTransition> _pageTransitions = new();

    public TraceSession? CurrentSession => _session;

    public Task<TraceSession> StartSessionAsync(string traceId, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
    {
        _session = new TraceSession(traceId, DateTimeOffset.UtcNow, null, metadata);
        return Task.FromResult(_session);
    }

    public Task EndSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_session != null)
            _session = _session with { EndTime = DateTimeOffset.UtcNow };
        return Task.CompletedTask;
    }

    public Task RecordTransitionAsync(StateTransition transition, CancellationToken cancellationToken = default)
    {
        _transitions.Add(transition);
        return Task.CompletedTask;
    }

    public Task RecordAICallAsync(AICallRecord record, CancellationToken cancellationToken = default)
    {
        _aiCalls.Add(record);
        return Task.CompletedTask;
    }

    public Task RecordExecutionAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        _executions.Add(record);
        return Task.CompletedTask;
    }

    public Task RecordErrorAsync(ErrorRecord record, CancellationToken cancellationToken = default)
    {
        _errors.Add(record);
        return Task.CompletedTask;
    }

    public Task RecordPageTransitionAsync(PageTransition transition, CancellationToken cancellationToken = default)
    {
        _pageTransitions.Add(transition);
        return Task.CompletedTask;
    }

    public Task<List<PageTransition>> GetPageTransitionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_pageTransitions.ToList());

    public Task<List<StateTransition>> GetTransitionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_transitions.ToList());

    public Task<List<AICallRecord>> GetAICallsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_aiCalls.ToList());

    public Task<List<ExecutionRecord>> GetExecutionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_executions.ToList());

    public Task<List<ErrorRecord>> GetErrorsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_errors.ToList());

    public Task<string> ExportTraceAsync(string format = "json", CancellationToken cancellationToken = default)
        => Task.FromResult("{}");
}

/// <summary>
/// Tests for SpanType, PageTransition, and InMemoryTraceRecorder (Phase 2.2 trace minimal).
/// </summary>
public class TraceMinimalTests
{
    [Fact(DisplayName = "SpanType: 11 个值覆盖 operation_rules + trace_integrity")]
    public void SpanType_HasExpectedValues()
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

    [Fact(DisplayName = "PageTransition: record structure 7 字段正确")]
    public void PageTransition_RecordStructure()
    {
        var pt = new PageTransition("home", "wifi", "forward",
            NodeId: "node-1", DurationMs: 150.0, Timestamp: DateTimeOffset.UtcNow);

        Assert.Equal("home", pt.FromPage);
        Assert.Equal("wifi", pt.ToPage);
        Assert.Equal("forward", pt.TransitionType);
        Assert.Equal("node-1", pt.NodeId);
        Assert.Equal(150.0, pt.DurationMs);
        Assert.Null(pt.Metadata);
    }

    [Fact(DisplayName = "ExecutionRecord: SpanType 字段 backward compatible")]
    public void ExecutionRecord_SpanType_BackwardCompatible()
    {
        // Without SpanType (default null) — backward compatible
        var r1 = new ExecutionRecord("click", "success");
        Assert.Null(r1.SpanType);
        Assert.Null(r1.Target);

        // With SpanType — new functionality
        var r2 = new ExecutionRecord("toggle", "success", SpanType: SpanType.RestoreOp);
        Assert.Equal(SpanType.RestoreOp, r2.SpanType);
    }

    [Fact(DisplayName = "InMemoryTraceRecorder: RecordPageTransitionAsync + GetPageTransitionsAsync")]
    public async Task InMemoryTraceRecorder_PageTransitionMethods()
    {
        var recorder = new InMemoryTraceRecorder();
        var pt = new PageTransition("home", "wifi", "forward");
        await recorder.RecordPageTransitionAsync(pt);

        var transitions = await recorder.GetPageTransitionsAsync();
        Assert.Single(transitions);
        Assert.Equal("home", transitions[0].FromPage);
        Assert.Equal("wifi", transitions[0].ToPage);
    }

    [Fact(DisplayName = "InMemoryTraceRecorder: RecordExecutionAsync with SpanType")]
    public async Task InMemoryTraceRecorder_ExecutionRecordWithSpanType()
    {
        var recorder = new InMemoryTraceRecorder();
        var r = new ExecutionRecord("restore", "success", SpanType: SpanType.RestoreOp);
        await recorder.RecordExecutionAsync(r);

        var executions = await recorder.GetExecutionsAsync();
        Assert.Single(executions);
        Assert.Equal(SpanType.RestoreOp, executions[0].SpanType);
    }
}
