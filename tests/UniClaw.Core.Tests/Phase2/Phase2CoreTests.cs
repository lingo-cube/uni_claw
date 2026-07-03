using System.Collections.Immutable;
using System.Collections.ObjectModel;
using UniClaw.Core.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Trace;
using Xunit;

namespace UniClaw.Core.Tests.Phase2;

/// <summary>
/// Phase 2.0 tests — TraceNode hierarchy + TraversalRuntimeContext + ITraversalContext + Snapshot + ULID
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

public class NodeTypeTests
{
    [Fact]
    public void NodeType_HasExactly8Values()
    {
        var values = Enum.GetValues<NodeType>();
        Assert.Equal(8, values.Length);
    }

    [Fact]
    public void NodeType_ValuesMatchExpected()
    {
        var expected = new[] {
            NodeType.Container, NodeType.LeafSwitch, NodeType.LeafSlider,
            NodeType.LeafAction, NodeType.LeafInfo, NodeType.Screen,
            NodeType.Action, NodeType.Target
        };
        Assert.Equal(expected, Enum.GetValues<NodeType>());
    }

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
    {
        Assert.Equal("UniClaw.Core.Domain.Models.Content", typeof(NodeType).Namespace);
    }
}

public class TraversalRuntimeContextTests
{
    [Fact]
    public void TraversalRuntimeContext_IsSealedClass_NotRecord()
    {
        Assert.True(typeof(TraversalRuntimeContext).IsSealed);
        // Verify it's not a record by checking it doesn't have the record-specific methods
        var recordMethods = typeof(TraversalRuntimeContext).GetMethods()
            .Where(m => m.Name == "EqualityContract" || m.Name == "PrintMembers");
        Assert.Empty(recordMethods);
    }

    [Fact]
    public void TraversalRuntimeContext_ImplementsITraversalContext()
    {
        Assert.True(typeof(TraversalRuntimeContext).GetInterfaces().Contains(typeof(ITraversalContext)));
    }

    [Fact]
    public void AppendPath_AddsToCurrentPath()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.AppendPath("settings");

        Assert.Single(ctx.CurrentPath);
        Assert.Equal("settings", ctx.CurrentPath[0]);
    }

    [Fact]
    public void PopPath_RemovesLastElement()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.AppendPath("home");
        ctx.AppendPath("settings");
        ctx.PopPath();

        Assert.Single(ctx.CurrentPath);
        Assert.Equal("home", ctx.CurrentPath[0]);
    }

    [Fact]
    public void PopPath_OnEmptyPath_IsNoOp()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.PopPath();
        Assert.Empty(ctx.CurrentPath);
    }

    [Fact]
    public void MarkVisited_AddsToVisitedPages()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.MarkVisited("home_screen");

        Assert.Contains("home_screen", ctx.VisitedPages);
    }

    [Fact]
    public void MarkNodeVisited_AddsToVisitedNodes()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.MarkNodeVisited("node-42");

        Assert.Contains("node-42", ctx.VisitedNodes);
    }

    [Fact]
    public void IncrementStepCount_IncrementsStepCount()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        Assert.Equal(0, ctx.StepCount);
        ctx.IncrementStepCount();
        Assert.Equal(1, ctx.StepCount);
        ctx.IncrementStepCount();
        Assert.Equal(2, ctx.StepCount);
    }

    [Fact]
    public void IncrementRetryCount_Increments()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        Assert.Equal(0, ctx.RetryCount);
        ctx.IncrementRetryCount();
        Assert.Equal(1, ctx.RetryCount);
    }

    [Fact]
    public void ConsecutiveErrors_IncrementAndReset()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        Assert.Equal(0, ctx.ConsecutiveErrors);
        ctx.IncrementConsecutiveErrors();
        ctx.IncrementConsecutiveErrors();
        Assert.Equal(2, ctx.ConsecutiveErrors);
        ctx.ResetConsecutiveErrors();
        Assert.Equal(0, ctx.ConsecutiveErrors);
    }

    [Fact]
    public void CurrentPath_AsReadOnly_PreventsCastBackMutation()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.AppendPath("home");

        var path = ctx.CurrentPath;
        Assert.IsType<ReadOnlyCollection<string>>(path);

        // Cast-back to List<string> should fail
        var castAttempt = path as List<string>;
        Assert.Null(castAttempt);
    }

    [Fact]
    public void VisitedPages_AsReadOnlySet_PreventsMutationViaInterface()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.MarkVisited("home");

        var pages = ctx.VisitedPages;
        Assert.Contains("home", pages);

        // IReadOnlySet<string> doesn't expose mutation methods
        var mutableMethods = typeof(IReadOnlySet<string>).GetMethods()
            .Where(m => m.Name == "Add" || m.Name == "Remove" || m.Name == "Clear");
        Assert.Empty(mutableMethods);
    }

    [Fact]
    public void VisitedChildren_AsReadOnlyDictionary_WithNestedReadOnlySets()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.AddVisitedChild("container-1", "child-a");
        ctx.AddVisitedChild("container-1", "child-b");

        var children = ctx.VisitedChildren;
        Assert.True(children.ContainsKey("container-1"));
        Assert.Contains("child-a", children["container-1"]);
        Assert.Contains("child-b", children["container-1"]);

        // Verify nested set is IReadOnlySet<string>
        var nestedSet = children["container-1"];
        Assert.IsAssignableFrom<IReadOnlySet<string>>(nestedSet);
    }

    [Fact]
    public void MutationMethods_NotAccessibleViaITraversalContext()
    {
        ITraversalContext iface = new TraversalRuntimeContext("test-trace");

        var ifaceMethods = typeof(ITraversalContext).GetMethods();
        Assert.DoesNotContain(ifaceMethods, m => m.Name == "AppendPath");
        Assert.DoesNotContain(ifaceMethods, m => m.Name == "PopPath");
        Assert.DoesNotContain(ifaceMethods, m => m.Name == "MarkVisited");
        Assert.DoesNotContain(ifaceMethods, m => m.Name == "MarkNodeVisited");
        Assert.DoesNotContain(ifaceMethods, m => m.Name == "IncrementStepCount");
        Assert.DoesNotContain(ifaceMethods, m => m.Name == "IncrementRetryCount");
        Assert.DoesNotContain(ifaceMethods, m => m.Name == "IncrementConsecutiveErrors");
        Assert.DoesNotContain(ifaceMethods, m => m.Name == "ResetConsecutiveErrors");
    }
}

public class TraversalContextSnapshotTests
{
    [Fact]
    public void TraversalContextSnapshot_IsSealedRecordClass()
    {
        Assert.True(typeof(TraversalContextSnapshot).IsSealed);
    }

    [Fact]
    public void Snapshot_Has8ImmutableFields()
    {
        var properties = typeof(TraversalContextSnapshot).GetProperties();
        Assert.Equal(8, properties.Length);

        var names = properties.Select(p => p.Name).ToList();
        Assert.Contains("NodeIds", names);
        Assert.Contains("CurrentPath", names);
        Assert.Contains("VisitedPages", names);
        Assert.Contains("VisitedNodes", names);
        Assert.Contains("MaxDepth", names);
        Assert.Contains("StepCount", names);
        Assert.Contains("ActionHistory", names);
        Assert.Contains("FailedNodes", names);
    }

    [Fact]
    public void Snapshot_IsIndependentFromSourceContext()
    {
        var ctx = new TraversalRuntimeContext("test-trace", maxDepth: 10);
        ctx.AppendPath("home");
        ctx.MarkVisited("home_screen");
        ctx.MarkNodeVisited("node-1");
        ctx.IncrementStepCount();

        var snapshot = ctx.CreateReadOnlySnapshot();

        // Verify snapshot reflects current state
        Assert.Contains("home_screen", snapshot.VisitedPages);
        Assert.Contains("node-1", snapshot.VisitedNodes);
        Assert.Equal(1, snapshot.StepCount);

        // Modify the source context
        ctx.MarkVisited("settings_screen");
        ctx.MarkNodeVisited("node-2");
        ctx.IncrementStepCount();

        // Snapshot should NOT reflect the modifications
        Assert.DoesNotContain("settings_screen", snapshot.VisitedPages);
        Assert.DoesNotContain("node-2", snapshot.VisitedNodes);
        Assert.Equal(1, snapshot.StepCount); // Still 1, not 2
    }

    [Fact]
    public void Snapshot_NodeIds_CaptureStackStateAtCreation()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var testNode = new TestTraversalNode("root", "root", NodeType.Container);
        ctx.NodeStack.Push(testNode, new List<string> { "child-1" });

        var snapshot = ctx.CreateReadOnlySnapshot();
        Assert.Contains("root", snapshot.NodeIds);

        // Pop from stack after snapshot
        ctx.NodeStack.Pop();

        // Snapshot should still contain "root"
        Assert.Contains("root", snapshot.NodeIds);
    }
}

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

public class ITraversalContextInterfaceTests
{
    [Fact]
    public void ITraversalContext_CurrentPath_IsIReadOnlyList()
    {
        var prop = typeof(ITraversalContext).GetProperty("CurrentPath");
        Assert.Equal(typeof(IReadOnlyList<string>), prop!.PropertyType);
    }

    [Fact]
    public void ITraversalContext_VisitedPages_IsIReadOnlySet()
    {
        var prop = typeof(ITraversalContext).GetProperty("VisitedPages");
        Assert.Equal(typeof(IReadOnlySet<string>), prop!.PropertyType);
    }

    [Fact]
    public void ITraversalContext_VisitedChildren_IsIReadOnlyDictionary_WithNestedIReadOnlySet()
    {
        var prop = typeof(ITraversalContext).GetProperty("VisitedChildren");
        Assert.Equal(typeof(IReadOnlyDictionary<string, IReadOnlySet<string>>), prop!.PropertyType);
    }

    [Fact]
    public void ITraversalContext_VisitedNodes_IsIReadOnlySet()
    {
        var prop = typeof(ITraversalContext).GetProperty("VisitedNodes");
        Assert.Equal(typeof(IReadOnlySet<string>), prop!.PropertyType);
    }

    [Fact]
    public void ITraversalContext_CurrentFrame_HasSetter()
    {
        var prop = typeof(ITraversalContext).GetProperty("CurrentFrame");
        Assert.NotNull(prop!.SetMethod);
    }

    [Fact]
    public void ITraversalContext_StepCount_IsReadOnly()
    {
        var prop = typeof(ITraversalContext).GetProperty("StepCount");
        Assert.Null(prop!.SetMethod);
    }

    [Fact]
    public void ITraversalContext_GlobalState_HasSetter()
    {
        var prop = typeof(ITraversalContext).GetProperty("GlobalState");
        Assert.NotNull(prop!.SetMethod);
    }

    [Fact]
    public void ITraversalContext_LastError_HasSetter()
    {
        var prop = typeof(ITraversalContext).GetProperty("LastError");
        Assert.NotNull(prop!.SetMethod);
    }
}

// Test helper — minimal ITraversalNode implementation
internal sealed class TestTraversalNode : ITraversalNode
{
    public string NodeId { get; init; }
    public string Name { get; init; }
    public NodeType NodeType { get; init; }
    public List<string> StaticChildren { get; init; }
    public ChildrenStrategy ChildrenStrategy { get; init; }

    public TestTraversalNode(string nodeId, string name, NodeType nodeType, List<string>? staticChildren = null, ChildrenStrategy? childrenStrategy = null)
    {
        NodeId = nodeId;
        Name = name;
        NodeType = nodeType;
        StaticChildren = staticChildren ?? new List<string>();
        ChildrenStrategy = childrenStrategy ?? new ChildrenStrategy(ChildrenStrategyType.None);
    }
}
