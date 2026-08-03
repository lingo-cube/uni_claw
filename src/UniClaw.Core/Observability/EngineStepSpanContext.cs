namespace UniClaw.Core.Observability;

/// <summary>
/// EngineStepSpanContext — 引擎 step span 上下文的 AsyncLocal 通道（trace-parent-linkage 2.7）。
/// 生产链路（经用户确认的裁决）用 AsyncLocal 替代跨实例引用：TraversalEngine 在每次
/// engine.step scope 开启时 <see cref="Set"/> 当前 step span id、关闭时 <see cref="Reset"/>，
/// PageAnalyzer 在 ai.call 创建时经 <see cref="ITraceContextProvider.CurrentSpanId"/> 读取——
/// AsyncLocal 按 async flow 流动，引擎 run 的整个 await 链（含 AnalyzeCurrentPageAsync）
/// 都可见同一个值，无需共享 coordinator 实例。
/// 静态单例：生产组合根注入 <see cref="Instance"/>，PageAnalyzer 持有同一实例引用。
/// 非引擎入口（AsyncLocal 值为 null）→ ai.call 保留孤儿根 span（不跳过记录）。
/// </summary>
public sealed class EngineStepSpanContext : ITraceContextProvider
{
    private static readonly AsyncLocal<string?> _current = new();

    /// <summary>静态单例 — 生产组合根与测试 fixture 共用同一实例。</summary>
    public static EngineStepSpanContext Instance { get; } = new();

    private EngineStepSpanContext()
    {
    }

    /// <summary>当前最内层 engine.step span id；非引擎上下文为 null。</summary>
    public string? CurrentSpanId => _current.Value;

    /// <summary>设置当前 engine.step span id（引擎 step scope 开启处调用；按 async flow 隔离）。</summary>
    internal void Set(string? spanId) => _current.Value = spanId;

    /// <summary>清空当前 engine.step span id（引擎 step scope 关闭处调用；悬挂错误路径由下一次 Set 自然覆盖）。</summary>
    internal void Reset() => _current.Value = null;
}
