using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

/// <summary>Controlled evidence fixtures for fail-closed historical correction gates.</summary>
public sealed class WorldPageCorrectionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("wrong-target")]
    [InlineData("wrong-previous-identity")]
    [InlineData("missing-profile")]
    [InlineData("wrong-capture")]
    [InlineData("wrong-cycle")]
    [InlineData("wrong-session")]
    [InlineData("wrong-time")]
    [InlineData("wrong-space")]
    [InlineData("wrong-signature")]
    [InlineData("unknown-target")]
    public void InvalidCorrectionEvidence_PreservesOriginalAttributionAndSnapshot(string defect)
    {
        var (kernel, world, ledger) = Create();
        var knownB = Capture("known-b", 1);
        Identify(kernel, knownB, "native:page:B");
        var targetB = world.Current!.CurrentPageObservation!.ContainerId!;

        var original = Capture("target-a", 2);
        var captureId = kernel.Process(CaptureProposal(original)).Admission.EvidenceId!;
        var oldIdentity = kernel.Process(IdentityProposal(original, captureId, "page:A")).Admission.EvidenceId!;
        var oldOwner = world.Current!.CurrentPageObservation!.ContainerId!;
        var oldRevision = world.Current.RevisionId;
        var oldSnapshot = System.Text.Json.JsonSerializer.Serialize(world.Current);

        var replacementSignature = defect == "unknown-target" ? "native:page:unknown" : "native:page:B";
        var (support, declaredSignature) = Support(original, replacementSignature, defect);
        var supportId = kernel.Process(support).Admission.EvidenceId!;
        var correction = new PageInterpretationCorrection(
            defect == "wrong-target" ? "missing-capture-evidence" : captureId,
            defect == "wrong-previous-identity" ? "missing-identity-evidence" : oldIdentity,
            new(original.CaptureId, original.ObservationCycleId, original.SessionCorrelation,
                original.CaptureTime, declaredSignature, "Settings"), [supportId], "controlled-native-context:v1");
        var proposal = CorrectionProposal(correction, captureId, supportId, original.CaptureTime);
        kernel.Process(proposal);

        Assert.Equal(oldOwner, world.Current!.CurrentPageObservation!.ContainerId);
        Assert.Equal(oldRevision, world.RevisionHistory.Single(r => r.RevisionId == oldRevision).RevisionId);
        Assert.Equal(oldSnapshot, System.Text.Json.JsonSerializer.Serialize(world.RevisionHistory.Single(r => r.RevisionId == oldRevision)));
        Assert.Equal(oldOwner, world.DerivePageView(oldOwner).Observations.Single(o => o.CaptureEvidenceId == captureId).ContainerId);
        Assert.NotEqual(targetB, oldOwner);
        Assert.Contains(captureId, ledger.CanonicalRecords.Keys);
    }

    [Fact]
    public void CorrectionOfC1AfterIndependentC2_DoesNotMoveCurrentOrCreateNavigation()
    {
        var (kernel, world, _) = Create();
        var c1 = Capture("c1", 1);
        var c1Evidence = Identify(kernel, c1, "page:A");
        var oldIdentity = world.Current!.CurrentPageObservation!.IdentityEvidenceId!;
        var c2 = Capture("c2", 2);
        Identify(kernel, c2, "native:page:B");
        var currentOwner = world.Current!.CurrentPageObservation!.ContainerId;
        var beforeRevision = world.Current.RevisionId;
        var beforeRelations = world.Current.Relations!.ToArray();
        var supportId = kernel.Process(Support(c1, "native:page:B", "valid").Proposal).Admission.EvidenceId!;
        var correction = new PageInterpretationCorrection(c1Evidence, oldIdentity,
            new(c1.CaptureId, c1.ObservationCycleId, c1.SessionCorrelation, c1.CaptureTime,
                "native:page:B", "Settings"), [supportId], "controlled-native-context:v1");
        kernel.Process(CorrectionProposal(correction, c1Evidence, supportId, c1.CaptureTime));

        Assert.True(world.PageCorrectionLog[^1].Applied, world.PageCorrectionLog[^1].Reason);
        Assert.Equal(currentOwner, world.Current!.CurrentPageObservation!.ContainerId);
        Assert.Equal("c2", world.Current.CurrentPageObservation.Capture.CaptureId);
        Assert.Equal(beforeRelations, world.Current.Relations);
        Assert.NotEqual(beforeRevision, world.Current.RevisionId);
    }

    private static (UniKernel Kernel, WorldModel World, EvidenceLedger Ledger) Create()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject, PageInterpretationCodec.Subject, "ui.node.*" }, new ProductAssociationStrategy());
        return (new UniKernel(ledger, world, DisabledRunTrace.Instance), world, ledger);
    }

    private static string Identify(UniKernel kernel, PageCaptureObservation capture, string signature)
    {
        var source = kernel.Process(CaptureProposal(capture)).Admission.EvidenceId!;
        kernel.Process(IdentityProposal(capture, source, signature));
        return source;
    }

    private static PageCaptureObservation Capture(string id, int second) => new PageCaptureObservation(id, "cycle:" + id, "session",
        T0.AddSeconds(second), CoordinateSpace.DeviceViewport(100, 100, captureId: id),
        [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
         new("row", "ui.element", "Settings", new UiBounds(0, 30, 90, 60))]) { DeviceId = "fixture-device" };

    private static ObservationProposal CaptureProposal(PageCaptureObservation capture) =>
        new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
            ObservationContext.External, new("recorded-layout", capture.CaptureTime, "capture:" + capture.CaptureId,
                ["controlled-fixture:" + capture.CaptureId]));

    private static ObservationProposal IdentityProposal(PageCaptureObservation capture, string sourceId, string signature) =>
        new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
                capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
                signature, "Settings"))), IngressKind.Observation, ObservationContext.External,
            new("identity-supply", capture.CaptureTime, "identity:" + capture.CaptureId, ["capture:" + capture.CaptureId]))
        { DerivedFromEvidenceIds = [sourceId], TransitiveEvidenceBasis = new HashSet<string> { sourceId },
          FusionRule = "page.identity.observed-title-layout", FusionVersion = "1" };

    private static (ObservationProposal Proposal, string Signature) Support(PageCaptureObservation c, string signature, string defect)
    {
        var d = new HierarchyCaptureDescriptor(c.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "controlled-native-profile:v1", UiHierarchyFormat.UiAutomatorXml, "fixture-device",
            c.SessionCorrelation, c.ObservationCycleId, c.CaptureTime, null, HierarchyCapability.SemanticText,
            CoverageCompleteness.Partial, "viewport", 0, null, "resource_id", c.Space);
        var provenance = new Provenance("platform.uiautomator", c.CaptureTime, "context:" + c.CaptureId,
            ["typed-hierarchy:v1", PageInterpretationCodec.NativeContextProfile], d);
        var actualDescriptor = defect switch
        {
            "wrong-capture" => d with { CaptureId = "other-capture" },
            "wrong-cycle" => d with { ObservationCycleId = "other-cycle" },
            "wrong-session" => d with { SessionCorrelation = "other-session" },
            "wrong-time" => d with { CaptureTimestamp = c.CaptureTime.AddSeconds(1) },
            "wrong-space" => d with { Space = CoordinateSpace.DeviceViewport(101, 100, captureId: c.CaptureId) },
            _ => d,
        };
        var actual = defect == "missing-profile"
            ? provenance with { TransformationLineage = ["typed-hierarchy:v1"] }
            : provenance with { Hierarchy = actualDescriptor as HierarchyCaptureDescriptor ?? d };
        var declared = defect == "wrong-signature" ? "native:page:NOT-B" : signature;
        return (new ObservationProposal(new("ui.node." + c.CaptureId + "#0.resource_id", signature), IngressKind.Observation,
            ObservationContext.External, actual), declared);
    }

    private static ObservationProposal CorrectionProposal(PageInterpretationCorrection correction,
        string sourceId, string supportId, DateTimeOffset time) =>
        new(new(PageInterpretationCodec.Subject, PageInterpretationCodec.Encode(correction)), IngressKind.Observation,
            ObservationContext.External, new("history-interpreter", time, "reinterpret:" + sourceId, ["controlled-fixture"]))
        { DerivedFromEvidenceIds = [sourceId, supportId], TransitiveEvidenceBasis = new HashSet<string> { sourceId, supportId },
          FusionRule = "page.interpretation", FusionVersion = "1" };
}
