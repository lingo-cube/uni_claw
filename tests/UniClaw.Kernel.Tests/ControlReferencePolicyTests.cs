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

namespace UniClaw.Kernel.Tests;

/// <summary>
/// CTL-001 验收 T1–T7 —— Control 参考确定性 policy（DescriptorTargetPolicy，
/// 重构后 Slice occurrence 景观的第一个真实 reader，ADR-0011）+ traversal
/// 簿记（descriptor-keyed visited，D2）+ UniKernel 最小接地编排缝
/// （ActViaCurrentGrounding，D3：非 Unique 候选 → Act=null，不强制选择）。
/// 真实资产场景复用 PER-002 corpus 管线（golden-case-a-before 真帧 /
/// nav03-parent 真三按钮）；T5 为 synthetic double（如实标注）。
/// 全部确定性：零 wall-clock / random（D4）。
/// </summary>
public sealed class ControlReferencePolicyTests
{
    private static readonly CorpusManifest Corpus = CorpusManifest.Load();
    private static readonly DateTimeOffset ActTime = new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<IReadOnlySet<string>> SignatureScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.PageSignatureSubject));

    private static readonly Lazy<IReadOnlySet<string>> DialogScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.DialogTitleSubject));

    private static IReadOnlySet<string> ScopesWithSubject(string subject) => Corpus.Scenarios
        .Where(s => s.Observations.Any(o => o.Subject == subject))
        .Select(s => "artifact:" + Corpus.Artifact(s.ScenarioId).ArtifactId)
        .ToHashSet();

    // ---- 测试替身 ---------------------------------------------------------

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(CanonicalBinding binding) =>
            new(DispatchOutcome.Delivered, "scripted:ok", ActTime);
    }

    private sealed class FailingDriver : IEffectDriver
    {
        public DispatchResult Deliver(CanonicalBinding binding) =>
            new(DispatchOutcome.Failed, "scripted:fail", ActTime);
    }

    /// <summary>synthetic double（如实标注）：同 owner 同 (Role, SemanticDescriptor)
    /// 双 occurrence——T5 双胞胎场景；真实语料无同 role+descriptor 对。</summary>
    private sealed class TwinObservationStrategy : IUiObservationStrategy
    {
        private readonly string _owner;
        public TwinObservationStrategy(string owner) => _owner = owner;

        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            new[]
            {
                new ProposedOccurrence(_owner, "button", "duplicate"),
                new ProposedOccurrence(_owner, "button", "duplicate"),
            };
    }

    /// <summary>SeedContainer 首条 evidence 探针（与 UIWorldGroundingSeamTests
    /// ProbeContainerId 同法）：确定性预知 minted root container id。</summary>
    private static string ProbeContainerId(string seedValue)
    {
        var (admission, _) = new EvidenceLedger().Admit(
            UIWorldDoubles.Observation(seedValue, UIWorldDoubles.T0));
        return "ctr-" + admission.EvidenceId![3..15];
    }

    // ---- 组装 / corpus 管线 helper（照抄 RealAssetEntityModelTests 模式）----

    private static (UniKernel Kernel, WorldModel World, DescriptorTargetPolicy Policy,
        EffectBoundary Effects) NewCorpusKernel(
            TargetSpec[] specs, IEffectDriver? driver = null,
            string objective = "tap-the-children", string allowedEffect = "tap")
    {
        var world = new WorldModel(
            Corpus.SubjectScope,
            new CorpusAssociationStrategy(SignatureScopes.Value, DialogScopes.Value),
            new OwnedCorpusObservationStrategy(),
            new CorpusContinuityStrategy());
        var policy = new DescriptorTargetPolicy(specs);
        var effects = new EffectBoundary(driver ?? new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()), effects);
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: objective,
            Scope: new HashSet<string> { "perception.page.signature" },
            AllowedEffects: new HashSet<string> { allowedEffect },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "target-acted" }));
        return (kernel, world, policy, effects);
    }

    /// <summary>scenario → RawArtifact.Capture（真实 PNG bytes）→
    /// FastPerception("perception.corpus") → 逐条 Process（RealAssetEntityModelTests
    /// 同管线）。post-action context 变体 → 新 evidence ids → 新 revision +
    /// 新铸 occurrence ids（R-UW-01 内容派生）。</summary>
    private static void Observe(UniKernel kernel, string scenarioId,
        ObservationContext context = ObservationContext.External,
        TransitionContext? transition = null)
    {
        var perception = new FastPerception(
            "perception.corpus", new CorpusFastPerception(Corpus, scenarioId));
        foreach (var proposal in perception.Observe(Corpus.Artifact(scenarioId), context))
            kernel.Process(proposal, transition);
    }

    /// <summary>revision advance：向同一 frame scope 追加一条唯一 capture time
    /// 的 evidence（真实 corpus subject/value 原值复述，同值无 conflict）→ 新
    /// revision；observation strategy 按累积 claims 重铸全量 occurrence（新
    /// evidence id → 新 occ ids）。replay 确定：capture time 为固定常量序列。</summary>
    private static void AdvanceRevision(UniKernel kernel, string scenarioId, int round)
    {
        var artifact = Corpus.Artifact(scenarioId);
        var (subject, value) = Corpus.Scenario(scenarioId).Observations[0];
        kernel.Process(new ObservationProposal(
            new ObservationClaim(subject, value),
            IngressKind.Observation, ObservationContext.PostActionEffectFlow,
            new Provenance(
                Producer: "perception.corpus",
                CaptureTime: ActTime.AddMinutes(round),
                Scope: $"artifact:{artifact.ArtifactId}",
                TransformationLineage: new[] { "fast:perception.corpus", $"artifact:{artifact.ArtifactId}" })));
    }

    // ---- T1：golden-run 真帧 → switch occurrence 端到端 ---------------------

    [Fact]
    public void T1_GoldenRunRealSwitchOccurrence_Selects_Grounds_Dispatches()
    {
        var (kernel, world, _, _) = NewCorpusKernel(
            new[] { new TargetSpec("switch", SemanticDescriptor: null, EffectClass: "toggle") },
            objective: "toggle-the-switch", allowedEffect: "toggle");
        Observe(kernel, "golden-case-a-before");
        var root = world.Current!.Containers.Single().Identity.ContainerId;

        // 真实 occurrence 景观进 Slice（真实 YOLO class 值：icon 6 / text_block 9 /
        // switch 1 = 16，descriptor 均无 ui.text.<key> → null——先探后断的真值）
        var slice = kernel.DeriveSlice(root);
        Assert.Equal(16, slice.Occurrences.Count);
        Assert.Equal(6, slice.Occurrences.Count(o => o.Role == "icon"));
        Assert.Equal(9, slice.Occurrences.Count(o => o.Role == "text_block"));
        var switchOccurrence = Assert.Single(slice.Occurrences, o => o.Role == "switch");
        Assert.Null(switchOccurrence.SemanticDescriptor);

        var intent = kernel.SelectIntent(slice);
        Assert.Equal(ControlIntentKind.Act, intent.Kind);
        Assert.Equal("toggle", intent.EffectClass);
        Assert.Equal("switch", intent.TargetSubject);

        var grounded = kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("switch"));
        Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, grounded.View.Result);
        Assert.NotNull(grounded.Act);
        Assert.Equal(switchOccurrence.OccurrenceId, grounded.Act!.Binding!.Canonical!.TargetOccurrenceId);
        Assert.NotNull(grounded.Act.Receipt);
        Assert.Equal(switchOccurrence.OccurrenceId, grounded.Act.Receipt!.TargetSubject);
    }

    // ---- T2/T6/T7 共享：nav03-parent 真三按钮依序 traversal -----------------

    private sealed record TraversalTrace(
        List<string> IntentKinds,
        List<string?> IntentSubjects,
        List<string> ReceiptTargets,
        List<string> ChildAIdPerRound,
        int FinalVisitedCount);

    private static TraversalTrace RunNav03Traversal()
    {
        var children = new[] { "CHILD A", "CHILD B", "CHILD C" };
        var specs = children.Select(c => new TargetSpec("Button", c, "tap")).ToArray();
        var (kernel, world, policy, _) = NewCorpusKernel(specs);
        Observe(kernel, "nav03-parent");
        var root = world.Current!.Containers.Single().Identity.ContainerId;

        var kinds = new List<string>();
        var subjects = new List<string?>();
        var receipts = new List<string>();
        var childAIds = new List<string>();
        for (var round = 0; round < 3; round++)
        {
            var slice = kernel.DeriveSlice(root);
            var intent = kernel.SelectIntent(slice);
            kinds.Add(intent.Kind.ToString());
            subjects.Add(intent.TargetSubject);

            var grounded = kernel.ActViaCurrentGrounding(
                intent, new TargetDescriptor("Button", children[round]));
            Assert.NotNull(grounded.Act);
            Assert.NotNull(grounded.Act!.Receipt);
            receipts.Add(grounded.Act.Receipt!.TargetSubject);
            childAIds.Add(slice.Occurrences.Single(o => o.SemanticDescriptor == "CHILD A").OccurrenceId);
            Assert.Equal(round + 1, policy.Visited.Count);   // visited 逐轮增长

            // revision advance：追加一条唯一 capture time 的 evidence（新
            // evidence id → 新 revision + 新铸 occurrence ids）
            AdvanceRevision(kernel, "nav03-parent", round);
        }

        // 完毕转 Observe
        var finalIntent = kernel.SelectIntent(kernel.DeriveSlice(root));
        kinds.Add(finalIntent.Kind.ToString());
        subjects.Add(finalIntent.TargetSubject);

        return new TraversalTrace(kinds, subjects, receipts, childAIds, policy.Visited.Count);
    }

    [Fact]
    public void T2_Nav03ThreeRealButtons_TraversedInOrder_NoRepeat_ThenObserve()
    {
        var trace = RunNav03Traversal();

        // 三轮 act 目标依序 A→B→C，第四轮 Observe
        Assert.Equal(
            new[] { "Act", "Act", "Act", "Observe" }, trace.IntentKinds.ToArray());
        Assert.Equal(
            new[] { "Button:CHILD A", "Button:CHILD B", "Button:CHILD C", null },
            trace.IntentSubjects.ToArray());

        // 无重复点击：三个 receipt 目标（occurrence id）互异
        Assert.Equal(3, trace.ReceiptTargets.Distinct().Count());
        // visited 终态 = 3 个 descriptor key
        Assert.Equal(3, trace.FinalVisitedCount);
    }

    // ---- T3：空景观 → Observe ----------------------------------------------

    [Fact]
    public void T3_EmptyOccurrenceLandscape_YieldsObserveIntent()
    {
        // 无 observation strategy 的 world：Slice.Occurrences 恒空（Occurrences
        // null → DeriveSlice 投空集），policy 无可行动目标 → Observe
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            new SeedContainerAssociationStrategy(), observationStrategy: null);
        var policy = new DescriptorTargetPolicy(new[] { new TargetSpec("button", "submit", "tap") });
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(new OkDriver()));
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1", Objective: "tap-the-button",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "button-tapped" }));
        kernel.Process(UIWorldDoubles.Observation("page:seed:v1", UIWorldDoubles.T0));

        var slice = kernel.DeriveSlice(world.Current!.Containers.Single().Identity.ContainerId);
        Assert.Empty(slice.Occurrences);

        var intent = kernel.SelectIntent(slice);
        Assert.Equal(ControlIntentKind.Observe, intent.Kind);
        Assert.Null(intent.EffectClass);
        Assert.Null(intent.TargetSubject);
        Assert.Empty(policy.Visited);
    }

    // ---- T4：dispatch 失败 → 既有强制 Recovery；新 revision 后续行 -----------

    [Fact]
    public void T4_FailedDispatch_ForcesRecoveryOnSameRevision_PolicyContinuesAfterAdvance()
    {
        var (kernel, world, policy, _) = NewCorpusKernel(
            new[] { new TargetSpec("Button", "CHILD A", "tap"), new TargetSpec("Button", "CHILD B", "tap") },
            driver: new FailingDriver());
        Observe(kernel, "nav03-parent");
        var root = world.Current!.Containers.Single().Identity.ContainerId;

        // 第一轮：act A → dispatch failed
        var slice1 = kernel.DeriveSlice(root);
        var intent1 = kernel.SelectIntent(slice1);
        Assert.Equal(ControlIntentKind.Act, intent1.Kind);
        var grounded1 = kernel.ActViaCurrentGrounding(intent1, new TargetDescriptor("Button", "CHILD A"));
        Assert.NotNull(grounded1.Act!.Receipt);
        Assert.Equal(DispatchOutcome.Failed, grounded1.Act.Receipt!.Outcome);

        // 同 revision 再 SelectIntent → ControlLoop 既有 _pendingRecovery 强制
        // Recovery（TargetSubject 沿载失败 receipt 的目标 = occurrence id）
        var recovery = kernel.SelectIntent(kernel.DeriveSlice(root));
        Assert.Equal(ControlIntentKind.Recovery, recovery.Kind);
        Assert.Equal(grounded1.Act.Receipt!.TargetSubject, recovery.TargetSubject);

        // revision advance 后 policy 续行 Act（A 已 visited → B）
        AdvanceRevision(kernel, "nav03-parent", round: 1);
        var intent2 = kernel.SelectIntent(kernel.DeriveSlice(root));
        Assert.Equal(ControlIntentKind.Act, intent2.Kind);
        Assert.Equal("Button:CHILD B", intent2.TargetSubject);
        var grounded2 = kernel.ActViaCurrentGrounding(intent2, new TargetDescriptor("Button", "CHILD B"));
        Assert.NotNull(grounded2.Act);
        Assert.NotNull(grounded2.Act!.Receipt);
        Assert.Equal(2, policy.Visited.Count);
    }

    // ---- T5：双胞胎 → MultipleCandidates、Act=null、零强制 ------------------

    [Fact]
    public void T5_TwinOccurrences_GroundToMultipleCandidates_ActNull_NoForcedChoice()
    {
        // synthetic double（真实语料无同 role+descriptor 对，如实标注）
        var cid = ProbeContainerId("page:twin:v1");
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            new SeedContainerAssociationStrategy(), new TwinObservationStrategy(cid));
        var policy = new DescriptorTargetPolicy(new[] { new TargetSpec("button", "duplicate", "tap") });
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()), effects);
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1", Objective: "tap-the-button",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "button-tapped" }));
        kernel.Process(UIWorldDoubles.Observation("page:twin:v1", UIWorldDoubles.T0));

        var slice = kernel.DeriveSlice(cid);
        Assert.Equal(2, slice.Occurrences.Count(o => o.Role == "button" && o.SemanticDescriptor == "duplicate"));

        var intent = kernel.SelectIntent(slice);
        Assert.Equal(ControlIntentKind.Act, intent.Kind);
        Assert.Equal("button:duplicate", intent.TargetSubject);

        var grounded = kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("button", "duplicate"));
        Assert.Equal(CurrentCandidateSetResultKind.MultipleCandidates, grounded.View.Result);
        Assert.Equal(2, grounded.View.Candidates.Count);
        Assert.Null(grounded.Act);
        // 零强制：无 candidate 构造 / 无 binding 认定 / 无 dispatch
        Assert.Empty(effects.BindingLog);
        Assert.Empty(effects.ReceiptLog);
    }

    // ---- T6：visited 跨 revision 存活（新 occ ids 不重置簿记） ---------------

    [Fact]
    public void T6_VisitedSurvivesRevisionAdvance_NewOccurrenceIds_SameDescriptorNotReselected()
    {
        var trace = RunNav03Traversal();

        // 每轮 revision advance 重铸 occurrence ids：CHILD A 三轮 id 互不相同
        Assert.Equal(3, trace.ChildAIdPerRound.Distinct().Count());
        // 同 descriptor 不再被选中：意图序列 A→B→C→Observe（无 A 回访）
        Assert.Equal(
            new[] { "Button:CHILD A", "Button:CHILD B", "Button:CHILD C", null },
            trace.IntentSubjects.ToArray());
        // visited 终态存活到最后一轮
        Assert.Equal(3, trace.FinalVisitedCount);
    }

    // ---- T7：replay 确定性 ---------------------------------------------------

    [Fact]
    public void T7_SameSequenceTwoFreshInstances_ReplaysIdenticalIntentsVisitedAndReceipts()
    {
        var run1 = RunNav03Traversal();
        var run2 = RunNav03Traversal();

        Assert.Equal(run1.IntentKinds, run2.IntentKinds);
        Assert.Equal(run1.IntentSubjects, run2.IntentSubjects);
        Assert.Equal(run1.ReceiptTargets, run2.ReceiptTargets);
        Assert.Equal(run1.ChildAIdPerRound, run2.ChildAIdPerRound);
        Assert.Equal(run1.FinalVisitedCount, run2.FinalVisitedCount);
    }
}

/// <summary>
/// CTL-001 测试侧 corpus 约定：CorpusObservationStrategy 的 owned 变体。
/// corpus 真实 occurrence（真实 YOLO/ViewNode class 值）原样沿用，唯一 delta =
/// 为 occurrence 指派确定性 OwningContainerId，使 occurrence 进入 DeriveSlice
/// 的 InScope 景观（DeriveSlice 过滤 ownerless occurrence；产品代码不改）。
/// owner = previous revision 的 CurrentContainer（真实 minted container；
/// signature 帧 container 由 signature 记录铸造、legacy 帧由首条记录铸造——
/// 从 previous 读即与 association 产物一致）；previous 尚无 container 时
/// （frame 内 container 铸造前的中间 revision）按同一铸造约定预测
/// "ctr-" + evId[3..15]，仅影响 frame 内被后续 revision 取代的中间态。
/// </summary>
internal sealed class OwnedCorpusObservationStrategy : IUiObservationStrategy
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

        var owner = previous is { Containers.Count: > 0 } prev
            ? prev.WorldState.TryGetValue(WorldModel.CurrentContainerSubject, out var current)
                && prev.Containers.Any(c => c.Identity.ContainerId == current.Value)
                ? current.Value
                : prev.Containers[^1].Identity.ContainerId
            : "ctr-" + record.EvidenceId[3..15];

        return _claims.Keys
            .Where(IsKeyedClassSubject)
            .Select(subject => (Key: subject.Split('.')[2], Role: _claims[subject]))
            .OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => new ProposedOccurrence(
                OwningContainerId: owner,
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
