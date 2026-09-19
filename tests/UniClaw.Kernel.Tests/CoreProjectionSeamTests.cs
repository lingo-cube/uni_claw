using UniClaw.Core;
using UniClaw.Kernel.Core;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Tests.Perception;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

using CoreEvidence = UniClaw.Core.EvidenceRecord;
using CoreSlice = UniClaw.Core.Slice;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// CORE-002 第二阶段验收 —— 现有手机/UI 滚动—点击 realization 接到
/// UniClaw.Kernel realization → UniClaw.Core 候选语义 seam。
/// tracer 语义路径（qspec §7）：观察 → Evidence → Page Segment → Slice S1
/// → 滚动（Effect/Attempt/Binding，attempt 时间独立）→ Slice S2 → 元素
/// Claim → tap Effect → TargetBinding → Attempt（实际点击时间）→ 点击后
/// 新 Evidence → Claim 再评价。全部确定性（level: DETERMINISTIC）：corpus
/// 管线复用 RealAssetEntityModelTests / ControlReferencePolicyTests 已验证
/// 组合；内存管线复用 DispatchSeamSpecificationTests 模式；零 wall-clock /
/// random / GUID。
/// 只证明行为：realization 语义可无损投影到 Core 候选记录且 Core 不变量
/// 成立；不证明最终字段、继承树、公共 API 或第二种非 UI realization。
/// </summary>
public sealed class CoreProjectionSeamTests
{
    private static readonly CorpusManifest Corpus = CorpusManifest.Load();

    // corpus capture time 全集 ≤ 2026-09-09T10:30（scenarios.json 真值）；
    // act 时间均取其后 → Attempt 时间与 Slice 观察时间（FreshnessBasis.AsOf）
    // 严格可分。
    private static readonly DateTimeOffset ScrollActTime = new(2026, 9, 9, 10, 31, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TapActTime = new(2026, 9, 9, 10, 33, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PostClickObserveTime = new(2026, 9, 9, 10, 40, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset T0 = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 9, 10, 5, 0, TimeSpan.Zero);

    private static readonly Lazy<IReadOnlySet<string>> SignatureScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.PageSignatureSubject));

    private static readonly Lazy<IReadOnlySet<string>> DialogScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.DialogTitleSubject));

    [Fact]
    public void Ui_contract_projects_requirement_and_criteria_without_inventing_permission()
    {
        var contract = new ExecutionContractView(
            Version: "ui-c1",
            Objective: "scroll-the-list-then-tap-a-row",
            Scope: new HashSet<string> { "perception.page.signature" },
            AllowedEffects: new HashSet<string> { "scroll", "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "row-tapped" });

        var clauses = CoreSemanticProjection.ProjectClauses(
            contract,
            DateTimeOffset.Parse("2026-09-19T10:00:00Z"));

        Assert.Collection(
            clauses,
            objective =>
            {
                Assert.Equal(ClauseKind.Requirement, objective.Kind);
                Assert.Equal(contract.Objective, objective.Statement);
            },
            criterion =>
            {
                Assert.Equal(ClauseKind.Criterion, criterion.Kind);
                Assert.Equal("row-tapped", criterion.Statement);
            });
        Assert.DoesNotContain(clauses, clause => clause.Kind == ClauseKind.Permission);
    }

    private static IReadOnlySet<string> ScopesWithSubject(string subject) => Corpus.Scenarios
        .Where(s => s.Observations.Any(o => o.Subject == subject))
        .Select(s => "artifact:" + Corpus.Artifact(s.ScenarioId).ArtifactId)
        .ToHashSet();

    // ---- 测试替身 ---------------------------------------------------------

    /// <summary>tracer 编排 policy：第一轮 scroll（page signature 字符串通道），
    /// 之后 tap（descriptor 接地）。零状态除轮次序数。</summary>
    private sealed class ScrollThenTapPolicy : IControlPolicy
    {
        private int _round;

        public ControlDecision Decide(ControlInputs inputs) =>
            ++_round == 1
                ? new ControlDecision(ControlIntentKind.Act, "scroll", "perception.page.signature")
                : new ControlDecision(ControlIntentKind.Act, "tap", "TextView:Item 03");
    }

    private sealed class ScriptedPolicy : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, "tap", "screen.home");
    }

    /// <summary>确定性 queued driver：outcome + 下游确认时间序列，机械投递。</summary>
    private sealed class QueuedDriver(params (DispatchOutcome Outcome, DateTimeOffset At, string? Reason)[] results)
        : IEffectDriver
    {
        private readonly Queue<DispatchResult> _results =
            new(results.Select(r => new DispatchResult(r.Outcome, $"scripted:{r.Outcome}", r.At, r.Reason)));

        public DispatchResult Deliver(DispatchRequest request) => _results.Dequeue();
    }

    private static QueuedDriver OkAt(params DateTimeOffset[] times) =>
        new(times.Select(t => (DispatchOutcome.DeliveryCompleted, t, (string?)null)).ToArray());

    // ---- corpus 组装（RealAssetEntityModelTests / ControlReferencePolicyTests 模式）----

    private static (UniKernel Kernel, WorldModel World, EvidenceLedger Ledger) NewCorpusKernel()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(
            Corpus.SubjectScope,
            new CorpusAssociationStrategy(SignatureScopes.Value, DialogScopes.Value),
            new OwnedCorpusObservationStrategy(),
            new CorpusContinuityStrategy());
        var kernel = new UniKernel(
            ledger, world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(new ScrollThenTapPolicy()),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(OkAt(ScrollActTime, TapActTime)));
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: "scroll-the-list-then-tap-a-row",
            Scope: new HashSet<string> { "perception.page.signature" },
            AllowedEffects: new HashSet<string> { "scroll", "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "row-tapped" }));
        return (kernel, world, ledger);
    }

    private static void Observe(UniKernel kernel, string scenarioId,
        ObservationContext context = ObservationContext.External,
        TransitionContext? transition = null)
    {
        var perception = new FastPerception(
            "perception.corpus", new CorpusFastPerception(Corpus, scenarioId));
        foreach (var proposal in perception.Observe(Corpus.Artifact(scenarioId), context))
            kernel.Process(proposal, transition);
    }

    /// <summary>点击后新 Evidence（ControlReferencePolicyTests.AdvanceRevision 同法）：
    /// 真实 corpus subject/value 原值复述 + 唯一 capture time → 新 evidence id
    /// → 新 revision（同值 Reaffirm，旧依据不被改写）。</summary>
    private static void PostClickObservation(UniKernel kernel, string scenarioId)
    {
        var artifact = Corpus.Artifact(scenarioId);
        var (subject, value) = Corpus.Scenario(scenarioId).Observations[0];
        kernel.Process(new ObservationProposal(
            new ObservationClaim(subject, value),
            IngressKind.Observation, ObservationContext.PostActionEffectFlow,
            new Provenance(
                Producer: "perception.corpus",
                CaptureTime: PostClickObserveTime,
                Scope: $"artifact:{artifact.ArtifactId}",
                TransformationLineage: new[] { "fast:perception.corpus", $"artifact:{artifact.ArtifactId}" })));
    }

    // ---- 内存管线组装（DispatchSeamSpecificationTests 模式）----------------

    private static (UniKernel Kernel, WorldModel World, EffectBoundary Effects) NewMemoryKernel(
        IEffectDriver driver, IFreshnessEvaluator? freshness = null)
    {
        var world = new WorldModel(new HashSet<string> { "screen.home" }, new SeedContainerAssociationStrategy());
        var effects = new EffectBoundary(driver);
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(new ScriptedPolicy()),
            new RuntimeAssurance(freshness ?? new FreshnessDoubles.Satisfying()), effects);
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: "verify-home-screen",
            Scope: new HashSet<string> { "screen.home" },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string> { "swipe" },
            ProofCriteria: new[] { "home-screen-observed" }));
        kernel.Process(Observation("screen.home", "idle", T0));
        return (kernel, world, effects);
    }

    private static ObservationProposal Observation(string subject, string value, DateTimeOffset captureTime) =>
        new(new ObservationClaim(subject, value), IngressKind.Observation, ObservationContext.External,
            new Provenance("provider.scripts", captureTime, $"scope:{subject}",
                new[] { "raw://capture", "encode:v1" }));

    private static ControlIntent ActOnce(UniKernel kernel, WorldModel world) =>
        kernel.SelectIntent(kernel.DeriveSlice(world.Current!.Containers.Single().Identity.ContainerId));

    // ---- P1：滚动—点击全 tracer 投影到 Core seam ---------------------------

    [Fact]
    public void ScrollThenTap_RealizationProjectsOntoCore_WithFixedHistoryAndSeparateAttemptTime()
    {
        var (kernel, world, ledger) = NewCorpusKernel();

        // 观察 v1 → admitted Evidence → Page Segment → Slice S1
        Observe(kernel, "scroll01-v1");
        var rev1 = world.Current!;
        var pageContainerId = Assert.Single(rev1.Containers).Identity.ContainerId;
        var segment = CoreSemanticProjection.ProjectSegment(pageContainerId, "ui.page");
        var s1Kernel = kernel.DeriveSlice(pageContainerId);
        var s1 = CoreSemanticProjection.ProjectSlice(s1Kernel, rev1);

        // Evidence 投影：真实 corpus evidence（来源/时间/链/上下文）
        var evidenceId = Assert.Single(ledger.CanonicalRecords.Values,
            r => r.Provenance.Scope == $"artifact:{Corpus.Artifact("scroll01-v1").ArtifactId}"
                && r.Claim.Subject == "perception.page.signature").EvidenceId;
        var coreEvidence = CoreSemanticProjection.ProjectEvidence(ledger.CanonicalRecords[evidenceId]);
        Assert.Equal("perception.corpus", coreEvidence.Source);
        Assert.Equal(new CoreId("perception.page.signature"), coreEvidence.SubjectId);
        Assert.Contains(s1.ObservationEvidenceIds, id => id == new CoreId(evidenceId));

        // 滚动前元素 Claim（行文本真值 Item 01）
        var claimV1 = CoreSemanticProjection.ProjectClaim(
            "ui.text.row_title", rev1.WorldState["ui.text.row_title"], conflicted: false);
        Assert.Equal("Item 01", claimV1.Value);

        // 滚动 Effect → TargetBinding（basis = 建立绑定时的 S1）→ Attempt
        var scrollIntent = kernel.SelectIntent(kernel.DeriveSlice(pageContainerId));
        Assert.Equal("scroll", scrollIntent.EffectClass);
        var scrollAct = kernel.Act(scrollIntent, new CandidateBinding(
            "perception.page.signature", "SCROLL_01 — Long List", rev1.RevisionId));
        Assert.NotNull(scrollAct.Receipt);
        Assert.NotNull(scrollAct.Binding!.Canonical);

        var scrollEffect = CoreSemanticProjection.ProjectEffect(scrollAct.Binding.Canonical!);
        var scrollBinding = CoreSemanticProjection.ProjectTargetBinding(
            scrollAct.Binding.Canonical!, CoreSemanticProjection.SliceId(s1Kernel),
            segment.Id, world.Current!.RevisionId, scrollAct.Gate);
        var scrollAttempt = CoreSemanticProjection.ProjectAttempt(
            scrollAct.Receipt!, scrollEffect.Id,
            new CoreId(scrollAct.AttemptEvidenceReflux!.Admission.EvidenceId!));

        Assert.Equal("scroll", scrollEffect.Operation);
        Assert.Equal("perception.page.signature", scrollEffect.SubjectId.Value);
        Assert.Equal(BindingDisposition.Canonical, scrollBinding.Disposition);
        Assert.True(CoreInvariants.CanDispatch(scrollBinding));
        Assert.True(CoreInvariants.IsHistoricalBasisStable(scrollBinding, CoreSemanticProjection.SliceId(s1Kernel)));
        Assert.Equal(ScrollActTime, scrollAttempt.StartedAt);
        Assert.True(scrollAttempt.StartedAt > s1.ObservedAt);            // Attempt 时间独立于观察时间
        Assert.Equal(DeliveryOutcome.Completed, scrollAttempt.Delivery);
        Assert.True(CoreInvariants.IsTerminalDelivery(scrollAttempt));

        // 滚动后 v2（带 attempt transition prior）→ 同一 Segment 的 Slice S2 + 行 Revise
        Observe(kernel, "scroll01-v2", ObservationContext.PostActionEffectFlow, scrollAct.Transition);
        var rev2 = world.Current!;
        Assert.Equal(pageContainerId, Assert.Single(rev2.Containers).Identity.ContainerId);
        var s2Kernel = kernel.DeriveSlice(pageContainerId);
        var s2 = CoreSemanticProjection.ProjectSlice(s2Kernel, rev2);

        Assert.Equal(s1.SegmentId, s2.SegmentId);                          // 同一 Segment 的多个 Slice
        Assert.NotEqual(s1.Id, s2.Id);
        Assert.NotEqual(s1.ObservedRecordIds, s2.ObservedRecordIds);       // occurrence 景观重铸（revision-local）

        // 多 Slice 历史：v1 claim 依据保留在 v2 claim 痕迹链（不被 latest 改写）
        var claimV2 = CoreSemanticProjection.ProjectClaim(
            "ui.text.row_title", rev2.WorldState["ui.text.row_title"], conflicted: false);
        Assert.Equal("Item 02", claimV2.Value);
        Assert.Contains(claimV1.EvidenceBasis[0], claimV2.EvidenceBasis);

        // 旧 binding 在新 revision 下 currency 掉 → Stale fail-closed，basis 不漂移
        var scrollBindingAfterScroll = CoreSemanticProjection.ProjectTargetBinding(
            scrollAct.Binding.Canonical!, CoreSemanticProjection.SliceId(s1Kernel),
            segment.Id, rev2.RevisionId);
        Assert.Equal(BindingDisposition.Stale, scrollBindingAfterScroll.Disposition);
        Assert.False(CoreInvariants.CanDispatch(scrollBindingAfterScroll));
        Assert.Equal(scrollBinding.BasisSliceId, scrollBindingAfterScroll.BasisSliceId);

        // 元素 Claim（点击目标真值）→ tap Effect → TargetBinding（basis = S2）→ Attempt（点击时间）
        var elementClaim = CoreSemanticProjection.ProjectClaim(
            "ui.text.row_title2", rev2.WorldState["ui.text.row_title2"], conflicted: false);
        Assert.Equal("Item 03", elementClaim.Value);

        var tapIntent = kernel.SelectIntent(kernel.DeriveSlice(pageContainerId));
        Assert.Equal("tap", tapIntent.EffectClass);
        var grounded = kernel.ActViaCurrentGrounding(tapIntent, new TargetDescriptor("TextView", "Item 03"));
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, grounded.View.Result);
        var tappedOccurrenceId = grounded.View.Candidates.Single().OccurrenceId;
        // S2 局部观察覆盖点击目标（元素在 bind-time slice 的景观内）
        Assert.Contains(new CoreId(tappedOccurrenceId), s2.ObservedRecordIds);
        var tapAct = grounded.Act!;
        Assert.NotNull(tapAct.Receipt);
        var tapCanonical = tapAct.Binding!.Canonical!;

        var tapEffect = CoreSemanticProjection.ProjectEffect(tapCanonical);
        var tapBinding = CoreSemanticProjection.ProjectTargetBinding(
            tapCanonical, CoreSemanticProjection.SliceId(s2Kernel),
            segment.Id, world.Current!.RevisionId, tapAct.Gate);
        var tapAttempt = CoreSemanticProjection.ProjectAttempt(
            tapAct.Receipt!, tapEffect.Id,
            new CoreId(tapAct.AttemptEvidenceReflux!.Admission.EvidenceId!));

        Assert.Equal("tap", tapEffect.Operation);
        Assert.Equal(tapCanonical.TargetOccurrenceId, tapEffect.SubjectId.Value);
        Assert.Equal(new CoreId(tapCanonical.BindingId), tapAttempt.BindingId); // receipt↔binding 方向实证
        Assert.Equal(TapActTime, tapAttempt.StartedAt);
        Assert.True(tapAttempt.StartedAt > s2.ObservedAt);                 // 点击时间独立于 S2 观察时间
        Assert.Equal(BindingDisposition.Canonical, tapBinding.Disposition);
        Assert.True(CoreInvariants.CanDispatch(tapBinding));
        Assert.Equal(CoreSemanticProjection.SliceId(s2Kernel), tapBinding.BasisSliceId);
        Assert.False(CoreInvariants.IsHistoricalBasisStable(tapBinding, CoreSemanticProjection.SliceId(s1Kernel)));

        // 点击后：receipt/attempt 不产生 World Result（reflux 是 kind-gated
        // irrelevant，无 revision、无 attempt.* World claim）
        var revisionAfterTap = world.Current!.RevisionId;
        Assert.Equal(rev2.RevisionId, revisionAfterTap);
        Assert.DoesNotContain(world.Current.WorldState.Keys,
            k => k.StartsWith("attempt.", StringComparison.Ordinal));
        Assert.Equal(DeliveryOutcome.Completed, tapAttempt.Delivery);

        // 点击后新 Evidence（真实 subject/value 复述 + 唯一 capture time）→
        // 新 revision → Claim 再评价：值不变、旧依据仍在（Reaffirm 语义）
        PostClickObservation(kernel, "scroll01-v2");
        var rev3 = world.Current!;
        Assert.NotEqual(rev2.RevisionId, rev3.RevisionId);
        var claimV3 = CoreSemanticProjection.ProjectClaim(
            "ui.text.row_title", rev3.WorldState["ui.text.row_title"], conflicted: false);
        Assert.Equal("Item 02", claimV3.Value);
        Assert.Contains(claimV2.EvidenceBasis[0], claimV3.EvidenceBasis);
        Assert.Equal(ClaimDisposition.Accepted, claimV3.Disposition);

        // tap binding 重新投影（同输入 + 新 current）：basis 不漂移、Stale fail-closed
        var tapBindingLater = CoreSemanticProjection.ProjectTargetBinding(
            tapCanonical, CoreSemanticProjection.SliceId(s2Kernel),
            segment.Id, rev3.RevisionId, tapAct.Gate);
        Assert.Equal(tapBinding.BasisSliceId, tapBindingLater.BasisSliceId);
        Assert.Equal(BindingDisposition.Stale, tapBindingLater.Disposition);
        Assert.False(CoreInvariants.CanDispatch(tapBindingLater));
        // 旧 binding 的历史依据仍可对齐它建立时的 Slice
        Assert.True(CoreInvariants.IsHistoricalBasisStable(tapBinding, CoreSemanticProjection.SliceId(s2Kernel)));
    }

    // ---- P2：Unknown 投递 fail-closed ---------------------------------------

    [Fact]
    public void UnknownDelivery_RealizationProjection_StaysUnknown_NoWorldResult()
    {
        var (kernel, world, effects) = NewMemoryKernel(new QueuedDriver(
            (DispatchOutcome.UnknownOutcome, T1, "timeout-killed")));
        var revisionBefore = world.Current!.RevisionId;
        var root = world.Current.Containers.Single().Identity.ContainerId;
        var sliceKernel = kernel.DeriveSlice(root);
        var slice = CoreSemanticProjection.ProjectSlice(sliceKernel, world.Current);

        var act = kernel.Act(ActOnce(kernel, world), new CandidateBinding(
            "screen.home", "idle", world.Current.RevisionId));
        Assert.Equal(DispatchOutcome.UnknownOutcome, act.Receipt!.Outcome);

        var effect = CoreSemanticProjection.ProjectEffect(act.Binding!.Canonical!);
        var binding = CoreSemanticProjection.ProjectTargetBinding(
            act.Binding.Canonical!, CoreSemanticProjection.SliceId(sliceKernel),
            new CoreId(root), world.Current!.RevisionId, act.Gate);
        var attempt = CoreSemanticProjection.ProjectAttempt(
            act.Receipt!, effect.Id, new CoreId(act.AttemptEvidenceReflux!.Admission.EvidenceId!));

        // Unknown 只表达投递未知：非终态、不产生 World Result、不改写 belief
        Assert.Equal(DeliveryOutcome.Unknown, attempt.Delivery);
        Assert.False(CoreInvariants.IsTerminalDelivery(attempt));
        Assert.Equal(DeliveryOutcome.Unknown, attempt.Delivery);
        Assert.Equal(revisionBefore, world.Current.RevisionId);   // attempt reflux 无 revision
        Assert.DoesNotContain(world.Current.WorldState.Keys,
            k => k.StartsWith("attempt.", StringComparison.Ordinal));

        // 投递三态映射不升级（原样映射，无猜测）
        Assert.Equal(DeliveryOutcome.Completed, CoreSemanticProjection.MapDelivery(DispatchOutcome.DeliveryCompleted));
        Assert.Equal(DeliveryOutcome.Failed, CoreSemanticProjection.MapDelivery(DispatchOutcome.DeliveryFailed));
        Assert.Equal(DeliveryOutcome.Unknown, CoreSemanticProjection.MapDelivery(DispatchOutcome.UnknownOutcome));

        // 防重发执法留在 realization EB（receipt log per binding），Core 候选
        // 不重复拥有该执法——结构对照：投影产物只含语义记录
        Assert.Single(effects.ReceiptLog);
    }

    // ---- P3：binding 有效性 / 拒绝态与授权拒绝 ------------------------------

    [Fact]
    public void CanonicalBindingBecomesStaleAfterRevisionAdvance_BasisStaysFixed()
    {
        var (kernel, world, _) = NewMemoryKernel(OkAt(T1));
        var root = world.Current!.Containers.Single().Identity.ContainerId;
        var bindTimeSlice = kernel.DeriveSlice(root);
        var bindTimeSliceId = CoreSemanticProjection.SliceId(bindTimeSlice);

        var act = kernel.Act(ActOnce(kernel, world), new CandidateBinding(
            "screen.home", "idle", world.Current!.RevisionId));
        Assert.NotNull(act.Receipt);

        var binding = CoreSemanticProjection.ProjectTargetBinding(
            act.Binding!.Canonical!, bindTimeSliceId,
            new CoreId(root), world.Current!.RevisionId, act.Gate);
        Assert.Equal(BindingDisposition.Canonical, binding.Disposition);

        // 新 Evidence → 新 revision → 同一 binding 重新投影 → Stale，basis 不漂移
        kernel.Process(Observation("screen.home", "idle", T1));
        Assert.NotEqual(bindTimeSlice.SourceRevisionId, world.Current!.RevisionId);

        var later = CoreSemanticProjection.ProjectTargetBinding(
            act.Binding.Canonical!, bindTimeSliceId,
            new CoreId(root), world.Current.RevisionId, act.Gate);
        Assert.Equal(BindingDisposition.Stale, later.Disposition);
        Assert.False(CoreInvariants.CanDispatch(later));
        Assert.Equal(bindTimeSliceId, later.BasisSliceId);
        Assert.True(CoreInvariants.IsHistoricalBasisStable(later, bindTimeSliceId));
    }

    [Fact]
    public void RejectedCandidates_ProduceNoCanonicalBinding_NothingProjectable()
    {
        var (kernel, world, _) = NewMemoryKernel(OkAt(T1));
        var current = world.Current!.RevisionId;

        // stale 候选（SourceRevisionId 非 current）
        var stale = kernel.Act(ActOnce(kernel, world), new CandidateBinding("screen.home", "idle", "rev-0"));
        Assert.Null(stale.Binding!.Canonical);
        Assert.Equal(BindingRejectionReason.StaleRevision, stale.Binding.RejectionReason);
        Assert.Null(stale.Receipt);

        // ambiguous 候选（grounding 报多义）
        var ambiguous = kernel.Act(ActOnce(kernel, world),
            CandidateBinding.ForUiTarget(new UiTargetReference("occ-1", current), isAmbiguous: true));
        Assert.Null(ambiguous.Binding!.Canonical);
        Assert.Equal(BindingRejectionReason.Ambiguous, ambiguous.Binding.RejectionReason);

        // unknown target 候选（occurrence 不在 current belief）
        var unknownTarget = kernel.Act(ActOnce(kernel, world),
            CandidateBinding.ForUiTarget(new UiTargetReference("occ-missing", current)));
        Assert.Null(unknownTarget.Binding!.Canonical);
        Assert.Equal(BindingRejectionReason.UnknownTarget, unknownTarget.Binding.RejectionReason);

        // 三种拒绝态在 realization 都不产生 canonical binding → Core 投影
        // 无 TargetBinding 可产（fail-closed：投影输入要求 CanonicalBinding）
    }

    [Fact]
    public void DeniedGate_ProjectsToUnauthorized_NotCanonical()
    {
        // freshness Insufficient（确定性替身）→ judgment 拒绝 → gate 执法
        // "not-authorized" → canonical binding 存在但投递被拒
        var (kernel, world, _) = NewMemoryKernel(
            OkAt(T1), freshness: new FreshnessDoubles.ByEffectClass(FreshnessSufficiency.Insufficient));
        var root = world.Current!.Containers.Single().Identity.ContainerId;
        var sliceId = CoreSemanticProjection.SliceId(kernel.DeriveSlice(root));

        var act = kernel.Act(ActOnce(kernel, world), new CandidateBinding(
            "screen.home", "idle", world.Current!.RevisionId));

        Assert.NotNull(act.Binding!.Canonical);
        Assert.NotNull(act.Gate);
        Assert.False(act.Gate!.Allowed);
        Assert.Equal("not-authorized", act.Gate.Reason);
        Assert.Null(act.Receipt);

        var binding = CoreSemanticProjection.ProjectTargetBinding(
            act.Binding.Canonical!, sliceId, new CoreId(root),
            world.Current!.RevisionId, act.Gate);
        Assert.Equal(BindingDisposition.Unauthorized, binding.Disposition);
        Assert.False(CoreInvariants.CanDispatch(binding));
    }

    // ---- P4：locator 材料与 Core 纯度（提取纪律的结构证明）------------------

    [Fact]
    public void LocatorMaterial_ProjectsToOpaqueKindKey_ReferenceIdentityNeverPosesAsLocator()
    {
        var native = new CanonicalBinding(
            "bind-n", "intent-n", "tap", "occ-n", "v", "rev-1", 1,
            TargetOccurrenceId: "occ-n",
            TargetNative: NativeLocator.BrowserNode("node-42"));
        var spatial = new CanonicalBinding(
            "bind-s", "intent-s", "tap", "occ-s", "v", "rev-1", 1,
            TargetOccurrenceId: "occ-s",
            TargetLocator: new SpatialLocator(0.1, 0.2, 0.3, 0.4, "device-viewport"));
        var bare = new CanonicalBinding(
            "bind-b", "intent-b", "tap", "occ-b", "v", "rev-1", 1, TargetOccurrenceId: "occ-b");

        var nativeBinding = CoreSemanticProjection.ProjectTargetBinding(
            native, new CoreId("slice:x"), new CoreId("seg:x"), "rev-1");
        var spatialBinding = CoreSemanticProjection.ProjectTargetBinding(
            spatial, new CoreId("slice:x"), new CoreId("seg:x"), "rev-1");
        var bareBinding = CoreSemanticProjection.ProjectTargetBinding(
            bare, new CoreId("slice:x"), new CoreId("seg:x"), "rev-1");

        Assert.Equal("browser.backend-node-id", nativeBinding.LocatorKind);
        Assert.Equal("node-42", nativeBinding.LocatorKey);
        Assert.Equal("spatial:device-viewport", spatialBinding.LocatorKind);
        Assert.Equal("0.1000,0.2000,0.3000,0.4000", spatialBinding.LocatorKey);
        Assert.Equal("none", bareBinding.LocatorKind);
        Assert.Equal(string.Empty, bareBinding.LocatorKey);   // 无材料不冒充（reference id 不进 locator）

        // Core 候选记录只携带领域无关值：程序集级无 realization 依赖，
        // 属性类型只允许 string / CoreId / DateTimeOffset / 枚举 / CoreId 列表
        var coreAssembly = typeof(CoreSlice).Assembly;
        Assert.DoesNotContain(coreAssembly.GetReferencedAssemblies(),
            a => a.Name!.StartsWith("UniClaw.Kernel", StringComparison.Ordinal));

        foreach (var type in new[]
                 {
                     typeof(CoreSlice), typeof(Segment), typeof(CoreEvidence), typeof(Claim),
                     typeof(Effect), typeof(Attempt), typeof(TargetBinding),
                 })
        {
            Assert.All(type.GetProperties(), p =>
            {
                var t = p.PropertyType;
                var allowed = t == typeof(string)
                    || t == typeof(CoreId) || t == typeof(CoreId?)
                    || t == typeof(DateTimeOffset)
                    || t == typeof(BasisReference)
                    || t == typeof(AttemptExecutionState)
                    || t.IsEnum
                    || (t.IsGenericType
                        && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                        && (t.GetGenericArguments()[0] == typeof(CoreId)
                            || t.GetGenericArguments()[0] == typeof(string)
                            || t.GetGenericArguments()[0] == typeof(BasisReference)));
                Assert.True(allowed, $"{type.Name}.{p.Name} 携带非领域无关值类型 {t}");
            });
        }
    }
}
