using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class WorldPageHistoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NativeContextCorrection_ReassignsOnlyItsOriginalObservation_AndPreservesOldSnapshot()
    {
        var (kernel, world, ledger) = Create();
        string Identify(PageCaptureObservation c, string signature)
        {
            var captureId = kernel.Process(CaptureProposal(c)).Admission.EvidenceId!;
            kernel.Process(IdentityProposal(c, captureId, signature));
            return captureId;
        }
        // A known native-context candidate was independently established first.
        Identify(Capture("known-b", 1), "native:page:B");
        var b = world.Current!.CurrentPageObservation!.ContainerId!;
        var originalCapture = Capture("wrong", 2);
        var source = Identify(originalCapture, "A");
        var a = world.Current!.CurrentPageObservation!.ContainerId!;
        var previousIdentity = world.Current.CurrentPageObservation.IdentityEvidenceId!;
        var oldRevision = world.Current.RevisionId;
        var oldBytes = System.Text.Json.JsonSerializer.Serialize(world.Current);
        var support = NativeContextSource(originalCapture, "native:page:B");
        var supportId = kernel.Process(support).Admission.EvidenceId!;
        var correction = new PageInterpretationCorrection(source, previousIdentity,
            new(originalCapture.CaptureId, originalCapture.ObservationCycleId, originalCapture.SessionCorrelation,
                originalCapture.CaptureTime, "native:page:B", "Settings"), [supportId], "native-context-parser:v1");
        var proposal = new ObservationProposal(new(PageInterpretationCodec.Subject, PageInterpretationCodec.Encode(correction)),
            IngressKind.Observation, ObservationContext.External,
            new("history-interpreter", originalCapture.CaptureTime, "reinterpret:" + source, ["original-capture-reprocessing"]))
        { DerivedFromEvidenceIds = [source, supportId], TransitiveEvidenceBasis = new HashSet<string> { source, supportId },
          FusionRule = "page.interpretation", FusionVersion = "1" };

        var applied = kernel.Process(proposal);

        Assert.Equal(b, world.Current!.CurrentPageObservation!.ContainerId);
        Assert.NotEqual(a, world.Current.CurrentPageObservation.ContainerId);
        Assert.All(world.Current.Occurrences!, occurrence => Assert.Equal(b, occurrence.OwningContainerId));
        Assert.Equal(originalCapture.CaptureTime, world.Current.CurrentPageObservation.Capture.CaptureTime);
        Assert.Equal(oldBytes, System.Text.Json.JsonSerializer.Serialize(world.RevisionHistory.Single(r => r.RevisionId == oldRevision)));
        var committed = world.Current;
        Assert.Null(kernel.Process(proposal).ResultingRevision);
        Assert.Same(committed, world.Current);
        Assert.Equal(AdmissionDecision.Accepted, applied.Admission.Decision);
        Assert.Equal(2, world.Current.Containers!.Count);
        Assert.Contains(source, ledger.CanonicalRecords.Keys);
    }

    private static ObservationProposal NativeContextSource(PageCaptureObservation capture, string signature) =>
        new(new("ui.node." + capture.CaptureId + "#0.resource_id", signature), IngressKind.Observation,
            ObservationContext.External, new("platform.uiautomator", capture.CaptureTime, "context:" + capture.CaptureId,
                ["typed-hierarchy:v1", PageInterpretationCodec.NativeContextProfile],
                new HierarchyCaptureDescriptor(capture.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
                    "controlled-recorded-profile:v1", UiHierarchyFormat.UiAutomatorXml, "fixture-device",
                    capture.SessionCorrelation, capture.ObservationCycleId, capture.CaptureTime, null,
                    HierarchyCapability.SemanticText, CoverageCompleteness.Partial, "viewport", 0, null,
                    "resource_id", capture.Space)));

    [Fact]
    public void PageHistory_UsesFixedOwnerSnapshots_AndNeverTurnsOldFragmentsIntoCurrentTargets()
    {
        var (kernel, world, ledger) = Create();
        string Observe(PageCaptureObservation c)
        {
            var id = kernel.Process(CaptureProposal(c)).Admission.EvidenceId!;
            kernel.Process(IdentityProposal(c, id, "A"));
            return id;
        }
        var first = Capture("c1", 1);
        var source = Observe(first);
        var root = world.Current!.CurrentPageObservation!.ContainerId!;
        var request = new SemanticPerceptionRequest("meaning:c1", new CapabilityCorrelation("session",
                captureId: "c1", observationCycleId: "cycle:c1"),
            new(first.CaptureId, first.ObservationCycleId, first.SessionCorrelation, first.CaptureTime,
                first.Space, first.Items, [source]), [new("settings", "设置", "设备设置")]);
        var proposal = Assert.Single(SemanticObservationProjector.CreateProposals(request,
            SemanticPerceptionResult.Observed(request, "settings", "Settings")));
        kernel.Process(proposal);
        var oldRevision = world.Current!.RevisionId;
        var original = world.DerivePageView(root);
        var bytes = System.Text.Json.JsonSerializer.Serialize(original);
        var second = Capture("c2", 3) with { Items = [first.Items[0], first.Items[1] with { Text = "Bluetooth" }] };
        Observe(second);
        var current = world.DerivePageView(root);

        Assert.Equal(2, current.Observations.Count);
        Assert.False(current.Observations[0].IsCurrentCapture);
        Assert.True(current.Observations[1].IsCurrentCapture);
        Assert.Contains(current.Observations[0].Occurrences, o => o.SemanticDescriptor == "Wi-Fi");
        Assert.Contains(current.Observations[1].Occurrences, o => o.SemanticDescriptor == "Bluetooth");
        Assert.DoesNotContain(world.DeriveSlice(root).Occurrences, o => o.SemanticDescriptor == "Wi-Fi");
        Assert.True(Assert.Single(current.SemanticReferences).IsHistorical);
        Assert.Equal(bytes, System.Text.Json.JsonSerializer.Serialize(world.DerivePageView(root, oldRevision)));
        Assert.Equal(bytes, System.Text.Json.JsonSerializer.Serialize(original));
        Assert.Contains("whole-page-coverage-unproven", current.Unknown);
        Assert.Contains("visit-occurrence-unproven", current.Unknown);
        Assert.Throws<InvalidOperationException>(() => world.DerivePageView(root, "unknown-revision"));
        Assert.Throws<InvalidOperationException>(() => world.DerivePageView("unknown-container"));

        var currentBytes = System.Text.Json.JsonSerializer.Serialize(current);
        // A late interpretation of C1 is retained as accepted evidence but cannot annotate C2.
        var late = proposal with { Provenance = proposal.Provenance with { Producer = "late-model" } };
        kernel.Process(late);
        Assert.Equal("c2", world.Current!.CurrentPageObservation!.Capture.CaptureId);
        Assert.Equal(current.ContentDigest, world.DerivePageView(root).ContentDigest);
        Assert.Equal(currentBytes, System.Text.Json.JsonSerializer.Serialize(current));
        WorldVisualizationExport.Write("late-result", WorldVisualizationExport.Frame(
            "late-c1-interpretation", "Late interpretation remains tied to C1; C2 stays the current capture",
            kernel, world, [root]));
        _ = ledger;
    }

    [Fact]
    public void PageHistory_KeepsLastConflictingAttribution_InsteadOfFallingBackToEarlierConfirmation()
    {
        var (kernel, world, _) = Create();
        var capture = Capture("c1", 1);
        var source = kernel.Process(CaptureProposal(capture)).Admission.EvidenceId!;
        kernel.Process(IdentityProposal(capture, source, "A"));
        var root = world.Current!.CurrentPageObservation!.ContainerId!;
        kernel.Process(IdentityProposal(capture, source, "B"));
        var view = world.DerivePageView(root);
        var fragment = Assert.Single(view.Observations);
        Assert.Null(fragment.ContainerId);
        Assert.Null(fragment.IdentityEvidenceId);
        Assert.All(fragment.Occurrences, occurrence => Assert.Null(occurrence.OwningContainerId));
        Assert.Contains("observation-attribution-conflicted:" + source, view.Unknown);
    }

    [Fact]
    public void CaptureCodec_PreservesDeclaredSurfaceCoverage_WithoutInferringWholePageCompleteness()
    {
        var raw = System.Text.Json.Nodes.JsonNode.Parse(PageObservationCodec.Encode(Capture("c1", 1)))!.AsObject();
        raw["coverage"] = System.Text.Json.Nodes.JsonNode.Parse(
            "{\"completeness\":1,\"limitation\":\"viewport-export;offscreen-unproven\"}");
        Assert.True(PageObservationCodec.TryReadCapture(raw.ToJsonString(), out var decoded));
        Assert.Contains("offscreen-unproven", PageObservationCodec.Encode(decoded!));
        raw["coverage"]!["completeness"] = 999;
        Assert.False(PageObservationCodec.TryReadCapture(raw.ToJsonString(), out _));
    }

    [Fact]
    public void RegionSlices_KeepExactSource_AndSelectOnlyContainedCurrentOccurrences()
    {
        var (kernel, world, ledger) = Create();
        var capture = Capture("c1", 1);
        var source = ledger.CanonicalRecords[kernel.Process(CaptureProposal(capture)).Admission.EvidenceId!];
        kernel.Process(IdentityProposal(capture, source.EvidenceId, "A"));
        var root = world.Current!.CurrentPageObservation!.ContainerId!;
        var before = world.Current;
        var top = new SliceRegion("c1", capture.Space, new SpatialLocator(0, 0, 1, .25, "device-viewport"));
        var row = new SliceRegion("c1", capture.Space, new SpatialLocator(0, .25, 1, 1, "device-viewport"));
        var a = kernel.DeriveSlice(root, region: top);
        var b = kernel.DeriveSlice(root, region: row);
        Assert.Equal("Settings", Assert.Single(a.Occurrences).SemanticDescriptor);
        Assert.Equal("Wi-Fi", Assert.Single(b.Occurrences).SemanticDescriptor);
        Assert.NotEqual(Core.CoreSemanticProjection.SliceId(a), Core.CoreSemanticProjection.SliceId(b));
        Assert.Same(before, world.Current);
        Assert.Throws<InvalidOperationException>(() => kernel.DeriveSlice(root, region:
            new SliceRegion("other", CoordinateSpace.DeviceViewport(100, 100, captureId: "other"), top.Bounds)));
        Assert.Throws<InvalidOperationException>(() => kernel.DeriveSlice(root, region:
            new SliceRegion("c1", CoordinateSpace.DeviceViewport(200, 100, captureId: "c1"), top.Bounds)));
        Assert.Throws<ArgumentException>(() => new SliceRegion("c1", capture.Space,
            new SpatialLocator(0, 0, 1, 1, "different-frame")));
        Assert.Throws<ArgumentException>(() => new SliceRegion("c1", capture.Space,
            new SpatialLocator(0, 0, 0, 1, "device-viewport")));
        var scope = new[] { root };
        var full = kernel.DeriveSlice(root, scope);
        scope[0] = "caller-mutation";
        Assert.Equal(root, Assert.Single(full.InScopeContainerIds));
        var next = Capture("c2", 2);
        kernel.Process(CaptureProposal(next));
        Assert.False(kernel.IsSliceValid(a));
        Assert.Equal("c1", a.Region!.CaptureId);
        Assert.Equal("Settings", Assert.Single(a.Occurrences).SemanticDescriptor);
    }

    [Fact]
    public void ConfirmedCaptureSemanticProposal_UsesOriginalCaptureBasis_WithoutInventingIdentityParent()
    {
        var (kernel, world, ledger) = Create();
        var capture = Capture("c1", 1);
        var source = ledger.CanonicalRecords[kernel.Process(CaptureProposal(capture)).Admission.EvidenceId!];
        kernel.Process(IdentityProposal(capture, source.EvidenceId, "settings/details"));
        var root = world.Current!.CurrentPageObservation!.ContainerId!;
        var request = new SemanticPerceptionRequest("meaning:c1",
            new CapabilityCorrelation("session", captureId: "c1", observationCycleId: "cycle:c1"),
            new(capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
                capture.Space, capture.Items, [source.EvidenceId]),
            [new("settings", "设置", "修改设备设置")]);
        var result = SemanticPerceptionResult.Observed(request, "settings", "Settings");
        var proposal = Assert.Single(SemanticObservationProjector.CreateProposals(request, result));

        var accepted = ledger.CanonicalRecords[kernel.Process(proposal).Admission.EvidenceId!];

        Assert.Contains(source.EvidenceId, accepted.DerivedFromEvidenceIds);
        Assert.Equal(proposal.Claim.Value, world.DeriveSlice(root).ScopedClaims[root + ".semantic.page"]);
        Assert.Equal("settings/details", world.Current!.WorldState[WorldModel.SignatureSubjectPrefix + root].Value);
        Assert.Single(world.Current.Containers!);
    }

    private static (UniKernel Kernel, WorldModel World, EvidenceLedger Ledger) Create()
    {
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject, "ui.semantic.page", "ui.node.*", PageInterpretationCodec.Subject }, new ProductAssociationStrategy());
        var ledger = new EvidenceLedger();
        return (new UniKernel(ledger, world, DisabledRunTrace.Instance), world, ledger);
    }

    private static PageCaptureObservation Capture(string id, int second) => new(id, "cycle:" + id, "session",
        T0.AddSeconds(second), CoordinateSpace.DeviceViewport(100, 100, captureId: id),
        [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
         new("row", "ui.element", "Wi-Fi", new UiBounds(0, 30, 90, 60))]) { DeviceId = "fixture-device" };

    private static ObservationProposal CaptureProposal(PageCaptureObservation capture) =>
        new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
            ObservationContext.External, new("recorded-layout", capture.CaptureTime, "capture:" + capture.CaptureId,
                ["capture:" + capture.CaptureId]));

    private static ObservationProposal IdentityProposal(PageCaptureObservation capture, string sourceId, string signature) =>
        new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
                capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
                signature, "Settings"))), IngressKind.Observation, ObservationContext.External,
            new("identity-supply", capture.CaptureTime, "identity:" + capture.CaptureId, ["capture:" + capture.CaptureId]))
        { DerivedFromEvidenceIds = [sourceId], TransitiveEvidenceBasis = new HashSet<string> { sourceId },
          FusionRule = "page.identity.observed-title-layout", FusionVersion = "1" };
}
