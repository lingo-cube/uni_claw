using UniClaw.Core.Graph.Models;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation;

/// <summary>
/// 测试用 INodeRegistry — 字典存储 TraversalNode，按 nodeId 查找。
/// 用于仿真 E2E 测试中 StepOrchestrator 的子节点发现。
/// </summary>
public sealed class SimpleNodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, TraversalNode> _nodes = new();

    public TraversalNode? GetNode(string nodeId)
        => _nodes.TryGetValue(nodeId, out var n) ? n : null;

    public void Register(TraversalNode node)
        => _nodes[node.NodeId] = node;
}
