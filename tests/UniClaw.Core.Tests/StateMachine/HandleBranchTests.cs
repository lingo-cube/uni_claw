using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// HandleBranch handler tests (6 scenarios).
/// Exercises the real HandleBranch logic via Step().
/// </summary>
public class HandleBranchTests
{
    /// <summary>
    /// Creates a TraversalNode with the given ChildrenStrategy.
    /// </summary>
    private static TraversalNode CreateNode(string id, ChildrenStrategy strategy,
        NodeType nodeType = NodeType.Container)
        => new(id, id, nodeType,
            new Operation(OperationType.NoAction),
            strategy);

    /// <summary>
    /// Helper: drives FSM to Branch state.
    /// NodeSelect → PreconditionCheck → Execute → ResultVerify → Branch
    /// </summary>
    private static TraversalFSM DriveToBranch(TraversalRuntimeContext ctx, TraversalNode? node = null)
    {
        var fsm = new TraversalFSM(ctx);
        if (node != null)
            ctx.NodeStack.Push(node);

        // Go through the valid transition chain to reach Branch
        fsm.TransitionTo(TraversalState.Branch);          // NodeSelect → Branch (stack may be empty)
        return fsm;
    }

    /// <summary>
    /// Helper: drives FSM to Branch state with a node on the stack (with ChildrenStrategy.Static + children).
    /// </summary>
    private static (TraversalFSM, TraversalRuntimeContext) SetupWithStaticChildren(
        List<string> staticChildren, HashSet<string>? visited = null)
    {
        var ctx = new TraversalRuntimeContext("test");
        var strategy = new ChildrenStrategy(ChildrenStrategyType.Static, StaticChildren: staticChildren);
        var node = CreateNode("parent", strategy);
        ctx.NodeStack.Push(node);
        ctx.SetCurrentFrame(node);

        if (visited != null)
        {
            foreach (var childId in visited)
                ctx.AddVisitedChild("parent", childId);
        }

        // Drive to Branch: NodeSelect → Branch
        var fsm = new TraversalFSM(ctx);
        fsm.TransitionTo(TraversalState.Branch);
        return (fsm, ctx);
    }

    [Fact(DisplayName = "分支处理: Static策略有未访问子节点 → NodeSelect")]
    public async Task Branch_StaticUnvisited()
    {
        var (fsm, _) = SetupWithStaticChildren(
            new List<string> { "child1", "child2" },
            new HashSet<string> { "child1" }); // child2 is unvisited

        // Step triggers HandleBranch → should find unvisited child2 → NodeSelect
        var result = await fsm.StepAsync();

        Assert.Equal(TraversalState.NodeSelect, result);
    }

    [Fact(DisplayName = "分支处理: Static策略全部子节点已访问 → FrameComplete")]
    public async Task Branch_StaticAllVisited()
    {
        var (fsm, _) = SetupWithStaticChildren(
            new List<string> { "child1", "child2" },
            new HashSet<string> { "child1", "child2" }); // all visited

        var result = await fsm.StepAsync();

        Assert.Equal(TraversalState.FrameComplete, result);
    }

    [Fact(DisplayName = "分支处理: DynamicMatch策略 → 乐观NodeSelect")]
    public async Task Branch_DynamicMatch()
    {
        var ctx = new TraversalRuntimeContext("test");
        var strategy = new ChildrenStrategy(ChildrenStrategyType.DynamicMatch);
        var node = CreateNode("parent", strategy);
        ctx.NodeStack.Push(node);
        ctx.SetCurrentFrame(node);

        var fsm = new TraversalFSM(ctx);
        fsm.TransitionTo(TraversalState.Branch);

        var result = await fsm.StepAsync();

        // DYNAMIC_MATCH → optimistic NodeSelect
        Assert.Equal(TraversalState.NodeSelect, result);
    }

    [Fact(DisplayName = "分支处理: 叶节点深度>1 → FrameComplete弹回父节点")]
    public async Task Branch_LeafNode_DepthMoreThan1()
    {
        var ctx = new TraversalRuntimeContext("test");
        // Push parent container first, then the leaf → depth = 2
        var parentStrategy = new ChildrenStrategy(ChildrenStrategyType.Static,
            StaticChildren: new List<string> { "leaf" });
        var parent = CreateNode("parent", parentStrategy);
        ctx.NodeStack.Push(parent);

        var leafStrategy = new ChildrenStrategy(ChildrenStrategyType.None);
        var leaf = CreateNode("leaf", leafStrategy, NodeType.LeafAction);
        ctx.NodeStack.Push(leaf);
        ctx.SetCurrentFrame(leaf);

        var fsm = new TraversalFSM(ctx);
        fsm.TransitionTo(TraversalState.Branch);

        var result = await fsm.StepAsync();

        // Leaf at depth > 1 → FrameComplete (pop back to parent)
        Assert.Equal(TraversalState.FrameComplete, result);
    }

    [Fact(DisplayName = "分支处理: 叶节点深度=1 → NodeSelect")]
    public async Task Branch_LeafNode_Depth1()
    {
        var ctx = new TraversalRuntimeContext("test");
        // Only one node on stack → depth = 1
        var leafStrategy = new ChildrenStrategy(ChildrenStrategyType.None);
        var leaf = CreateNode("leaf", leafStrategy, NodeType.LeafAction);
        ctx.NodeStack.Push(leaf);
        ctx.SetCurrentFrame(leaf);

        var fsm = new TraversalFSM(ctx);
        fsm.TransitionTo(TraversalState.Branch);

        var result = await fsm.StepAsync();

        // Leaf at depth 1 (root) → NodeSelect
        Assert.Equal(TraversalState.NodeSelect, result);
    }

    [Fact(DisplayName = "分支处理: 无已访问子节点记录 → 全部视为未访问→NodeSelect")]
    public async Task Branch_EmptyVisitedChildren()
    {
        // NodeId not in VisitedChildren dict → treat as all unvisited
        var (fsm, _) = SetupWithStaticChildren(
            new List<string> { "child1", "child2" });
        // No visited children added → empty VisitedChildren dict

        var result = await fsm.StepAsync();

        // All children unvisited → NodeSelect
        Assert.Equal(TraversalState.NodeSelect, result);
    }
}
