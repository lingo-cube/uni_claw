using System.Collections.Immutable;
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

public sealed class WorldSemanticReuseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
    private const string Device = "fixture-device";
    private const string Package = "com.example.settings";
    private const string HomeResource = Package + ":id/home";

    [Fact]
    public void VerifiedMeaning_IsReadableAsHistoryBeforeCurrentAnchor_ThenReusedOnlyWithFreshCorrespondence()
    {
        var setup = Create(configureProfile: true);
        var first = EstablishCapture(setup, Capture("c1", 1, "Wi-Fi"),
            new SemanticPageCandidate("settings-home", "Settings", "Settings home"));
        var firstTyped = AddTypedSources(setup, first.Capture, setup.Profile);
        var firstVerification = Verify(setup, first, firstTyped.AnchorId);
        var firstKnowledge = Assert.Single(setup.World.Current!.SemanticKnowledge);
        Assert.False(firstKnowledge.Reused);
        Assert.Equal(first.SemanticId, firstKnowledge.OriginalSemanticEvidenceId);
        Assert.Equal(firstVerification, firstKnowledge.VerificationEvidenceId);

        var oldRevision = setup.World.Current;
        var oldRevisionBytes = JsonSerializer.Serialize(oldRevision);
        var oldSlice = setup.Kernel.DeriveSlice(first.ContainerId);
        var oldSliceBytes = JsonSerializer.Serialize(oldSlice);
        var oldView = setup.World.DerivePageView(first.ContainerId, oldRevision.RevisionId);
        var oldViewBytes = JsonSerializer.Serialize(oldView);

        var second = EstablishCapture(setup, Capture("c2", 2, "Bluetooth"));
        Assert.Equal(first.ContainerId, second.ContainerId);
        Assert.Empty(setup.World.Current!.SemanticKnowledge);
        var beforeAnchor = setup.World.DeriveKnowledgeGaps();
        var historical = Assert.Single(beforeAnchor.SemanticReferences);
        Assert.Equal(first.SemanticId, historical.EvidenceId);
        Assert.Equal(firstVerification, historical.VerificationEvidenceId);
        Assert.True(historical.IsHistorical);
        Assert.Empty(beforeAnchor.CurrentSemantics);
        var currentSliceBeforeAnchor = setup.Kernel.DeriveSlice(second.ContainerId);
        Assert.DoesNotContain(first.ContainerId + ".semantic.page", currentSliceBeforeAnchor.ScopedClaims.Keys);
        Assert.Contains(currentSliceBeforeAnchor.Occurrences, occurrence => occurrence.SemanticDescriptor == "Bluetooth");
        Assert.DoesNotContain(currentSliceBeforeAnchor.Occurrences, occurrence => occurrence.SemanticDescriptor == "Wi-Fi");

        var secondTyped = AddTypedSources(setup, second.Capture, setup.Profile);
        var current = Assert.Single(setup.World.Current!.SemanticKnowledge);
        Assert.True(current.Reused);
        Assert.Equal(first.SemanticId, current.OriginalSemanticEvidenceId);
        Assert.Equal(firstVerification, current.VerificationEvidenceId);
        Assert.Equal(second.CaptureEvidenceId, current.CurrentCaptureEvidenceId);
        Assert.Equal(second.Capture.CaptureId, current.CaptureId);
        Assert.Contains(second.CaptureEvidenceId, current.CorrespondenceEvidenceIds);
        Assert.Contains(second.IdentityEvidenceId, current.CorrespondenceEvidenceIds);
        Assert.Contains(secondTyped.AnchorId, current.CorrespondenceEvidenceIds);
        Assert.DoesNotContain(first.CaptureEvidenceId, current.CorrespondenceEvidenceIds);
        Assert.DoesNotContain(firstTyped.AnchorId, current.CorrespondenceEvidenceIds);
        Assert.Equal(current, Assert.Single(setup.Kernel.DeriveSlice(second.ContainerId).SemanticKnowledge));
        Assert.DoesNotContain(first.ContainerId + ".semantic.page", setup.Kernel.DeriveSlice(second.ContainerId).ScopedClaims.Keys);
        Assert.Contains(setup.Kernel.DeriveSlice(second.ContainerId).Occurrences,
            occurrence => occurrence.SemanticDescriptor == "Bluetooth");
        Assert.DoesNotContain(setup.Kernel.DeriveSlice(second.ContainerId).Occurrences,
            occurrence => occurrence.SemanticDescriptor == "Wi-Fi");

        Assert.Equal(oldRevisionBytes, JsonSerializer.Serialize(oldRevision));
        Assert.Equal(oldSliceBytes, JsonSerializer.Serialize(oldSlice));
        Assert.Equal(oldViewBytes, JsonSerializer.Serialize(oldView));
        Assert.Equal(oldRevisionBytes, JsonSerializer.Serialize(setup.World.RevisionHistory
            .Single(revision => revision.RevisionId == oldRevision.RevisionId)));
    }

    [Fact]
    public void WorldRejectsVerification_WhenAcceptedSemanticDisagreesWithProfileMapping()
    {
        var setup = Create(configureProfile: true);
        var first = EstablishCapture(setup, Capture("bad-meaning", 1, "Wi-Fi"),
            new SemanticPageCandidate("unmapped-page", "Other", "Different page"));
        var typed = AddTypedSources(setup, first.Capture, setup.Profile);
        var semantic = Record(setup, first.SemanticId);
        var capture = Record(setup, first.CaptureEvidenceId);
        var identity = Record(setup, first.IdentityEvidenceId);
        var anchor = Record(setup, typed.AnchorId);

        Assert.Null(NativeContextObservationProjector.VerifySemantic(semantic, capture, identity, anchor, setup.Profile));
        var manuallyConstructed = ManualVerification(semantic, capture, identity, anchor, setup.Profile);
        var admitted = setup.Kernel.Process(manuallyConstructed);

        Assert.Equal(AdmissionDecision.Accepted, admitted.Admission.Decision);
        Assert.False(Assert.Single(setup.World.SemanticVerificationLog.Where(item => item.EvidenceId == admitted.Admission.EvidenceId)).Applied);
        Assert.Empty(setup.World.Current!.SemanticKnowledge);
        Assert.Empty(setup.Kernel.DeriveSlice(first.ContainerId).SemanticKnowledge);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DifferentOrUncollectedDevices_DoNotBorrowTheOriginalPageIdentityOrHistory(bool bothUnknown)
    {
        var setup = Create(configureProfile: true);
        var firstCapture = Capture("device-first", 1, "Wi-Fi");
        if (bothUnknown) firstCapture = firstCapture with { DeviceId = null };
        var first = EstablishCapture(setup, firstCapture);
        var other = Capture("device-other", 2, "Bluetooth") with { DeviceId = bothUnknown ? null : "other-device" };
        EstablishCapture(setup, other);
        Assert.Null(setup.World.Current!.CurrentPageObservation!.ContainerId);
        Assert.NotNull(setup.World.Current.ObservationNeed);
        Assert.Empty(setup.World.Current.SemanticKnowledge);
        Assert.DoesNotContain(setup.World.DerivePageView(first.ContainerId).Observations,
            observation => observation.Capture.CaptureId == other.CaptureId);
    }

    [Fact]
    public void WorldRequiresItsOwnExplicitProfileEvenWhenProjectorCanBuildAValidProposal()
    {
        var setup = Create(configureProfile: false);
        var first = EstablishCapture(setup, Capture("no-world-profile", 1, "Wi-Fi"),
            new SemanticPageCandidate("settings-home", "Settings", "Settings home"));
        var typed = AddTypedSources(setup, first.Capture, setup.Profile);
        var proposal = NativeContextObservationProjector.VerifySemantic(
            Record(setup, first.SemanticId), Record(setup, first.CaptureEvidenceId),
            Record(setup, first.IdentityEvidenceId), Record(setup, typed.AnchorId), setup.Profile);

        Assert.NotNull(proposal);
        var result = setup.Kernel.Process(proposal!);

        Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);
        Assert.False(Assert.Single(setup.World.SemanticVerificationLog.Where(item => item.EvidenceId == result.Admission.EvidenceId)).Applied);
        Assert.Empty(setup.World.Current!.SemanticKnowledge);
    }

    [Theory]
    [InlineData("missing-anchor")]
    [InlineData("wrong-profile")]
    [InlineData("wrong-device")]
    public void SameContainerAloneDoesNotReuseWhenCurrentNativeAnchorIsAbsentOrMismatched(string mismatch)
    {
        var setup = Create(configureProfile: true);
        var first = EstablishCapture(setup, Capture("source", 1, "Wi-Fi"),
            new SemanticPageCandidate("settings-home", "Settings", "Settings home"));
        var firstTyped = AddTypedSources(setup, first.Capture, setup.Profile);
        _ = Verify(setup, first, firstTyped.AnchorId);

        var secondCapture = Capture("current", 2, "Bluetooth");
        var second = EstablishCapture(setup, secondCapture);
        Assert.Equal(first.ContainerId, second.ContainerId);
        Assert.Empty(setup.World.Current!.SemanticKnowledge);

        if (mismatch == "wrong-profile")
            _ = AddTypedSources(setup, second.Capture, Profile(profileRef: "different-native-profile:v1"));
        else if (mismatch == "wrong-device")
            _ = AddTypedSources(setup, second.Capture, setup.Profile, observationDevice: "other-device");

        Assert.Empty(setup.World.Current!.SemanticKnowledge);
        Assert.Empty(setup.Kernel.DeriveSlice(second.ContainerId).SemanticKnowledge);
        Assert.Contains(setup.World.DeriveKnowledgeGaps().SemanticReferences,
            reference => reference.EvidenceId == first.SemanticId && reference.IsHistorical);
    }

    private static Setup Create(bool configureProfile)
    {
        var profile = Profile();
        IReadOnlySet<string> scope = new HashSet<string>(StringComparer.Ordinal)
        {
            PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject,
            "ui.semantic.page",
            SemanticVerificationCodec.Subject,
            "ui.node.*",
        };
        var world = new WorldModel(scope, new ProductAssociationStrategy(),
            nativeContextProfiles: configureProfile ? new[] { profile } : Array.Empty<NativeContextSignatureProfile>());
        var ledger = new EvidenceLedger();
        return new(new UniKernel(ledger, world, DisabledRunTrace.Instance), world, ledger, profile);
    }

    private static NativeContextSignatureProfile Profile(string profileRef = "settings-page:v1") =>
        new(profileRef, Device, Package, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "androidx-uiautomator/1", [HomeResource], new Dictionary<string, SemanticPageCandidate>
            { [HomeResource] = new("settings-home", "Settings", "Settings home") });

    private static CaptureSetup EstablishCapture(Setup setup, PageCaptureObservation capture,
        SemanticPageCandidate? semanticCandidate = null)
    {
        var captureId = Process(setup, CaptureProposal(capture));
        var identityId = Process(setup, IdentityProposal(capture, captureId, HomeResource));
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
        var semanticId = Process(setup, semanticProposal);
        return new(capture, captureId, identityId, semanticId, containerId);
    }

    private static string Verify(Setup setup, CaptureSetup source, string anchorId)
    {
        var proposal = NativeContextObservationProjector.VerifySemantic(
            Record(setup, source.SemanticId), Record(setup, source.CaptureEvidenceId),
            Record(setup, source.IdentityEvidenceId), Record(setup, anchorId), setup.Profile);
        Assert.NotNull(proposal);
        var result = setup.Kernel.Process(proposal!);
        Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);
        var decision = setup.World.SemanticVerificationLog.Single(item => item.EvidenceId == result.Admission.EvidenceId);
        Assert.True(decision.Applied, decision.Reason);
        return result.Admission.EvidenceId!;
    }

    private static (string AnchorId, string PackageId) AddTypedSources(Setup setup,
        PageCaptureObservation capture, NativeContextSignatureProfile projectionProfile,
        string? observationDevice = null)
    {
        var metadata = new CaptureMetadata(capture.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "androidx-uiautomator/1", UiHierarchyFormat.AccessibilityTree, capture.CaptureTime,
            TimeSpan.FromMilliseconds(10), observationDevice ?? capture.DeviceId!, capture.SessionCorrelation,
            capture.ObservationCycleId, new HierarchyCapabilities(HierarchyCapability.SemanticText),
            new HierarchyCoverage(CoverageCompleteness.Partial, "viewport-export-only"), capture.Space);
        var observation = new UiHierarchyObservation(metadata, Array.Empty<UiWindowOccurrence>(),
        [
            Node(capture.CaptureId, 0, HomeResource, Package, capture.Items[0].Text, null),
            Node(capture.CaptureId, 1, Package + ":id:row", Package, capture.Items[1].Text, 0),
        ]);
        var proposals = NativeContextObservationProjector.Project(observation, projectionProfile, ObservationContext.External);
        var anchorProposal = Assert.Single(proposals.Where(proposal =>
            proposal.Claim.Subject == $"ui.node.{capture.CaptureId}#0.resource_id"));
        var packageProposal = Assert.Single(proposals.Where(proposal =>
            proposal.Claim.Subject == $"ui.node.{capture.CaptureId}#0.package"));
        var anchorId = Process(setup, anchorProposal);
        var packageId = Process(setup, packageProposal);
        return (anchorId, packageId);
    }

    private static UiNodeObservation Node(string captureId, int index, string resourceId, string package,
        string? text, int? parent) => new(
        new OccurrenceRef(captureId, index), parent is { } parentIndex ? new OccurrenceRef(captureId, parentIndex) : null,
        null, index, null,
        ObservedValue<string>.Observed("android.view.View"), ObservedValue<string>.Observed(resourceId),
        ObservedValue<string>.Observed(package), ObservedValue<string>.Observed(text ?? ""),
        ObservedValue<string>.Unknown("missing"), ObservedValue<string>.Unknown("missing"),
        ObservedValue<bool>.Unknown("missing"), ObservedValue<CheckedState>.Unknown("missing"),
        ObservedValue<bool>.Observed(true), ObservedValue<bool>.Unknown("missing"),
        ObservedValue<bool>.Unknown("missing"), ObservedValue<bool>.Observed(false),
        ObservedValue<bool>.Observed(true), ObservedValue<bool>.Observed(true),
        ObservedValue<bool>.Observed(true), ObservedValue<bool>.Unknown("missing"),
        ObservedValue<UiBounds>.Observed(new(0, index * 30, 90, index * 30 + 20)));

    private static ObservationProposal CaptureProposal(PageCaptureObservation capture) =>
        new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
            ObservationContext.External, new("controlled-layout-fixture", capture.CaptureTime,
                "capture:" + capture.CaptureId, ["controlled-capture:v1", "capture:" + capture.CaptureId]));

    private static ObservationProposal IdentityProposal(PageCaptureObservation capture, string captureEvidenceId,
        string signature) => new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(
            new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation,
                capture.CaptureTime, signature, capture.Items[0].Text))), IngressKind.Observation,
            ObservationContext.External, new("controlled-identity-fixture", capture.CaptureTime,
                "identity:" + capture.CaptureId, ["controlled-identity:v1", "capture:" + capture.CaptureId]))
        { DerivedFromEvidenceIds = [captureEvidenceId], TransitiveEvidenceBasis = new HashSet<string>([captureEvidenceId], StringComparer.Ordinal),
          FusionRule = "page.identity.observed-title-layout", FusionVersion = "1" };

    private static PageCaptureObservation Capture(string id, int second, string rowText) =>
        new(id, "cycle:" + id, "fixture-session", T0.AddSeconds(second),
            CoordinateSpace.DeviceViewport(100, 100, captureId: id),
            [new("title", "text", "Settings", new(0, 0, 90, 20)),
             new("row", "ui.element", rowText, new(0, 30, 90, 60))])
        { DeviceId = Device, Coverage = new(CoverageCompleteness.Partial, "viewport-export-only") };

    private static ObservationProposal ManualVerification(EvidenceRecord semantic, EvidenceRecord capture,
        EvidenceRecord identity, EvidenceRecord anchor, NativeContextSignatureProfile profile)
    {
        var value = SemanticVerificationCodec.Encode(new(semantic.EvidenceId, capture.EvidenceId,
            identity.EvidenceId, anchor.EvidenceId, profile.Fingerprint));
        var sources = new[] { semantic, capture, identity, anchor };
        var leaves = sources.SelectMany(record => record.TransitiveEvidenceBasis.Count == 0
            ? Enumerable.Repeat(record.EvidenceId, 1) : record.TransitiveEvidenceBasis)
            .ToImmutableHashSet(StringComparer.Ordinal);
        return new(new(SemanticVerificationCodec.Subject, value), IngressKind.Observation, ObservationContext.External,
            new("native-context-verifier", capture.Provenance.CaptureTime, "manual-verification-fixture",
                ["semantic-native-profile-verification:v1", "native-profile-ref:" + profile.ProfileRef,
                 "native-profile-sha256:" + profile.Fingerprint]))
        { DerivedFromEvidenceIds = sources.Select(source => source.EvidenceId).ToArray(),
          TransitiveEvidenceBasis = leaves, FusionRule = "semantic.native-profile-verification", FusionVersion = "1" };
    }

    private static string Process(Setup setup, ObservationProposal proposal)
    {
        var result = setup.Kernel.Process(proposal);
        Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);
        return result.Admission.EvidenceId!;
    }

    private static EvidenceRecord Record(Setup setup, string id) => setup.Ledger.CanonicalRecords[id];

    private sealed record Setup(UniKernel Kernel, WorldModel World, EvidenceLedger Ledger,
        NativeContextSignatureProfile Profile);
    private sealed record CaptureSetup(PageCaptureObservation Capture, string CaptureEvidenceId,
        string IdentityEvidenceId, string SemanticId, string ContainerId);
}
