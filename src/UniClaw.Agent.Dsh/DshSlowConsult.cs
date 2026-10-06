using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.Fusion;

namespace UniClaw.Agent.Dsh;

/// <summary>AGT-017/PER-019 fetch 结果：transport+parse 的纯数据（无 kernel 副作用），
/// 供同步 Consult 与异步流水（UniPerception）共用。SemanticDisposition 为模型
/// 返回的语义判断（Supported/Contradicted/Unknown…），非权威、不进 gate，只进诊断面。</summary>
public sealed record DshSlowFetchResult(
    SlowConsultationStatus Status,
    bool Admitted,
    IReadOnlyList<(string Subject, string Value)> Proposals,
    string? SemanticDisposition = null,
    string? Diagnostic = null)
{
    public static DshSlowFetchResult Reject(string diagnostic) =>
        new(SlowConsultationStatus.Rejected, false, Array.Empty<(string, string)>(), null, diagnostic);
}

/// <summary>
/// AGT-017/PER-019 — Slow 感知 live 桥：Kernel 公开 Slow 缝 ↔ DSH peer /slow 端点。
/// 只用公开类型；有界等待强制；错误/超时诚实映射（零投影）。
/// PER-019：Fetch/Project 分离——Fetch 无 kernel 副作用（可后台执行），
/// Project 在驱动线程调用（kernel 非并发安全，投影必须单线程）。
/// </summary>
public sealed class DshSlowConsult
{
    /// <summary>与 DshOpenedHttpPeer.ExecuteSlowAsync 同形的传输缝（测试注入 fake）。</summary>
    public delegate Task<DshSlowResponse> Transport(
        string requestId, string prompt, ModelConfiguration model,
        byte[]? imagePng, CancellationToken cancellationToken);

    private static readonly TimeSpan DefaultBoundedWait = TimeSpan.FromSeconds(15);

    private readonly Transport _transport;
    private readonly ModelConfiguration _textModel;
    private readonly ModelConfiguration? _visualModel;

    public DshSlowConsult(Transport transport, ModelConfiguration textModel, ModelConfiguration? visualModel = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _textModel = textModel ?? throw new ArgumentNullException(nameof(textModel));
        _visualModel = visualModel;
    }

    /// <summary>结构化 JSON 响应合同（与 E2E slow 测试同款）：
    /// {"status":"Succeeded","semanticDisposition":"Supported","proposals":[{"subject":"…","value":"…"}]}</summary>
    public static string BuildPrompt(SlowConsultationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var fastNote = request.FastBasis is { } basis
            ? $"\nFast perception context: yolo=[{string.Join(",", basis.YoloDetections.Take(8))}] ocr=[{string.Join(",", basis.OcrTokens.Take(20))}]"
            : "";
        return "You are the slow semantic perception of an Android Settings agent. "
            + "Answer with exactly one JSON object and no markdown: "
            + "{\"status\":\"Succeeded\",\"semanticDisposition\":\"Supported|Contradicted|Unknown\","
            + "\"proposals\":[{\"subject\":\"<claim subject>\",\"value\":\"<observed value>\"}]}. "
            + $"Target control: {request.Target}. Claim under question: {request.ClaimSubject} "
            + $"(field: {request.ClaimField}). Trigger reason: {request.Reason}."
            + fastNote;
    }

    /// <summary>fetch 阶段：门控（text 档 fast 前置）+ transport + 解析。零 kernel、零投影。
    /// sessionCorrelation 由调用方给出（run 未激活时退回 BuyerRef——Kernel 同款约定）。</summary>
    public async Task<DshSlowFetchResult> FetchAsync(
        SlowConsultationRequest request, string sessionCorrelation,
        TimeSpan? boundedWait = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RequestId) || string.IsNullOrWhiteSpace(request.ClaimSubject))
            return new(SlowConsultationStatus.Invalid, false, Array.Empty<(string, string)>(),
                null, "invalid slow request");

        var visual = request.RequiresRawArtifact && request.RawArtifact is { Length: > 0 };
        var model = visual ? _visualModel : _textModel;
        if (model is null)
            return new(SlowConsultationStatus.NotConfigured, false, Array.Empty<(string, string)>(),
                null, visual ? "visual slow model not configured" : "text slow model not configured");

        // 设计对齐（runtime-capability-integration-seams §4.0.1，2026-10-06 更名）：
        // Fast 与 Slow Text 是「UniPerception」组合能力，依赖链固定 Fast→Slow
        // Text——缺可用/对齐的 fast 前置不得调用（public SlowTextGate 同款执法；
        // visual 不经此门）。
        if (!visual)
        {
            var gate = SlowTextGate.Evaluate(
                new FusionCapture(request.CaptureId, sessionCorrelation,
                    request.ObservationCycleId, request.CaptureTimestamp),
                request.FastBasis);
            if (!gate.Eligible)
                return DshSlowFetchResult.Reject($"text-slow-gate:{gate.Diagnostic}");
        }

        var budget = boundedWait ?? DefaultBoundedWait;
        if (budget <= TimeSpan.Zero)
            return new(SlowConsultationStatus.TimedOut, false, Array.Empty<(string, string)>(),
                null, "bounded wait exhausted before transport (effect-critical)");

        DshSlowResponse response;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(budget);
            response = await _transport(
                request.RequestId, BuildPrompt(request), model,
                visual ? request.RawArtifact : null, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new(SlowConsultationStatus.TimedOut, false, Array.Empty<(string, string)>(),
                null, $"bounded-wait-exhausted:{(int)budget.TotalMilliseconds}ms");
        }
        catch (Exception error)
        {
            return DshSlowFetchResult.Reject($"slow-transport-failed:{error.Message}");
        }

        if (!string.IsNullOrWhiteSpace(response.Error))
            return DshSlowFetchResult.Reject($"slow-endpoint-error:{response.Error}:{response.Diagnostic}");
        if (string.IsNullOrWhiteSpace(response.Text))
            return DshSlowFetchResult.Reject("slow-endpoint-empty-text");

        try
        {
            using var json = JsonDocument.Parse(response.Text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
            });
            var root = json.RootElement;
            var succeeded = root.TryGetProperty("status", out var statusElement)
                && string.Equals(statusElement.GetString(), "Succeeded", StringComparison.Ordinal);
            var disposition = root.TryGetProperty("semanticDisposition", out var dispositionElement)
                && dispositionElement.ValueKind == JsonValueKind.String
                ? dispositionElement.GetString()
                : null;
            var proposals = new List<(string, string)>();
            if (root.TryGetProperty("proposals", out var entries)
                && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("subject", out var subjectElement)
                        || !entry.TryGetProperty("value", out var valueElement))
                        continue;
                    var subject = subjectElement.GetString();
                    var value = valueElement.GetString();
                    if (string.IsNullOrWhiteSpace(subject) || value is null)
                        continue;
                    proposals.Add((subject, value));
                }
            }
            return new(
                succeeded ? SlowConsultationStatus.Succeeded : SlowConsultationStatus.Partial,
                true, proposals, disposition,
                succeeded ? null : "slow-model-reported-non-success");
        }
        catch (JsonException error)
        {
            return DshSlowFetchResult.Reject($"slow-response-not-json:{error.Message}");
        }
    }

    /// <summary>project 阶段：解析结果 → kernel.Process（P2 公开缝）。必须在驱动线程调用。
    /// isLate=true 时 lineage 标记晚到（PER-019 异步流水）。producer 统一为
    /// unified.perception（组合能力名 UniPerception，2026-10-06 命名裁决）。</summary>
    public SlowConsultationOutcome Project(
        SlowConsultationRequest request, UniKernel kernel,
        DshSlowFetchResult fetched, bool isLate = false)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fetched);
        if (fetched.Status is not (SlowConsultationStatus.Succeeded or SlowConsultationStatus.Partial))
            return new(fetched.Status, false, 0, fetched.Diagnostic, IsLate: isLate);
        var lineage = new List<string>
        {
            "real-model",
            "capture:" + request.CaptureId,
            "request:" + request.RequestId,
            "reason:" + request.Reason,
        };
        if (request.FastBasis is { } basis)
            lineage.Add("basis:" + basis.CaptureId);
        if (isLate)
            lineage.Add("late");
        var projected = 0;
        foreach (var (subject, value) in fetched.Proposals)
        {
            kernel.Process(new ObservationProposal(
                new ObservationClaim(subject, value),
                IngressKind.Observation,
                ObservationContext.External,
                new Provenance("uni.perception", request.CaptureTimestamp,
                    $"scope:slow:{request.ObservationCycleId}", lineage)));
            projected++;
        }
        return new(fetched.Status, true, projected, fetched.Diagnostic, IsLate: isLate);
    }

    /// <summary>同步便利路径（测试与遗留调用）：fetch + project。</summary>
    public SlowConsultationOutcome Consult(
        SlowConsultationRequest request, UniKernel kernel,
        bool effectCritical = false, TimeSpan? boundedWait = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        var sessionCorrelation = string.IsNullOrWhiteSpace(kernel.RunId)
            ? request.BuyerRef
            : kernel.RunId;
        var fetched = FetchAsync(request, sessionCorrelation, boundedWait).GetAwaiter().GetResult();
        return Project(request, kernel, fetched);
    }
}
