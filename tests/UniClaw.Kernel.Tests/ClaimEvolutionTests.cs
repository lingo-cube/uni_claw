using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Tests.Perception;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// CLE-001 验收 A1–A6 —— Claim Evolution realization（frame/producer 域
/// Revise + Reaffirm，ADR-0016 / UWM-009 §18）：
/// 同 producer 异 scope 的再观察 = presentation re-observation → Revise
///（值替换 + SupersededEvidenceIds 痕迹链 + ClaimEvolutionLog，不产生
/// Conflict）；同值 → Reaffirm（belief 零变化，log 留痕）；同 producer 同
/// scope 异值 / 异 producer 异值 → Conflict（语义保持）。
/// 全部确定性（level: DETERMINISTIC）；A1/A2 用真实语料 scroll01 v1→v2
///（复用 RealAssetEntityModelTests / FastPerceptionSliceTests 的 corpus
/// 管线 helper 模式）。
/// </summary>
public sealed class ClaimEvolutionTests
{
    private static readonly CorpusManifest Corpus = CorpusManifest.Load();
    private static readonly TransitionContext Scroll = new("scroll", "attempt-cle-1", TransitionStrength.Attempt);
    private static readonly DateTimeOffset T0 = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 9, 10, 1, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 9, 9, 10, 2, 0, TimeSpan.Zero);
    private const string Row = "ui.text.row";

    // ---- corpus 管线 helpers（RealAssetEntityModelTests / FastPerceptionSliceTests 模式）----

    private static FastPerception Perception(string detectionSet) =>
        new("perception.fast", new CorpusFastPerception(Corpus, detectionSet));

    private static (UniKernel Kernel, WorldModel World) CorpusKernel(
        IAssociationStrategy strategy, params string[] extraScope)
    {
        var scope = new HashSet<string>(Corpus.SubjectScope);
        foreach (var s in extraScope) scope.Add(s);
        var world = new WorldModel(scope, strategy);
        return (new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance), world);
    }

    private static void Observe(UniKernel kernel, string detectionSet,
        ObservationContext context = ObservationContext.External,
        TransitionContext? transition = null)
    {
        foreach (var proposal in Perception(detectionSet).Observe(Corpus.Artifact(detectionSet), context))
            kernel.Process(proposal, transition);
    }

    /// <summary>自定义 provenance 观察（A3–A6 构造域：producer/scope 全可控）。</summary>
    private static ObservationProposal Observation(
        string subject, string value, string producer, string scope, DateTimeOffset t) =>
        new(new ObservationClaim(subject, value), IngressKind.Observation, ObservationContext.External,
            new Provenance(producer, t, scope, new[] { "raw://capture", "encode:v1" }));

    private static (UniKernel Kernel, WorldModel World) PlainWorld() =>
        CorpusKernel(new SignatureHintAssociationStrategy(), Row);

    // ---- A1：真实语料 scroll v1→v2 → Revise -------------------------------

    [Fact]
    public void A1_ScrollV1ToV2_RowShiftRevisesNotConflicts()
    {
        var (kernel, world) = CorpusKernel(new SignatureHintAssociationStrategy());
        Observe(kernel, "scroll01-v1");
        var v1Revision = world.Current!;
        var v1Evidence = v1Revision.WorldState["ui.text.row_title"].EvidenceId;
        var conflictsAfterV1 = v1Revision.Conflicts.Count;

        Observe(kernel, "scroll01-v2", ObservationContext.PostActionEffectFlow, Scroll);

        var current = world.Current!;
        // 同 producer（perception.fast）异 scope（artifact v1/v2）→ Revise：
        // 行文本位移（Item 01 → Item 02）新值生效
        Assert.Equal("Item 02", current.WorldState["ui.text.row_title"].Value);
        Assert.NotEqual(v1Evidence, current.WorldState["ui.text.row_title"].EvidenceId);
        // 痕迹链含 v1 establishing evidence（不静默覆盖）
        Assert.NotNull(current.WorldState["ui.text.row_title"].SupersededEvidenceIds);
        Assert.Contains(v1Evidence, current.WorldState["ui.text.row_title"].SupersededEvidenceIds);
        // 零新 Conflict / ConflictingClaimCount 不增
        Assert.DoesNotContain(current.Conflicts, c => c.Subject.StartsWith("ui.text.row_title", StringComparison.Ordinal));
        Assert.Equal(conflictsAfterV1, current.Conflicts.Count);
        Assert.Equal(conflictsAfterV1, current.Uncertainty.ConflictingClaimCount);
    }

    // ---- A2：Slice ScopedClaims 供 v2 值（stale 呈现消除）------------------

    [Fact]
    public void A2_SliceScopedClaimsServeV2Value_AfterRevise()
    {
        // 分区约定 subject = <containerId>.<rest>（UIW-004 / S8 先例）：
        // 真实语料帧建立 root container，scoped row claim 随真实帧 scope 演进
        var seed = Perception("scroll01-v1").Observe(Corpus.Artifact("scroll01-v1"))
            .Single(p => p.Claim.Subject == SignatureHintAssociationStrategy.SignatureSubject);
        var (probeAdmission, _) = new EvidenceLedger().Admit(seed);
        var root = "ctr-" + probeAdmission.EvidenceId![3..15];
        var scopedSubject = $"{root}.ui.text.row_title";
        var (kernel, world) = CorpusKernel(new SignatureHintAssociationStrategy(), scopedSubject);

        Observe(kernel, "scroll01-v1");
        var v1Scope = $"artifact:{Corpus.Artifact("scroll01-v1").ArtifactId}";
        var v2Scope = $"artifact:{Corpus.Artifact("scroll01-v2").ArtifactId}";
        kernel.Process(Observation(scopedSubject, "Item 01", "perception.fast", v1Scope, T0));
        var v1Evidence = world.Current!.WorldState[scopedSubject].EvidenceId;

        // v2 全帧 + scoped row claim 随 v2 scope 再观察（同 producer 异 scope）
        Observe(kernel, "scroll01-v2", ObservationContext.PostActionEffectFlow, Scroll);
        kernel.Process(Observation(scopedSubject, "Item 02", "perception.fast", v2Scope, T1));

        Assert.Contains(v1Evidence, world.Current!.WorldState[scopedSubject].SupersededEvidenceIds);
        // Slice 供 v2 值：Revise 后消费侧不再看到 stale v1 呈现
        var slice = world.DeriveSlice(root);
        Assert.Equal("Item 02", slice.ScopedClaims[scopedSubject]);
        Assert.DoesNotContain(world.Current.Conflicts, c => c.Subject == scopedSubject);
    }

    // ---- A3：同帧内矛盾（同 producer 同 scope 异值）→ Conflict 保持 --------

    [Fact]
    public void A3_SameProducerSameScopeDifferentValue_StaysConflict()
    {
        var (kernel, world) = PlainWorld();
        var r1 = kernel.Process(Observation(Row, "Item 01", "perception.fast", "artifact:frame-1", T0));
        var r2 = kernel.Process(Observation(Row, "Item 99", "perception.fast", "artifact:frame-1", T1));

        var revision = r2.ResultingRevision!;
        var conflict = Assert.Single(revision.Conflicts);
        Assert.Equal(Row, conflict.Subject);
        Assert.Equal("Item 01", conflict.EstablishedValue);
        Assert.Equal("Item 99", conflict.ChallengingValue);
        Assert.Equal(r1.Admission.EvidenceId, conflict.EstablishedEvidenceId);
        Assert.Equal(r2.Admission.EvidenceId, conflict.ChallengingEvidenceId);
        // established 保留（latest 不获胜）
        Assert.Equal("Item 01", revision.WorldState[Row].Value);
        Assert.Equal(1, revision.Uncertainty.ConflictingClaimCount);
        // 同 scope 矛盾不是 Revise：无演进留痕
        Assert.DoesNotContain(world.ClaimEvolutionLog, d => d.Subject == Row && d.Kind == ClaimEvolutionKind.Revise);
    }

    // ---- A4：异 producer 异值 → Conflict 保持 ------------------------------

    [Fact]
    public void A4_DifferentProducerDifferentValue_StaysConflict()
    {
        var (kernel, world) = PlainWorld();
        kernel.Process(Observation(Row, "Item 01", "perception.fast", "artifact:frame-1", T0));
        var cross = kernel.Process(Observation(Row, "Item 50", "perception.slow", "artifact:frame-2", T1));

        var revision = cross.ResultingRevision!;
        var conflict = Assert.Single(revision.Conflicts);
        Assert.Equal(Row, conflict.Subject);
        Assert.Equal("Item 50", conflict.ChallengingValue);
        Assert.Equal("Item 01", revision.WorldState[Row].Value); // 跨源矛盾不压平
        Assert.DoesNotContain(world.ClaimEvolutionLog, d => d.Subject == Row && d.Kind == ClaimEvolutionKind.Revise);
    }

    // ---- A5：同值再观察 → Reaffirm（belief 不变，log 留痕）-----------------

    [Fact]
    public void A5_SameValueReobservationIsReaffirm_BeliefUnchangedLogEntries()
    {
        var (kernel, world) = PlainWorld();
        var r1 = kernel.Process(Observation(Row, "Item 01", "perception.fast", "artifact:frame-1", T0));
        var e1 = r1.Admission.EvidenceId!;
        var r2 = kernel.Process(Observation(Row, "Item 01", "perception.fast", "artifact:frame-2", T1));

        var revision = r2.ResultingRevision!;
        // belief 零变化：值 + establishing evidence 不动（幂等路径照旧）
        var claim = revision.WorldState[Row];
        Assert.Equal("Item 01", claim.Value);
        Assert.Equal(e1, claim.EvidenceId);
        Assert.Null(claim.SupersededEvidenceIds);
        Assert.Empty(revision.Conflicts);
        Assert.Equal(0, revision.Uncertainty.ConflictingClaimCount);

        // ClaimEvolutionLog 留痕（Reaffirm）
        var entry = Assert.Single(world.ClaimEvolutionLog, d => d.Subject == Row);
        Assert.Equal(ClaimEvolutionKind.Reaffirm, entry.Kind);
        Assert.Equal("Item 01", entry.PreviousValue);
        Assert.Equal("Item 01", entry.NewValue);
        Assert.Null(entry.SupersededEvidenceId);
        Assert.Equal(e1, entry.EstablishingEvidenceId);
        Assert.Equal("perception.fast", entry.Producer);
        Assert.Equal("artifact:frame-2", entry.Scope);
    }

    // ---- A6：replay 确定性 + ClaimEvolutionLog 全链溯源 --------------------

    [Fact]
    public void A6_ReplayDeterministic_LogCarriesFullEvolutionChain()
    {
        // 双实例 replay 同一语义输入序列（v1→v2→v3，同 producer 异 scope）
        var run1 = Evolve();
        var run2 = Evolve();

        // replay 确定性：log 与最终 belief 完全一致
        Assert.Equal(run1.Log, run2.Log);
        Assert.Equal(
            (run1.FinalClaim.Value, run1.FinalClaim.EvidenceId,
                run1.FinalClaim.EstablishingProducer, run1.FinalClaim.EstablishingScope),
            (run2.FinalClaim.Value, run2.FinalClaim.EvidenceId,
                run2.FinalClaim.EstablishingProducer, run2.FinalClaim.EstablishingScope));
        Assert.Equal(
            run1.FinalClaim.SupersededEvidenceIds!.OrderBy(x => x),
            run2.FinalClaim.SupersededEvidenceIds!.OrderBy(x => x));

        // 全链溯源：subject 历代 value/evidence 均可从 log + 痕迹链恢复
        Assert.Equal(2, run1.Log.Count);
        var first = run1.Log[0];
        var second = run1.Log[1];
        Assert.All(run1.Log, d => Assert.Equal(ClaimEvolutionKind.Revise, d.Kind));
        Assert.Equal("Item 01", first.PreviousValue);
        Assert.Equal("Item 02", first.NewValue);
        Assert.Equal(run1.E1, first.SupersededEvidenceId);
        Assert.Equal(run1.E2, first.EstablishingEvidenceId);
        Assert.Equal("Item 02", second.PreviousValue);
        Assert.Equal("Item 03", second.NewValue);
        Assert.Equal(run1.E2, second.SupersededEvidenceId);
        Assert.Equal(run1.E3, second.EstablishingEvidenceId);
        // 链相邻衔接：后一次 Revise 的 superseded = 前一次的 establishing
        Assert.Equal(first.EstablishingEvidenceId, second.SupersededEvidenceId);
        // 最终 claim 痕迹链 = 旧链 ∪ 历代 superseded
        Assert.Equal(new[] { run1.E1, run1.E2 },
            run1.FinalClaim.SupersededEvidenceIds!.OrderBy(x => x).ToArray());

        static (IReadOnlyList<ClaimEvolutionDecision> Log, WorldClaim FinalClaim, string E1, string E2, string E3) Evolve()
        {
            var (kernel, world) = PlainWorld();
            var r1 = kernel.Process(Observation(Row, "Item 01", "perception.fast", "artifact:frame-1", T0));
            var r2 = kernel.Process(Observation(Row, "Item 02", "perception.fast", "artifact:frame-2", T1));
            var r3 = kernel.Process(Observation(Row, "Item 03", "perception.fast", "artifact:frame-3", T2));
            return (world.ClaimEvolutionLog.Where(d => d.Subject == Row).ToArray(),
                world.Current!.WorldState[Row],
                r1.Admission.EvidenceId!, r2.Admission.EvidenceId!, r3.Admission.EvidenceId!);
        }
    }
}
