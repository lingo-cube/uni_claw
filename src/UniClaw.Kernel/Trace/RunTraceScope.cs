namespace UniClaw.Kernel.Trace;

/// <summary>Trace 的 canonical correlation 根：Primary Run / RunId。</summary>
public sealed record RunCorrelation(string RunId);

/// <summary>
/// 一个 Primary Run 内的瞬态录制上下文（TRC-001 术语）：caller-owned
/// （G3：驱动 Primary Run 执行的那一个组件持有 lifecycle；不是 Product
/// Session，不是 canonical state）。Trace 传给 UniKernel composition
/// seam；FinalizeArtifact 在 runtime outcome emission 之后调用，幂等。
/// </summary>
public sealed class RunTraceScope
{
    private readonly IRunTraceSink _sink;

    internal RunTraceScope(IRunTraceSink sink) => _sink = sink;

    /// <summary>传给 UniKernel 的观察面。</summary>
    public IRunTrace Trace => _sink;

    /// <summary>
    /// finalize → immutable RunTraceArtifact（幂等）。emission 观测状态
    /// 由 Kernel 组合缝经 internal sink 标记（评审二轮 Standards 2：本
    /// caller 面无标记能力——外部不可在无 emission 时伪造 Finalized）；
    /// 未观测到 emission 的 finalize 产物 RecorderTerminal=Quarantined。
    /// </summary>
    public RunTraceArtifact FinalizeArtifact() => _sink.Finalize();
}

/// <summary>
/// caller 侧唯一入口：显式选择启用（InMemory 确定性录制）或禁用
/// （DisabledRunTrace）——禁用必须显式，无 nullable / 全局 / 隐式
/// fallback。
/// </summary>
public static class RunTraceFactory
{
    public static RunTraceScope BeginRun(RunCorrelation correlation) => new(new InMemoryRunTrace(correlation));

    public static RunTraceScope BeginDisabled(RunCorrelation correlation) => new(DisabledRunTrace.For(correlation));
}
