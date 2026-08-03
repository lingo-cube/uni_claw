namespace UniClaw.Core.Observability;

/// <summary>
/// ITraceContextProvider — 运行时引擎上下文通道（trace-parent-linkage D1）。
/// <see cref="PageAnalyzer"/> 在创建 ai.call span 时读取 <see cref="CurrentSpanId"/>
/// 作为 parentSpanId，使 ai.call 挂到当前最内层 engine.step span 下。
/// 非引擎上下文（provider 未注入或 CurrentSpanId 为 null）时 ai.call 保持孤儿根 span
/// （保留记录、不跳过）。实现：<see cref="EngineStepSpanContext"/>（AsyncLocal 通道，2.7）。
/// </summary>
public interface ITraceContextProvider
{
    /// <summary>当前最内层 engine.step span id；非引擎上下文为 null。</summary>
    string? CurrentSpanId { get; }
}
