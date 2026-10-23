using UniClaw.Host;
using UniClaw.Kernel;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class WorldCorrectionConsumptionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SameCaptureTypedField_FollowsWorldAcceptedIdentityCorrection()
    {
        var ledger = new EvidenceLedger();
        var strategy = new UiHierarchyOccurrenceStrategy();
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject, PageInterpretationCodec.Subject, "ui.node.*" },
            new ProductAssociationStrategy(), strategy);
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance);

        var known = Capture("known", 1);
        Identify(kernel, known, "native:page:B");
        var targetB = world.Current!.CurrentPageObservation!.ContainerId!;
        var original = Capture("target", 2);
        var captureEvidence = kernel.Process(CaptureProposal(original)).Admission.EvidenceId!;
        var originalIdentity = kernel.Process(IdentityProposal(original, captureEvidence, "page:A"));
        var ownerA = world.Current!.CurrentPageObservation!.ContainerId!;
        Assert.NotEqual(targetB, ownerA);

        // A typed field binds this capture's occurrence cache to World owner A.
        kernel.Process(TypedField(original, "resource_id", "native:page:B"));
        kernel.Process(TypedField(original, "enabled", "true"));
        kernel.Process(TypedField(original, "clickable", "true"));
        kernel.Process(TypedField(original, "bounds", "0,0,90,90"));
        var support = kernel.Process(TypedField(original, "resource_id", "native:page:B", nativeContextProfile: true));
        var correction = new PageInterpretationCorrection(captureEvidence,
            originalIdentity.Admission.EvidenceId!,
            new(original.CaptureId, original.ObservationCycleId, original.SessionCorrelation,
                original.CaptureTime, "native:page:B", "Settings"),
            [support.Admission.EvidenceId!], "controlled-native-context:v1");
        kernel.Process(new(new(PageInterpretationCodec.Subject, PageInterpretationCodec.Encode(correction)),
            IngressKind.Observation, ObservationContext.External,
            new("history-interpreter", original.CaptureTime, "correction:" + captureEvidence, ["controlled-fixture"]))
        { DerivedFromEvidenceIds = [captureEvidence, support.Admission.EvidenceId!],
          TransitiveEvidenceBasis = new HashSet<string> { captureEvidence, support.Admission.EvidenceId! },
          FusionRule = "page.interpretation", FusionVersion = "1" });
        Assert.Equal(targetB, world.Current!.CurrentPageObservation!.ContainerId);

        var after = kernel.Process(TypedField(original, "text", "post-correction-field"));

        Assert.Contains(world.Current!.Occurrences!, occurrence => occurrence.OwningContainerId == targetB
            && occurrence.SemanticDescriptor == "post-correction-field");
        Assert.DoesNotContain(world.Current.Occurrences!, occurrence => occurrence.OwningContainerId == ownerA);
        Assert.Equal(original.CaptureId, world.Current.CurrentPageObservation!.Capture.CaptureId);
        Assert.Contains(after.Admission.EvidenceId!, world.Current.EvidenceBasis);
    }

    private static void Identify(UniKernel kernel, PageCaptureObservation c, string signature)
    {
        var id = kernel.Process(CaptureProposal(c)).Admission.EvidenceId!;
        kernel.Process(new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
                c.CaptureId, c.ObservationCycleId, c.SessionCorrelation, c.CaptureTime, signature, "Settings"))),
            IngressKind.Observation, ObservationContext.External,
            new("identity-supply", c.CaptureTime, "identity:" + c.CaptureId, ["capture:" + c.CaptureId]))
        { DerivedFromEvidenceIds = [id], TransitiveEvidenceBasis = new HashSet<string> { id },
          FusionRule = "page.identity.observed-title-layout", FusionVersion = "1" });
    }

    private static PageCaptureObservation Capture(string id, int second) => new PageCaptureObservation(id, "cycle:" + id, "session",
        T0.AddSeconds(second), CoordinateSpace.DeviceViewport(100, 100, captureId: id),
        [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
         new("row", "ui.element", "Item", new UiBounds(0, 25, 90, 60))]) { DeviceId = "fixture-device" };

    private static ObservationProposal CaptureProposal(PageCaptureObservation c) =>
        new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(c)), IngressKind.Observation,
            ObservationContext.External, new("recorded-layout", c.CaptureTime, "capture:" + c.CaptureId, ["controlled-fixture"]));

    private static ObservationProposal IdentityProposal(PageCaptureObservation c, string source, string signature) =>
        new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
                c.CaptureId, c.ObservationCycleId, c.SessionCorrelation, c.CaptureTime, signature, "Settings"))),
            IngressKind.Observation, ObservationContext.External,
            new("identity-supply", c.CaptureTime, "identity:" + c.CaptureId, ["capture:" + c.CaptureId]))
        { DerivedFromEvidenceIds = [source], TransitiveEvidenceBasis = new HashSet<string> { source },
          FusionRule = "page.identity.observed-title-layout", FusionVersion = "1" };

    private static ObservationProposal TypedField(PageCaptureObservation c, string field, string value,
        bool nativeContextProfile = false)
    {
        var descriptor = new HierarchyCaptureDescriptor(c.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "controlled-profile:v1", UiHierarchyFormat.UiAutomatorXml, "fixture-device", c.SessionCorrelation,
            c.ObservationCycleId, c.CaptureTime, null, HierarchyCapability.SemanticText, CoverageCompleteness.Partial,
            "viewport", 0, null, field, c.Space);
        var proposal = new ObservationProposal(new($"ui.node.{c.CaptureId}#0.{field}", value), IngressKind.Observation,
            ObservationContext.External, new("platform.uiautomator", c.CaptureTime, "typed:" + c.CaptureId,
                nativeContextProfile
                    ? ["typed-hierarchy:v1", PageInterpretationCodec.NativeContextProfile]
                    : ["typed-hierarchy:v1"], descriptor));
        return proposal;
    }
}
