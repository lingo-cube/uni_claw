namespace UniClaw.Kernel.Trace;

/// <summary>
/// UniKernel composition seam 的观察面（TRC-001）：记录经批准的 operation
/// occurrence + typed references；绝不参与任何 Runtime 决策（acceptance 2：
/// 本面的一切故障由调用侧吸收）。
/// </summary>
public interface IRunTrace
{
    /// <summary>显式 parent（无 ambient authority）；根级 operation 传 null。</summary>
    ITraceOperationScope StartOperation(
        SpanDefinition definition,
        TraceContext? parent,
        IReadOnlyList<TraceReference> references);
}

/// <summary>
/// TraceOperationScope 契约：Record 只接受所属 catalog 的事件定义
/// （TraceEventDefinition 单例；词表执法在 recorder 侧 fail-closed）；
/// Complete 显式结构关闭；未显式 Complete 的 span 在 finalize 时如实标
/// Incomplete，不伪造 duration / success。
/// </summary>
public interface ITraceOperationScope : IDisposable
{
    TraceContext Context { get; }

    void Record(TraceEventDefinition eventDefinition, IReadOnlyList<TraceReference> references, string? reasonCode = null);

    void Complete(StructuralOutcome outcome);
}

/// <summary>
/// Recorder sink：caller-owned RunTraceScope 的 lifecycle + finalize 面。
/// internal——只经 RunTraceFactory / RunTraceScope 暴露，L2 永远看不到。
/// </summary>
internal interface IRunTraceSink : IRunTrace
{
    /// <summary>Acceptance 8 机械执法：caller 在 runtime outcome emission
    /// 完成后显式标记；未标记的 Finalize fail-closed。</summary>
    void MarkOutcomeEmitted();

    /// <summary>幂等；须在 MarkOutcomeEmitted 之后调用。</summary>
    RunTraceArtifact Finalize();
}
