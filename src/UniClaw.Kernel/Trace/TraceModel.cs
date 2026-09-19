using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace UniClaw.Kernel.Trace;

/// <summary>
/// Recorder buffer 的终局状态（TRC-001 设计三态的现行两态实现）：
/// Finalized = 观测到 runtime outcome emission（Kernel 组合缝标记）后
/// 正常封存；Quarantined = 封存时未观测到 emission（失败 run 诊断仍可
/// 用，但显式降级）；CaptureFailed = recorder 故障路径（预留，无现行
/// 触发路径）。
/// </summary>
public enum RecorderTerminal
{
    Finalized,
    Quarantined,
    CaptureFailed,
}

/// <summary>Recorder 自身故障 / 词表执法信息；不得伪装成 Runtime failure。</summary>
public sealed record TraceDiagnostic(string Reason);

/// <summary>
/// span 内有界事件：reference-first；ReasonCode 只允许所属
/// TraceEventDefinition.AllowedReasonCodes 的成员（owner 已发布词汇，
/// G4 三边界），禁自由文本考古。
/// </summary>
public sealed record TraceEvent(
    string EventId,
    ImmutableArray<TraceReference> References,
    string? ReasonCode);

/// <summary>
/// 技术 correlation identity（TraceId/SpanId 无 domain authority；RunId
/// 才是唯一语义根）。bullet 实现确定性派生（序数 / 内容哈希），无
/// random / ambient。
/// </summary>
public sealed record TraceContext(string TraceId, string SpanId);

/// <summary>一次 SpanDefinition 的运行时 occurrence（immutable）。</summary>
public sealed record TraceSpan(
    string SpanId,
    string? ParentSpanId,
    string SpanDefinitionId,
    StructuralOutcome StructuralOutcome,
    ImmutableArray<TraceReference> References,
    ImmutableArray<TraceEvent> Events,
    int CaptureSequence);

/// <summary>
/// finalize 后的 immutable structural projection（ADR-0013）：非权威、
/// reference-oriented、不参与任何 Runtime 决策。深冻结（评审 Standards
/// V2）：全部集合成员为 ImmutableArray，无 List 强转改写面。Timing 为
/// 可选观测元数据——bullet 版刻意省略（canonical clock 维持 Deferred ⑪）。
/// </summary>
public sealed record RunTraceArtifact(
    string SchemaVersion,
    string RunId,
    string TraceId,
    string? RootSpanId,
    RecorderTerminal RecorderTerminal,
    ImmutableArray<TraceSpan> Spans,
    ImmutableArray<TraceDiagnostic> RecorderDiagnostics,
    string IntegritySha256 = "");

/// <summary>
/// Sealed trace 的内容完整性封套。摘要覆盖除摘要自身外的全部现行字段，
/// 只证明 artifact 在封存后未被改写；不把 Trace 提升为 Runtime truth。
/// </summary>
internal static class RunTraceArtifactIntegrity
{
    internal static RunTraceArtifact Seal(RunTraceArtifact artifact) =>
        artifact with { IntegritySha256 = Compute(artifact) };

    internal static bool IsValid(RunTraceArtifact artifact) =>
        artifact.IntegritySha256.Length == 64 &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(artifact.IntegritySha256.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(Compute(artifact)));

    private static string Compute(RunTraceArtifact artifact)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        Write(writer, artifact.SchemaVersion);
        Write(writer, artifact.RunId);
        Write(writer, artifact.TraceId);
        Write(writer, artifact.RootSpanId);
        writer.Write((int)artifact.RecorderTerminal);
        writer.Write(artifact.Spans.Length);
        foreach (var span in artifact.Spans)
        {
            Write(writer, span.SpanId);
            Write(writer, span.ParentSpanId);
            Write(writer, span.SpanDefinitionId);
            writer.Write((int)span.StructuralOutcome);
            writer.Write(span.CaptureSequence);
            WriteReferences(writer, span.References);
            writer.Write(span.Events.Length);
            foreach (var traceEvent in span.Events)
            {
                Write(writer, traceEvent.EventId);
                WriteReferences(writer, traceEvent.References);
                Write(writer, traceEvent.ReasonCode);
            }
        }
        writer.Write(artifact.RecorderDiagnostics.Length);
        foreach (var diagnostic in artifact.RecorderDiagnostics)
            Write(writer, diagnostic.Reason);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteReferences(BinaryWriter writer, ImmutableArray<TraceReference> references)
    {
        writer.Write(references.Length);
        foreach (var reference in references)
        {
            writer.Write((int)reference.Kind);
            Write(writer, reference.Value);
        }
    }

    private static void Write(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
            writer.Write(value);
    }
}
