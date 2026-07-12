using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// 遍历状态
/// </summary>
public enum TraversalState
{
    /// <summary>选择节点</summary>
    NodeSelect,

    /// <summary>前置条件检查</summary>
    PreconditionCheck,

    /// <summary>执行操作</summary>
    Execute,

    /// <summary>结果验证</summary>
    ResultVerify,

    /// <summary>分支决策</summary>
    Branch,

    /// <summary>容器完成 (V6)</summary>
    FrameComplete,

    /// <summary>错误处理 (V6)</summary>
    ErrorHandling,

    /// <summary>弹窗处理 (V6)</summary>
    PopupHandling
}

/// <summary>
/// 遍历状态机接口
/// </summary>
public interface ITraversalStateMachine
{
    /// <summary>
    /// 当前状态
    /// </summary>
    TraversalState CurrentState { get; }

    /// <summary>
    /// 遍历上下文
    /// </summary>
    ITraversalContext Context { get; }

    /// <summary>
    /// 尝试转换到目标状态
    /// </summary>
    /// <param name="targetState">目标状态</param>
    /// <param name="nodeId">相关节点ID</param>
    /// <param name="metadata">元数据</param>
    StateTransitionResult TransitionTo(
        TraversalState targetState,
        string? nodeId = null,
        Dictionary<string, object>? metadata = null);

    /// <summary>
    /// 检查是否有未访问的子节点
    /// </summary>
    /// <param name="engine">遍历引擎引用</param>
    bool HasUnvisitedChildren(UniClaw.Core.Traversal.IGraphTraversalEngine? engine = null);

    /// <summary>
    /// 获取下一个状态
    /// </summary>
    TraversalState GetNextState();
}

/// <summary>
/// 遍历上下文接口 (D-4: 强类型只读集合)。
/// 消费者不能通过此接口修改内部集合。
/// </summary>
public interface ITraversalContext
{
    /// <summary>节点栈</summary>
    INodeStack NodeStack { get; }

    /// <summary>当前路径 (只读视图，修改只能通过引擎内部的 AppendPath/PopPath)</summary>
    IReadOnlyList<string> CurrentPath { get; }

    /// <summary>已访问的页面 (只读集合，修改只能通过引擎内部的 MarkVisited)</summary>
    IReadOnlySet<string> VisitedPages { get; }

    /// <summary>已访问的子节点 (只读字典+只读嵌套集合)</summary>
    IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren { get; }

    /// <summary>已访问的节点 (只读集合，修改只能通过引擎内部的 MarkNodeVisited)</summary>
    IReadOnlySet<string> VisitedNodes { get; }

    /// <summary>当前帧（节点） — 只读，修改通过 SetCurrentFrame()</summary>
    ITraversalNode? CurrentFrame { get; }

    /// <summary>步骤计数 — 只读，引擎通过 IncrementStepCount() 递增</summary>
    int StepCount { get; }

    /// <summary>全局状态 — 只读，修改通过 SetGlobalState()</summary>
    GlobalState GlobalState { get; }

    /// <summary>最后的错误 — 只读，修改通过 SetLastError()</summary>
    Exception? LastError { get; }
}

/// <summary>
/// 节点栈接口
/// </summary>
public interface INodeStack
{
    /// <summary>
    /// 栈深度
    /// </summary>
    int Depth { get; }

    /// <summary>
    /// 最大深度
    /// </summary>
    int MaxDepth { get; }

    /// <summary>
    /// 压入节点
    /// </summary>
    bool Push(ITraversalNode node, List<string>? children = null);

    /// <summary>
    /// 弹出节点
    /// </summary>
    IStackFrame? Pop();

    /// <summary>
    /// 查看节点（不弹出）
    /// </summary>
    IStackFrame? Peek(int offset = 0);

    /// <summary>
    /// 检查是否为空
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// 清空栈
    /// </summary>
    void Clear();
}
