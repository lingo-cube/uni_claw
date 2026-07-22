using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Traversal;

/// <summary>
/// 图遍历引擎接口 — 8 成员 async 接口。
/// TraversalResult 使用新版 (P-5: sealed record class + ImmutableArray)。
/// </summary>
public interface IGraphTraversalEngine
{
    /// <summary>当前计划</summary>
    TraversalPlan Plan { get; }

    /// <summary>当前上下文 (只读接口 P-3)</summary>
    ITraversalContext Context { get; }

    /// <summary>当前全局状态</summary>
    GlobalState CurrentState { get; }

    /// <summary>动作执行器 (测试场景用于 mock 服务数据收集)</summary>
    IActionExecutor ActionExecutor { get; }

    /// <summary>UniBrain AI 服务 (测试场景用于 mock 数据收集)</summary>
    IUniBrain Brain { get; }

    /// <summary>初始化引擎（构造器已初始化，此方法为 contract validation no-op）</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>运行遍历 — 返回新版 TraversalResult</summary>
    Task<TraversalResult> RunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停遍历 — 通过 TaskCompletionSource gate 挂起步骤循环。
    /// 前置校验: GlobalState 必须为 Traversing。
    /// 抛出 DomainValidationException 如果前置条件不满足。
    /// </summary>
    Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复遍历 — 完成 TCS gate 解锁步骤循环。
    /// 前置校验: GlobalState 必须为 Paused。
    /// B1 生命周期钩子在 gate 打开前触发。
    /// 抛出 DomainValidationException 如果前置条件不满足。
    /// </summary>
    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>停止遍历</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>获取当前全局状态</summary>
    Task<GlobalState> GetStateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 动作执行器接口
/// </summary>
public interface IActionExecutor
{
    /// <summary>点击操作</summary>
    Task<bool> TapAsync(double x, double y, CancellationToken cancellationToken = default);

    /// <summary>滑动操作</summary>
    Task<bool> SwipeAsync(
        double startX, double startY,
        double endX, double endY,
        int durationMs,
        CancellationToken cancellationToken = default);

    /// <summary>按返回键</summary>
    Task<bool> PressBackAsync(CancellationToken cancellationToken = default);

    /// <summary>输入文本</summary>
    Task<bool> InputTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>长按操作</summary>
    Task<bool> LongPressAsync(double x, double y, int durationMs, CancellationToken cancellationToken = default);

    /// <summary>等待</summary>
    Task WaitAsync(int milliseconds, CancellationToken cancellationToken = default);

    /// <summary>获取操作历史</summary>
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
