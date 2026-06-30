using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// 遍历结果
/// </summary>
/// <param name="Status">最终状态</param>
/// <param name="ElapsedSeconds">耗时（秒）</param>
/// <param name="TotalSteps">总步数</param>
/// <param name="VisitedNodes">已访问节点ID集合</param>
/// <param name="Trace">追踪记录</param>
/// <param name="TraceId">追踪ID</param>
/// <param name="Error">错误（如果有）</param>
/// <param name="Metrics">指标数据</param>
public sealed record class TraversalResult(
    GlobalState Status,
    double ElapsedSeconds,
    int TotalSteps,
    HashSet<string> VisitedNodes,
    List<Dictionary<string, object>> Trace,
    string? TraceId = null,
    Exception? Error = null,
    Dictionary<string, object>? Metrics = null)
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Status == GlobalState.Completed && Error == null;

    /// <summary>
    /// 是否失败
    /// </summary>
    public bool IsFailed => Status == GlobalState.Error || Error != null;
}

/// <summary>
/// 图遍历引擎接口
/// </summary>
public interface IGraphTraversalEngine
{
    /// <summary>
    /// 当前计划
    /// </summary>
    TraversalPlan? Plan { get; }

    /// <summary>
    /// 当前上下文
    /// </summary>
    ITraversalContext? Context { get; }

    /// <summary>
    /// 当前状态
    /// </summary>
    GlobalState CurrentState { get; }

    /// <summary>
    /// 初始化引擎
    /// </summary>
    /// <param name="plan">遍历计划</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> InitializeAsync(TraversalPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// 运行遍历
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TraversalResult> RunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停遍历
    /// </summary>
    Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复遍历
    /// </summary>
    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止遍历
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前状态
    /// </summary>
    Task<GlobalState> GetStateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 动作执行器接口
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// 点击操作
    /// </summary>
    Task<bool> TapAsync(double x, double y, CancellationToken cancellationToken = default);

    /// <summary>
    /// 滑动操作
    /// </summary>
    Task<bool> SwipeAsync(
        double startX, double startY,
        double endX, double endY,
        int durationMs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按返回键
    /// </summary>
    Task<bool> PressBackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 输入文本
    /// </summary>
    Task<bool> InputTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// 长按操作
    /// </summary>
    Task<bool> LongPressAsync(double x, double y, int durationMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// 等待
    /// </summary>
    Task WaitAsync(int milliseconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取操作历史
    /// </summary>
    List<ActionRecord> GetHistory();
}

/// <summary>
/// 动作记录
/// </summary>
/// <param name="Action">操作类型</param>
/// <param name="Timestamp">时间戳</param>
/// <param name="Parameters">参数</param>
/// <param name="Success">是否成功</param>
public sealed record class ActionRecord(
    string Action,
    DateTimeOffset Timestamp,
    Dictionary<string, object> Parameters,
    bool Success);
