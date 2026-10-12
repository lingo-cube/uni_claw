using UniClaw.Kernel.Evidence;
using Xunit;

namespace UniClaw.Kernel.Tests.Evidence;

/// <summary>
/// PROTOCOL-FIX-001 / ADR-0048 — Observation Origin 语义锚定：
/// ① canonical origin 由账本从 Context 机械推导（判定权单一收口）；
/// ② producer 自报仅诊断，不进 record、不进 EvidenceId；
/// ③ S5：MaterialEffect 门槛要求推导的 PostActionSelf（RuntimeAssurance 侧）。
/// 运行待网络恢复（本 change 事后核对项）；文件本身保证编译期语义在场。
/// </summary>
public sealed class OriginSemanticsTests
{
    // CaptureTime 固定：EvidenceId 是内容哈希（含 CaptureTime），断言
    // "同内容 → 同 id"必须排除时间漂移（UtcNow 两次调用毫秒差即翻 id）。
    private static readonly DateTimeOffset FixedCaptureTime =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Provenance ValidProvenance(string producer = "test.producer") => new(
        Producer: producer,
        CaptureTime: FixedCaptureTime,
        Scope: "scope:test",
        TransformationLineage: new[] { "test:direct" });

    private static ObservationProposal Proposal(
        ObservationContext context,
        ObservationOrigin? reported = null) => new(
        new ObservationClaim("wifi-toggle", "on"),
        IngressKind.Observation,
        context,
        ValidProvenance(),
        Origin: reported ?? ObservationOrigin.External);

    [Fact]
    public void CanonicalOrigin_IsDerivedFromContext_PostAction()
    {
        var ledger = new EvidenceLedger();
        var (_, record) = ledger.Admit(Proposal(ObservationContext.PostActionEffectFlow));
        Assert.NotNull(record);
        Assert.Equal(ObservationOrigin.PostActionSelf, record!.Origin);
    }

    [Fact]
    public void CanonicalOrigin_IsDerivedFromContext_External()
    {
        var ledger = new EvidenceLedger();
        var (_, record) = ledger.Admit(Proposal(ObservationContext.External));
        Assert.NotNull(record);
        Assert.Equal(ObservationOrigin.External, record!.Origin);
    }

    [Fact]
    public void ReportedOrigin_IsDiagnosticOnly_DoesNotAffectCanonicalValue()
    {
        // 自报 External + Context=PostActionEffectFlow → canonical 仍 PostActionSelf（推导胜出）
        var ledger = new EvidenceLedger();
        var (_, record) = ledger.Admit(Proposal(
            ObservationContext.PostActionEffectFlow,
            reported: ObservationOrigin.External));
        Assert.NotNull(record);
        Assert.Equal(ObservationOrigin.PostActionSelf, record!.Origin);
    }

    [Fact]
    public void ReportedOrigin_IsDiagnosticOnly_DoesNotAffectEvidenceId()
    {
        var ledger = new EvidenceLedger();
        var (_, a) = ledger.Admit(Proposal(ObservationContext.External, reported: ObservationOrigin.External));
        var (_, b) = ledger.Admit(Proposal(ObservationContext.External, reported: ObservationOrigin.PostActionSelf));
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(a!.EvidenceId, b!.EvidenceId); // 同内容（Context 相同）→ 同 id；自报不参与
        Assert.Same(a, b); // 且幂等复用同一实例
    }
}
