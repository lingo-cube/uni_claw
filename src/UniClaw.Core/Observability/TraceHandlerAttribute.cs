namespace UniClaw.Core.Observability;

/// <summary>
/// TraceHandlerAttribute — 方法级 trace 标注。
/// C-10 阶段只作文档化标注，不运行逻辑。
/// Phase 3-B: Roslyn 源生成器扫描此属性自动注入 span 生命周期代码。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TraceHandlerAttribute : Attribute
{
    /// <summary>Span 语义分类</summary>
    public SpanType SpanType { get; }

    /// <summary>操作描述（"handle_popup", "analyze_screen" 等）</summary>
    public string Action { get; }

    /// <summary>
    /// 构造 TraceHandlerAttribute。
    /// </summary>
    /// <param name="spanType">Span 语义分类</param>
    /// <param name="action">操作描述字符串</param>
    public TraceHandlerAttribute(SpanType spanType, string action)
    {
        SpanType = spanType;
        Action = action;
    }
}
