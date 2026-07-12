using UniClaw.Core.Simulation.Scroll;

namespace UniClaw.Core.StateMachine.Scroll;

/// <summary>
/// 滚动动作执行委托
/// </summary>
/// <param name="stepPercent">滚动步长百分比</param>
/// <returns>执行结果</returns>
public delegate ScrollActionResult ScrollActionDelegate(double stepPercent);

/// <summary>
/// 步骤 4：滚动动作执行
/// 通过 Hook Dispatch Table 执行滚动动作，支持异常回退和默认 None 处理。
/// </summary>
public sealed class ScrollActionExecutor
{
    private readonly Dictionary<ScrollActionType, ScrollActionDelegate> _handlers;
    private readonly Func<ScrollActionResult> _defaultNoneHandler;

    /// <summary>
    /// 创建滚动动作执行器
    /// </summary>
    /// <param name="defaultNoneHandler">默认 None 处理器（可选）</param>
    public ScrollActionExecutor(Func<ScrollActionResult>? defaultNoneHandler = null)
    {
        _handlers = new Dictionary<ScrollActionType, ScrollActionDelegate>();
        _defaultNoneHandler = defaultNoneHandler ?? (() => ScrollActionResult.DefaultNone());
    }

    /// <summary>
    /// 注册滚动动作处理器
    /// </summary>
    /// <param name="actionType">动作类型</param>
    /// <param name="handler">处理器委托</param>
    public void RegisterHandler(ScrollActionType actionType, ScrollActionDelegate handler)
    {
        _handlers[actionType] = handler;
    }

    /// <summary>
    /// 执行滚动动作
    /// </summary>
    /// <param name="context">滚动上下文</param>
    /// <returns>执行结果</returns>
    public ScrollActionResult Execute(ScrollContext context)
    {
        // None 动作直接返回默认结果
        if (context.ActionType == ScrollActionType.None)
            return _defaultNoneHandler();

        // 查找注册的处理器
        if (_handlers.TryGetValue(context.ActionType, out var handler))
        {
            try
            {
                return handler(context.StepPercent);
            }
            catch (Exception ex)
            {
                return ScrollActionResult.Failed(context.ActionType, ex.Message);
            }
        }

        // 未注册处理器，返回默认 None 结果
        return _defaultNoneHandler();
    }

    /// <summary>
    /// 执行滚动动作（带回调）
    /// </summary>
    /// <param name="actionType">动作类型</param>
    /// <param name="stepPercent">步长百分比</param>
    /// <param name="fallback">回退处理器（可选）</param>
    /// <returns>执行结果</returns>
    public ScrollActionResult ExecuteDirect(
        ScrollActionType actionType,
        double stepPercent,
        ScrollActionDelegate? fallback = null)
    {
        if (actionType == ScrollActionType.None)
            return _defaultNoneHandler();

        if (_handlers.TryGetValue(actionType, out var handler))
        {
            try
            {
                return handler(stepPercent);
            }
            catch (Exception ex)
            {
                return ScrollActionResult.Failed(actionType, ex.Message);
            }
        }

        // 使用回退处理器
        if (fallback != null)
        {
            try
            {
                return fallback(stepPercent);
            }
            catch (Exception ex)
            {
                return ScrollActionResult.Failed(actionType, ex.Message);
            }
        }

        return _defaultNoneHandler();
    }

    /// <summary>检查是否已注册处理器</summary>
    public bool HasHandler(ScrollActionType actionType) => _handlers.ContainsKey(actionType);

    /// <summary>移除处理器</summary>
    public bool UnregisterHandler(ScrollActionType actionType) => _handlers.Remove(actionType);

    /// <summary>清除所有处理器</summary>
    public void ClearHandlers() => _handlers.Clear();
}
