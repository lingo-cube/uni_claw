using UniClaw.Host;
using UniClaw.Kernel;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>Real Ledger/World semantic verification with controlled external perception doubles.</summary>
public sealed class PageSemanticReadSupplyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
    private const string Device = "fixture-device";
    private const string Package = "com.example.settings";
    private const string HomeResource = Package + ":id/home";

    [Fact]
    public async Task HistoricalReferenceStillCallsProvider_ButWorldConfirmedCurrentCorrespondenceIsReadDirectly()
    {
        var setup = Create();
        var firstCapture = Capture("read-c1", 1, "Wi-Fi");
        var first = EstablishCapture(setup, firstCapture, new("settings-home", "Settings", "Settings home"));
        var firstAnchor = AddNativeContext(setup, firstCapture);
        var firstVerification = Verify(setup, first, firstAnchor);
        var firstMeaning = Assert.Single(setup.World.Current!.SemanticKnowledge);
        Assert.False(firstMeaning.Reused);

        var oldRevision = setup.World.Current!;
        var secondCapture = Capture("read-c2", 2, "Bluetooth");
        var second = EstablishCapture(setup, secondCapture);
        Assert.Equal(first.ContainerId, second.ContainerId);
        Assert.Empty(setup.World.Current!.SemanticKnowledge);
        var history = Assert.Single(setup.World.DeriveKnowledgeGaps().SemanticReferences);
        Assert.Equal(first.SemanticEvidenceId, history.EvidenceId);
        Assert.Equal(firstVerification, history.VerificationEvidenceId);
        Assert.True(history.IsHistorical);

        var fallback = new CountingPerception();
        var historicalResult = await PageSemanticReadSupply.ReadAsync(setup.Kernel, fallback,
            Request(secondCapture, second.CaptureEvidenceId, second.IdentityEvidenceId, setup.World.Current!.RevisionId),
            CancellationToken.None);
        Assert.Null(historicalResult.Current);
        Assert.NotNull(historicalResult.Observation);
        Assert.Equal(1, fallback.Calls); // a historical reference cannot suppress a current read
        Assert.Empty(setup.World.Current.SemanticKnowledge);

        var secondAnchor = AddNativeContext(setup, secondCapture);
        var currentMeaning = Assert.Single(setup.World.Current!.SemanticKnowledge);
        Assert.True(currentMeaning.Reused);
        Assert.Equal(first.SemanticEvidenceId, currentMeaning.OriginalSemanticEvidenceId);
        Assert.Equal(firstVerification, currentMeaning.VerificationEvidenceId);
        Assert.Contains(second.CaptureEvidenceId, currentMeaning.CorrespondenceEvidenceIds);
        Assert.Contains(second.IdentityEvidenceId, currentMeaning.CorrespondenceEvidenceIds);
        Assert.Contains(secondAnchor, currentMeaning.CorrespondenceEvidenceIds);
        Assert.DoesNotContain(first.CaptureEvidenceId, currentMeaning.CorrespondenceEvidenceIds);

        var neverFinishes = new NeverCompletingPerception();
        var reused = await PageSemanticReadSupply.ReadAsync(setup.Kernel, neverFinishes,
            Request(secondCapture, second.CaptureEvidenceId, second.IdentityEvidenceId, setup.World.Current.RevisionId),
            CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Same(currentMeaning, reused.Current);
        Assert.Null(reused.Observation);
        Assert.Equal(0, neverFinishes.Calls);
        Assert.Equal(currentMeaning, Assert.Single(setup.Kernel.DeriveSlice(second.ContainerId).SemanticKnowledge));
        Assert.Equal(firstMeaning.OriginalSemanticEvidenceId, Assert.Single(oldRevision.SemanticKnowledge).OriginalSemanticEvidenceId);
    }

    [Fact]
    public async Task WrongCaptureOldRevisionOrStaleRequestFallsBackToPerception()
    {
        var setup = Create();
        var firstCapture = Capture("stale-c1", 1, "Wi-Fi");
        var first = EstablishCapture(setup, firstCapture, new("settings-home", "Settings", "Settings home"));
        var firstAnchor = AddNativeContext(setup, firstCapture);
        _ = Verify(setup, first, firstAnchor);
        var oldRevisionId = setup.World.Current!.RevisionId;

        var currentCapture = Capture("stale-c2", 2, "Bluetooth");
        var current = EstablishCapture(setup, currentCapture);
        var anchor = AddNativeContext(setup, currentCapture);
        Assert.True(Assert.Single(setup.World.Current!.SemanticKnowledge).Reused);
        Assert.NotNull(anchor);

        var perception = new CountingPerception();
        var exact = Request(currentCapture, current.CaptureEvidenceId, current.IdentityEvidenceId,
            setup.World.Current!.RevisionId);
        var wrongCapture = exact with { Current = exact.Current with { CaptureId = "different-capture" } };
        var oldWorldRequest = Request(firstCapture, first.CaptureEvidenceId, first.IdentityEvidenceId, oldRevisionId);
        var stale = exact with { Current = exact.Current with { IsFresh = false } };

        foreach (var request in new[] { wrongCapture, oldWorldRequest, stale })
        {
            var supplied = await PageSemanticReadSupply.ReadAsync(setup.Kernel, perception, request, CancellationToken.None);
            Assert.Null(supplied.Current);
            Assert.NotNull(supplied.Observation);
        }
        Assert.Equal(3, perception.Calls);
    }

    private static Setup Create()
    {
        var profile = new NativeContextSignatureProfile("settings-page:v1", Device, Package,
            UiHierarchyAcquirerKind.AndroidXUiAutomator, "androidx-uiautomator/1", [HomeResource],
            new Dictionary<string, SemanticPageCandidate>
                { [HomeResource] = new("settings-home", "Settings", "Settings home") });
        var scope = new HashSet<string>(StringComparer.Ordinal)
        {
            PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject, "ui.semantic.page",
            SemanticVerificationCodec.Subject, "ui.node.*",
        };
        var world = new WorldModel(scope, new ProductAssociationStrategy(), nativeContextProfiles: [profile]);
        var ledger = new EvidenceLedger();
        return new(new UniKernel(ledger, world, DisabledRunTrace.Instance), world, ledger, profile);
    }

    private static CaptureSetup EstablishCapture(Setup setup, PageCaptureObservation capture,
        SemanticPageCandidate? meaning = null)
    {
        var captureId = Process(setup, new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, ObservationContext.External,
            new("controlled-layout", capture.CaptureTime, "capture:" + capture.CaptureId, ["fixture:capture"])));
        var identityId = Process(setup, new(new(PageObservationCodec.IdentitySubject,
            PageObservationCodec.Encode(new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId,
                capture.SessionCorrelation, capture.CaptureTime, HomeResource, "Settings"))),
            IngressKind.Observation, ObservationContext.External,
            new("controlled-identity", capture.CaptureTime, "identity:" + capture.CaptureId, ["fixture:identity"]))
        {
            DerivedFromEvidenceIds = [captureId],
            TransitiveEvidenceBasis = new HashSet<string> { captureId },
            FusionRule = "controlled-page-identity",
            FusionVersion = "1",
        });
        var container = setup.World.Current!.CurrentPageObservation!.ContainerId!;
        if (meaning is null) return new(capture, captureId, identityId, null, container);

        var request = Request(capture, captureId, identityId, setup.World.Current!.RevisionId);
        var proposal = Assert.Single(SemanticObservationProjector.CreateProposals(request,
            SemanticPerceptionResult.Observed(request, meaning.Key, "Settings")));
        var semanticId = Process(setup, proposal);
        return new(capture, captureId, identityId, semanticId, container);
    }

    private static string AddNativeContext(Setup setup, PageCaptureObservation capture)
    {
        var metadata = new CaptureMetadata(capture.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "androidx-uiautomator/1", UiHierarchyFormat.AccessibilityTree, capture.CaptureTime,
            TimeSpan.FromMilliseconds(10), Device, capture.SessionCorrelation, capture.ObservationCycleId,
            new HierarchyCapabilities(HierarchyCapability.SemanticText),
            new HierarchyCoverage(CoverageCompleteness.Partial, "viewport-export-only"), capture.Space);
        var observation = new UiHierarchyObservation(metadata, Array.Empty<UiWindowOccurrence>(),
        [
            Node(capture.CaptureId, 0, HomeResource, Package, "Settings", null),
            Node(capture.CaptureId, 1, Package + ":id:row", Package, capture.Items[1].Text, 0),
        ]);
        var proposals = NativeContextObservationProjector.Project(observation, setup.Profile, ObservationContext.External);
        var anchor = Assert.Single(proposals.Where(p => p.Claim.Subject == $"ui.node.{capture.CaptureId}#0.resource_id"));
        var package = Assert.Single(proposals.Where(p => p.Claim.Subject == $"ui.node.{capture.CaptureId}#0.package"));
        _ = Process(setup, package);
        return Process(setup, anchor);
    }

    private static string Verify(Setup setup, CaptureSetup source, string anchorId)
    {
        var proposal = NativeContextObservationProjector.VerifySemantic(
            setup.Ledger.CanonicalRecords[source.SemanticEvidenceId!],
            setup.Ledger.CanonicalRecords[source.CaptureEvidenceId],
            setup.Ledger.CanonicalRecords[source.IdentityEvidenceId],
            setup.Ledger.CanonicalRecords[anchorId], setup.Profile);
        Assert.NotNull(proposal);
        var id = Process(setup, proposal!);
        Assert.True(Assert.Single(setup.World.SemanticVerificationLog.Where(d => d.EvidenceId == id)).Applied);
        return id;
    }

    private static SemanticPerceptionRequest Request(PageCaptureObservation capture, string captureEvidenceId,
        string identityEvidenceId, string revisionId, bool isFresh = true) => new(
        "semantic-read:" + capture.CaptureId,
        new CapabilityCorrelation(capture.SessionCorrelation, captureId: capture.CaptureId,
            observationCycleId: capture.ObservationCycleId),
        new(capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
            capture.Space, capture.Items, [captureEvidenceId], isFresh, revisionId),
        [new("settings-home", "Settings", "Settings home")]);

    private static PageCaptureObservation Capture(string id, int second, string rowText) => new(
        id, "cycle:" + id, "fixture-session", T0.AddSeconds(second),
        CoordinateSpace.DeviceViewport(100, 100, captureId: id),
        [new("title", "text", "Settings", new(0, 0, 90, 20)),
         new("row", "ui.element", rowText, new(0, 30, 90, 60))])
    { DeviceId = Device, Coverage = new(CoverageCompleteness.Partial, "viewport-export-only") };

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

    private static string Process(Setup setup, ObservationProposal proposal)
    {
        var result = setup.Kernel.Process(proposal);
        Assert.True(result.Admission.Decision == AdmissionDecision.Accepted, result.Admission.RejectionReason);
        return result.Admission.EvidenceId!;
    }

    private sealed record Setup(UniKernel Kernel, WorldModel World, EvidenceLedger Ledger,
        NativeContextSignatureProfile Profile);
    private sealed record CaptureSetup(PageCaptureObservation Capture, string CaptureEvidenceId,
        string IdentityEvidenceId, string? SemanticEvidenceId, string ContainerId);

    private sealed class CountingPerception : ISemanticPerception
    {
        public int Calls { get; private set; }
        public CapabilityDescription Description => new("test.semantic", "1", CapabilityScope.ProductRuntime,
            [new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version)],
            Array.Empty<CapabilityDependency>(), HealthStatus.Healthy);

        public ValueTask<SemanticPerceptionResult> ObserveSemanticsAsync(SemanticPerceptionRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(SemanticPerceptionResult.Empty(request, CapabilityStatus.Unavailable,
                PerceptionDisposition.Unavailable, "controlled-provider-double"));
        }
    }

    private sealed class NeverCompletingPerception : ISemanticPerception
    {
        private readonly TaskCompletionSource<SemanticPerceptionResult> _never =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public CapabilityDescription Description => new("test.never-semantic", "1", CapabilityScope.ProductRuntime,
            [new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version)],
            Array.Empty<CapabilityDependency>(), HealthStatus.Healthy);

        public ValueTask<SemanticPerceptionResult> ObserveSemanticsAsync(SemanticPerceptionRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return new ValueTask<SemanticPerceptionResult>(_never.Task);
        }
    }
}
