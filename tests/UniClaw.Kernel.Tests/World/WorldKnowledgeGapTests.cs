using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class WorldKnowledgeGapTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
    private const string Device = "fixture-device";
    private const string Package = "com.example.settings";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FourGaps_AreProducedHeldThenResolvedOnlyByCurrentEvidence(bool foreignParent)
    {
        var (kernel, world, ledger) = Create();
        var parentCapture = Capture("parent", 1, CoverageCompleteness.CompleteWithinDeclaredSurface);
        if (foreignParent) parentCapture = parentCapture with { DeviceId = "other-device" };
        var parentEvidence = kernel.Process(CaptureProposal(parentCapture)).Admission.EvidenceId!;
        kernel.Process(IdentityProposal(parentCapture, parentEvidence, "com.example.settings:id/parent"));
        var parent = world.Current!.CurrentPageObservation!.ContainerId!;

        var childCapture = Capture("child", 2, CoverageCompleteness.Partial, "viewport-export;offscreen-unproven");
        var childEvidence = kernel.Process(CaptureProposal(childCapture)).Admission.EvidenceId!;
        var unresolvedRevision = world.Current!.RevisionId;
        var initial = world.DeriveKnowledgeGaps(parent);
        Assert.Equal(4, initial.Gaps.Count);
        Assert.Equal(new[] { WorldKnowledgeGapKind.Identity, WorldKnowledgeGapKind.Attribution,
            WorldKnowledgeGapKind.RelationBasis, WorldKnowledgeGapKind.ObservationCoverage },
            initial.Gaps.Select(g => g.Kind).OrderBy(k => k));
        var oldSnapshot = world.DeriveKnowledgeGaps(parent, unresolvedRevision);

        kernel.Process(IdentityProposal(childCapture, childEvidence, "com.example.settings:id/child"));
        var afterIdentity = world.DeriveKnowledgeGaps(parent);
        Assert.DoesNotContain(afterIdentity.Gaps, g => g.Kind is WorldKnowledgeGapKind.Identity or WorldKnowledgeGapKind.Attribution);
        Assert.Contains(afterIdentity.Gaps, g => g.Kind == WorldKnowledgeGapKind.RelationBasis);
        Assert.Contains(afterIdentity.Gaps, g => g.Kind == WorldKnowledgeGapKind.ObservationCoverage);
        Assert.Equal(4, world.DeriveKnowledgeGaps(parent, unresolvedRevision).Gaps.Count);

        var profile = Profile();
        var hierarchy = Hierarchy(childCapture, childContextParent: 0);
        var projected = NativeContextObservationProjector.Project(hierarchy, profile, ObservationContext.External);
        var admitted = projected.Select(p =>
        {
            var result = kernel.Process(p);
            Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);
            return ledger.CanonicalRecords[result.Admission.EvidenceId!];
        }).ToArray();
        var parentAnchor = Assert.Single(admitted.Where(r => r.Claim.Subject.EndsWith("#0.resource_id", StringComparison.Ordinal)));
        var childAnchor = Assert.Single(admitted.Where(r => r.Claim.Subject.EndsWith("#1.resource_id", StringComparison.Ordinal)));
        var relation = kernel.Process(ContainerContextCodec.CreateProposal(parentAnchor, childAnchor));
        Assert.Equal(AdmissionDecision.Accepted, relation.Admission.Decision);
        if (foreignParent)
        {
            Assert.DoesNotContain(world.Current!.Relations!, r => r.Kind == ContainerRelationKind.Contains);
            Assert.Contains(world.DeriveKnowledgeGaps(parent).Gaps, g => g.Kind == WorldKnowledgeGapKind.RelationBasis);
            return;
        }
        Assert.Contains(world.Current!.Relations!, r => r.Kind == ContainerRelationKind.Contains
            && r.SourceContainerId == parent && r.TargetContainerId == world.Current.CurrentPageObservation!.ContainerId);
        Assert.DoesNotContain(world.DeriveKnowledgeGaps(parent).Gaps, g => g.Kind == WorldKnowledgeGapKind.RelationBasis);
        Assert.Contains(world.DeriveKnowledgeGaps(parent).Gaps, g => g.Kind == WorldKnowledgeGapKind.ObservationCoverage);

        // A new complete viewport capture resolves only its own coverage gap;
        // the prior capture-scoped Contains relation is not inherited as current.
        var third = Capture("child-complete", 3, CoverageCompleteness.CompleteWithinDeclaredSurface);
        var thirdId = kernel.Process(CaptureProposal(third)).Admission.EvidenceId!;
        kernel.Process(IdentityProposal(third, thirdId, "com.example.settings:id/child"));
        Assert.DoesNotContain(world.DeriveKnowledgeGaps().Gaps, g => g.Kind == WorldKnowledgeGapKind.ObservationCoverage);
        Assert.DoesNotContain(world.DeriveKnowledgeGaps().Gaps, g => g.Kind == WorldKnowledgeGapKind.RelationBasis);
        Assert.Contains(world.DeriveKnowledgeGaps(parent).Gaps, g => g.Kind == WorldKnowledgeGapKind.RelationBasis);
        Assert.Equal(4, oldSnapshot.Gaps.Count);
    }

    [Fact]
    public void NativeContextRelation_RequiresDirectParentAndMatchingProfileSources()
    {
        var (kernel, world, ledger) = Create();
        var parentCapture = Capture("parent", 1, CoverageCompleteness.CompleteWithinDeclaredSurface);
        var parentId = kernel.Process(CaptureProposal(parentCapture)).Admission.EvidenceId!;
        kernel.Process(IdentityProposal(parentCapture, parentId, "com.example.settings:id/parent"));
        var childCapture = Capture("child", 2, CoverageCompleteness.Partial, "viewport-only");
        var childId = kernel.Process(CaptureProposal(childCapture)).Admission.EvidenceId!;
        kernel.Process(IdentityProposal(childCapture, childId, "com.example.settings:id/child"));

        var nonDirect = NativeContextObservationProjector.Project(Hierarchy(childCapture, childContextParent: null),
            Profile(), ObservationContext.External);
        var records = nonDirect.Select(p =>
        {
            var result = kernel.Process(p);
            return ledger.CanonicalRecords[result.Admission.EvidenceId!];
        }).ToArray();
        var parentAnchor = Assert.Single(records.Where(r => r.Claim.Subject.EndsWith("#0.resource_id", StringComparison.Ordinal)));
        var childAnchor = Assert.Single(records.Where(r => r.Claim.Subject.EndsWith("#1.resource_id", StringComparison.Ordinal)));
        Assert.Throws<ArgumentException>(() => ContainerContextCodec.CreateProposal(parentAnchor, childAnchor));
        Assert.DoesNotContain(world.Current!.Relations!, r => r.Kind == ContainerRelationKind.Contains);
        Assert.Contains(world.DeriveKnowledgeGaps(world.Current!.Containers!.First(c =>
            world.Current!.WorldState[WorldModel.SignatureSubjectPrefix + c.Identity.ContainerId].Value == "com.example.settings:id/parent").Identity.ContainerId)
            .Gaps, g => g.Kind == WorldKnowledgeGapKind.RelationBasis);
    }

    private static (UniKernel Kernel, WorldModel World, EvidenceLedger Ledger) Create()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject, "ui.node.*", ContainerContextCodec.Subject }, new ProductAssociationStrategy());
        return (new UniKernel(ledger, world, DisabledRunTrace.Instance), world, ledger);
    }

    private static PageCaptureObservation Capture(string id, int second, CoverageCompleteness coverage, string? limitation = null) =>
        new(id, "cycle:" + id, "session", T0.AddSeconds(second),
            CoordinateSpace.DeviceViewport(100, 100, captureId: id),
            [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
             new("context", "ui.element", "Settings", new UiBounds(0, 20, 90, 90))])
        { DeviceId = Device, Coverage = new(coverage, limitation) };

    private static ObservationProposal CaptureProposal(PageCaptureObservation capture) =>
        new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
            ObservationContext.External, new("controlled-recorded-layout", capture.CaptureTime,
                "capture:" + capture.CaptureId, ["controlled-fixture:" + capture.CaptureId]));

    private static ObservationProposal IdentityProposal(PageCaptureObservation capture, string source, string signature) =>
        new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
                capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
                signature, "Settings"))), IngressKind.Observation, ObservationContext.External,
            new("controlled-identity", capture.CaptureTime, "identity:" + capture.CaptureId, ["capture:" + capture.CaptureId]))
        { DerivedFromEvidenceIds = [source], TransitiveEvidenceBasis = new HashSet<string> { source },
          FusionRule = "controlled-page-identity", FusionVersion = "1" };

    private static NativeContextSignatureProfile Profile() => new("controlled-page-context:v1", Device, Package,
        UiHierarchyAcquirerKind.AndroidXUiAutomator, "androidx-uiautomator/1",
        ["com.example.settings:id/parent", "com.example.settings:id/child"]);

    private static UiHierarchyObservation Hierarchy(PageCaptureObservation c, int? childContextParent)
    {
        var metadata = new CaptureMetadata(c.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "androidx-uiautomator/1", UiHierarchyFormat.AccessibilityTree, c.CaptureTime, TimeSpan.FromMilliseconds(10),
            Device, c.SessionCorrelation, c.ObservationCycleId, new HierarchyCapabilities(HierarchyCapability.None),
            new HierarchyCoverage(CoverageCompleteness.Partial, "viewport-export"), c.Space);
        return new(metadata, Array.Empty<UiWindowOccurrence>(),
            [Node(c.CaptureId, 0, "com.example.settings:id/parent", null),
             Node(c.CaptureId, 1, "com.example.settings:id/child", childContextParent)]);
    }

    private static UiNodeObservation Node(string captureId, int index, string resourceId, int? parent) => new(
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
}
