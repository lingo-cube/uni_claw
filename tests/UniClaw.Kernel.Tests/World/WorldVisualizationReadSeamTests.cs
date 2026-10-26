using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

/// <summary>
/// WORLD-VISUALIZATION-001 切片 0：两个只读缝（历史图投影 + revision 时间线）
/// 的行为锁定。不变量：历史快照不可被后续事件改写、标签只用选定 revision
/// 知识、Unknown 不折叠、单参调用兼容、时间轴只表达采集时间高水位。
/// 接口形状与审阅处置见 changes/WORLD-VISUALIZATION-001/read-seam-interface-draft.md。
/// </summary>
public sealed class WorldVisualizationReadSeamTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
    private const string Device = "fixture-device";
    private const string Package = "com.example.settings";

    // ---- 夹具 1：导航观察（沿 WorldObservedNavigationTests 模式） ----

    private static string NavEvidenceId(int sequence) =>
        "ev-" + sequence.ToString("x2") + "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd";

    private static EvidenceRecord NavPageObservation(string pageContext, int sequence) => new(
        NavEvidenceId(sequence),
        new ObservationClaim("ui.screen", pageContext),
        IngressKind.Observation,
        ObservationContext.External,
        new Provenance("settings-feed", T0.AddSeconds(sequence),
            $"scope:ui.screen:{sequence}", new[] { $"capture:{sequence}" }));

    private static (UniKernel Kernel, WorldModel World) NavCreate()
    {
        var world = new WorldModel(new HashSet<string> { "ui.screen" }, new ProductAssociationStrategy());
        return (new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance), world);
    }

    private static WorldBeliefRevision NavObserve(WorldModel world, string pageContext, int sequence)
    {
        var record = NavPageObservation(pageContext, sequence);
        return world.Reconcile(record, world.JudgeRelevance(record));
    }

    private static string NavCurrentPage(WorldBeliefRevision revision) =>
        revision.WorldState[WorldModel.CurrentContainerSubject].Value;

    private static ObservedNavigation NavVerified(
        WorldBeliefRevision sourceRevision, string source, string sourceEvidenceId,
        WorldBeliefRevision targetRevision, string target, string targetEvidenceId) => new(
        sourceRevision.RevisionId, source, sourceEvidenceId,
        targetRevision.RevisionId, target, targetEvidenceId,
        "run:viz-nav", "verification:post-action:verified");

    [Fact]
    public void HistoricalGraphProjection_IsSnapshotStable_AcrossLaterRevisions()
    {
        var (_, world) = NavCreate();
        var rootRevision = NavObserve(world, "root", 1);
        var root = NavCurrentPage(rootRevision);
        var childRevision = NavObserve(world, "root/entry:network", 2);
        var child = NavCurrentPage(childRevision);

        var before = JsonSerializer.Serialize(world.DeriveGraphProjection(childRevision.RevisionId, "run:viz-nav"));
        var historicalNodes = world.DeriveGraphProjection(childRevision.RevisionId, "run:viz-nav").Nodes.Count;

        Assert.True(world.RecordObservedNavigation(NavVerified(
            rootRevision, root, NavEvidenceId(1), childRevision, child, NavEvidenceId(2))));
        var returnRevision = NavObserve(world, "root", 3);
        Assert.True(world.RecordObservedNavigation(NavVerified(
            childRevision, child, NavEvidenceId(2), returnRevision, root, NavEvidenceId(3))));

        var after = world.DeriveGraphProjection(childRevision.RevisionId, "run:viz-nav");
        Assert.Equal(before, JsonSerializer.Serialize(after));
        Assert.Equal(2, historicalNodes);
        Assert.Equal(2, after.Nodes.Count);
        // 后续 revision 的导航边不出现在历史投影中。
        Assert.Empty(after.Edges);
    }

    [Fact]
    public void ProjectWorldGraph_SingleArgument_EqualsInternalCurrentProjection()
    {
        var (kernel, world) = NavCreate();
        NavObserve(world, "root", 1);
        var childRevision = NavObserve(world, "root/entry:network", 2);
        Assert.True(world.RecordObservedNavigation(NavVerified(
            world.RevisionHistory[0], NavCurrentPage(world.RevisionHistory[0]), NavEvidenceId(1),
            childRevision, NavCurrentPage(childRevision), NavEvidenceId(2))));

        var viaKernel = kernel.ProjectWorldGraph("run:viz-nav");
        var viaInternal = world.DeriveGraphProjection(world.Current!.RevisionId, "run:viz-nav");
        Assert.NotNull(viaKernel);
        Assert.Equal(JsonSerializer.Serialize(viaInternal), JsonSerializer.Serialize(viaKernel));
    }

    [Fact]
    public void ProjectWorldGraph_UnknownRevision_YieldsEmptyProjectionWithMarker()
    {
        var (kernel, world) = NavCreate();
        NavObserve(world, "root", 1);

        var projection = kernel.ProjectWorldGraph("run:viz-nav", "rev-404");
        Assert.NotNull(projection);
        Assert.Empty(projection!.Nodes);
        Assert.Empty(projection.Edges);
        Assert.Empty(projection.StructuralRelations);
        Assert.Empty(projection.NodeLabels);
        Assert.Contains("source-revision-unknown", projection.Unknown);
    }

    [Fact]
    public void EmptyWorld_GraphProjectionIsNull_AndTimelineIsEmpty()
    {
        var world = new WorldModel(new HashSet<string> { "ui.screen" }, new ProductAssociationStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);

        Assert.Null(kernel.ProjectWorldGraph("run:viz-nav"));
        var timeline = kernel.ProjectWorldTimeline();
        Assert.Empty(timeline.Revisions);
        Assert.Null(timeline.CurrentRevisionId);
    }

    [Fact]
    public void Timeline_PageAndNavigationMarkers_OrderingAndCaptureHighWater()
    {
        var (kernel, world) = NavCreate();
        var rootRevision = NavObserve(world, "root", 1);
        var root = NavCurrentPage(rootRevision);
        var childRevision = NavObserve(world, "root/entry:network", 2);
        var child = NavCurrentPage(childRevision);
        Assert.True(world.RecordObservedNavigation(NavVerified(
            rootRevision, root, NavEvidenceId(1), childRevision, child, NavEvidenceId(2))));
        var navRevision = world.Current!;
        var returnRevision = NavObserve(world, "root", 3);

        var timeline = kernel.ProjectWorldTimeline();
        Assert.Equal(new[] { 1, 2, 3, 4 },
            timeline.Revisions.Select(summary => summary.RevisionNumber));
        Assert.Null(timeline.Revisions[0].ParentRevisionId);
        for (var index = 1; index < timeline.Revisions.Count; index++)
            Assert.Equal(timeline.Revisions[index - 1].RevisionId, timeline.Revisions[index].ParentRevisionId);
        Assert.Equal(returnRevision.RevisionId, timeline.CurrentRevisionId);

        var first = timeline.Revisions[0];
        // ui.screen 夹具不产生 typed page capture：PageObservation 诚实为 false
        //（页标记断言在语义夹具测试中覆盖）。
        Assert.False(first.Changes.PageObservation);
        Assert.Empty(first.Changes.ObservedContainerIds);
        Assert.Equal(new[] { NavEvidenceId(1) }, first.Changes.NewEvidenceIds);
        Assert.Empty(first.Changes.StructuralRelationChanges); // 首基线 = 空集
        Assert.False(first.Changes.NavigationVerified);
        Assert.False(first.Changes.CorrectionApplied);
        Assert.False(first.Changes.SemanticKnowledgeUpdated);
        Assert.Equal(T0.AddSeconds(1), first.LatestCaptureTime);

        // 导航关系发布所在的 revision 标记 NavigationVerified；到达锚是关系携带
        // 的 TargetRevisionId（快照一致语义）。
        var navSummary = Assert.Single(timeline.Revisions.Where(s => s.Changes.NavigationVerified));
        Assert.Equal(navRevision.RevisionId, navSummary.RevisionId);

        var last = timeline.Revisions[^1];
        Assert.Equal(T0.AddSeconds(3), last.LatestCaptureTime); // 采集时间高水位
        Assert.False(last.Changes.PageObservation);
    }

    // ---- 夹具 2：语义沿用（沿 WorldSemanticReuseTests 模式） ----

    private const string HomeResource = Package + ":id/home";

    private sealed record SemanticSetup(UniKernel Kernel, WorldModel World, EvidenceLedger Ledger,
        NativeContextSignatureProfile Profile);
    private sealed record SemanticCapture(PageCaptureObservation Capture, string CaptureEvidenceId,
        string IdentityEvidenceId, string SemanticId, string ContainerId);

    private static SemanticSetup SemanticCreate()
    {
        var profile = SemanticProfile();
        IReadOnlySet<string> scope = new HashSet<string>(StringComparer.Ordinal)
        {
            PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
            "ui.semantic.page", SemanticVerificationCodec.Subject, "ui.node.*",
        };
        var world = new WorldModel(scope, new ProductAssociationStrategy(),
            nativeContextProfiles: new[] { profile });
        var ledger = new EvidenceLedger();
        return new(new UniKernel(ledger, world, DisabledRunTrace.Instance), world, ledger, profile);
    }

    private static NativeContextSignatureProfile SemanticProfile() =>
        new("settings-page:v1", Device, Package, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "androidx-uiautomator/1", [HomeResource], new Dictionary<string, SemanticPageCandidate>
            { [HomeResource] = new("settings-home", "Settings", "Settings home") });

    private static SemanticCapture EstablishSemanticCapture(SemanticSetup setup, PageCaptureObservation capture,
        SemanticPageCandidate? semanticCandidate = null)
    {
        var captureId = SemanticProcess(setup, SemanticCaptureProposal(capture));
        var identityId = SemanticProcess(setup, SemanticIdentityProposal(capture, captureId, HomeResource));
        var containerId = setup.World.Current!.CurrentPageObservation!.ContainerId!;
        if (semanticCandidate is null)
            return new(capture, captureId, identityId, string.Empty, containerId);
        var request = new SemanticPerceptionRequest("meaning:" + capture.CaptureId,
            new CapabilityCorrelation("fixture-session", captureId: capture.CaptureId,
                observationCycleId: capture.ObservationCycleId),
            new(capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
                capture.Space, capture.Items, [captureId]), [semanticCandidate]);
        var semanticProposal = Assert.Single(SemanticObservationProjector.CreateProposals(request,
            SemanticPerceptionResult.Observed(request, semanticCandidate.Key, capture.Items[0].Text)));
        var semanticId = SemanticProcess(setup, semanticProposal);
        return new(capture, captureId, identityId, semanticId, containerId);
    }

    private static string VerifySemantic(SemanticSetup setup, SemanticCapture source, string anchorId)
    {
        var proposal = NativeContextObservationProjector.VerifySemantic(
            SemanticRecord(setup, source.SemanticId), SemanticRecord(setup, source.CaptureEvidenceId),
            SemanticRecord(setup, source.IdentityEvidenceId), SemanticRecord(setup, anchorId), setup.Profile);
        Assert.NotNull(proposal);
        var result = setup.Kernel.Process(proposal!);
        Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);
        var decision = setup.World.SemanticVerificationLog.Single(item => item.EvidenceId == result.Admission.EvidenceId);
        Assert.True(decision.Applied, decision.Reason);
        return result.Admission.EvidenceId!;
    }

    private static (string AnchorId, string PackageId) AddTypedSources(SemanticSetup setup, PageCaptureObservation capture)
    {
        var metadata = new CaptureMetadata(capture.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "androidx-uiautomator/1", UiHierarchyFormat.AccessibilityTree, capture.CaptureTime,
            TimeSpan.FromMilliseconds(10), capture.DeviceId!, capture.SessionCorrelation,
            capture.ObservationCycleId, new HierarchyCapabilities(HierarchyCapability.SemanticText),
            new HierarchyCoverage(CoverageCompleteness.Partial, "viewport-export-only"), capture.Space);
        var observation = new UiHierarchyObservation(metadata, Array.Empty<UiWindowOccurrence>(),
        [
            SemanticNode(capture.CaptureId, 0, HomeResource, capture.Items[0].Text, null),
            SemanticNode(capture.CaptureId, 1, Package + ":id:row", capture.Items[1].Text, 0),
        ]);
        var proposals = NativeContextObservationProjector.Project(observation, setup.Profile, ObservationContext.External);
        var anchorProposal = Assert.Single(proposals.Where(p => p.Claim.Subject == $"ui.node.{capture.CaptureId}#0.resource_id"));
        var packageProposal = Assert.Single(proposals.Where(p => p.Claim.Subject == $"ui.node.{capture.CaptureId}#0.package"));
        return (SemanticProcess(setup, anchorProposal), SemanticProcess(setup, packageProposal));
    }

    private static UiNodeObservation SemanticNode(string captureId, int index, string resourceId,
        string? text, int? parent) => new(
        new OccurrenceRef(captureId, index), parent is { } parentIndex ? new OccurrenceRef(captureId, parentIndex) : null,
        null, index, null,
        ObservedValue<string>.Observed("android.view.View"), ObservedValue<string>.Observed(resourceId),
        ObservedValue<string>.Observed(Package), ObservedValue<string>.Observed(text ?? ""),
        ObservedValue<string>.Unknown("missing"), ObservedValue<string>.Unknown("missing"),
        ObservedValue<bool>.Unknown("missing"), ObservedValue<CheckedState>.Unknown("missing"),
        ObservedValue<bool>.Observed(true), ObservedValue<bool>.Unknown("missing"),
        ObservedValue<bool>.Unknown("missing"), ObservedValue<bool>.Observed(false),
        ObservedValue<bool>.Observed(true), ObservedValue<bool>.Observed(true),
        ObservedValue<bool>.Observed(true), ObservedValue<bool>.Unknown("missing"),
        ObservedValue<UiBounds>.Observed(new(0, index * 30, 90, index * 30 + 20)));

    private static PageCaptureObservation SemanticCaptureFixture(string id, int second, string rowText) =>
        new(id, "cycle:" + id, "fixture-session", T0.AddSeconds(second),
            CoordinateSpace.DeviceViewport(100, 100, captureId: id),
            [new("title", "text", "Settings", new(0, 0, 90, 20)),
             new("row", "ui.element", rowText, new(0, 30, 90, 60))])
        { DeviceId = Device, Coverage = new(CoverageCompleteness.Partial, "viewport-export-only") };

    private static ObservationProposal SemanticCaptureProposal(PageCaptureObservation capture) =>
        new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
            ObservationContext.External, new("controlled-layout-fixture", capture.CaptureTime,
                "capture:" + capture.CaptureId, ["controlled-capture:v1", "capture:" + capture.CaptureId]));

    private static ObservationProposal SemanticIdentityProposal(PageCaptureObservation capture,
        string captureEvidenceId, string signature) =>
        new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(
            new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation,
                capture.CaptureTime, signature, capture.Items[0].Text))), IngressKind.Observation,
            ObservationContext.External, new("controlled-identity-fixture", capture.CaptureTime,
                "identity:" + capture.CaptureId, ["controlled-identity:v1", "capture:" + capture.CaptureId]))
        { DerivedFromEvidenceIds = [captureEvidenceId],
          TransitiveEvidenceBasis = new HashSet<string>([captureEvidenceId], StringComparer.Ordinal),
          FusionRule = "page.identity.observed-title-layout", FusionVersion = "1" };

    private static string SemanticProcess(SemanticSetup setup, ObservationProposal proposal)
    {
        var result = setup.Kernel.Process(proposal);
        Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);
        return result.Admission.EvidenceId!;
    }

    private static EvidenceRecord SemanticRecord(SemanticSetup setup, string id) => setup.Ledger.CanonicalRecords[id];

    [Fact]
    public void NodeLabels_JoinAndDegradation_AreSnapshotStable()
    {
        var setup = SemanticCreate();
        var first = EstablishSemanticCapture(setup, SemanticCaptureFixture("c1", 1, "Wi-Fi"),
            new SemanticPageCandidate("settings-home", "Settings", "Settings home"));
        var firstTyped = AddTypedSources(setup, first.Capture);
        var verification = VerifySemantic(setup, first, firstTyped.AnchorId);
        var knowledgeRevision = setup.World.Current!;

        var labeled = setup.World.DeriveGraphProjection(knowledgeRevision.RevisionId, "run:viz-nav");
        var label = Assert.Single(labeled.NodeLabels);
        Assert.Equal(first.ContainerId, label.ContainerId);
        Assert.Equal("Settings", label.PageName);
        Assert.Equal("Settings home", label.PagePurpose);
        Assert.Equal("settings-home", label.PageKey);
        Assert.False(label.Reused);
        Assert.False(label.Conflicted);
        Assert.Equal(knowledgeRevision.RevisionId, label.KnowledgeSourceRevisionId);
        Assert.Contains(first.SemanticId, label.EvidenceRefs);
        Assert.Contains(verification, label.EvidenceRefs);

        // 换到新 capture：当前无语义知识 → 无标签（诚实降级，不伪造）；
        // 历史投影保持原标签（快照不可变）。
        var second = EstablishSemanticCapture(setup, SemanticCaptureFixture("c2", 2, "Bluetooth"));
        Assert.Equal(first.ContainerId, second.ContainerId);
        Assert.Empty(setup.World.Current!.SemanticKnowledge);
        Assert.Empty(setup.World.DeriveGraphProjection(setup.World.Current.RevisionId, "run:viz-nav").NodeLabels);
        var historical = setup.World.DeriveGraphProjection(knowledgeRevision.RevisionId, "run:viz-nav");
        var historicalLabel = Assert.Single(historical.NodeLabels);
        Assert.Equal(JsonSerializer.Serialize(label), JsonSerializer.Serialize(historicalLabel));
    }

    [Fact]
    public void NodeLabels_ReuseFlagFlowsFromKnowledge()
    {
        var setup = SemanticCreate();
        var first = EstablishSemanticCapture(setup, SemanticCaptureFixture("c1", 1, "Wi-Fi"),
            new SemanticPageCandidate("settings-home", "Settings", "Settings home"));
        var firstTyped = AddTypedSources(setup, first.Capture);
        VerifySemantic(setup, first, firstTyped.AnchorId);
        var second = EstablishSemanticCapture(setup, SemanticCaptureFixture("c2", 2, "Bluetooth"));
        AddTypedSources(setup, second.Capture);

        var current = setup.World.DeriveGraphProjection(setup.World.Current!.RevisionId, "run:viz-nav");
        var label = Assert.Single(current.NodeLabels);
        Assert.True(label.Reused);
        Assert.False(label.Conflicted);
    }

    [Fact]
    public void Timeline_SemanticMarkers_UseStableComparisonKey()
    {
        var setup = SemanticCreate();
        var first = EstablishSemanticCapture(setup, SemanticCaptureFixture("c1", 1, "Wi-Fi"),
            new SemanticPageCandidate("settings-home", "Settings", "Settings home"));
        var firstTyped = AddTypedSources(setup, first.Capture);
        VerifySemantic(setup, first, firstTyped.AnchorId);
        var knowledgeRevision = setup.World.Current!;

        // 纯采集/身份 revision：语义比较键不变 → Updated=false（不恒真）。
        var timeline = setup.Kernel.ProjectWorldTimeline();
        Assert.All(timeline.Revisions.Where(summary => summary.RevisionNumber <= knowledgeRevision.RevisionNumber - 1),
            summary => Assert.False(summary.Changes.SemanticKnowledgeUpdated));
        // typed page capture：页标记真实出现，归属容器在身份确立后可读。
        Assert.Contains(timeline.Revisions, summary => summary.Changes.PageObservation);
        Assert.Contains(timeline.Revisions, summary => summary.Changes.ObservedContainerIds.Contains(first.ContainerId));

        var second = EstablishSemanticCapture(setup, SemanticCaptureFixture("c2", 2, "Bluetooth"));
        AddTypedSources(setup, second.Capture);

        timeline = setup.Kernel.ProjectWorldTimeline();
        var established = timeline.Revisions.Single(s => s.RevisionId == knowledgeRevision.RevisionId);
        Assert.True(established.Changes.SemanticKnowledgeUpdated);
        Assert.False(established.Changes.SemanticKnowledgeReused);
        // 沿用 = 新键（Reused 翻转）出现：重锚定表现为键增删（复审 NIT 锁定）。
        var reused = Assert.Single(timeline.Revisions.Where(s => s.Changes.SemanticKnowledgeReused));
        Assert.True(reused.Changes.SemanticKnowledgeUpdated);
        Assert.Contains(timeline.Revisions.Where(s => s.RevisionNumber > knowledgeRevision.RevisionNumber
            && s.RevisionNumber < reused.RevisionNumber), s => s.Changes.SemanticKnowledgeUpdated); // 清空也计变更
        Assert.Equal(timeline.Revisions[^1].RevisionId, timeline.CurrentRevisionId);
    }

    // ---- 夹具 3：归属纠错（沿 WorldPageCorrectionTests 模式） ----

    private static (UniKernel Kernel, WorldModel World) CorrectionCreate()
    {
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject, PageInterpretationCodec.Subject, "ui.node.*" },
            new ProductAssociationStrategy());
        return (new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance), world);
    }

    private static PageCaptureObservation CorrectionCapture(string id, int second) => new(id, "cycle:" + id, "session",
        T0.AddSeconds(second), CoordinateSpace.DeviceViewport(100, 100, captureId: id),
        [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
         new("row", "ui.element", "Settings", new UiBounds(0, 30, 90, 60))]) { DeviceId = Device };

    private static ObservationProposal CorrectionCaptureProposal(PageCaptureObservation capture) =>
        new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
            ObservationContext.External, new("recorded-layout", capture.CaptureTime, "capture:" + capture.CaptureId,
                ["controlled-fixture:" + capture.CaptureId]));

    private static ObservationProposal CorrectionIdentityProposal(PageCaptureObservation capture,
        string sourceId, string signature) =>
        new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
                capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
                signature, "Settings"))), IngressKind.Observation, ObservationContext.External,
            new("identity-supply", capture.CaptureTime, "identity:" + capture.CaptureId, ["capture:" + capture.CaptureId]))
        { DerivedFromEvidenceIds = [sourceId], TransitiveEvidenceBasis = new HashSet<string> { sourceId },
          FusionRule = "page.identity.observed-title-layout", FusionVersion = "1" };

    private static string CorrectionIdentify(UniKernel kernel, PageCaptureObservation capture, string signature)
    {
        var source = kernel.Process(CorrectionCaptureProposal(capture)).Admission.EvidenceId!;
        kernel.Process(CorrectionIdentityProposal(capture, source, signature));
        return source;
    }

    private static ObservationProposal CorrectionSupport(PageCaptureObservation capture, string signature)
    {
        var descriptor = new HierarchyCaptureDescriptor(capture.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "controlled-native-profile:v1", UiHierarchyFormat.UiAutomatorXml, Device,
            capture.SessionCorrelation, capture.ObservationCycleId, capture.CaptureTime, null,
            HierarchyCapability.SemanticText, CoverageCompleteness.Partial, "viewport", 0, null,
            "resource_id", capture.Space);
        var provenance = new Provenance("platform.uiautomator", capture.CaptureTime, "context:" + capture.CaptureId,
            ["typed-hierarchy:v1", PageInterpretationCodec.NativeContextProfile], descriptor);
        return new ObservationProposal(new("ui.node." + capture.CaptureId + "#0.resource_id", signature),
            IngressKind.Observation, ObservationContext.External, provenance);
    }

    private static ObservationProposal CorrectionProposal(PageInterpretationCorrection correction,
        string sourceId, string supportId, DateTimeOffset time) =>
        new(new(PageInterpretationCodec.Subject, PageInterpretationCodec.Encode(correction)), IngressKind.Observation,
            ObservationContext.External, new("history-interpreter", time, "reinterpret:" + sourceId, ["controlled-fixture"]))
        { DerivedFromEvidenceIds = [sourceId, supportId],
          TransitiveEvidenceBasis = new HashSet<string> { sourceId, supportId },
          FusionRule = "page.interpretation", FusionVersion = "1" };

    [Fact]
    public void Timeline_CorrectionMarkers_DistinguishAppliedAndRejected()
    {
        var (kernel, world) = CorrectionCreate();
        var knownB = CorrectionCapture("known-b", 1);
        CorrectionIdentify(kernel, knownB, "native:page:B");

        var original = CorrectionCapture("target-a", 2);
        var captureId = kernel.Process(CorrectionCaptureProposal(original)).Admission.EvidenceId!;
        var oldIdentity = kernel.Process(CorrectionIdentityProposal(original, captureId, "page:A")).Admission.EvidenceId!;
        var beforeCorrection = world.Current!.RevisionId;

        var supportId = kernel.Process(CorrectionSupport(original, "native:page:B")).Admission.EvidenceId!;
        var applied = new PageInterpretationCorrection(captureId, oldIdentity,
            new(original.CaptureId, original.ObservationCycleId, original.SessionCorrelation,
                original.CaptureTime, "native:page:B", "Settings"), [supportId], "controlled-native-context:v1");
        kernel.Process(CorrectionProposal(applied, captureId, supportId, original.CaptureTime));
        Assert.True(world.PageCorrectionLog[^1].Applied, world.PageCorrectionLog[^1].Reason);

        // 被拒纠错：目标真实、但旧解释不匹配 → 不足不选择新归属（拒绝发生在
        // World 门；derived-from 须用真实 evidence id，否则被账本拒收不产生条目）。
        var rejected = new PageInterpretationCorrection(captureId, "missing-identity-evidence",
            new(original.CaptureId, original.ObservationCycleId, original.SessionCorrelation,
                original.CaptureTime, "native:page:B", "Settings"), [supportId], "controlled-native-context:v1");
        kernel.Process(CorrectionProposal(rejected, captureId, supportId, original.CaptureTime));
        Assert.Equal(2, world.PageCorrectionLog.Count);
        Assert.False(world.PageCorrectionLog[^1].Applied);

        var timeline = kernel.ProjectWorldTimeline();
        var appliedSummary = Assert.Single(timeline.Revisions.Where(s => s.Changes.CorrectionApplied));
        Assert.Equal(new[] { captureId }, appliedSummary.Changes.CorrectedCaptureEvidenceIds);
        Assert.False(appliedSummary.Changes.CorrectionRejected);
        var rejectedSummary = Assert.Single(timeline.Revisions.Where(s => s.Changes.CorrectionRejected));
        Assert.False(rejectedSummary.Changes.CorrectionApplied);
        Assert.Empty(rejectedSummary.Changes.CorrectedCaptureEvidenceIds);
        Assert.NotEmpty(rejectedSummary.Changes.NewEvidenceIds); // 被拒纠错也发布 revision
        // 纠错前的历史 revision 标记不受影响。
        Assert.False(timeline.Revisions.Single(s => s.RevisionId == beforeCorrection).Changes.CorrectionApplied);
    }

    [Fact]
    public void SignatureWithdrawn_AppearsOnLosingContainer_AndHistoricalRevisionStaysClean()
    {
        var (kernel, world) = CorrectionCreate();
        var knownB = CorrectionCapture("known-b", 1);
        CorrectionIdentify(kernel, knownB, "native:page:B");

        var original = CorrectionCapture("target-a", 2);
        var captureId = kernel.Process(CorrectionCaptureProposal(original)).Admission.EvidenceId!;
        var oldIdentity = kernel.Process(CorrectionIdentityProposal(original, captureId, "page:A")).Admission.EvidenceId!;
        var oldOwner = world.Current!.CurrentPageObservation!.ContainerId!;
        var beforeCorrection = world.Current.RevisionId;
        var beforeProjection = world.DeriveGraphProjection(beforeCorrection, "run:viz-nav");

        var supportId = kernel.Process(CorrectionSupport(original, "native:page:B")).Admission.EvidenceId!;
        var applied = new PageInterpretationCorrection(captureId, oldIdentity,
            new(original.CaptureId, original.ObservationCycleId, original.SessionCorrelation,
                original.CaptureTime, "native:page:B", "Settings"), [supportId], "controlled-native-context:v1");
        kernel.Process(CorrectionProposal(applied, captureId, supportId, original.CaptureTime));
        Assert.True(world.PageCorrectionLog[^1].Applied, world.PageCorrectionLog[^1].Reason);

        // 纠错前：无撤回（历史不变量）。
        Assert.Empty(beforeProjection.SignatureWithdrawnContainers);
        // 纠错后：失签容器（page:A 的归属容器）被标记；容器仍保留为世界事实。
        var after = world.DeriveGraphProjection(world.Current!.RevisionId, "run:viz-nav");
        var withdrawn = Assert.Single(after.SignatureWithdrawnContainers);
        Assert.Equal(oldOwner, withdrawn);
        Assert.Contains(after.Nodes, node => node.Identity.ContainerId == oldOwner);
        // 历史 revision 投影不受其后纠错影响（不回填）。
        Assert.Empty(world.DeriveGraphProjection(beforeCorrection, "run:viz-nav").SignatureWithdrawnContainers);
    }

    // ---- 夹具 4：Contains 结构关系（沿 WorldKnowledgeGapTests 模式） ----

    private static (UniKernel Kernel, WorldModel World, EvidenceLedger Ledger) StructuralCreate()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject, "ui.node.*", ContainerContextCodec.Subject }, new ProductAssociationStrategy());
        return (new UniKernel(ledger, world, DisabledRunTrace.Instance), world, ledger);
    }

    private static PageCaptureObservation StructuralCapture(string id, int second) =>
        new(id, "cycle:" + id, "session", T0.AddSeconds(second),
            CoordinateSpace.DeviceViewport(100, 100, captureId: id),
            [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
             new("context", "ui.element", "Settings", new UiBounds(0, 20, 90, 90))]) { DeviceId = Device };

    private static NativeContextSignatureProfile StructuralProfile() => new("controlled-page-context:v1", Device, Package,
        UiHierarchyAcquirerKind.AndroidXUiAutomator, "androidx-uiautomator/1",
        ["com.example.settings:id/parent", "com.example.settings:id/child"]);

    private static UiHierarchyObservation StructuralHierarchy(PageCaptureObservation capture)
    {
        var metadata = new CaptureMetadata(capture.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "androidx-uiautomator/1", UiHierarchyFormat.AccessibilityTree, capture.CaptureTime,
            TimeSpan.FromMilliseconds(10), Device, capture.SessionCorrelation, capture.ObservationCycleId,
            new HierarchyCapabilities(HierarchyCapability.None),
            new HierarchyCoverage(CoverageCompleteness.Partial, "viewport-export"), capture.Space);
        return new(metadata, Array.Empty<UiWindowOccurrence>(),
            [StructuralNode(capture.CaptureId, 0, "com.example.settings:id/parent", null),
             StructuralNode(capture.CaptureId, 1, "com.example.settings:id/child", 0)]);
    }

    private static UiNodeObservation StructuralNode(string captureId, int index, string resourceId, int? parent) => new(
        new OccurrenceRef(captureId, index), parent is { } p ? new OccurrenceRef(captureId, p) : null,
        null, index, null, ObservedValue<string>.Observed("android.view.View"),
        ObservedValue<string>.Observed(resourceId), ObservedValue<string>.Observed(Package),
        ObservedValue<string>.Observed("Settings"), ObservedValue<string>.Unknown("missing"),
        ObservedValue<string>.Unknown("missing"), ObservedValue<bool>.Unknown("missing"),
        ObservedValue<CheckedState>.Unknown("missing"), ObservedValue<bool>.Observed(true),
        ObservedValue<bool>.Unknown("missing"), ObservedValue<bool>.Unknown("missing"),
        ObservedValue<bool>.Observed(false), ObservedValue<bool>.Observed(false),
        ObservedValue<bool>.Observed(false), ObservedValue<bool>.Observed(true),
        ObservedValue<bool>.Unknown("missing"), ObservedValue<UiBounds>.Observed(new(0, 0, 50, 50)));

    [Fact]
    public void StructuralRelations_CarryContainsSnapshot_BeyondRunScope()
    {
        var (kernel, world, ledger) = StructuralCreate();
        var parentCapture = StructuralCapture("parent", 1);
        var parentEvidence = kernel.Process(
            new ObservationProposal(new(PageObservationCodec.CaptureSubject,
                    PageObservationCodec.Encode(parentCapture)), IngressKind.Observation, ObservationContext.External,
                new("controlled-recorded-layout", parentCapture.CaptureTime, "capture:parent",
                    ["controlled-fixture:parent"]))).Admission.EvidenceId!;
        kernel.Process(new ObservationProposal(new(PageObservationCodec.IdentitySubject,
                PageObservationCodec.Encode(new PageIdentityObservation(parentCapture.CaptureId,
                    parentCapture.ObservationCycleId, parentCapture.SessionCorrelation, parentCapture.CaptureTime,
                    "com.example.settings:id/parent", "Settings"))), IngressKind.Observation, ObservationContext.External,
            new("controlled-identity", parentCapture.CaptureTime, "identity:parent", ["capture:parent"]))
        { DerivedFromEvidenceIds = [parentEvidence], TransitiveEvidenceBasis = new HashSet<string> { parentEvidence },
          FusionRule = "controlled-page-identity", FusionVersion = "1" });
        var parent = world.Current!.CurrentPageObservation!.ContainerId!;

        var childCapture = StructuralCapture("child", 2);
        var childEvidence = kernel.Process(
            new ObservationProposal(new(PageObservationCodec.CaptureSubject,
                    PageObservationCodec.Encode(childCapture)), IngressKind.Observation, ObservationContext.External,
                new("controlled-recorded-layout", childCapture.CaptureTime, "capture:child",
                    ["controlled-fixture:child"]))).Admission.EvidenceId!;
        kernel.Process(new ObservationProposal(new(PageObservationCodec.IdentitySubject,
                PageObservationCodec.Encode(new PageIdentityObservation(childCapture.CaptureId,
                    childCapture.ObservationCycleId, childCapture.SessionCorrelation, childCapture.CaptureTime,
                    "com.example.settings:id/child", "Settings"))), IngressKind.Observation, ObservationContext.External,
            new("controlled-identity", childCapture.CaptureTime, "identity:child", ["capture:child"]))
        { DerivedFromEvidenceIds = [childEvidence], TransitiveEvidenceBasis = new HashSet<string> { childEvidence },
          FusionRule = "controlled-page-identity", FusionVersion = "1" });
        var child = world.Current!.CurrentPageObservation!.ContainerId!;
        var beforeRelation = world.Current!.RevisionId;

        var projected = NativeContextObservationProjector.Project(StructuralHierarchy(childCapture),
            StructuralProfile(), ObservationContext.External);
        var admitted = projected.Select(p => ledger.CanonicalRecords[kernel.Process(p).Admission.EvidenceId!]).ToArray();
        var parentAnchor = Assert.Single(admitted.Where(r => r.Claim.Subject.EndsWith("#0.resource_id", StringComparison.Ordinal)));
        var childAnchor = Assert.Single(admitted.Where(r => r.Claim.Subject.EndsWith("#1.resource_id", StringComparison.Ordinal)));
        Assert.Equal(AdmissionDecision.Accepted,
            kernel.Process(ContainerContextCodec.CreateProposal(parentAnchor, childAnchor)).Admission.Decision);

        var relationRevision = world.Current!;
        var projection = world.DeriveGraphProjection(relationRevision.RevisionId, "run:viz-structural");
        var contains = Assert.Single(projection.StructuralRelations);
        Assert.Equal(ContainerRelationKind.Contains, contains.Kind);
        Assert.Equal(parent, contains.SourceContainerId);
        Assert.Equal(child, contains.TargetContainerId);
        Assert.NotEmpty(contains.EvidenceBasis);
        Assert.Empty(projection.Edges); // 无导航：run-scope 边为空，结构关系不受 scope 过滤

        // 时间线：结构关系相对父新增记 "+"；之前 revision 无该标记。
        var timeline = kernel.ProjectWorldTimeline();
        var structuralChange = Assert.Single(timeline.Revisions
            .Where(s => s.Changes.StructuralRelationChanges.Any(c => c.StartsWith('+'))));
        Assert.Equal(relationRevision.RevisionId, structuralChange.RevisionId);
        Assert.Contains("+contains:" + parent + ">" + child, structuralChange.Changes.StructuralRelationChanges);
        Assert.Empty(timeline.Revisions.Single(s => s.RevisionId == beforeRelation).Changes.StructuralRelationChanges);
    }
}
