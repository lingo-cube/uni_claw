using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Graph.Models;

/// <summary>
/// 遍历节点接口 — 从 StateMachine 层移入 Graph.Models 层 (H-5 修正)。
/// ITraversalNode 的职责是描述遍历节点（Graph 概念），不是 FSM 状态。
/// 依赖的类型都在 Domain/Graph：NodeType 已在 Domain，ChildrenStrategy 已在 Graph。
/// </summary>
public interface ITraversalNode
{
    /// <summary>节点ID</summary>
    string NodeId { get; }

    /// <summary>节点名称</summary>
    string Name { get; }

    /// <summary>节点类型</summary>
    NodeType NodeType { get; }

    /// <summary>静态子节点</summary>
    List<string> StaticChildren { get; }

    /// <summary>子节点策略 — StepOrchestrator 步骤 9/10 需检查 ChildrenStrategyType</summary>
    ChildrenStrategy ChildrenStrategy { get; }

    /// <summary>错误策略 — ErrorHandler 读取节点级 ErrorPolicy 决定恢复策略 (C-3)</summary>
    ErrorPolicy? ErrorPolicy { get; }
}

/// <summary>
/// 栈帧接口 — 从 StateMachine 层移入 Graph.Models 层 (H-5 修正)。
/// IStackFrame 引用 ITraversalNode，同文件移动保持一致。
/// </summary>
public interface IStackFrame
{
    /// <summary>节点ID</summary>
    string NodeId { get; }

    /// <summary>节点</summary>
    ITraversalNode Node { get; }

    /// <summary>子节点列表</summary>
    List<string> Children { get; }
}
