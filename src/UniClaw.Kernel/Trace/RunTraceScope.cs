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
    /// Acceptance 8 机械执法（人工裁决 2026-09-09 二轮，方案 A）：
    /// caller 在 runtime outcome emission 完成后显式标记；未标记即
    /// FinalizeArtifact → fail-closed 抛错。
    /// </summary>
    public void MarkRuntimeOutcomeEmitted() => _sink.MarkOutcomeEmitted();

    /// <summary>finalize → immutable RunTraceArtifact（幂等；须先标记
    /// emission）。</summary>
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
