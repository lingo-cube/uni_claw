using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// PER-003 验收 E1–E7 —— 真实感知资产（PER-002 corpus：真机帧 + golden
/// 真实 YOLO/OCR）驱动 entity model 三 seams（observation / association /
/// continuity）+ P22 producer 导出缝（EffectBoundary.ExportTransitionContext
/// → ActResult.Transition）。全部确定性（level: DETERMINISTIC）；策略为
/// corpus 约定的确定性真实策略（corpus 约定留 corpus 层，PER-002 先例）。
/// corpus 只读；REAL_ASSET_COVERAGE 注记见各 Fact。
/// </summary>
public sealed class RealAssetEntityModelTests
{
    private static readonly CorpusManifest Corpus = CorpusManifest.Load();
    private static readonly DateTimeOffset ScrollActTime =
        new(2026, 9, 9, 10, 11, 0, TimeSpan.Zero);

    private static readonly Lazy<IReadOnlySet<string>> SignatureScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.PageSignatureSubject));

    private static readonly Lazy<IReadOnlySet<string>> DialogScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.DialogTitleSubject));

    private static IReadOnlySet<string> ScopesWithSubject(string subject) => Corpus.Scenarios
        .Where(s => s.Observations.Any(o => o.Subject == subject))
        .Select(s => "artifact:" + Corpus.Artifact(s.ScenarioId).ArtifactId)
        .ToHashSet();

    // ---- 测试替身（Act 编排：scroll intent + 成功投递 driver） -------------

    private sealed class ScrollActPolicy : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Act, "scroll", "perception.page.signature");
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:scroll-complete", ScrollActTime);
    }

    // ---- 组装 / 端到端 helper ------------------------------------------------

    private static (UniKernel Kernel, WorldModel World) NewKernel(bool fullStack = false)
    {
        var world = new WorldModel(
            Corpus.SubjectScope,
            new CorpusAssociationStrategy(SignatureScopes.Value, DialogScopes.Value),
            new CorpusObservationStrategy(),
            new CorpusContinuityStrategy());
        if (!fullStack)
            return (new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance), world);

        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(new ScrollActPolicy()),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(new OkDriver()));
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: "scroll-the-list",
            Scope: new HashSet<string> { "perception.page.signature" },
            AllowedEffects: new HashSet<string> { "scroll" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "list-scrolled" }));
        return (kernel, world);
    }

    /// <summary>scenario → RawArtifact.Capture（真实 PNG bytes）→
    /// FastPerception("perception.corpus") → 逐条 Process（可选 P22 prior）。</summary>
    private static void Observe(UniKernel kernel, string scenarioId,
        ObservationContext context = ObservationContext.External,
        TransitionContext? transition = null)
    {
        var perception = new FastPerception(
            "perception.corpus", new CorpusFastPerception(Corpus, scenarioId));
        foreach (var proposal in perception.Observe(Corpus.Artifact(scenarioId), context))
            kernel.Process(proposal, transition);
    }

    // ---- E1：golden-run DIRECT 帧 → container New + occurrence 景观 ----------

    [Fact]
    public void E1_GoldenRunDirectFrameEstablishesContainerAndRealOccurrenceLandscape()
    {
        var (kernel, world) = NewKernel();
        Observe(kernel, "golden-case-a-before");

        var revision = world.Current!;
        // legacy-direct 真帧无 page signature（corpus knownAmbiguity 如实）：
        // corpus 约定 = 帧首条记录建 root container（New，evidence-backed）
        var decision = Assert.Single(kernel.AssociationLog,
            d => d.EffectiveKind == AssociationDispositionKind.New);
        var container = Assert.Single(revision.Containers);
        Assert.Equal(decision.EstablishedContainerId, container.Identity.ContainerId);

        // occurrence 景观 = 真实 YOLO class 值（icon / text_block / switch；
        // 本帧无 ui.node.*.class TextView/Button——真实值如实，REAL_ASSET_COVERAGE：
        // golden 帧只有 detect 家族 keyed nodes）
        var occurrences = revision.Occurrences!;
        Assert.Equal(16, occurrences.Count);
        Assert.Equal(6, occurrences.Count(o => o.Role == "icon"));
        Assert.Equal(9, occurrences.Count(o => o.Role == "text_block"));
        Assert.Equal(1, occurrences.Count(o => o.Role == "switch"));
        Assert.All(occurrences, o => Assert.Null(o.OwningContainerId));
        // occurrence id 确定性铸造（内容派生 + 序位）
        Assert.Distinct(occurrences.Select(o => o.OccurrenceId));
    }

    // ---- E2：SCROLL-01 真机对 + P22 producer（ActResult.Transition） ---------

    [Fact]
    public void E2_ScrollV1ToV2_ActTransitionContext_MatchedIdentityStable_RowShiftInConflicts()
    {
        var (kernel, world) = NewKernel(fullStack: true);
        Observe(kernel, "scroll01-v1");
        var v1Container = Assert.Single(world.Current!.Containers).Identity.ContainerId;
        var v1Signature = world.Current.WorldState[
            WorldModel.SignatureSubjectPrefix + v1Container].Value;

        // 模拟 scroll dispatch：ControlLoop 签发 scroll act-intent → 全链 Act
        var intent = kernel.SelectIntent(kernel.DeriveSlice(v1Container));
        var act = kernel.Act(intent, new CandidateBinding(
            "perception.page.signature", "SCROLL_01 — Long List", world.Current!.RevisionId));

        // P22 producer 导出缝：Transition 非 null（kind = effect class、
        // correlation = ReceiptId、epistemic strength = Attempt 腿）
        Assert.NotNull(act.Receipt);
        Assert.NotNull(act.Transition);
        Assert.Equal("scroll", act.Transition!.TransitionKind);
        Assert.Equal(act.Receipt!.ReceiptId, act.Transition.AttemptCorrelation);
        Assert.Equal(TransitionStrength.Attempt, act.Transition.Strength);

        // v2 全帧：每条 Process 传同一 Transition（P22 prior 消费面）
        Observe(kernel, "scroll01-v2", ObservationContext.PostActionEffectFlow, act.Transition);

        var matched = Assert.Single(kernel.AssociationLog,
            d => d.EffectiveKind == AssociationDispositionKind.Matched);
        Assert.Equal(v1Container, matched.MatchedContainerId);
        Assert.Equal(act.Receipt.ReceiptId, matched.TransitionCorrelation);
        Assert.Equal(TransitionStrength.Attempt, matched.TransitionStrength);

        // ContainerIdentity 稳定 + signature claim 稳定
        var container = Assert.Single(world.Current!.Containers);
        Assert.Equal(v1Container, container.Identity.ContainerId);
        Assert.Equal("SCROLL_01 — Long List",
            world.Current.WorldState[WorldModel.SignatureSubjectPrefix + v1Container].Value);
        Assert.Equal(v1Signature,
            world.Current.WorldState[WorldModel.SignatureSubjectPrefix + v1Container].Value);
        Assert.Equal(v1Container, world.Current.WorldState[WorldModel.CurrentContainerSubject].Value);

        // 真实行文本位移（Item 01..13 → Item 02..14）：13 个 row subjects 全部
        // 显式入 Conflicts（latest 不获胜），identity 不因内容变化被推翻
        for (var i = 1; i <= 13; i++)
        {
            var subject = i == 1 ? "ui.text.row_title" : $"ui.text.row_title{i}";
            Assert.Contains(world.Current.Conflicts, c => c.Subject == subject);
        }
    }

    // ---- E3：真实 continuity（row-anchor）------------------------------------

    [Fact]
    public void E3_RowAnchorDemand_ReferenceEstablished_ThenScrollFrame_ContradictedByRealTruth()
    {
        var (kernel, world) = NewKernel();
        Observe(kernel, "scroll01-v1");

        // 真实行节点（row_title：Item 01）作 anchor——demand 登记时 anchor
        // occurrence 必须仍在 current revision（ADR-0014 timing）
        var anchorOccurrence = world.Current!.Occurrences!.Single(o => o.SemanticDescriptor == "Item 01");
        var handle = world.RegisterContinuityDemand(new ContinuityDemand(
            "demand-row-title", ContinuityDemandSourceKind.EffectTargetCommitment,
            OwningContainerId: null, Role: "TextView", SemanticDescriptor: "Item 01",
            AnchorOccurrenceId: anchorOccurrence.OccurrenceId,
            AnchorRevisionId: world.Current.RevisionId, LogicalItemId: null));

        var first = world.ResolveContinuity(handle);
        Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, first.Outcome.Kind);
        Assert.NotNull(first.LogicalItemId);
        Assert.StartsWith("li-", first.LogicalItemId);

        // v2 帧：真实语料真值 = 该行槽位文本变化（Item 01 → Item 02，滚动位移），
        // 无任何候选描述等于 anchor → 按 corpus keyed-slot 约定判 Contradicted
        // （不是 SameReferent——本语料中行文本真实变化，如实断言）
        Observe(kernel, "scroll01-v2", ObservationContext.PostActionEffectFlow,
            new TransitionContext("scroll", "attempt-e3", TransitionStrength.Attempt));
        var second = world.ResolveContinuity(handle);

        Assert.Equal(ContinuityResolutionOutcomeKind.Adjudicated, second.Outcome.Kind);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.Contradicted, second.Outcome.AdjudicationKind);
        // Contradicted 只证伪候选：item 不 Ended（ADR-0015）
        var item = world.Current!.LogicalItems!.Single(i => i.LogicalItemId == first.LogicalItemId);
        Assert.Equal(LogicalItemLifecycle.Established, item.Lifecycle);
    }

    // ---- E4：POPUP-01 真实 dialog → 第二 container + Overlays ----------------

    [Fact]
    public void E4_PopupDialogFrameEstablishesSecondContainerWithOverlaysRelation()
    {
        var (kernel, world) = NewKernel();
        Observe(kernel, "popup09-before");
        var pageId = Assert.Single(world.Current!.Containers).Identity.ContainerId;

        Observe(kernel, "popup01-dialog", ObservationContext.PostActionEffectFlow,
            new TransitionContext("open-overlay", "attempt-e4", TransitionStrength.Attempt));

        var revision = world.Current!;
        // 第二 container（真实 dialog 帧，evidence-backed New）
        Assert.Equal(2, revision.Containers.Count);
        Assert.Contains(revision.Containers, c => c.Identity.ContainerId == pageId);
        var overlayDecision = Assert.Single(kernel.AssociationLog,
            d => d.Reason == "corpus-dialog-overlay-new");
        Assert.Equal(AssociationDispositionKind.New, overlayDecision.EffectiveKind);
        var dialogId = overlayDecision.EstablishedContainerId!;

        // Overlays relation（双方 evidence 过 gate：supporting ⊆ basis ∪ {当前}，
        // source/target 均为已知 container）
        var relation = Assert.Single(revision.Relations);
        Assert.Equal(ContainerRelationKind.Overlays, relation.Kind);
        Assert.Equal(dialogId, relation.SourceContainerId);
        Assert.Equal(pageId, relation.TargetContainerId);
        Assert.Equal(overlayDecision.EvidenceId, Assert.Single(relation.EvidenceBasis));

        // CurrentContainer 合法迁移到 dialog（Revise 语义）
        Assert.Equal(dialogId, revision.WorldState[WorldModel.CurrentContainerSubject].Value);
    }

    // ---- E5：NAV-03 parent/childA 同文本真帧（真实判别，不强制） --------------

    [Fact]
    public void E5_Nav03SameTextFrames_SignatureMatched_AnchorSameReferent_NoForcedAmbiguity()
    {
        var (kernel, world) = NewKernel();
        Observe(kernel, "nav03-parent");
        var parentContainer = Assert.Single(world.Current!.Containers).Identity.ContainerId;

        // 对真实 "CHILD A" 按钮 occurrence 登记 demand
        var anchorOccurrence = world.Current!.Occurrences!.Single(o => o.SemanticDescriptor == "CHILD A");
        Assert.Equal("Button", anchorOccurrence.Role);
        var handle = world.RegisterContinuityDemand(new ContinuityDemand(
            "demand-child-a", ContinuityDemandSourceKind.EffectTargetCommitment,
            OwningContainerId: null, Role: "Button", SemanticDescriptor: "CHILD A",
            AnchorOccurrenceId: anchorOccurrence.OccurrenceId,
            AnchorRevisionId: world.Current.RevisionId, LogicalItemId: null));
        var first = world.ResolveContinuity(handle);
        Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, first.Outcome.Kind);

        // childA 帧：语料真值 = fixture 记录 parent 与 sibling-child 文本/结构
        // 完全相同（含 page signature "NAV_03 — Page A" 相等）→ corpus 约定
        // 判 Matched 同一容器（corpus knownAmbiguity：duplicate-visible-titles
        // 的 distinct-logical-identity 不可由 fast text 证据判别，留给
        // UIWorld/Slow——REAL_ASSET_COVERAGE_PARTIAL，如实断言不伪造）
        Observe(kernel, "nav03-childa", ObservationContext.PostActionEffectFlow,
            new TransitionContext("navigate", "attempt-e5", TransitionStrength.Attempt));

        var matched = Assert.Single(kernel.AssociationLog,
            d => d.EffectiveKind == AssociationDispositionKind.Matched);
        Assert.Equal(parentContainer, matched.MatchedContainerId);
        Assert.Single(world.Current!.Containers);

        // anchor 匹配仍唯一（child 帧中 "CHILD A" 文本唯一）→ SameReferent
        var second = world.ResolveContinuity(handle);
        Assert.Equal(ContinuityResolutionOutcomeKind.Adjudicated, second.Outcome.Kind);
        Assert.Equal(ContinuityAdjudicationOutcomeKind.SameReferent, second.Outcome.AdjudicationKind);
        Assert.Equal(first.LogicalItemId, second.LogicalItemId);

        // 不产生强制选择：无 Ambiguous 判定（无 Ambiguous 时不得伪造）
        Assert.DoesNotContain(world.ContinuityLog,
            d => d.EffectiveOutcome.AdjudicationKind == ContinuityAdjudicationOutcomeKind.Ambiguous);
    }

    // ---- E6：P22 prior 不压签名反证 -------------------------------------------

    [Fact]
    public void E6_ScrollPriorCannotOverrideSignatureContradiction()
    {
        var (kernel, world) = NewKernel();
        Observe(kernel, "scroll01-v1");
        var v1Container = Assert.Single(world.Current!.Containers).Identity.ContainerId;

        // signature 不同的帧（popup04 真页）在场携带 scroll TransitionContext
        // （prior 说 scroll）——反证压过 prior：必须 New 而非 Matched
        Observe(kernel, "popup04-page", ObservationContext.PostActionEffectFlow,
            new TransitionContext("scroll", "attempt-e6", TransitionStrength.Attempt));

        // 第二帧（popup04）的 signature New：prior 在场（correlation 留痕）仍判 New
        var popupSignatureDecision = Assert.Single(kernel.AssociationLog,
            d => d.EffectiveKind == AssociationDispositionKind.New
                && d.TransitionCorrelation == "attempt-e6");
        Assert.NotEqual(v1Container, popupSignatureDecision.EstablishedContainerId);
        Assert.DoesNotContain(kernel.AssociationLog,
            d => d.EffectiveKind == AssociationDispositionKind.Matched);

        var containers = world.Current!.Containers;
        Assert.Equal(2, containers.Count); // v1 container 仍在 belief
        Assert.Contains(containers, c => c.Identity.ContainerId == v1Container);
    }

    // ---- E7：replay 确定性 -----------------------------------------------------

    [Fact]
    public void E7_SameFrameSequenceOnFreshKernels_ReplaysIdenticalIdsAndDecisions()
    {
        var run1 = ReplaySequence();
        var run2 = ReplaySequence();

        Assert.Equal(run1.ContainerIdsPerRevision, run2.ContainerIdsPerRevision);
        Assert.Equal(run1.OccurrenceIdsPerRevision, run2.OccurrenceIdsPerRevision);
        Assert.Equal(run1.AssociationDecisions, run2.AssociationDecisions);
        Assert.Equal(run1.ContinuityDecisions, run2.ContinuityDecisions);
        Assert.Equal(run1.LogicalItemIds, run2.LogicalItemIds);
    }

    private sealed record ReplayRun(
        List<string> ContainerIdsPerRevision,
        List<string> OccurrenceIdsPerRevision,
        List<string> AssociationDecisions,
        List<string> ContinuityDecisions,
        List<string> LogicalItemIds);

    private static ReplayRun ReplaySequence()
    {
        var (kernel, world) = NewKernel();
        Observe(kernel, "popup09-before");
        Observe(kernel, "popup01-dialog", ObservationContext.PostActionEffectFlow,
            new TransitionContext("open-overlay", "attempt-e7a", TransitionStrength.Attempt));
        Observe(kernel, "scroll01-v1");

        var anchor = world.Current!.Occurrences!.Single(o => o.SemanticDescriptor == "Item 01");
        var handle = world.RegisterContinuityDemand(new ContinuityDemand(
            "demand-e7", ContinuityDemandSourceKind.EffectTargetCommitment,
            OwningContainerId: null, Role: "TextView", SemanticDescriptor: "Item 01",
            anchor.OccurrenceId, world.Current.RevisionId, LogicalItemId: null));
        world.ResolveContinuity(handle);

        Observe(kernel, "scroll01-v2", ObservationContext.PostActionEffectFlow,
            new TransitionContext("scroll", "attempt-e7b", TransitionStrength.Attempt));
        world.ResolveContinuity(handle);

        return new ReplayRun(
            world.RevisionHistory.Select(r =>
                string.Join("|", r.Containers.Select(c => c.Identity.ContainerId))).ToList(),
            world.RevisionHistory.Select(r =>
                string.Join("|", (r.Occurrences ?? Array.Empty<OccurrenceBelief>())
                    .Select(o => o.OccurrenceId))).ToList(),
            kernel.AssociationLog.Select(d =>
                $"{d.RevisionId}:{d.EvidenceId}:{d.ProposedKind}:{d.EffectiveKind}:" +
                $"{d.MatchedContainerId}:{d.EstablishedContainerId}:{d.Reason}:{d.TransitionCorrelation}").ToList(),
            world.ContinuityLog.Select(d =>
                $"{d.RevisionId}:{d.DemandId}:{d.ProposedOutcome}:{d.EffectiveOutcome.Kind}:" +
                $"{d.EffectiveOutcome.AdjudicationKind}:{d.MatchedOccurrenceId}:{d.MatchedLogicalItemId}:{d.Reason}").ToList(),
            (world.Current!.LogicalItems ?? Array.Empty<LogicalItemBelief>())
                .Select(i => i.LogicalItemId).ToList());
    }
}

// ---- corpus 约定真实策略（test 侧，PER-002 “corpus 约定留 corpus 层” 先例）----
// 全部确定性：仅由调用序列（record.Provenance.Scope 分帧）+ 静态 corpus 事实
// 驱动；零 wall-clock / random / GUID。

/// <summary>
/// CorpusObservationStrategy（D3）：按 record.Provenance.Scope 分 frame
/// （FastPerception 产物 = "artifact:&lt;id&gt;"）；维护当前 frame 的 claim 累积，
/// 新 scope 到达 = 重置新 frame（revision-local 语义再现）。从累积 claims 按
/// corpus 约定派生 ProposedOccurrence：keyed node（ui.node/ui.detect 的
/// &lt;key&gt;.class claim 存在）→ Role = class 值；SemanticDescriptor = 同 key 的
/// ui.text.&lt;key&gt; 值（若有）；每 key 一个 proposal，按 key 排序（确定性）。
/// OwningContainerId = null（container 归属是 association 产物，不在此发明）。
/// </summary>
public sealed class CorpusObservationStrategy : IUiObservationStrategy
{
    private readonly Dictionary<string, string> _claims = new();
    private string? _frameScope;

    public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
    {
        var scope = record.Provenance.Scope;
        if (scope != _frameScope)
        {
            _frameScope = scope;
            _claims.Clear();
        }
        _claims[record.Claim.Subject] = record.Claim.Value;

        return _claims.Keys
            .Where(IsKeyedClassSubject)
            .Select(subject => (Key: subject.Split('.')[2], Role: _claims[subject]))
            .OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => new ProposedOccurrence(
                OwningContainerId: null,
                Role: k.Role,
                SemanticDescriptor: _claims.TryGetValue("ui.text." + k.Key, out var text) ? text : null))
            .ToArray();
    }

    private static bool IsKeyedClassSubject(string subject)
    {
        var parts = subject.Split('.');
        return parts.Length == 4
            && parts[0] == "ui"
            && (parts[1] == "node" || parts[1] == "detect")
            && parts[3] == "class";
    }
}

/// <summary>
/// CorpusAssociationStrategy（D4）：真实 signature Matched/New + dialog
/// Overlays。signature claim 约定：WorldModel 在 Matched/New 后写
/// ui.container.signature.&lt;id&gt;（读 previous.WorldState）。相等 → Matched
/// （supporting = previous 中该 container signature claim 的 evidence id；取不到
/// 用当前 record id——gate 要求 supporting ⊆ previous basis ∪ {当前}）；无相等
/// 或 previous 无容器 → New。dialog 帧（ui.text.alertTitle）→ New + Overlays
/// （source = 新 dialog，target = previous current container，supporting = 当前帧
/// evidence；dialog id 按与 WorldModel 相同的确定性铸造约定推导——double 知晓
/// realization 约定，product 不感知）。legacy-direct 帧无 signature/dialog 信号
/// → 帧首条记录建 root container（corpus 约定，E1）。P22 prior（input.Transition）
/// 只作 ranking 提示：本策略简单实现忽略之——MUST NOT 因 prior 改判（E6 负向由
/// 签名变化驱动）。
/// </summary>
public sealed class CorpusAssociationStrategy : IAssociationStrategy
{
    public const string PageSignatureSubject = "perception.page.signature";
    public const string DialogTitleSubject = "ui.text.alertTitle";

    private readonly IReadOnlySet<string> _signatureScopes;
    private readonly IReadOnlySet<string> _dialogScopes;
    private string? _frameScope;
    private bool _frameDecided;

    public CorpusAssociationStrategy(IReadOnlySet<string> signatureScopes, IReadOnlySet<string> dialogScopes) =>
        (_signatureScopes, _dialogScopes) = (signatureScopes, dialogScopes);

    public AssociationProposal Propose(AssociationInput input)
    {
        var scope = input.Current.Provenance.Scope;
        if (scope != _frameScope)
        {
            _frameScope = scope;
            _frameDecided = false;
        }
        var evId = input.Current.EvidenceId;
        var subject = input.Current.Claim.Subject;

        // dialog 帧：alertTitle 文本记录驱动 dialog-New + Overlays
        if (subject == DialogTitleSubject && _dialogScopes.Contains(scope))
        {
            if (input.Previous is { Containers.Count: > 0 } previous && !_frameDecided)
            {
                _frameDecided = true;
                var pageId = previous.WorldState.TryGetValue(WorldModel.CurrentContainerSubject, out var current)
                    && previous.Containers.Any(c => c.Identity.ContainerId == current.Value)
                    ? current.Value
                    : previous.Containers[0].Identity.ContainerId;
                var dialogId = "ctr-" + evId[3..15];
                return new AssociationProposal(
                    AssociationDispositionKind.New, MatchedContainerId: null,
                    new[] { new AssociationCandidate("(new)", new[] { evId }, Array.Empty<string>()) },
                    new[] { new ProposedRelation(ContainerRelationKind.Overlays, dialogId, pageId, new[] { evId }) },
                    "corpus-dialog-overlay-new");
            }
            return Insufficient("no-page-context");
        }

        // signature 帧：page signature 驱动 Matched/New
        if (subject == PageSignatureSubject && _signatureScopes.Contains(scope))
        {
            if (_frameDecided)
                return Insufficient("frame-already-associated");
            _frameDecided = true;
            var signature = input.Current.Claim.Value;
            if (input.Previous is { } prev)
            {
                foreach (var container in prev.Containers)
                {
                    if (prev.WorldState.TryGetValue(
                            WorldModel.SignatureSubjectPrefix + container.Identity.ContainerId, out var claim)
                        && claim.Value == signature)
                    {
                        var support = string.IsNullOrEmpty(claim.EvidenceId) ? evId : claim.EvidenceId;
                        return new AssociationProposal(
                            AssociationDispositionKind.Matched, container.Identity.ContainerId,
                            new[] { new AssociationCandidate(container.Identity.ContainerId,
                                new[] { support }, Array.Empty<string>()) },
                            Relations: Array.Empty<ProposedRelation>(), Reason: "corpus-signature-match");
                    }
                }
                return New(evId, "corpus-signature-unseen");
            }
            return New(evId, "corpus-first-observation");
        }

        // legacy-direct 帧（无 signature/dialog 信号，如 golden-run 真帧）：
        // 帧首条记录建 root container（previous 无容器）
        if (!_signatureScopes.Contains(scope) && !_dialogScopes.Contains(scope)
            && !_frameDecided
            && (input.Previous is null || input.Previous.Containers.Count == 0))
        {
            _frameDecided = true;
            return New(evId, "corpus-legacy-first-observation");
        }

        return Insufficient("no-identity-signal");
    }

    private static AssociationProposal New(string evId, string reason) => new(
        AssociationDispositionKind.New, MatchedContainerId: null,
        new[] { new AssociationCandidate("(new)", new[] { evId }, Array.Empty<string>()) },
        Relations: Array.Empty<ProposedRelation>(), Reason: reason);

    private static AssociationProposal Insufficient(string reason) => new(
        AssociationDispositionKind.Insufficient, MatchedContainerId: null,
        Candidates: Array.Empty<AssociationCandidate>(),
        Relations: Array.Empty<ProposedRelation>(), Reason: reason);
}

/// <summary>
/// CorpusContinuityStrategy（D5）：demand.SemanticDescriptor 为 anchor。
/// mint 路径：demand 尚无 LogicalItemId 且 anchor occurrence（或唯一描述相等
/// 候选）在 current → ReferenceEstablished。判别路径：恰好一个候选描述 ==
/// anchor → SameReferent；≥2 相等 → Ambiguous；零相等但存在描述不等的唯一
/// 候选 → Contradicted；零相等且 anchor 带 keyed 槽位（occurrence id 尾段
/// 序位，corpus 约定：同帧 keyed 节点集合稳定 → 序位稳定）时，同序位候选
/// 描述存在且不等 → Contradicted（滚动行位移真值）；anchor 缺失或候选描述
/// 缺失 → Insufficient。
/// </summary>
public sealed class CorpusContinuityStrategy : IContinuityStrategy
{
    public ContinuityProposal Propose(ContinuityAdjudicationInput input)
    {
        var demand = input.Demand;
        var anchor = demand.SemanticDescriptor;

        if (demand.LogicalItemId is null)
        {
            var occurrence = demand.AnchorOccurrenceId is null
                ? null
                : input.Candidates.FirstOrDefault(c => c.OccurrenceId == demand.AnchorOccurrenceId);
            if (occurrence is null && anchor is not null)
            {
                var equals = input.Candidates.Where(c => c.SemanticDescriptor == anchor).ToList();
                if (equals.Count == 1)
                    occurrence = equals[0];
            }
            return occurrence is null
                ? Insufficient("no-anchor-candidate")
                : new ContinuityProposal(
                    ContinuityProposedOutcomeKind.ReferenceEstablished,
                    occurrence.OccurrenceId, MatchedLogicalItemId: null,
                    occurrence.SupportingEvidenceIds.ToArray(), ContradictingEvidenceIds: Array.Empty<string>(),
                    TerminatedItems: Array.Empty<ProposedTermination>(), Reason: "corpus-anchor-mint");
        }

        if (anchor is null)
            return Insufficient("anchor-descriptor-missing");

        var exact = input.Candidates.Where(c => c.SemanticDescriptor == anchor).ToList();
        if (exact.Count == 1)
            return new ContinuityProposal(
                ContinuityProposedOutcomeKind.SameReferent,
                exact[0].OccurrenceId, demand.LogicalItemId,
                exact[0].SupportingEvidenceIds.ToArray(), Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "corpus-descriptor-unique-match");
        if (exact.Count >= 2)
            return Ambiguous("corpus-descriptor-multi-match");

        var described = input.Candidates.Where(c => c.SemanticDescriptor is not null).ToList();
        if (described.Count == 1 && described[0].SemanticDescriptor != anchor)
            return Contradicted(described[0], demand.LogicalItemId, "corpus-sole-candidate-mismatch");

        var slot = KeyedSlotIndex(demand.AnchorOccurrenceId);
        if (slot.HasValue && slot.Value >= 0 && slot.Value < input.Candidates.Count)
        {
            var slotCandidate = input.Candidates[slot.Value];
            if (slotCandidate.SemanticDescriptor is not null && slotCandidate.SemanticDescriptor != anchor)
                return Contradicted(slotCandidate, demand.LogicalItemId, "corpus-keyed-slot-mismatch");
        }
        return Insufficient("no-discriminating-candidate");
    }

    /// <summary>occurrence id 尾段序位（WorldModel 铸造约定：occ-&lt;hash&gt;-&lt;index&gt;）。</summary>
    private static int? KeyedSlotIndex(string? occurrenceId)
    {
        if (occurrenceId is null)
            return null;
        var lastDash = occurrenceId.LastIndexOf('-');
        return lastDash >= 0 && int.TryParse(occurrenceId[(lastDash + 1)..], out var index)
            ? index
            : null;
    }

    private static ContinuityProposal Contradicted(
        ContinuityCandidateOccurrence candidate, string itemId, string reason) => new(
        ContinuityProposedOutcomeKind.Contradicted,
        candidate.OccurrenceId, itemId,
        candidate.SupportingEvidenceIds.ToArray(), Array.Empty<string>(),
        Array.Empty<ProposedTermination>(), reason);

    private static ContinuityProposal Ambiguous(string reason) => new(
        ContinuityProposedOutcomeKind.Ambiguous, null, null,
        Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<ProposedTermination>(), reason);

    private static ContinuityProposal Insufficient(string reason) => new(
        ContinuityProposedOutcomeKind.Insufficient, null, null,
        Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<ProposedTermination>(), reason);
}
