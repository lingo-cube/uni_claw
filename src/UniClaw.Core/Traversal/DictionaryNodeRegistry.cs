using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Traversal;

/// <summary>
/// 字典存储 TraversalNode 的 INodeRegistry 实现。
/// 用于 TraversalEngine.CompilePlan() 和 DynamicChildManager 内部使用。
/// 非 Simulation 专用 — Traversal namespace 通用字典注册表。
/// </summary>
public sealed class DictionaryNodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, TraversalNode> _nodes = new();

    public TraversalNode? GetNode(string nodeId)
        => _nodes.TryGetValue(nodeId, out var n) ? n : null;

    public void Register(TraversalNode node)
        => _nodes[node.NodeId] = node;
}
