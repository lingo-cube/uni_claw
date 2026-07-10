namespace UniClaw.Core.Observability;

/// <summary>
/// Span type — trace semantic classification for traversal lifecycle events.
/// Locked at 11 values via constitution/locked-enums.md + EnumValueGuardTests.SpanType_Has11Values.
/// Adding or removing values requires constitution change flow (C-11 style).
/// </summary>
public enum SpanType
{
    /// <summary>DFS forward traversal — entering a child node</summary>
    DfsForward,

    /// <summary>DFS backtrack — returning from a child to parent</summary>
    DfsBacktrack,

    /// <summary>Restore operation — restoring traversal state after backtracking</summary>
    RestoreOp,

    /// <summary>Skip dangerous — skipping a node deemed dangerous (collision-proof)</summary>
    SkipDangerous,

    /// <summary>Popup handling — processing a detected popup</summary>
    PopupHandling,

    /// <summary>Container handling — processing container completion</summary>
    ContainerHandling,

    /// <summary>Error handling — processing an error and attempting recovery</summary>
    ErrorHandling,

    /// <summary>Page analysis — analyzing page structure via AI</summary>
    PageAnalysis,

    /// <summary>Cache operation — reading/writing traversal cache</summary>
    CacheOp,

    /// <summary>AI call — invoking AI capability (vision, text model)</summary>
    AICall,

    /// <summary>State decision — making a traversal state decision (transition choice)</summary>
    StateDecision,
}

/// <summary>
/// Page transition — records user-facing page navigation distinct from FSM StateTransition.
/// StateTransition records FSM state changes (Idle→Traversing), PageTransition records page navigation (home→wifi).
/// TransitionType values: "forward" (entering child), "back" (pressing back),
/// "sub_page" (sub-page completion), "popup_dismiss" (popup dismissed then page transition).
/// </summary>
public sealed record class PageTransition(
    string FromPage,
    string ToPage,
    string TransitionType,
    string? NodeId = null,
    double? DurationMs = null,
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);

/// <summary>
/// 追踪会话
/// </summary>
/// <param name="TraceId">追踪ID</param>
/// <param name="StartTime">开始时间</param>
/// <param name="EndTime">结束时间</param>
/// <param name="Metadata">元数据</param>
public sealed record class TraceSession(
    string TraceId,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime = null,
    Dictionary<string, object>? Metadata = null)
{
    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted => EndTime != null;

    /// <summary>
    /// 获取持续时间
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (!EndTime.HasValue)
            return null;

        return EndTime.Value - StartTime;
    }
}

/// <summary>
/// 状态转换记录
/// </summary>
/// <param name="FromState">源状态</param>
/// <param name="ToState">目标状态</param>
/// <param name="NodeId">节点ID</param>
/// <param name="Timestamp">时间戳</param>
/// <param name="Reason">原因</param>
/// <param name="Metadata">元数据</param>
public sealed record class StateTransition(
    string FromState,
    string ToState,
    string? NodeId = null,
    DateTimeOffset Timestamp = default,
    string? Reason = null,
    Dictionary<string, object>? Metadata = null);

/// <summary>
/// AI调用记录
/// </summary>
/// <param name="Capability">能力名称</param>
/// <param name="ProviderId">提供者ID</param>
/// <param name="Success">是否成功</param>
/// <param name="LatencyMs">延迟（毫秒）</param>
/// <param name="Tokens">Token数</param>
/// <param name="Timestamp">时间戳</param>
public sealed record class AICallRecord(
    string Capability,
    string ProviderId,
    bool Success,
    double LatencyMs,
    int? Tokens = null,
    DateTimeOffset Timestamp = default);

/// <summary>
/// 执行记录
/// </summary>
/// <param name="Action">操作</param>
/// <param name="Status">状态</param>
/// <param name="SpanType">语义分类标签 (optional, backward compatible — default null)</param>
/// <param name="Target">目标</param>
/// <param name="DurationMs">持续时间（毫秒）</param>
/// <param name="Timestamp">时间戳</param>
/// <param name="Metadata">元数据</param>
public sealed record class ExecutionRecord(
    string Action,
    string Status,
    SpanType? SpanType = null,
    object? Target = null,
    double DurationMs = 0,
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);

/// <summary>
/// 错误记录
/// </summary>
/// <param name="ErrorType">错误类型</param>
/// <param name="ErrorMessage">错误消息</param>
/// <param name="Severity">严重性</param>
/// <param name="Timestamp">时间戳</param>
/// <param name="Metadata">元数据</param>
public sealed record class ErrorRecord(
    string ErrorType,
    string ErrorMessage,
    ErrorSeverity Severity,
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);

/// <summary>
/// 错误严重性
/// </summary>
public enum ErrorSeverity
{
    /// <summary>调试</summary>
    Debug,

    /// <summary>信息</summary>
    Info,

    /// <summary>警告</summary>
    Warning,

    /// <summary>错误</summary>
    Error,

    /// <summary>致命</summary>
    Fatal
}

/// <summary>
/// 追踪记录器接口
/// </summary>
public interface ITraceRecorder
{
    /// <summary>
    /// 当前会话
    /// </summary>
    TraceSession? CurrentSession { get; }

    /// <summary>
    /// 开始新会话
    /// </summary>
    Task<TraceSession> StartSessionAsync(
        string traceId,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 结束当前会话
    /// </summary>
    Task EndSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录状态转换
    /// </summary>
    Task RecordTransitionAsync(
        StateTransition transition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录AI调用
    /// </summary>
    Task RecordAICallAsync(
        AICallRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录执行
    /// </summary>
    Task RecordExecutionAsync(
        ExecutionRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录错误
    /// </summary>
    Task RecordErrorAsync(
        ErrorRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有转换记录
    /// </summary>
    Task<List<StateTransition>> GetTransitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有AI调用记录
    /// </summary>
    Task<List<AICallRecord>> GetAICallsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有执行记录
    /// </summary>
    Task<List<ExecutionRecord>> GetExecutionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有错误记录
    /// </summary>
    Task<List<ErrorRecord>> GetErrorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录页面转换
    /// </summary>
    Task RecordPageTransitionAsync(
        PageTransition transition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有页面转换记录
    /// </summary>
    Task<List<PageTransition>> GetPageTransitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 导出追踪记录
    /// </summary>
    Task<string> ExportTraceAsync(
        string format = "json",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 指标收集器接口
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// 记录计数
    /// </summary>
    void RecordCount(string name, double value = 1, Dictionary<string, string>? tags = null);

    /// <summary>
    /// 记录延迟
    /// </summary>
    void RecordLatency(string name, double milliseconds, Dictionary<string, string>? tags = null);

    /// <summary>
    /// 记录值
    /// </summary>
    void RecordValue(string name, double value, Dictionary<string, string>? tags = null);

    /// <summary>
    /// 获取所有指标
    /// </summary>
    Dictionary<string, object> GetAllMetrics();

    /// <summary>
    /// 清除所有指标
    /// </summary>
    void Clear();
}
