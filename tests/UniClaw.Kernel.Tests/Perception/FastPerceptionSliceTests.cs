using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;
using Xunit;

using UniClaw.Kernel.Trace;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// PER-002B 验收 S1–S8 —— Fast Perception vertical slice：
/// RawArtifact → FastPerception → ObservationProposal →（P2）EvidenceLedger →
///（P3）WorldModel → Container Association / Reconciliation → WorldBeliefRevision。
/// 全部确定性（level: DETERMINISTIC）；perception 是 deterministic seam double，
/// real model runtime not yet migrated（真实资产 + recorded evidence）。
/// </summary>
public sealed class FastPerceptionSliceTests
{
    private static readonly CorpusManifest Corpus = CorpusManifest.Load();
    private static readonly TransitionContext Scroll = new("scroll", "attempt-scroll-1", TransitionStrength.Attempt);

    private static FastPerception Perception(string detectionSet) =>
        new("perception.fast", new CorpusFastPerception(Corpus, detectionSet));

    private static UniKernel NewKernel(IAssociationStrategy strategy)
    {
        var world = new WorldModel(Corpus.SubjectScope, strategy);
        return new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
    }

    private static UniKernel Observe(UniKernel kernel, string detectionSet,
        ObservationContext context = ObservationContext.External,
        TransitionContext? transition = null)
    {
        foreach (var proposal in Perception(detectionSet).Observe(Corpus.Artifact(detectionSet), context))
            kernel.Process(proposal, transition);
        return kernel;
    }

    // ---- S1：Text Evidence ------------------------------------------------

    [Fact]
    public void S1_TextObservationsAreEvidenceNotWorldTruth()
    {
        var kernel = NewKernel(new SignatureHintAssociationStrategy());
        Observe(kernel, "popup04-page");

        var revision = kernel.CurrentBelief!;
        // text claim 进 belief 且带 evidence 溯源
        Assert.Equal("POPUP_04 — Return Top Left", revision.WorldState["perception.page.signature"].Value);
        Assert.StartsWith("ev-", revision.WorldState["perception.page.signature"].EvidenceId);

        // identity 只经 association 决策建立（evidence-backed），不是 text 自动成 truth
        var newDecision = Assert.Single(kernel.AssociationLog, d => d.EffectiveKind == AssociationDispositionKind.New);
        var container = Assert.Single(revision.Containers);
        Assert.Equal(newDecision.EstablishedContainerId, container.Identity.ContainerId);
        Assert.Contains(newDecision.EvidenceId, container.EvidenceBasis);

        // DIRECT 路径：真实 legacy provider 输出（golden YOLO+OCR）→ text evidence，
        // 无 signature 信号 → 无任何 identity（identity 判定权在 UIWorld）
        var kernel2 = NewKernel(new SignatureHintAssociationStrategy());
        Observe(kernel2, "golden-case-a-before");
        var rev2 = kernel2.CurrentBelief!;
        Assert.Equal("Internet", rev2.WorldState["ui.text.ocr0"].Value);
        Assert.Equal("Wi-Fi", rev2.WorldState["ui.text.ocr3"].Value);
        Assert.Empty(rev2.Containers);
        Assert.All(kernel2.AssociationLog, d => Assert.Equal(AssociationDispositionKind.Insufficient, d.EffectiveKind));
    }

    // ---- S2：Structural Evidence -------------------------------------------

    [Fact]
    public void S2_DetectedRegionsAreSpatialClaimsNotContainerIdentity()
    {
        var kernel = NewKernel(new SignatureHintAssociationStrategy());
        var artifact = Corpus.Artifact("popup04-page");

        // RawArtifact 内容寻址：同 bytes → 同 id（可引用、replay 稳定）
        Assert.Equal(artifact.ArtifactId, Corpus.Artifact("popup04-page").ArtifactId);
        Assert.Matches("^art-[0-9a-f]{16}$", artifact.ArtifactId);

        foreach (var proposal in Perception("popup04-page").Observe(artifact))
            kernel.Process(proposal);

        var revision = kernel.CurrentBelief!;
        // structural/spatial observations → claims；spatial subject 必带 frame 段（P-UW-16）
        Assert.Equal("Button", revision.WorldState["ui.node.reset_button.class"].Value);
        Assert.Equal("48,140,1032,188", revision.WorldState["spatial.artifact.bounds.reset_button"].Value);
        Assert.All(
            revision.WorldState.Keys.Where(k => k.StartsWith("spatial.", StringComparison.Ordinal)),
            k => Assert.Equal("artifact", k.Split('.')[1])); // frame 显式

        // detected region ≠ ContainerIdentity：identity 只出现在 association 产出
        var container = Assert.Single(revision.Containers);
        Assert.DoesNotContain(container.Identity.ContainerId,
            revision.WorldState.Where(kv => kv.Key.StartsWith("spatial.", StringComparison.Ordinal))
                .Select(kv => kv.Value.Value));
    }

    // ---- S3：Scroll Fast Path（真实 scroll 对）------------------------------

    [Fact]
    public void S3_ScrollFastPath_MatchedViaStableSignatureEvidence()
    {
        var kernel = NewKernel(new SignatureHintAssociationStrategy());
        Observe(kernel, "scroll01-v1");
        var idA = Assert.Single(kernel.CurrentBelief!.Containers).Identity.ContainerId;
        var basisA = kernel.CurrentBelief!.Containers.Single().EvidenceBasis.Count;
        var v1FirstRow = kernel.CurrentBelief!.WorldState["ui.text.row_title"].Value;

        // Frame B：post-action 观察上下文 + P22 scroll prior（prior 只影响 ranking）
        Observe(kernel, "scroll01-v2", ObservationContext.PostActionEffectFlow, Scroll);

        var matched = Assert.Single(kernel.AssociationLog,
            d => d.EffectiveKind == AssociationDispositionKind.Matched);
        Assert.Equal(idA, matched.MatchedContainerId);
        Assert.Equal("attempt-scroll-1", matched.TransitionCorrelation);

        var container = Assert.Single(kernel.CurrentBelief!.Containers);
        Assert.Equal(idA, container.Identity.ContainerId);          // identity 不变
        Assert.True(container.EvidenceBasis.Count > basisA);         // basis 延续（v1+v2 evidence）
        // 滚动内容变化（Item 01..13 → 02..14）如实记录：row claims 冲突显式入
        // Conflict（latest 不获胜），identity 不因内容变化被推翻
        Assert.Equal("Item 01", v1FirstRow);
        Assert.Equal("Item 01", kernel.CurrentBelief!.WorldState["ui.text.row_title"].Value);
        Assert.Contains(kernel.CurrentBelief!.Conflicts, c => c.Subject == "ui.text.row_title");
    }

    // ---- S4：Scroll Contradiction -------------------------------------------

    [Fact]
    public void S4_ScrollPriorCannotOverrideContradictingEvidence()
    {
        var kernel = NewKernel(new SignatureHintAssociationStrategy());
        Observe(kernel, "scroll01-v1");
        var idA = Assert.Single(kernel.CurrentBelief!.Containers).Identity.ContainerId;

        // scroll prior 在场，但观察到的页面是另一页（signature 反证：不同 title）
        Observe(kernel, "popup04-page", ObservationContext.PostActionEffectFlow, Scroll);

        var secondFrameDecisions = kernel.AssociationLog
            .Where(d => d.EvidenceId != kernel.AssociationLog[0].EvidenceId).ToList();
        Assert.DoesNotContain(secondFrameDecisions,
            d => d.EffectiveKind == AssociationDispositionKind.Matched);   // 不得强判 Matched
        Assert.Contains(secondFrameDecisions,
            d => d.EffectiveKind == AssociationDispositionKind.New);       // 诚实 New（不同页面）

        var containers = kernel.CurrentBelief!.Containers;
        Assert.Equal(2, containers.Count);                                  // A 仍在 belief
        Assert.Contains(containers, c => c.Identity.ContainerId == idA);
    }

    // ---- S5：Partial Observation --------------------------------------------

    [Fact]
    public void S5_PartialDetectionIsInsufficient_NotNewNotAbsent()
    {
        var kernel = NewKernel(new SignatureHintAssociationStrategy());

        // 真实 artifact + partial detection 子集（无 page signature）
        foreach (var proposal in Perception("scroll01-v1-partial")
                     .Observe(Corpus.Artifact("scroll01-v1-partial")))
            kernel.Process(proposal);

        Assert.All(kernel.AssociationLog, d => Assert.Equal(AssociationDispositionKind.Insufficient, d.EffectiveKind));
        var revision = kernel.CurrentBelief!;
        Assert.Empty(revision.Containers);                       // 不是 New
        Assert.Equal(2, revision.WorldState.Count);              // 只记录观察到的两条
        // 未检测 ≠ absent：belief 对未观察节点零断言
        Assert.DoesNotContain(revision.WorldState, kv => kv.Value.Value.Contains("absent", StringComparison.OrdinalIgnoreCase));
    }

    // ---- S6：Similar Layout / Different Identity -----------------------------

    [Fact]
    public void S6_SimilarLayoutWithDifferentSignatureYieldsDistinctIdentities()
    {
        var kernel = NewKernel(new SignatureHintAssociationStrategy());
        Observe(kernel, "popup09-before");
        var idA = Assert.Single(kernel.CurrentBelief!.Containers).Identity.ContainerId;

        // 结构高度相似（同 title/STATE/OPEN/RESET 骨架）但语义反证（不同 scenario title）
        Assert.Equal("Button", kernel.CurrentBelief!.WorldState["ui.node.reset_button.class"].Value);
        Observe(kernel, "popup04-page");
        Assert.Equal("Button", kernel.CurrentBelief!.WorldState["ui.node.reset_button.class"].Value);

        // identity 仍由 UIWorld 判定：不合并、不强 match → 两个不同 identity
        var containers = kernel.CurrentBelief!.Containers;
        Assert.Equal(2, containers.Count);
        Assert.DoesNotContain(kernel.AssociationLog, d =>
            d.EffectiveKind == AssociationDispositionKind.Matched
            && d.MatchedContainerId == idA
            && d.Reason.Contains("popup04", StringComparison.Ordinal));
        Assert.Equal("POPUP_09 — Back Triggers Dialog",
            kernel.CurrentBelief!.WorldState[WorldModel.SignatureSubjectPrefix + idA].Value);
        var idB = kernel.CurrentBelief!.WorldState[WorldModel.CurrentContainerSubject].Value;
        Assert.NotEqual(idA, idB);
    }

    // ---- S7：Overlay / Dialog ------------------------------------------------

    [Fact]
    public void S7_DialogOverlayEstablishesNewContainerWithOverlaysRelation()
    {
        var kernel = NewKernel(new PageAndOverlayAssociationStrategy());
        Observe(kernel, "popup09-before");
        var pageId = Assert.Single(kernel.CurrentBelief!.Containers).Identity.ContainerId;

        // dialog 帧出现（真实 AlertDialog 层级）
        Observe(kernel, "popup01-dialog", ObservationContext.PostActionEffectFlow,
            new TransitionContext("open-overlay", "attempt-overlay-1", TransitionStrength.Attempt));

        var revision = kernel.CurrentBelief!;
        // 新 overlay container（evidence-backed New）
        var overlay = Assert.Single(kernel.AssociationLog, d => d.Reason == "dialog-overlay-new");
        Assert.Equal(AssociationDispositionKind.New, overlay.EffectiveKind);
        var dialogId = overlay.EstablishedContainerId!;
        // Overlays relation（revision-bound、evidence-backed graph claim）
        var relation = Assert.Single(revision.Relations);
        Assert.Equal(ContainerRelationKind.Overlays, relation.Kind);
        Assert.Equal(dialogId, relation.SourceContainerId);
        Assert.Equal(pageId, relation.TargetContainerId);
        Assert.Equal(overlay.EvidenceId, Assert.Single(relation.EvidenceBasis));
        // 页面 container 仍在；CurrentContainer 合法迁移到 dialog（Revise 语义）
        Assert.Contains(revision.Containers, c => c.Identity.ContainerId == pageId);
        Assert.Equal(dialogId, revision.WorldState[WorldModel.CurrentContainerSubject].Value);
        // dialog 空间证据在 belief（真实 bounds）
        Assert.Equal("191,880,888,908", revision.WorldState["spatial.artifact.bounds.alertTitle"].Value);
    }

    // ---- S8：Real Failure（negative）-----------------------------------------

    [Fact]
    public void S8_DegradedProviderOutputCannotCreateFalseCanonicalBelief()
    {
        // (a) 真实 artifact + 显式 synthetic 降级（wrong bounds + 缺失关键文本）：
        //     legacy FailureEpisode 未保存（evaluation/failure_candidate.py 自证），
        //     不伪造 legacy failure——降级场景在 manifest 中标记 provenance。
        Assert.Equal("synthetic-on-legacy-artifact", Corpus.Scenario("popup04-degraded").Provenance);
        var kernel = NewKernel(new SignatureHintAssociationStrategy());
        foreach (var proposal in Perception("popup04-degraded")
                     .Observe(Corpus.Artifact("popup04-degraded")))
            kernel.Process(proposal);

        Assert.All(kernel.AssociationLog, d => Assert.Equal(AssociationDispositionKind.Insufficient, d.EffectiveKind));
        var revision = kernel.CurrentBelief!;
        Assert.Empty(revision.Containers);   // 无假 identity
        // 错误 bounds 只是（弱）evidence claim——不是 canonical truth 的身份判决
        Assert.Equal("0,0,10,10", revision.WorldState["spatial.artifact.bounds.state_text"].Value);

        // (b) missing detection ≠ absence：popup09-before 无任何 dialog 节点被检测，
        //     belief 对 dialog 零断言（不产生 "dialog absent" 类 claim）
        var kernel2 = NewKernel(new SignatureHintAssociationStrategy());
        Observe(kernel2, "popup09-before");
        var rev2 = kernel2.CurrentBelief!;
        Assert.DoesNotContain(rev2.WorldState.Keys,
            k => k.Contains("alertTitle", StringComparison.Ordinal)
              || k.Contains("dialog", StringComparison.Ordinal));
        Assert.DoesNotContain(rev2.WorldState, kv => kv.Value.Value.Contains("absent", StringComparison.OrdinalIgnoreCase));
    }
}
