using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// ING-006 验收 2/3/4/5a + Evidence identity 语义（N1-N6）。
/// 纯内存（level: DETERMINISTIC）。测试验证行为，不验证实现细节。
/// 注意 N 系列不测试、也不得暗示伪造 Kind/Context 的可识别性（⑤b，
/// Deferred ⑦）——本片的判定门是语义归类门，不是真实性门。
/// </summary>
public sealed class ObservationIngressTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);

    private static ObservationProposal Proposal(
        string subject,
        string value,
        IngressKind kind = IngressKind.Observation,
        ObservationContext context = ObservationContext.External,
        string producer = "provider.scripts") =>
        new(new ObservationClaim(subject, value), kind, context,
            new Provenance(producer, T0, $"scope:{subject}",
                new[] { "raw://capture", "encode:v1" }));

    // ---- N1（验收 2）：未知 kind admission fail-closed -------------------

    [Fact]
    public void N1_UnknownKindIsRejectedWithZeroCanonicalSideEffects()
    {
        var ledger = new EvidenceLedger();

        var (admission, record) = ledger.Admit(
            Proposal("screen.home", "idle", kind: (IngressKind)999));

        Assert.Equal(AdmissionDecision.Rejected, admission.Decision);
        Assert.Equal("kind-recognized", admission.RejectionReason);
        Assert.Null(record);
        Assert.Empty(ledger.CanonicalRecords);
        Assert.Single(ledger.AdmissionLog);   // 拒绝留痕，零 canonical 副作用
    }

    [Fact]
    public void N7_UnknownContextIsRejectedSymmetrically()
    {
        // Review F2：context 与 kind 是同一 cast 攻击面，对称 fail-closed
        var ledger = new EvidenceLedger();

        var (admission, record) = ledger.Admit(
            Proposal("screen.home", "idle", context: (ObservationContext)999));

        Assert.Equal(AdmissionDecision.Rejected, admission.Decision);
        Assert.Equal("context-recognized", admission.RejectionReason);
        Assert.Null(record);
        Assert.Empty(ledger.CanonicalRecords);
    }

    // ---- N4（验收 3 强形式）：AttemptReport 定义性非 world-relevant -------

    [Fact]
    public void N4_AttemptReportIsNeverWorldRelevantEvenWhenSubjectInScope()
    {
        var world = new WorldModel(new HashSet<string> { "screen.home" });
        var kernel = new UniKernel(new EvidenceLedger(), world);

        // subject 故意落在 relevance scope 内——kind 门必须压过 scope 匹配
        var result = kernel.Process(Proposal("screen.home", "delivered",
            kind: IngressKind.AttemptReport,
            context: ObservationContext.PostActionEffectFlow));

        Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);  // admitted（结构完整）
        Assert.False(result.Relevance!.IsRelevant);                           // 但定义性非 world-relevant
        Assert.Equal("attempt-report-not-world-relevant", result.Relevance.Reason);
        Assert.Null(result.ResultingRevision);                                // 零 belief 变化
    }

    // ---- N5（D5 Evidence identity）：异 kind 不得复用同一 EvidenceId ------

    [Fact]
    public void N5_DifferentKindYieldsDifferentEvidenceId()
    {
        var ledger = new EvidenceLedger();

        var (a1, r1) = ledger.Admit(Proposal("screen.home", "idle", kind: IngressKind.Observation));
        var (a2, r2) = ledger.Admit(Proposal("screen.home", "idle", kind: IngressKind.AttemptReport));

        Assert.Equal(AdmissionDecision.Accepted, a1.Decision);
        Assert.Equal(AdmissionDecision.Accepted, a2.Decision);
        Assert.NotEqual(r1!.EvidenceId, r2!.EvidenceId);
        Assert.Equal(2, ledger.CanonicalRecords.Count);

        // 同语义内容重放仍幂等（第三条同第一条 → 复用）
        var (a3, r3) = ledger.Admit(Proposal("screen.home", "idle", kind: IngressKind.Observation));
        Assert.Equal(r1.EvidenceId, a3.EvidenceId);
        Assert.Same(r1, r3);
    }

    // ---- N2 / N3 / N6（验收 4 / 5a）：MaterialEffect policy 迁移 ----------

    [Fact]
    public void N2_AttemptReportWithPostActionContextNeverSatisfiesMaterialEffect()
    {
        // ⑤a：kind 门压过 context 门——即使 context 声明 PostActionEffectFlow
        var status = EvaluateMaterialEffect(
            kind: IngressKind.AttemptReport,
            context: ObservationContext.PostActionEffectFlow,
            producer: "effect.boundary");
        Assert.False(status.Satisfied);
    }

    [Fact]
    public void N3_CapabilityProducedPostActionObservationSatisfiesMaterialEffect()
    {
        // 原 producer-prefix 误拒情形消除：实际 producer 是 capability，
        // context 显式声明 post-action effect flow
        var status = EvaluateMaterialEffect(
            kind: IngressKind.Observation,
            context: ObservationContext.PostActionEffectFlow,
            producer: "vision.capability");
        Assert.True(status.Satisfied);
    }

    [Fact]
    public void N6_ExternalContextObservationDoesNotSatisfyMaterialEffect()
    {
        // 判定门是 context 而非 producer 前缀：producer 恰为 effect.boundary
        // 但 context=External → 不满足（前缀判别路径已删除的反向证明）
        var status = EvaluateMaterialEffect(
            kind: IngressKind.Observation,
            context: ObservationContext.External,
            producer: "effect.boundary");
        Assert.False(status.Satisfied);
    }

    /// <summary>
    /// 最小可判定环境：无关 subject 合法观察建立 current revision（保证
    /// current 非空），被测观察随后进入；单条 mandatory MaterialEffect
    /// obligation 期望 screen.home=active。
    /// </summary>
    private static ObligationStatus EvaluateMaterialEffect(
        IngressKind kind,
        ObservationContext context,
        string producer)
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "screen.home", "screen.header" });
        var kernel = new UniKernel(ledger, world);
        kernel.Process(Proposal("screen.header", "ok"));                     // rev-1（current 存在）
        kernel.Process(Proposal("screen.home", "active",                     // 被测观察
            kind: kind, context: context, producer: producer));

        var view = new ExecutionContractView(
            "c1", "verify-home-screen",
            new HashSet<string> { "screen.home" },
            new HashSet<string> { "tap" },
            new HashSet<string>(),
            new[] { "material" });
        var obligations = new ProofObligationState(new[]
        {
            new RunObligation("mat", RunObligationKind.MaterialEffect, "screen.home", "active", Mandatory: true),
        });

        var statuses = new RuntimeAssurance(new FreshnessDoubles.Satisfying()).EvaluateObligations(
            obligations,
            world.DeriveOutcomeAssuranceView(obligations.Obligations.Select(o => o.Subject)),
            ledger.CanonicalRecords);
        return statuses.Single(s => s.ObligationId == "mat");
    }
}
