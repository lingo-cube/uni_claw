namespace UniClaw.Core.Observability;

/// <summary>
/// IHandlerTraceWriter — Handler 生命周期 trace 接口，与 ITraceCoordinator（18 members）ISP 分离。
/// 供 PopupHandler/ContainerHandler/ErrorHandler 编排层注入和 DfsBacktrack 插入点使用。
/// 记录一条 ExecutionRecord，SpanType 区分 handler 类型，metadata 携带 handler 特有字段。
/// </summary>
public interface IHandlerTraceWriter
{
    /// <summary>
    /// 记录 handler 生命周期事件。
    /// </summary>
    /// <param name="action">描述字符串（"handle_popup", "dfs_backtrack" 等）</param>
    /// <param name="spanType">区分 handler 类型</param>
    /// <param name="status">状态（"success"/"fail"/"ok"）</param>
    /// <param name="metadata">handler 特有字段</param>
    /// <param name="context">可选的 TraceContext，用于将 handler 记录与引擎上下文关联</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RecordHandlerLifecycleAsync(
        string action,
        SpanType spanType,
        string status = "ok",
        Dictionary<string, object>? metadata = null,
        TraceContext? context = null,
        CancellationToken cancellationToken = default);
}
