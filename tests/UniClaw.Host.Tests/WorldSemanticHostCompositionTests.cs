using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>Controlled Host composition proof; source observations are fixture-controlled, not live device/model evidence.</summary>
public sealed class WorldSemanticHostCompositionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
    private const string Device = "fixture-device";
    private const string Package = "com.example.settings";
    private const string Home = Package + ":id/home";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SettingsCoverageComposition_OnlyExplicitProfileAllowsCurrentSemantics(bool configureProfile)
    {
        var profile = new NativeContextSignatureProfile("settings-home:v1", Device, Package,
            UiHierarchyAcquirerKind.AndroidXUiAutomator, "androidx-uiautomator/1", [Home],
            new Dictionary<string, SemanticPageCandidate>
                { [Home] = new("settings-home", "Settings", "Settings home") });
        var proposalBatch = new List<ObservationProposal>();
        var projectedCounts = new List<int>();
        var consultationKnowledge = new List<WorldKnowledgeView?>();
        RunDriverInput? Feed(ObservationDirective directive)
        {
            if (proposalBatch.Count == 0)
            {
                proposalBatch.AddRange(BuildVerifiedCapture(directive.Context, profile, configureProfile,
                    out var markedCount));
                projectedCounts.Add(markedCount);
            }
            return new RunDriverInput.Observation(proposalBatch.ToArray());
        }

        var runRoot = Path.Combine(Path.GetTempPath(), "semantic-host-composition-" + Guid.NewGuid().ToString("N"));
        try
        {
            _ = SettingsCoverageRunner.Run(runRoot, new SettingsCoverageRunner.Options(
                DeviceId: Device,
                Live: new LivePerception.LiveAssets(Device, "fixture", "/controlled", "/controlled/python"),
                Config: SettingsCoverageConfig.LoadDefault(),
                UnderlyingConsult: context =>
                {
                    consultationKnowledge.Add(context.WorldKnowledge);
                    return null;
                },
                FeedNext: Feed,
                EffectDriver: new NoEffectDriver(),
                LanguageInspection: new UniClaw.Host.Capability.LanguageInspectionTaskRequest(false, "en"),
                UseCoverageDirector: false,
                NativeContextProfile: configureProfile ? profile : null));

            Assert.NotEmpty(consultationKnowledge);
            Assert.Contains(projectedCounts, count => configureProfile ? count > 0 : count == 0);
            var currentMeanings = consultationKnowledge.SelectMany(view => view?.CurrentSemantics ?? [])
                .ToArray();
            if (configureProfile)
                Assert.Contains(currentMeanings, meaning => meaning.PageKey == "settings-home" && !meaning.Reused);
            else
                Assert.Empty(currentMeanings);
        }
        finally
        {
            if (Directory.Exists(runRoot)) Directory.Delete(runRoot, recursive: true);
        }
    }

    private static IReadOnlyList<ObservationProposal> BuildVerifiedCapture(ObservationContext context,
        NativeContextSignatureProfile profile, bool projectProfile, out int markedCount)
    {
        var capture = new PageCaptureObservation("host-profile-c1", "host-profile-cycle1", "fixture-session",
            T0, CoordinateSpace.DeviceViewport(100, 100, captureId: "host-profile-c1"),
            [new("title", "text", "Settings", new(0, 0, 90, 20)),
             new("row", "ui.element", "Wi-Fi", new(0, 30, 90, 60))])
        { DeviceId = Device, Coverage = new(CoverageCompleteness.Partial, "fixture-viewport-export") };
        var captureProposal = new ObservationProposal(
            new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, context,
            new("controlled-host-composition", T0, "capture:" + capture.CaptureId, ["controlled-source"]));
        var stagingLedger = new EvidenceLedger();
        var captureRecord = Admit(stagingLedger, captureProposal);
        var identity = new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId,
            capture.SessionCorrelation, capture.CaptureTime, Home, "Settings");
        var identityProposal = new ObservationProposal(
            new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(identity)),
            IngressKind.Observation, context,
            new("controlled-host-composition", T0, "identity:" + capture.CaptureId, ["fixture:layout-title"]))
        {
            DerivedFromEvidenceIds = [captureRecord.EvidenceId],
            TransitiveEvidenceBasis = new HashSet<string>([captureRecord.EvidenceId], StringComparer.Ordinal),
            FusionRule = "fixture-page-identity", FusionVersion = "1",
        };
        var identityRecord = Admit(stagingLedger, identityProposal);
        var request = new SemanticPerceptionRequest("fixture-semantic", new CapabilityCorrelation(
            capture.SessionCorrelation, captureId: capture.CaptureId, observationCycleId: capture.ObservationCycleId),
            new(capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
                capture.Space, capture.Items, [captureRecord.EvidenceId], true, "fixture-revision"),
            [new("settings-home", "Settings", "Settings home")]);
        var semanticProposal = Assert.Single(SemanticObservationProjector.CreateProposals(request,
            SemanticPerceptionResult.Observed(request, "settings-home", "Settings")));
        var semanticRecord = Admit(stagingLedger, semanticProposal);

        var metadata = new CaptureMetadata(capture.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "androidx-uiautomator/1", UiHierarchyFormat.AccessibilityTree, capture.CaptureTime,
            TimeSpan.FromMilliseconds(5), Device, capture.SessionCorrelation, capture.ObservationCycleId,
            new HierarchyCapabilities(HierarchyCapability.SemanticText),
            new HierarchyCoverage(CoverageCompleteness.Partial, "fixture-viewport-export"), capture.Space);
        var hierarchy = new UiHierarchyObservation(metadata, Array.Empty<UiWindowOccurrence>(),
        [
            Node(capture.CaptureId, 0, Home, Package, "Settings", null),
            Node(capture.CaptureId, 1, Package + ":id:wifi", Package, "Wi-Fi", 0),
        ]);
        var typed = NativeContextObservationProjector.Project(hierarchy,
            projectProfile ? profile : null, context);
        var anchor = Assert.Single(typed.Where(proposal => proposal.Claim.Subject ==
            $"ui.node.{capture.CaptureId}#0.resource_id"));
        markedCount = anchor.Provenance!.TransformationLineage.Count(value =>
            value == PageInterpretationCodec.NativeContextProfile);
        var packageProposal = Assert.Single(typed.Where(proposal => proposal.Claim.Subject ==
            $"ui.node.{capture.CaptureId}#0.package"));
        var packageRecord = Admit(stagingLedger, packageProposal);
        var anchorRecord = Admit(stagingLedger, anchor);
        ObservationProposal? verification = null;
        if (projectProfile)
            verification = NativeContextObservationProjector.VerifySemantic(semanticRecord, captureRecord,
                identityRecord, anchorRecord, profile);

        var all = new List<ObservationProposal> { captureProposal, identityProposal, semanticProposal };
        all.AddRange(typed);
        if (verification is not null) all.Add(verification);
        return all;
    }

    private static EvidenceRecord Admit(EvidenceLedger ledger, ObservationProposal proposal)
    {
        var admitted = ledger.Admit(proposal);
        Assert.Equal(AdmissionDecision.Accepted, admitted.Admission.Decision);
        return admitted.Record!;
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

    private sealed class NoEffectDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            throw new InvalidOperationException("this controlled scenario must remain zero-effect");
    }
}
