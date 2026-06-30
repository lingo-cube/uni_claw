namespace UniClaw.Core.StateMachine;

/// <summary>
/// 全局遍历任务状态
/// </summary>
public enum GlobalState
{
    /// <summary>空闲状态</summary>
    Idle,

    /// <summary>初始化中</summary>
    Initializing,

    /// <summary>遍历中</summary>
    Traversing,

    /// <summary>已暂停</summary>
    Paused,

    /// <summary>错误状态</summary>
    Error,

    /// <summary>恢复中</summary>
    Recovering,

    /// <summary>已完成</summary>
    Completed,

    /// <summary>已终止</summary>
    Terminated
}

/// <summary>
/// 状态转换结果
/// </summary>
/// <param name="IsSuccess">是否成功</param>
/// <param name="Error">错误信息</param>
public readonly record struct StateTransitionResult(bool IsSuccess, string? Error = null)
{
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static StateTransitionResult Success() => new(true);

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static StateTransitionResult Failure(string error) => new(false, error);
}

/// <summary>
/// 全局状态机接口
/// </summary>
public interface IGlobalStateMachine
{
    /// <summary>
    /// 当前状态
    /// </summary>
    GlobalState CurrentState { get; }

    /// <summary>
    /// 尝试转换到目标状态
    /// </summary>
    /// <param name="targetState">目标状态</param>
    /// <param name="reason">转换原因</param>
    /// <returns>转换结果</returns>
    StateTransitionResult TransitionTo(GlobalState targetState, string? reason = null);

    /// <summary>
    /// 检查是否完成
    /// </summary>
    bool IsComplete();

    /// <summary>
    /// 检查是否可以转换到目标状态
    /// </summary>
    bool CanTransitionTo(GlobalState targetState);

    /// <summary>
    /// 获取有效的状态转换
    /// </summary>
    IEnumerable<GlobalState> GetValidTransitions();
}

/// <summary>
/// 状态转换事件参数
/// </summary>
public sealed record class StateTransitionEventArgs(
    GlobalState FromState,
    GlobalState ToState,
    string? Reason = null,
    DateTimeOffset Timestamp = default);
