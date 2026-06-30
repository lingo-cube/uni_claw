namespace UniClaw.Core.StateMachine;

/// <summary>
/// 深度优先遍历栈实现
/// </summary>
public sealed class NodeStack : INodeStack
{
    private readonly List<StackFrame> _frames = new();

    /// <summary>
    /// 默认最大深度
    /// </summary>
    public const int DefaultMaxDepth = 10;

    /// <summary>
    /// 创建栈实例
    /// </summary>
    public NodeStack(int maxDepth = DefaultMaxDepth)
    {
        MaxDepth = maxDepth;
    }

    /// <inheritdoc />
    public int Depth => _frames.Count;

    /// <inheritdoc />
    public int MaxDepth { get; }

    /// <inheritdoc />
    public bool IsEmpty => _frames.Count == 0;

    /// <inheritdoc />
    public bool Push(ITraversalNode node, List<string>? children = null)
    {
        if (Depth >= MaxDepth)
            return false;

        var frame = new StackFrame(
            NodeId: node.NodeId,
            Node: node,
            Children: children ?? new List<string>()
        );

        _frames.Add(frame);
        return true;
    }

    /// <inheritdoc />
    public IStackFrame? Pop()
    {
        if (IsEmpty)
            return null;

        var frame = _frames[^1];
        _frames.RemoveAt(_frames.Count - 1);
        return frame;
    }

    /// <inheritdoc />
    public IStackFrame? Peek(int offset = 0)
    {
        if (offset < 0 || offset >= Depth)
            return null;

        return _frames[^(offset + 1)];
    }

    /// <inheritdoc />
    public void Clear()
    {
        _frames.Clear();
    }

    /// <summary>
    /// 栈帧实现
    /// </summary>
    private sealed record class StackFrame(
        string NodeId,
        ITraversalNode Node,
        List<string> Children) : IStackFrame;
}
