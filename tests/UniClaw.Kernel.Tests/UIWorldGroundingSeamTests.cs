using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// UIW-004 验收 S1–S9 —— P23 缝出面（ResolveCurrent / GroundingView）、
/// Slice 重构（RootContainerIdentity 锚定 + occurrence 景观 + ScopedClaims
/// 分区）、binding target shape 切换（UI = UiTargetReference 恒绑 occurrence，
/// 无字符串 fallback；非 UI 字符串通道兼容不变）。S10 = 全量回归
/// （dotnet test 全解决方案）。纯内存（level: DETERMINISTIC）。
/// </summary>
public sealed class UIWorldGroundingSeamTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 8, 9, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 9, 8, 9, 10, 0, TimeSpan.Zero);

    // ---- 测试替身 ---------------------------------------------------------

    private sealed class ActTapPolicy : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, "tap", "screen.home");
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:ok", T2);
    }

    /// <summary>双 owner occurrence double（S2 container 维度判别）。</summary>
    private sealed class TwoOwnerObservationStrategy : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            new[]
            {
                new ProposedOccurrence("ctr-owner-a", "button", "x"),
                new ProposedOccurrence("ctr-owner-b", "button", "x"),
            };
    }

    private static ExecutionContract Contract() => new(
        Version: "c1",
        Objective: "tap-the-button",
        Scope: new HashSet<string> { "screen.home" },
        AllowedEffects: new HashSet<string> { "tap" },
        ForbiddenEffects: new HashSet<string> { "swipe" },
        ProofCriteria: new[] { "button-tapped" });

    /// <summary>
    /// 组装 grounding seam kernel：SeedContainer association（首条 evidence 铸
    /// 一个 container）+ 可选 observation / continuity seams。seed evidence
    /// 内容确定性 → probe ledger 预知 minted container id；NewKernel 内部先
    /// Process 该 seed evidence（rev-1 铸 root），场景 evidence 随后（occurrence
    /// 按 revision-local 语义派生自触发 record，不受 seed 影响）。
    /// </summary>
    private static (UniKernel Kernel, WorldModel World, RunModel Run, ControlLoop Control,
        RuntimeAssurance Assurance, EffectBoundary Effects) NewKernel(
        IUiObservationStrategy? observation = null,
        IContinuityStrategy? continuity = null,
        bool seed = true,
        string[]? extraScope = null)
    {
        var scope = new HashSet<string> { UIWorldDoubles.Observed };
        if (extraScope is not null)
            foreach (var s in extraScope) scope.Add(s);
        var world = new WorldModel(
            scope, new SeedContainerAssociationStrategy(), observation, continuity);
        var run = new RunModel();
        var control = new ControlLoop(new ActTapPolicy());
        var assurance = new RuntimeAssurance(new FreshnessDoubles.Satisfying());
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance, run, control, assurance, effects);
        kernel.AdmitContract(Contract());
        if (seed)
            kernel.Process(UIWorldDoubles.Observation("page:seed:v1", T0));   // rev-1（铸 root）
        return (kernel, world, run, control, assurance, effects);
    }

    /// <summary>SeedContainer 首条 evidence（New accepted）→ minted container id 预知。</summary>
    private static string ProbeContainerId()
    {
        var (admission, _) = new EvidenceLedger().Admit(UIWorldDoubles.Observation("page:seed:v1", T0));
        return "ctr-" + admission.EvidenceId![3..15];
    }

    // ---- S1：ResolveCurrent 四态 + 只读性 ---------------------------------

    [Fact]
    public void S1_ResolveCurrentReportsFourKindsAndIsPurelyReadOnly()
    {
        // UniqueCandidate：单 role 匹配
        var (kernel, world, _, _, _, _) = NewKernel(new RoleObservationStrategy(ProbeContainerId()));
        kernel.Process(UIWorldDoubles.Observation("button:submit", T0));
        var unique = world.ResolveCurrent(new TargetDescriptor("button"));
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, unique.Result);
        Assert.Equal(world.Current!.RevisionId, unique.SourceRevisionId);
        var fact = Assert.Single(unique.Candidates);
        Assert.Equal("button", fact.Role);
        Assert.Equal("submit", fact.SemanticDescriptor);
        Assert.Equal(world.Current.RevisionId, fact.SourceRevisionId);

        // NoCandidate：role 无匹配
        var none = world.ResolveCurrent(new TargetDescriptor("label"));
        Assert.Equal(CurrentCandidateSetResultKind.NoCandidate, none.Result);
        Assert.Empty(none.Candidates);

        // MultipleCandidates：同 role 多 occurrence
        var (k2, w2, _, _, _, _) = NewKernel(new RoleObservationStrategy());
        k2.Process(UIWorldDoubles.Observation("button:a+button:b", T0));
        var multi = w2.ResolveCurrent(new TargetDescriptor("button"));
        Assert.Equal(CurrentCandidateSetResultKind.MultipleCandidates, multi.Result);
        Assert.Equal(2, multi.Candidates.Count);

        // ScopeProjectionUnavailable：无 observation seam（Occurrences == null）
        var (k3, w3, _, _, _, _) = NewKernel();
        k3.Process(UIWorldDoubles.Observation("page:v1", T0));
        var unavailable = w3.ResolveCurrent(new TargetDescriptor("button"));
        Assert.Equal(CurrentCandidateSetResultKind.ScopeProjectionUnavailable, unavailable.Result);
        Assert.Empty(unavailable.Candidates);

        // ScopeProjectionUnavailable：尚无 revision（无 current，fail-closed）
        var (_, w4, _, _, _, _) = NewKernel(new RoleObservationStrategy(), seed: false);
        Assert.Equal(
            CurrentCandidateSetResultKind.ScopeProjectionUnavailable,
            w4.ResolveCurrent(new TargetDescriptor("button")).Result);

        // 只读性：零 log / 零 revision / 零 registry 写入
        var revisions = world.RevisionHistory.Count;
        var relevance = world.RelevanceLog.Count;
        var associations = world.AssociationLog.Count;
        var demands = world.ContinuityDemands.Count;
        var continuity = world.ContinuityLog.Count;
        var current = world.Current;
        world.ResolveCurrent(new TargetDescriptor("button"));
        world.ResolveCurrent(new TargetDescriptor("label"));
        world.ResolveCurrent(new TargetDescriptor("button", "submit"));
        Assert.Equal(revisions, world.RevisionHistory.Count);
        Assert.Equal(relevance, world.RelevanceLog.Count);
        Assert.Equal(associations, world.AssociationLog.Count);
        Assert.Equal(demands, world.ContinuityDemands.Count);
        Assert.Equal(continuity, world.ContinuityLog.Count);
        Assert.Same(current, world.Current);
    }

    // ---- S2：descriptor 匹配维度（role / container / descriptor） ---------

    [Fact]
    public void S2_ResolveCurrentMatchesOnRoleContainerAndDescriptorDeterministically()
    {
        var (kernel, world, _, _, _, _) = NewKernel(new TwoOwnerObservationStrategy());
        kernel.Process(UIWorldDoubles.Observation("page:v1", T0));
        // 同 role、同 descriptor、不同 owning container → container 维度判别
        Assert.Equal(
            CurrentCandidateSetResultKind.MultipleCandidates,
            world.ResolveCurrent(new TargetDescriptor("button")).Result);
        Assert.Equal(
            CurrentCandidateSetResultKind.UniqueCandidate,
            world.ResolveCurrent(new TargetDescriptor("button", OwningContainerId: "ctr-owner-a")).Result);
        Assert.Equal("ctr-owner-a", world.ResolveCurrent(
            new TargetDescriptor("button", OwningContainerId: "ctr-owner-a")).Candidates.Single().OwningContainerId);

        // descriptor 维度判别（RoleObservationStrategy：同 role 不同 descriptor）
        var (k2, w2, _, _, _, _) = NewKernel(new RoleObservationStrategy());
        k2.Process(UIWorldDoubles.Observation("button:a+button:b", T0));
        var byDescriptor = w2.ResolveCurrent(new TargetDescriptor("button", SemanticDescriptor: "b"));
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, byDescriptor.Result);
        Assert.Equal("b", byDescriptor.Candidates.Single().SemanticDescriptor);
        // 组合维度：descriptor 无匹配（container 不匹配叠加）
        Assert.Equal(
            CurrentCandidateSetResultKind.NoCandidate,
            w2.ResolveCurrent(new TargetDescriptor("button", "b", OwningContainerId: "ctr-none")).Result);
    }

    // ---- S2b（RVR-001 F1/A1）：view.OwningContainerId = 派生 owner fact ----

    [Fact]
    public void S2b_ResolveCurrentDerivesOwningContainerIdFromMatchedCandidates()
    {
        // RVR-001 F1（A1 / ADR-0011 原则 4）：consumer 输入回显消除——
        // view.OwningContainerId 不再回传 descriptor.OwningContainerId，
        // 改为匹配候选集派生：恰好一个非 null owner 值 → 该值；否则（全
        // null / ≥2 个不同非 null / 零候选）→ null。
        // S1/S2 既有断言只覆盖候选 fact 的 OwningContainerId（非 view 字段），
        // 无需迁移；本测试按新语义锁定 view 字段。

        // descriptor 不带 container：单候选（owner = 铸造 container）→ 派生该 owner
        var cid = ProbeContainerId();
        var (kernel, world, _, _, _, _) = NewKernel(new RoleObservationStrategy(cid));
        kernel.Process(UIWorldDoubles.Observation("button:submit", T0));
        var bare = world.ResolveCurrent(new TargetDescriptor("button"));
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, bare.Result);
        Assert.Equal(cid, bare.OwningContainerId);

        // descriptor 带 container：唯一匹配候选 → 派生该候选 owner（值来自候选，非回显）
        Assert.Equal(cid, world.ResolveCurrent(
            new TargetDescriptor("button", OwningContainerId: cid)).OwningContainerId);

        // owner 不一致（≥2 个不同非 null）→ null
        var (k2, w2, _, _, _, _) = NewKernel(new TwoOwnerObservationStrategy());
        k2.Process(UIWorldDoubles.Observation("page:v1", T0));
        Assert.Null(w2.ResolveCurrent(new TargetDescriptor("button")).OwningContainerId);
        // 收窄到唯一候选 → 派生该候选 owner
        Assert.Equal("ctr-owner-a", w2.ResolveCurrent(
            new TargetDescriptor("button", OwningContainerId: "ctr-owner-a")).OwningContainerId);

        // 全 null owner → null
        var (k3, w3, _, _, _, _) = NewKernel(new RoleObservationStrategy());
        k3.Process(UIWorldDoubles.Observation("button:submit", T0));
        Assert.Null(w3.ResolveCurrent(new TargetDescriptor("button")).OwningContainerId);

        // 零候选（NoCandidate）→ null；投影不可用 → null（均无候选可派生）
        Assert.Null(w3.ResolveCurrent(new TargetDescriptor("label")).OwningContainerId);
        var (k4, w4, _, _, _, _) = NewKernel();
        k4.Process(UIWorldDoubles.Observation("page:v1", T0));
        Assert.Null(w4.ResolveCurrent(new TargetDescriptor("button")).OwningContainerId);
    }

    // ---- S3：Slice 新形状派生 ---------------------------------------------

    [Fact]
    public void S3_SliceDerivesRootAnchoredOccurrenceLandscapeAndScopedClaims()
    {
        var cid = ProbeContainerId();
        var (kernel, world, _, _, _, _) = NewKernel(
            new RoleObservationStrategy(cid),
            extraScope: new[] { $"{cid}.panel.title", "ui.other.panel.title" });

        // root fail-closed：不存在的 container id
        kernel.Process(UIWorldDoubles.Observation("button:submit", T0));   // rev-1（铸 cid）
        Assert.Throws<InvalidOperationException>(() => kernel.DeriveSlice("ctr-bogus"));

        // 无 current 同样 fail-closed
        var (_, fresh, _, _, _, _) = NewKernel(new RoleObservationStrategy(cid), seed: false);
        Assert.Throws<InvalidOperationException>(() => fresh.DeriveSlice(cid));

        // 默认 InScope = {root}：occurrence 景观 + revision 锚
        var slice = kernel.DeriveSlice(cid);
        Assert.Equal(world.Current!.RevisionId, slice.SourceRevisionId);
        Assert.Equal(cid, slice.RootContainerId);
        Assert.Equal(new[] { cid }, slice.InScopeContainerIds);
        var occurrence = Assert.Single(slice.Occurrences);
        Assert.Equal(cid, occurrence.OwningContainerId);
        Assert.Equal("button", occurrence.Role);
        Assert.Equal("submit", occurrence.SemanticDescriptor);
        Assert.Equal(
            world.Current.Occurrences!.Single().OccurrenceId,
            occurrence.OccurrenceId);

        // inScope 非法项 fail-closed（⊆ Current containers）
        Assert.Throws<InvalidOperationException>(() => kernel.DeriveSlice(cid, new[] { cid, "ctr-bogus" }));

        // ScopedClaims 分区（<containerId>.<rest> 约定）：scope 内 claim 入分区，
        // scope 外 claim 不入（omission ≠ absence——仍留在 belief 中）
        kernel.Process(UIWorldDoubles.SubjectObservation($"{cid}.panel.title", "hello", T1));
        kernel.Process(UIWorldDoubles.SubjectObservation("ui.other.panel.title", "world", T2));
        var partitioned = kernel.DeriveSlice(cid);
        var claim = Assert.Single(partitioned.ScopedClaims);
        Assert.Equal($"{cid}.panel.title", claim.Key);
        Assert.Equal("hello", claim.Value);
        Assert.True(world.Current.WorldState.ContainsKey("ui.other.panel.title"));

        // occurrence 景观按 OwningContainerId ∈ InScope 过滤（owner null 不入）
        var (k3, w3, _, _, _, _) = NewKernel(new RoleObservationStrategy());  // owner null
        k3.Process(UIWorldDoubles.Observation("button:submit", T0));
        var ownerless = w3.DeriveSlice(ProbeContainerId());
        Assert.Empty(ownerless.Occurrences);
    }

    // ---- S4：UI bind 正路径闭环 -------------------------------------------

    [Fact]
    public void S4_UiTargetCandidateBindsOccurrenceThroughJudgeAndGate()
    {
        var (kernel, world, _, _, _, effects) = NewKernel(new RoleObservationStrategy(ProbeContainerId()));
        kernel.Process(UIWorldDoubles.Observation("button:submit", T0));

        var grounding = world.ResolveCurrent(new TargetDescriptor("button"));
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, grounding.Result);
        var occurrenceId = grounding.Candidates.Single().OccurrenceId;

        var intent = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));
        var candidate = new CandidateBinding(
            "screen.home", "idle", world.Current!.RevisionId,
            UiTarget: new UiTargetReference(occurrenceId, world.Current.RevisionId));
        var act = kernel.Act(intent, candidate);

        var canonical = act.Binding!.Canonical!;
        Assert.NotNull(canonical);
        Assert.Equal(occurrenceId, canonical.TargetOccurrenceId);
        Assert.Equal(occurrenceId, canonical.TargetSubject);          // attempt 留痕约定沿载
        Assert.Null(canonical.OwningContainerId);                     // view 无 container fact（尽力而为不取）
        Assert.Null(canonical.LogicalItemId);
        Assert.Equal(world.Current.RevisionId, canonical.RevisionId);
        Assert.True(act.Judgment!.IsAdmissible);
        Assert.True(act.Gate!.Allowed);
        Assert.NotNull(act.Receipt);
        Assert.Equal(occurrenceId, act.Receipt!.TargetSubject);
        // attempt evidence 约定自然工作（TargetSubject 沿载 occurrence id）
        var attempt = effects.ExportAttemptEvidence(act.Receipt);
        Assert.Contains(occurrenceId, attempt.Claim.Subject);
    }

    // ---- S4b（RVR-001 F3/A3）：UI 通道 candidate 工厂 ---------------------

    [Fact]
    public void S4b_CandidateBindingForUiTargetFactoryBuildsUiChannelCandidate()
    {
        // RVR-001 F3（A3 / UIW-004）：UI 通道 candidate 统一经工厂构造——
        // 字符串通道字段对 UI 无作用（Bind 只读 UiTarget），null 压制收拢到
        // 工厂一处；禁止调用点自铸 null!。
        var uiTarget = new UiTargetReference("occ-factory-0", "rev-9");
        var candidate = CandidateBinding.ForUiTarget(uiTarget);
        Assert.Same(uiTarget, candidate.UiTarget);
        Assert.Equal("rev-9", candidate.SourceRevisionId);
        Assert.False(candidate.IsAmbiguous);
        Assert.True(CandidateBinding.ForUiTarget(uiTarget, isAmbiguous: true).IsAmbiguous);

        // 工厂产物沿 S4 正路径全通（Judge → Bind → Gate → dispatch）
        var (kernel, world, _, _, _, _) = NewKernel(new RoleObservationStrategy(ProbeContainerId()));
        kernel.Process(UIWorldDoubles.Observation("button:submit", T0));
        var occurrenceId = world.Current!.Occurrences!.Single().OccurrenceId;
        var intent = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));
        var act = kernel.Act(intent, CandidateBinding.ForUiTarget(
            new UiTargetReference(occurrenceId, world.Current.RevisionId)));
        Assert.NotNull(act.Binding!.Canonical);
        Assert.Equal(occurrenceId, act.Binding.Canonical!.TargetOccurrenceId);
        Assert.True(act.Gate!.Allowed);
        Assert.NotNull(act.Receipt);
    }

    // ---- S5：stale occurrence candidate → StaleRevision，无 fallback ------

    [Fact]
    public void S5_StaleOccurrenceCandidateIsRejectedAsStaleRevisionWithoutFallback()
    {
        var (kernel, world, _, _, _, effects) = NewKernel(new RoleObservationStrategy(ProbeContainerId()));
        kernel.Process(UIWorldDoubles.Observation("button:submit", T0));   // rev-1
        var staleOccurrence = world.Current!.Occurrences!.Single().OccurrenceId;

        kernel.Process(UIWorldDoubles.Observation("button:submit", T1));   // rev-2（occurrence 重铸）

        var intent = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));
        var candidate = new CandidateBinding(
            "screen.home", "idle", "rev-1",
            UiTarget: new UiTargetReference(staleOccurrence, "rev-1"));
        var decision = effects.Bind(intent, candidate, world.DeriveBindingView(null, staleOccurrence));

        Assert.Null(decision.Canonical);
        Assert.Equal(BindingRejectionReason.StaleRevision, decision.RejectionReason);
        Assert.Single(effects.BindingLog);                              // 无 fallback 留痕
    }

    // ---- S6：occurrence 不在当前投影 → UnknownTarget -----------------------

    [Fact]
    public void S6_OccurrenceAbsentFromCurrentProjectionIsRejectedAsUnknownTarget()
    {
        var (kernel, world, _, _, _, effects) = NewKernel(new RoleObservationStrategy(ProbeContainerId()));
        kernel.Process(UIWorldDoubles.Observation("button:submit", T0));
        var intent = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));

        // revision 对齐但 occurrence id 不在当前投影（HasTargetOccurrence=false）
        var candidate = new CandidateBinding(
            "screen.home", "idle", world.Current!.RevisionId,
            UiTarget: new UiTargetReference("occ-nonexistent-0", world.Current.RevisionId));
        var view = world.DeriveBindingView(null, "occ-nonexistent-0");
        Assert.False(view.HasTargetOccurrence);

        var decision = effects.Bind(intent, candidate, view);
        Assert.Null(decision.Canonical);
        Assert.Equal(BindingRejectionReason.UnknownTarget, decision.RejectionReason);

        // 单参调用行为保持：HasTargetOccurrence 恒 false（即使 occurrence 存在）
        var realId = world.Current.Occurrences!.Single().OccurrenceId;
        Assert.False(world.DeriveBindingView(UIWorldDoubles.Observed).HasTargetOccurrence);
        Assert.True(world.DeriveBindingView(null, realId).HasTargetOccurrence);
    }

    // ---- S7：ambiguous candidate → Ambiguous（四态语义沿用） ---------------

    [Fact]
    public void S7_AmbiguousUiTargetCandidateIsRejectedAsAmbiguous()
    {
        var (kernel, world, _, _, _, effects) = NewKernel(new RoleObservationStrategy(ProbeContainerId()));
        kernel.Process(UIWorldDoubles.Observation("button:submit", T0));
        var intent = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));

        var occurrenceId = world.Current!.Occurrences!.Single().OccurrenceId;
        var candidate = new CandidateBinding(
            "screen.home", "idle", world.Current.RevisionId, IsAmbiguous: true,
            UiTarget: new UiTargetReference(occurrenceId, world.Current.RevisionId));
        var decision = effects.Bind(intent, candidate, world.DeriveBindingView(null, occurrenceId));

        Assert.Null(decision.Canonical);
        Assert.Equal(BindingRejectionReason.Ambiguous, decision.RejectionReason);
    }

    // ---- S8：continuity re-bind 闭环（UIW-003 集成）-----------------------

    [Fact]
    public void S8_ContinuityRebindResolvesSameReferentAndRebindsNewOccurrence()
    {
        var (kernel, world, _, _, _, _) = NewKernel(
            new RoleObservationStrategy(), new RoleContinuityStrategy());

        // rev-1：occurrence #1
        kernel.Process(UIWorldDoubles.Observation("button:submit", T0));
        var occ1 = world.Current!.Occurrences!.Single().OccurrenceId;

        // demand 登记（anchor 在 current 内，ADR-0014 timing）→ 首次判定 mint item
        var handle = world.RegisterContinuityDemand(UIWorldContinuityDoubles.Demand(
            "demand-1", "button", "submit", anchor: occ1, anchorRevision: world.Current.RevisionId));
        var first = world.ResolveContinuity(handle);
        Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, first.Outcome.Kind);
        Assert.NotNull(first.LogicalItemId);

        // 首绑：occurrence #1 candidate → canonical → dispatch
        var intent1 = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));
        var act1 = kernel.Act(intent1, new CandidateBinding(
            "screen.home", "idle", world.Current!.RevisionId,
            UiTarget: new UiTargetReference(occ1, world.Current.RevisionId)));
        Assert.NotNull(act1.Receipt);

        // revision advance：新 evidence 重铸 occurrence #2
        kernel.Process(UIWorldDoubles.Observation("button:submit", T1));
        var occ2 = world.Current.Occurrences!.Single().OccurrenceId;
        Assert.NotEqual(occ1, occ2);

        // ResolveContinuity → SameReferent（demand 已绑 item）
        var rebind = world.ResolveContinuity(handle);
        Assert.Equal(ContinuityResolutionOutcomeKind.Adjudicated, rebind.Outcome.Kind);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.SameReferent, rebind.Outcome.AdjudicationKind);
        Assert.Equal(first.LogicalItemId, rebind.LogicalItemId);

        // 新 occurrence candidate → 重绑成功（Judge → Gate/dispatch 全通）
        var intent2 = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));
        var act2 = kernel.Act(intent2, new CandidateBinding(
            "screen.home", "idle", world.Current!.RevisionId,
            UiTarget: new UiTargetReference(occ2, world.Current.RevisionId)));
        var canonical = act2.Binding!.Canonical!;
        Assert.Equal(occ2, canonical.TargetOccurrenceId);
        Assert.True(act2.Judgment!.IsAdmissible);
        Assert.True(act2.Gate!.Allowed);
        Assert.NotNull(act2.Receipt);
    }

    // ---- S9：非 UI 字符串通道兼容不变 -------------------------------------

    [Fact]
    public void S9_NonUiStringChannelBehaviorIsUnchanged()
    {
        var (kernel, world, _, _, _, effects) = NewKernel();
        kernel.Process(UIWorldDoubles.Observation("page:v1", T0));   // rev-1（string claim）
        var intent = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));

        // 正路径：string candidate → canonical 尾字段全 null（无 UI 泄漏）
        var ok = effects.Bind(
            intent, new CandidateBinding(UIWorldDoubles.Observed, "page:v1", world.Current!.RevisionId),
            world.DeriveBindingView(UIWorldDoubles.Observed));
        var canonical = ok.Canonical!;
        Assert.Equal(UIWorldDoubles.Observed, canonical.TargetSubject);
        Assert.Equal("page:v1", canonical.TargetValue);
        Assert.Null(canonical.TargetOccurrenceId);
        Assert.Null(canonical.OwningContainerId);
        Assert.Null(canonical.LogicalItemId);

        // 三态拒绝语义不变：stale / unknown / ambiguous
        Assert.Equal(BindingRejectionReason.StaleRevision, effects.Bind(
            intent, new CandidateBinding(UIWorldDoubles.Observed, "page:v1", "rev-0"),
            world.DeriveBindingView(UIWorldDoubles.Observed)).RejectionReason);
        Assert.Equal(BindingRejectionReason.UnknownTarget, effects.Bind(
            intent, new CandidateBinding("ui.unknown", "?", world.Current.RevisionId),
            world.DeriveBindingView("ui.unknown")).RejectionReason);
        Assert.Equal(BindingRejectionReason.Ambiguous, effects.Bind(
            intent, new CandidateBinding(UIWorldDoubles.Observed, "page:v1", world.Current.RevisionId, IsAmbiguous: true),
            world.DeriveBindingView(UIWorldDoubles.Observed)).RejectionReason);

        // 第四态不变：candidate null → NoCandidate
        Assert.Equal(BindingRejectionReason.NoCandidate,
            effects.Bind(intent, null, world.DeriveBindingView(UIWorldDoubles.Observed)).RejectionReason);
    }
}

/// <summary>
/// UIW-004 迁移 double：首条 evidence 提议 New（evidence-backed，经 Authority
/// gates）铸一个 container，之后一律 Insufficient（不再产生 identity 变异）。
/// 用途：为无 containers 的既有 E2B/C2E 测试世界种子一个 root container，
/// 使 DeriveSlice(root) 在新形状下可用——场景语义不变。
/// </summary>
internal sealed class SeedContainerAssociationStrategy : IAssociationStrategy
{
    public AssociationProposal Propose(AssociationInput input) => input.Previous is null
        ? new AssociationProposal(
            AssociationDispositionKind.New, MatchedContainerId: null,
            new[] { new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
            Relations: Array.Empty<ProposedRelation>(), Reason: "seed-root-container")
        : new AssociationProposal(
            AssociationDispositionKind.Insufficient, MatchedContainerId: null,
            Candidates: Array.Empty<AssociationCandidate>(),
            Relations: Array.Empty<ProposedRelation>(), Reason: "seed-once");
}

/// <summary>
/// UIW-004 迁移 helper：新 Slice 形状下既有测试的 DeriveSlice 调用点适配
/// （ControlLoop 只读 SourceRevisionId——root 选择不影响语义）。
/// </summary>
internal static class SliceSeed
{
    public static string RootOf(UniKernel kernel) =>
        kernel.CurrentBelief!.Containers.Single().Identity.ContainerId;
}
