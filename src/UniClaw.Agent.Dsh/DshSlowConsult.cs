using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.Fusion;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// AGT-017 — Slow 感知 live 桥：把 Kernel 公开 Slow 缝（SlowConsultationRequest/
/// SlowConsultationOutcome，AGT-009 FROZEN 形状）接到 DSH peer 的 /slow 端点。
/// 只用公开类型：不改 Kernel internals，不触碰冻结的 InternalsVisibleTo 纪律。
/// 异步保证：有界等待强制（CTS）；超时 → TimedOut 零投影，周期永不超 bound；
/// 服务端/传输错误 → Rejected 带原始诊断，不静默降级、不伪造结果。
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

    public SlowConsultationOutcome Consult(
        SlowConsultationRequest request, UniKernel kernel,
        bool effectCritical = false, TimeSpan? boundedWait = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        if (request is null || string.IsNullOrWhiteSpace(request.RequestId)
            || string.IsNullOrWhiteSpace(request.ClaimSubject))
            return new(SlowConsultationStatus.Invalid, false, 0, "invalid slow request", IsLate: false);

        var visual = request.RequiresRawArtifact && request.RawArtifact is { Length: > 0 };
        var model = visual ? _visualModel : _textModel;
        if (model is null)
            return new(SlowConsultationStatus.NotConfigured, false, 0,
                visual ? "visual slow model not configured" : "text slow model not configured", IsLate: false);

        // 设计对齐（runtime-capability-integration-seams §Fast+SlowText）：Fast 与
        // Slow Text 是「Text Semantic Perception」组合能力，依赖链固定 Fast→Slow
        // Text——缺可用/对齐的 fast 前置时不得调用（Kernel 侧 SlowTextGate 同款
        // 执法，AGT-009；桥不复制规则，直接复用 public 门）。Visual 不经此门。
        if (!visual)
        {
            var sessionCorrelation = string.IsNullOrWhiteSpace(kernel.RunId)
                ? request.BuyerRef
                : kernel.RunId;
            var gate = SlowTextGate.Evaluate(
                new FusionCapture(request.CaptureId, sessionCorrelation,
                    request.ObservationCycleId, request.CaptureTimestamp),
                request.FastBasis);
            if (!gate.Eligible)
                return new(SlowConsultationStatus.Rejected, false, 0,
                    $"text-slow-gate:{gate.Diagnostic}", IsLate: false);
        }

        var budget = boundedWait ?? DefaultBoundedWait;
        if (budget <= TimeSpan.Zero)
            return new(SlowConsultationStatus.TimedOut, false, 0,
                "bounded wait exhausted before transport (effect-critical)", IsLate: false);

        DshSlowResponse response;
        try
        {
            using var cts = new CancellationTokenSource(budget);
            response = _transport(
                request.RequestId, BuildPrompt(request), model,
                visual ? request.RawArtifact : null, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return new(SlowConsultationStatus.TimedOut, false, 0,
                $"bounded-wait-exhausted:{(int)budget.TotalMilliseconds}ms", IsLate: false);
        }
        catch (Exception error)
        {
            return new(SlowConsultationStatus.Rejected, false, 0,
                $"slow-transport-failed:{error.Message}", IsLate: false);
        }

        if (!string.IsNullOrWhiteSpace(response.Error))
            return new(SlowConsultationStatus.Rejected, false, 0,
                $"slow-endpoint-error:{response.Error}:{response.Diagnostic}", IsLate: false);
        if (string.IsNullOrWhiteSpace(response.Text))
            return new(SlowConsultationStatus.Rejected, false, 0,
                "slow-endpoint-empty-text", IsLate: false);

        try
        {
            using var json = JsonDocument.Parse(response.Text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
            });
            var root = json.RootElement;
            var status = root.TryGetProperty("status", out var statusElement)
                && string.Equals(statusElement.GetString(), "Succeeded", StringComparison.Ordinal);

            if (root.TryGetProperty("proposals", out var proposals)
                && proposals.ValueKind == JsonValueKind.Array)
            {
                var projected = 0;
                foreach (var entry in proposals.EnumerateArray())
                {
                    if (!entry.TryGetProperty("subject", out var subjectElement)
                        || !entry.TryGetProperty("value", out var valueElement))
                        continue;
                    var subject = subjectElement.GetString();
                    var value = valueElement.GetString();
                    if (string.IsNullOrWhiteSpace(subject) || value is null)
                        continue;
                    var proposal = new ObservationProposal(
                        new ObservationClaim(subject, value),
                        IngressKind.Observation,
                        ObservationContext.External,
                        new Provenance(
                            "dsh.live.slow",
                            request.CaptureTimestamp,
                            $"scope:slow:{request.ObservationCycleId}",
                            new[]
                            {
                                "real-model",
                                "capture:" + request.CaptureId,
                                "request:" + request.RequestId,
                                "reason:" + request.Reason,
                            }));
                    kernel.Process(proposal);
                    projected++;
                }
                return new(
                    status ? SlowConsultationStatus.Succeeded : SlowConsultationStatus.Partial,
                    true, projected,
                    status ? null : "slow-model-reported-non-success", IsLate: false);
            }
            return new(
                status ? SlowConsultationStatus.Succeeded : SlowConsultationStatus.Partial,
                true, 0, status ? null : "slow-model-reported-non-success", IsLate: false);
        }
        catch (JsonException error)
        {
            return new(SlowConsultationStatus.Rejected, false, 0,
                $"slow-response-not-json:{error.Message}", IsLate: false);
        }
    }
}
