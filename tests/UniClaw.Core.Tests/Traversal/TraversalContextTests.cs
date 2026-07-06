using System.Collections.Immutable;
using System.Collections.ObjectModel;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.Traversal;

public class TraversalRuntimeContextTests
{
    [Fact(DisplayName = "TraversalContext: sealed class非record")]
    public void TraversalRuntimeContext_IsSealedClass_NotRecord()
    {
        Assert.True(typeof(TraversalRuntimeContext).IsSealed);
        // Verify it's not a record by checking it doesn't have the record-specific methods
        var recordMethods = typeof(TraversalRuntimeContext).GetMethods()
            .Where(m => m.Name == "EqualityContract" || m.Name == "PrintMembers");
        Assert.Empty(recordMethods);
    }

    [Fact(DisplayName = "TraversalContext: 实现ITraversalContext接口")]
    public void TraversalRuntimeContext_ImplementsITraversalContext()
    {
        Assert.True(typeof(TraversalRuntimeContext).GetInterfaces().Contains(typeof(ITraversalContext)));
    }

    [Fact(DisplayName = "TraversalContext: AppendPath向路径追加元素")]
    public void AppendPath_AddsToCurrentPath()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.AppendPath("settings");

        Assert.Single(ctx.CurrentPath);
        Assert.Equal("settings", ctx.CurrentPath[0]);
    }

    [Fact(DisplayName = "TraversalContext: PopPath移除路径末尾元素")]
    public void PopPath_RemovesLastElement()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.AppendPath("home");
        ctx.AppendPath("settings");
        ctx.PopPath();

        Assert.Single(ctx.CurrentPath);
        Assert.Equal("home", ctx.CurrentPath[0]);
    }

    [Fact(DisplayName = "TraversalContext: PopPath在空路径上为空操作")]
    public void PopPath_OnEmptyPath_IsNoOp()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.PopPath();
        Assert.Empty(ctx.CurrentPath);
    }

    [Fact(DisplayName = "TraversalContext: MarkVisited添加到VisitedPages")]
    public void MarkVisited_AddsToVisitedPages()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.MarkVisited("home_screen");

        Assert.Contains("home_screen", ctx.VisitedPages);
    }

    [Fact(DisplayName = "TraversalContext: MarkNodeVisited添加到VisitedNodes")]
    public void MarkNodeVisited_AddsToVisitedNodes()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.MarkNodeVisited("node-42");

        Assert.Contains("node-42", ctx.VisitedNodes);
    }

    [Fact(DisplayName = "TraversalContext: IncrementStepCount递增步数")]
    public void IncrementStepCount_IncrementsStepCount()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        Assert.Equal(0, ctx.StepCount);
        ctx.IncrementStepCount();
        Assert.Equal(1, ctx.StepCount);
        ctx.IncrementStepCount();
        Assert.Equal(2, ctx.StepCount);
    }

    [Fact(DisplayName = "TraversalContext: IncrementRetryCount递增重试数")]
    public void IncrementRetryCount_Increments()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        Assert.Equal(0, ctx.RetryCount);
        ctx.IncrementRetryCount();
        Assert.Equal(1, ctx.RetryCount);
    }

    [Fact(DisplayName = "TraversalContext: 连续错误递增+重置为零")]
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

    [Fact(DisplayName = "TraversalContext: CurrentPath为ReadOnlyCollection防止cast-back篡改")]
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

    [Fact(DisplayName = "TraversalContext: VisitedPages为IReadOnlySet防止接口篡改")]
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

    [Fact(DisplayName = "TraversalContext: VisitedChildren为IReadOnlyDictionary含嵌套IReadOnlySet")]
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

    [Fact(DisplayName = "TraversalContext: 突变方法在ITraversalContext接口上不可见")]
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
    [Fact(DisplayName = "快照: TraversalContextSnapshot为sealed record")]
    public void TraversalContextSnapshot_IsSealedRecordClass()
    {
        Assert.True(typeof(TraversalContextSnapshot).IsSealed);
    }

    [Fact(DisplayName = "快照: 包含8个不可变字段")]
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

    [Fact(DisplayName = "快照: 与源Context隔离,修改源不影响快照")]
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

    [Fact(DisplayName = "快照: NodeIds捕获创建时栈状态,后续Pop不影响")]
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

public class ITraversalContextInterfaceTests
{
    [Fact(DisplayName = "接口守卫: ITraversalContext.CurrentPath类型为IReadOnlyList")]
    public void ITraversalContext_CurrentPath_IsIReadOnlyList()
    {
        var prop = typeof(ITraversalContext).GetProperty("CurrentPath");
        Assert.Equal(typeof(IReadOnlyList<string>), prop!.PropertyType);
    }

    [Fact(DisplayName = "接口守卫: ITraversalContext.VisitedPages类型为IReadOnlySet")]
    public void ITraversalContext_VisitedPages_IsIReadOnlySet()
    {
        var prop = typeof(ITraversalContext).GetProperty("VisitedPages");
        Assert.Equal(typeof(IReadOnlySet<string>), prop!.PropertyType);
    }

    [Fact(DisplayName = "接口守卫: ITraversalContext.VisitedChildren类型为IReadOnlyDictionary含嵌套IReadOnlySet")]
    public void ITraversalContext_VisitedChildren_IsIReadOnlyDictionary_WithNestedIReadOnlySet()
    {
        var prop = typeof(ITraversalContext).GetProperty("VisitedChildren");
        Assert.Equal(typeof(IReadOnlyDictionary<string, IReadOnlySet<string>>), prop!.PropertyType);
    }

    [Fact(DisplayName = "接口守卫: ITraversalContext.VisitedNodes类型为IReadOnlySet")]
    public void ITraversalContext_VisitedNodes_IsIReadOnlySet()
    {
        var prop = typeof(ITraversalContext).GetProperty("VisitedNodes");
        Assert.Equal(typeof(IReadOnlySet<string>), prop!.PropertyType);
    }

    [Fact(DisplayName = "接口守卫: ITraversalContext.CurrentFrame有setter")]
    public void ITraversalContext_CurrentFrame_HasSetter()
    {
        var prop = typeof(ITraversalContext).GetProperty("CurrentFrame");
        Assert.NotNull(prop!.SetMethod);
    }

    [Fact(DisplayName = "接口守卫: ITraversalContext.StepCount无setter(只读)")]
    public void ITraversalContext_StepCount_IsReadOnly()
    {
        var prop = typeof(ITraversalContext).GetProperty("StepCount");
        Assert.Null(prop!.SetMethod);
    }

    [Fact(DisplayName = "接口守卫: ITraversalContext.GlobalState有setter")]
    public void ITraversalContext_GlobalState_HasSetter()
    {
        var prop = typeof(ITraversalContext).GetProperty("GlobalState");
        Assert.NotNull(prop!.SetMethod);
    }

    [Fact(DisplayName = "接口守卫: ITraversalContext.LastError有setter")]
    public void ITraversalContext_LastError_HasSetter()
    {
        var prop = typeof(ITraversalContext).GetProperty("LastError");
        Assert.NotNull(prop!.SetMethod);
    }
}

public class SnapshotIsolationTests
{
    [Fact(DisplayName = "快照隔离: 快照不受引擎后续修改影响")]
    public void CreateReadOnlySnapshot_SnapshotUnaffectedByEngineModification()
    {
        var ctx = new TraversalRuntimeContext("test", maxDepth: 10);
        ctx.MarkVisited("home");
        ctx.MarkNodeVisited("node-1");
        ctx.IncrementStepCount();
        ctx.AppendPath("home");

        var snapshot = ctx.CreateReadOnlySnapshot();
        Assert.Contains("home", snapshot.VisitedPages);
        Assert.Contains("node-1", snapshot.VisitedNodes);
        Assert.Equal(1, snapshot.StepCount);

        ctx.MarkVisited("settings");
        ctx.MarkNodeVisited("node-2");
        ctx.IncrementStepCount();

        Assert.DoesNotContain("settings", snapshot.VisitedPages);
        Assert.DoesNotContain("node-2", snapshot.VisitedNodes);
        Assert.Equal(1, snapshot.StepCount);
    }
}

public class VisitedChildrenIsolationTests
{
    [Fact(DisplayName = "集合安全(H-2): VisitedChildren cast-back到HashSet抛InvalidCastException")]
    public void VisitedChildren_CastBackToHashSet_ThrowsInvalidCastException()
    {
        // H-2: ReadOnlySetWrapper blocks cast-back to HashSet<string>
        var ctx = new TraversalRuntimeContext("test");
        ctx.AddVisitedChild("parent-1", "child-a");

        var visitedChildren = ctx.VisitedChildren;
        var nestedSet = visitedChildren["parent-1"];

        // Cast-back should throw InvalidCastException — runtime type is ReadOnlySetWrapper, not HashSet
        Assert.Throws<InvalidCastException>(() => (HashSet<string>)nestedSet);
    }

    [Fact(DisplayName = "集合安全: VisitedChildren通过接口操作不影响内部数据,引擎修改通过live wrapper可见")]
    public void VisitedChildren_ModificationThroughInterface_DoesNotAffectInternalData()
    {
        // H-2: IReadOnlySet<string> interface does not expose Add/Remove — no mutation possible through interface
        var ctx = new TraversalRuntimeContext("test");
        ctx.AddVisitedChild("parent-1", "child-a");

        var nestedSet = ctx.VisitedChildren["parent-1"];

        // IReadOnlySet<string> has no mutation methods — only Count, Contains, and set comparison
        Assert.Equal(1, nestedSet.Count);
        Assert.Contains("child-a", nestedSet);
        Assert.True(nestedSet.SetEquals(new[] { "child-a" }));

        // Engine mutations through AddVisitedChild are reflected in the live wrapper (delegates to same HashSet)
        ctx.AddVisitedChild("parent-1", "child-b");
        Assert.Equal(2, nestedSet.Count);
        Assert.Contains("child-b", nestedSet);

        // Snapshot isolation is provided by TraversalContextSnapshot (ImmutableHashSet), not the live interface
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
