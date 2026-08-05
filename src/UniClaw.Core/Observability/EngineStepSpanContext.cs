using System.Collections.Immutable;

namespace UniClaw.Core.Observability;

/// <summary>
/// EngineStepSpanContext — 引擎 span 上下文的 AsyncLocal 栈式通道（trace-correlated-logging D-2）。
/// 生产链路（经用户确认的裁决）用 AsyncLocal 替代跨实例引用：调用方在 BeginSpanAsync 返回后
/// 显式 <see cref="Push"/>（async 方法内 AsyncLocal 写入对调用方 ExecutionContext 不可见——
/// .NET async boundary copy-on-write 语义，D-222/D-223 记录）、span scope 关闭时
/// <see cref="Pop"/>；PageAnalyzer 在 ai.call 创建时经
/// <see cref="ITraceContextProvider.CurrentSpanId"/> 读取栈顶（当前最内层 span id）。
/// 使用 <see cref="ImmutableStack{T}"/> 保证 per-flow 隔离——每个 Push/Pop 产生新栈引用，
/// AsyncLocal 值替换为不可变引用，Task.Run 子 flow 可安全读写而不污染父 flow。
/// 静态单例：生产组合根注入 <see cref="Instance"/>，PageAnalyzer 持有同一实例引用。
/// 非引擎入口（栈为空）→ ai.call 保留孤儿根 span（不跳过记录）。
/// </summary>
public sealed class EngineStepSpanContext : ITraceContextProvider
{
    private static readonly AsyncLocal<ImmutableStack<string?>> _stack = new();

    /// <summary>静态单例 — 生产组合根与测试 fixture 共用同一实例。</summary>
    public static EngineStepSpanContext Instance { get; } = new();

    private EngineStepSpanContext()
    {
    }

    /// <summary>当前最内层 span id（栈顶）；栈为空时为 null。</summary>
    public string? CurrentSpanId
    {
        get
        {
            var stack = _stack.Value;
            return stack != null && !stack.IsEmpty ? stack.Peek() : null;
        }
    }

    /// <summary>
    /// 压入 span id（调用方在 BeginSpanAsync 返回后显式调用；按 async flow 隔离）。
    /// 使用 ImmutableStack——Push 返回新引用，旧引用不变，保证 per-flow 互不污染。
    /// </summary>
    internal void Push(string? spanId)
    {
        _stack.Value = (_stack.Value ?? ImmutableStack<string?>.Empty).Push(spanId);
    }

    /// <summary>
    /// 弹出栈顶 span id（span scope 关闭处调用，恢复父 span）；栈空时 no-op。
    /// 使用 ImmutableStack——Pop 返回新引用（tail），原引用不变。
    /// </summary>
    internal void Pop()
    {
        var stack = _stack.Value;
        if (stack != null && !stack.IsEmpty)
            _stack.Value = stack.Pop();
    }
}
