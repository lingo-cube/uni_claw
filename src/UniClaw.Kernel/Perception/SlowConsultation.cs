using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.Fusion;

namespace UniClaw.Kernel.Perception;

/// <summary>
/// AGT-009 — 公开 Slow 咨询结果的封闭状态词汇。不是 Effect 授权：任何状态下
/// 本缝都不产生 dispatch 路径（结构性保证，无对应代码）。
/// </summary>
public enum SlowConsultationStatus
{
    /// <summary>结果完整并已投影进 P2。</summary>
    Succeeded,

    /// <summary>结果部分有效（部分 proposal 被 P2 拒绝或 realization 报 Partial）。</summary>
    Partial,

    /// <summary>有界等待内未取得结果（回到 Defer/Unknown 语义，零投影）。</summary>
    TimedOut,

    /// <summary>能力/binding 未配置（零投影；诚实缺能力，不静默降级）。</summary>
    NotConfigured,

    /// <summary>realization 失败或重复 attempt（零投影）。</summary>
    Rejected,

    /// <summary>请求字段不满足最小相关性要求（零投影，realization 不被调用）。</summary>
    Invalid,
}

/// <summary>
/// AGT-009 — Host 侧 Slow 咨询请求（公开形状 FROZEN）。只携带相关性与
/// bounded context 的输入；不携带 expected value 或模型提示。
/// </summary>
public sealed record SlowConsultationRequest(
    string RequestId,
    string Target,
    string ClaimSubject,
    string ClaimField,
    bool RequiresRawArtifact,
    string BuyerRef,
    string Reason,
    string CaptureId,
    DateTimeOffset CaptureTimestamp,
    string ObservationCycleId,
    IReadOnlyList<string> EvidenceIds,
    byte[]? RawArtifact = null,
    FastTextBasis? FastBasis = null);

/// <summary>
/// AGT-009 — Host 侧 Slow 咨询结果：状态 + P2 admission 摘要。ProjectedProposals
/// 只统计经 SlowResultProjector 进入既有 P2 的 proposal 数；单一 confidence
/// 不授权任何动作（evidence only）。
/// </summary>
public sealed record SlowConsultationOutcome(
    SlowConsultationStatus Status,
    bool Admitted,
    int ProjectedProposals,
    string? Diagnostic,
    bool IsLate);

/// <summary>
/// AGT-009 — 公开 Slow 组合缝（Product Host 买方，ADR-0030）。复用内部
/// SlowPerceptionOrchestrator + SlowResultProjector（不建第二套 Slow
/// orchestration）；结果唯一 ingress 是既有 P2。本类型不存在任何通往 Effect
/// dispatch 的成员——Timeout/NotConfigured/Rejected → Defer/Unknown 语义，
/// 由调用方继续有界流程。
/// </summary>
public sealed class SlowConsultation
{
    private readonly SlowPerceptionOrchestrator _orchestrator;
    private readonly SlowModelManagement _models;

    /// <summary>测试 seam（Kernel.Tests 注入 deterministic replay）。</summary>
    internal SlowConsultation(SlowPerceptionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _models = orchestrator.Realization.Models;
    }

    /// <summary>Product 缺省组合：deterministic realization + 缺省 replay bindings。</summary>
    public SlowConsultation()
        : this(new SlowPerceptionOrchestrator(
            new EphemeralAttemptLedger(),
            new DeterministicSlowRealization(SlowReplayProfiles.CreateDefault())))
    {
    }

    /// <summary>
    /// 构建内部 SlowPerceptionRequest 并执行；结果只经 SlowResultProjector 投影。
    /// LogicalProfile：visual 当且仅当 RequiresRawArtifact 且 RawArtifact 非 null，
    /// 否则 text（视觉能力必须显式配置且携带真实 artifact）。binding 不可解析 →
    /// NotConfigured（零投影，realization 不被调用）。
    /// </summary>
    public SlowConsultationOutcome Consult(
        SlowConsultationRequest request, UniKernel kernel,
        bool effectCritical = false, TimeSpan? boundedWait = null,
        FastTextBasis? fastBasis = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        if (request is null || !HasRequiredFields(request))
            return new(SlowConsultationStatus.Invalid, false, 0, "invalid consultation request", IsLate: false);

        var profile = request.RequiresRawArtifact && request.RawArtifact is not null
            ? LogicalProfileId.Visual
            : LogicalProfileId.Text;
        fastBasis ??= request.FastBasis;
        var resolution = _models.Resolve(profile);
        if (!resolution.IsResolved)
            return new(SlowConsultationStatus.NotConfigured, false, 0,
                resolution.Diagnostic, IsLate: false);
        if (profile == LogicalProfileId.Text)
        {
            var gate = SlowTextGate.Evaluate(
                new FusionCapture(request.CaptureId,
                    string.IsNullOrWhiteSpace(kernel.RunId) ? request.BuyerRef : kernel.RunId,
                    request.ObservationCycleId, request.CaptureTimestamp), fastBasis);
            if (!gate.Eligible)
                return new(SlowConsultationStatus.Rejected, false, 0, gate.Diagnostic, false);
        }

        var claim = new RequiredClaim(request.ClaimSubject, request.ClaimField);
        // Session correlation 用当前 run；run 未激活（host 直接咨询）时退回
        // BuyerRef——字段必须非空，capture 相关性不因此伪造 run 身份。
        var sessionCorrelation = string.IsNullOrWhiteSpace(kernel.RunId) ? request.BuyerRef : kernel.RunId;
        var capture = new FusionCapture(
            request.CaptureId, sessionCorrelation, request.ObservationCycleId, request.CaptureTimestamp);
        var context = EvidenceContextBuilder.Build(claim,
            semantic: new SemanticReasoningContext(claim, request.EvidenceIds, Reason: request.Reason));
        if (!context.IsValid)
            return new(SlowConsultationStatus.Invalid, false, 0,
                "bounded evidence context is insufficient", IsLate: false);

        var internalRequest = new SlowPerceptionRequest(
            request.RequestId,
            new SlowAttemptKey(request.Target, request.ObservationCycleId, claim, profile),
            claim, profile, request.BuyerRef, request.Reason, capture, context,
            profile == LogicalProfileId.Visual ? new RawArtifactRef(request.CaptureId) : null,
            resolution.Binding,
            boundedWait,
            fastBasis);

        var orchestration = _orchestrator.Execute(internalRequest, effectCritical, boundedWait);
        return Project(orchestration, kernel);
    }

    /// <summary>测试/未来 late-ingest seam：接收已完成的迟到结果（不重消耗 attempt）；
    /// 仍只经 projector 进 P2，且 IsLate 恒 true（晚到不追溯授权）。</summary>
    internal SlowConsultationOutcome AcceptLate(SlowResult result, UniKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        return Project(_orchestrator.AcceptLate(result), kernel);
    }

    private SlowConsultationOutcome Project(SlowOrchestrationResult orchestration, UniKernel kernel)
    {
        var result = orchestration.Result;
        var isLate = orchestration.IsLate;
        if (orchestration.WaitDisposition is SlowWaitDisposition.TimedOut or SlowWaitDisposition.Cancelled
            || result.Status is SlowExecutionStatus.Timeout or SlowExecutionStatus.Cancelled)
            return new(SlowConsultationStatus.TimedOut, false, 0,
                result.Diagnostic ?? orchestration.WaitDisposition.ToString(), isLate);
        if (result.Status is SlowExecutionStatus.ModelUnavailable or SlowExecutionStatus.UnsupportedCapability)
            return new(SlowConsultationStatus.NotConfigured, false, 0, result.Diagnostic, isLate);
        if (!result.IsSuccessful)
            return new(SlowConsultationStatus.Rejected, false, 0,
                result.Diagnostic ?? result.Status.ToString(), isLate);

        // 唯一 ingress：既有 SlowResultProjector → P2。晚到结果同样投影
        //（P2 决定新鲜度），但 IsLate 恒透传。
        var admission = SlowResultProjector.Project(result, kernel);
        var status = result.Status == SlowExecutionStatus.Partial
            ? SlowConsultationStatus.Partial
            : SlowConsultationStatus.Succeeded;
        return new(status, admission.Accepted, admission.ProposalCount, admission.RejectionReason, isLate);
    }

    private static bool HasRequiredFields(SlowConsultationRequest request) =>
        !string.IsNullOrWhiteSpace(request.RequestId)
        && !string.IsNullOrWhiteSpace(request.Target)
        && !string.IsNullOrWhiteSpace(request.ClaimSubject)
        && !string.IsNullOrWhiteSpace(request.ClaimField)
        && !string.IsNullOrWhiteSpace(request.BuyerRef)
        && !string.IsNullOrWhiteSpace(request.Reason)
        && !string.IsNullOrWhiteSpace(request.CaptureId)
        && !string.IsNullOrWhiteSpace(request.ObservationCycleId)
        && request.EvidenceIds is not null;
}
