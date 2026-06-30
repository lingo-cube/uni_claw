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
    PopupHandling,

    /// <summary>动态匹配 (V6.9)</summary>
    DynamicMatch
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
    bool HasUnvisitedChildren(IGraphTraversalEngine? engine = null);

    /// <summary>
    /// 获取下一个状态
    /// </summary>
    TraversalState GetNextState();
}

/// <summary>
/// 遍历上下文接口
/// </summary>
public interface ITraversalContext
{
    /// <summary>
    /// 节点栈
    /// </summary>
    INodeStack NodeStack { get; }

    /// <summary>
    /// 当前路径
    /// </summary>
    List<string> CurrentPath { get; }

    /// <summary>
    /// 已访问的页面
    /// </summary>
    Dictionary<string, object> VisitedPages { get; }

    /// <summary>
    /// 已访问的子节点
    /// </summary>
    Dictionary<string, List<string>> VisitedChildren { get; }

    /// <summary>
    /// 当前帧（节点）
    /// </summary>
    ITraversalNode? CurrentFrame { get; set; }

    /// <summary>
    /// 步骤计数
    /// </summary>
    int StepCount { get; }

    /// <summary>
    /// 全局状态
    /// </summary>
    GlobalState GlobalState { get; set; }

    /// <summary>
    /// 最后的错误
    /// </summary>
    Exception? LastError { get; set; }
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

/// <summary>
/// 栈帧接口
/// </summary>
public interface IStackFrame
{
    /// <summary>
    /// 节点ID
    /// </summary>
    string NodeId { get; }

    /// <summary>
    /// 节点
    /// </summary>
    ITraversalNode Node { get; }

    /// <summary>
    /// 子节点列表
    /// </summary>
    List<string> Children { get; }
}

/// <summary>
/// 遍历节点接口
/// </summary>
public interface ITraversalNode
{
    /// <summary>
    /// 节点ID
    /// </summary>
    string NodeId { get; }

    /// <summary>
    /// 节点名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 节点类型
    /// </summary>
    NodeType NodeType { get; }

    /// <summary>
    /// 静态子节点
    /// </summary>
    List<string> StaticChildren { get; }
}

/// <summary>
/// 节点类型枚举
/// </summary>
public enum NodeType
{
    /// <summary>容器节点（可包含子节点）</summary>
    Container,
    /// <summary>叶子节点 - 开关</summary>
    LeafSwitch,
    /// <summary>叶子节点 - 滑块</summary>
    LeafSlider,
    /// <summary>叶子节点 - 可执行动作</summary>
    LeafAction,
    /// <summary>叶子节点 - 仅展示信息</summary>
    LeafInfo,
    /// <summary>屏幕节点</summary>
    Screen,
    /// <summary>动作节点</summary>
    Action,
    /// <summary>目标节点</summary>
    Target
}

/// <summary>
/// 图遍历引擎接口（最小定义）
/// </summary>
public interface IGraphTraversalEngine
{
    // 最小接口定义，避免循环依赖
}
