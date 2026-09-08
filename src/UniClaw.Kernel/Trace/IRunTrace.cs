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
/// TraceOperationScope 契约：Record 有界事件（references + 可选封闭
/// reason code）；Complete 显式结构关闭；未显式 Complete 的 span 在
/// finalize 时如实标 Incomplete，不伪造 duration / success。
/// </summary>
public interface ITraceOperationScope : IDisposable
{
    TraceContext Context { get; }

    void Record(string eventId, IReadOnlyList<TraceReference> references, string? reasonCode = null);

    void Complete(StructuralOutcome outcome);
}

/// <summary>
/// Recorder sink：caller-owned RunTraceScope 的 finalize 面。internal——
/// 只经 RunTraceFactory / RunTraceScope 暴露，L2 永远看不到。
/// </summary>
internal interface IRunTraceSink : IRunTrace
{
    /// <summary>幂等；在 runtime outcome emission 之后由 caller 调用。</summary>
    RunTraceArtifact Finalize();
}
