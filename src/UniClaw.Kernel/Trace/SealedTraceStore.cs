using System.Security.Cryptography;

namespace UniClaw.Kernel.Trace;

/// <summary>
/// sealed trace artifact 完整性校验失败 / 产物缺失（fail closed；
/// ScenarioImport 侧包装为导入失败）。internal realization，
/// 非公共契约（TRW-001 D1）。
/// </summary>
internal sealed class SealedTraceIntegrityException(string message) : Exception(message);

/// <summary>
/// sealed artifact 持久产物读取 + 完整性复核（TRW-001 D4）：
/// seal 时以 canonical rendering 的 SHA-256 随 artifact 携带（.trace.json +
/// .trace.sha256）；导入侧重算并比对，mismatch / 缺失 → fail closed。
/// canonical rendering 与 RunTraceArtifactIntegrity 覆盖同一字段集
/// （SchemaVersion / RunId / TraceId / RootSpanId / RecorderTerminal /
/// 全部 span（CaptureSequence 序）及其 events / references / diagnostics），
/// 逐字段长度帧化，无 timing 字段 → 同输入恒等（D6 确定性）。
/// internal realization，非公共契约（TRW-001 D1）。
/// </summary>
internal static class SealedTraceStore
{
    /// <summary>
    /// 读取并复核一个已 seal 的持久化 artifact；任何缺失 / 不可解析 /
    /// hash 失配 → SealedTraceIntegrityException（fail closed）。
    /// </summary>
    internal static RunTraceArtifact LoadVerified(string directory, string runCorrelationId)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(runCorrelationId);

        var name = Sanitize(runCorrelationId);
        var jsonPath = Path.Combine(directory, name + ".trace.json");
        var hashPath = Path.Combine(directory, name + ".trace.sha256");
        if (!File.Exists(jsonPath))
            throw new SealedTraceIntegrityException("sealed-trace-missing:" + jsonPath);
        if (!File.Exists(hashPath))
            throw new SealedTraceIntegrityException("sealed-hash-missing:" + hashPath);

        RunTraceArtifact artifact;
        try
        {
            artifact = System.Text.Json.JsonSerializer.Deserialize<RunTraceArtifact>(
                File.ReadAllText(jsonPath))
                ?? throw new SealedTraceIntegrityException("sealed-trace-empty:" + jsonPath);
        }
        catch (SealedTraceIntegrityException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new SealedTraceIntegrityException($"sealed-trace-unreadable:{e.GetType().Name}:{e.Message}");
        }

        var stored = File.ReadAllText(hashPath).Trim();
        var actual = CanonicalRendering(artifact);
        if (!string.Equals(stored, actual, StringComparison.Ordinal))
            throw new SealedTraceIntegrityException(
                $"sealed-trace-integrity-mismatch: stored={stored} actual={actual}");

        // review 修法 3：双重校验——sidecar 与 canonical rendering 一致之外，
        // artifact 内嵌 IntegritySha256 也必须匹配同一 rendering（借助
        // TraceModel 的 RunTraceArtifactIntegrity，只调用不修改）。
        if (!RunTraceArtifactIntegrity.IsValid(artifact))
            throw new SealedTraceIntegrityException(
                $"sealed-trace-embedded-integrity-mismatch: embedded={artifact.IntegritySha256} actual={actual}");
        return artifact;
    }

    /// <summary>
    /// artifact 的 canonical rendering 之 SHA-256（小写 hex；不含
    /// IntegritySha256 自身）。与 RunTraceArtifactIntegrity 的字段覆盖一致，
    /// 因此该值同时满足 RunTraceArtifactIntegrity.IsValid。
    /// </summary>
    internal static string CanonicalRendering(RunTraceArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

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

    /// <summary>RunId → 文件名安全形式（非 [字母数字.-] 一律替换为 '_'）。</summary>
    internal static string Sanitize(string runId) =>
        new(runId.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_').ToArray());

    private static void WriteReferences(
        BinaryWriter writer, System.Collections.Immutable.ImmutableArray<TraceReference> references)
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
