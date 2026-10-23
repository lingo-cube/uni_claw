using System.Collections.Frozen;
using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

/// <summary>
/// Exercises World-owned accumulated content using accepted page captures,
/// identities, and controlled hierarchy evidence. The recorded operation is a
/// controlled World verification reference, not proof of live device delivery.
/// </summary>
public sealed class WorldAccumulatedContentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VerifiedAlignedScroll_AccumulatesOnlyWorldMatchedFragments_AndKeepsOldViewImmutable()
    {
        var setup = Create();
        var sourceCapture = Capture("content-c1", 1);
        var sourceId = ObservePage(setup.Kernel, sourceCapture);
        var container = setup.World.Current!.CurrentPageObservation!.ContainerId!;
        AddViewportLandscape(setup.Kernel, sourceCapture, "source");
        var sourceRevision = setup.World.Current!.RevisionId;
        var oldView = setup.World.DerivePageView(container);
        var oldBytes = JsonSerializer.Serialize(oldView);
        var before = setup.World.Current;
        var scrollRegion = before!.Occurrences!.Single(o => o.Role == "scrollable");

        var targetCapture = Capture("content-c2", 4);
        var targetId = ObservePage(setup.Kernel, targetCapture);
        Assert.Equal(container, setup.World.Current!.CurrentPageObservation!.ContainerId);
        AddViewportLandscape(setup.Kernel, targetCapture, "target");
        var targetRevision = setup.World.Current!.RevisionId;
        var transition = new ObservedViewportTransition(sourceRevision, targetRevision, container,
            "receipt:controlled-scroll", "binding:controlled-scroll", scrollRegion.OccurrenceId,
            "swipe-up", T0.AddSeconds(3), "run:content-fixture", "verification:controlled-scroll");

        Assert.True(setup.World.RecordObservedViewportTransition(transition));
        var current = setup.World.Current!;
        var pageView = setup.World.DerivePageView(container);
        var accumulated = pageView.AccumulatedContent!;

        Assert.Equal(current.RevisionId, accumulated.SourceRevisionId);
        Assert.Equal(targetId, accumulated.CurrentCaptureEvidenceId);
        Assert.Equal(new[] { "verification:controlled-scroll" }, accumulated.RegistrationSourceRefs);
        var edge = Assert.Single(pageView.ViewportTransitions);
        Assert.Equal("Aligned", edge.AlignmentStatus);
        Assert.Equal(2, edge.Correspondences.Count(c => !c.Stationary));
        Assert.DoesNotContain(accumulated.Unknown, value => value.StartsWith("viewport-registration-incomplete:", StringComparison.Ordinal));
        var oldRow = Assert.Single(accumulated.Items.Where(i => i.SemanticDescriptor == "old-row"));
        Assert.Equal("selected", oldRow.ObservedState);
        Assert.Null(oldRow.CurrentState);
        Assert.Equal("previously-observed-current-presence-unknown", oldRow.CurrentPresence);
        Assert.Equal(sourceId, oldRow.CaptureEvidenceId);
        Assert.Equal(-.01, oldRow.Bounds.Y1, 6);
        Assert.IsType<WorldContentBounds>(oldRow.Bounds);
        var currentHeader = Assert.Single(accumulated.Items.Where(i => i.SemanticDescriptor == "fixed-header"));
        Assert.Equal(targetId, currentHeader.CaptureEvidenceId);
        Assert.Equal("header", currentHeader.CurrentState);
        Assert.Equal(.02, currentHeader.Bounds.Y1, 6); // retained at its target-frame position
        var currentSlice = setup.World.DeriveSlice(container);
        Assert.DoesNotContain(currentSlice.Occurrences, occurrence => occurrence.SemanticDescriptor == "old-row");
        Assert.Contains(currentSlice.Occurrences, occurrence => occurrence.SemanticDescriptor == "new-bottom");
        Assert.Equal(oldBytes, JsonSerializer.Serialize(oldView));
        Assert.Empty(before.ViewportTransitions);
    }

    [Theory]
    [InlineData("weak", "InsufficientOverlap")]
    [InlineData("ambiguous", "AmbiguousAnchors")]
    public void IncompleteOrAmbiguousRegistration_DoesNotAccumulateHistoricalContent(string mode, string alignment)
    {
        var setup = Create();
        var sourceCapture = Capture("negative-c1", 1);
        ObservePage(setup.Kernel, sourceCapture);
        var container = setup.World.Current!.CurrentPageObservation!.ContainerId!;
        AddViewportLandscape(setup.Kernel, sourceCapture, "source");
        var source = setup.World.Current!;
        var scroll = source.Occurrences!.Single(o => o.Role == "scrollable");
        var oldBytes = JsonSerializer.Serialize(setup.World.DerivePageView(container));

        var targetCapture = Capture("negative-c2", 4);
        ObservePage(setup.Kernel, targetCapture);
        AddViewportLandscape(setup.Kernel, targetCapture, mode);
        var target = setup.World.Current!;
        Assert.True(setup.World.RecordObservedViewportTransition(new(source.RevisionId, target.RevisionId,
            container, "receipt:" + mode, "binding:" + mode, scroll.OccurrenceId,
            "swipe-up", T0.AddSeconds(3), "run:content-fixture", "verification:" + mode)));

        var pageView = setup.World.DerivePageView(container);
        Assert.Equal(alignment, Assert.Single(pageView.ViewportTransitions).AlignmentStatus);
        Assert.Contains(pageView.AccumulatedContent!.Unknown,
            value => value.StartsWith("viewport-registration-incomplete:", StringComparison.Ordinal));
        Assert.DoesNotContain(pageView.AccumulatedContent.Items, item => item.SemanticDescriptor == "old-row");
        Assert.Equal(oldBytes, JsonSerializer.Serialize(setup.World.DerivePageView(container, source.RevisionId)));
    }

    [Fact]
    public void NoVerifiedTransitionOrIndependentCapture_NeverInheritsEarlierContent()
    {
        var setup = Create();
        var first = Capture("isolated-c1", 1);
        ObservePage(setup.Kernel, first);
        var container = setup.World.Current!.CurrentPageObservation!.ContainerId!;
        AddViewportLandscape(setup.Kernel, first, "source");
        var oldRowEvidence = setup.World.Current!.CurrentPageObservation!.EvidenceId;

        var second = Capture("isolated-c2", 4);
        ObservePage(setup.Kernel, second);
        AddViewportLandscape(setup.Kernel, second, "target");
        var withoutEdge = setup.World.DerivePageView(container).AccumulatedContent!;
        Assert.Equal(setup.World.Current!.CurrentPageObservation!.EvidenceId, withoutEdge.CurrentCaptureEvidenceId);
        Assert.DoesNotContain(withoutEdge.Items, item => item.SemanticDescriptor == "old-row");

        var third = Capture("isolated-c3", 6);
        ObservePage(setup.Kernel, third);
        AddViewportLandscape(setup.Kernel, third, "independent");
        var independent = setup.World.DerivePageView(container).AccumulatedContent!;
        Assert.DoesNotContain(independent.Items, item => item.CaptureEvidenceId == oldRowEvidence);
        Assert.DoesNotContain(independent.Items, item => item.SemanticDescriptor == "old-row");
        Assert.DoesNotContain(independent.Unknown, value => value.StartsWith("viewport-registration-incomplete:", StringComparison.Ordinal));
    }

    private static (UniKernel Kernel, WorldModel World) Create()
    {
        var world = new WorldModel(new HashSet<string>
            { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject, "test.viewport" },
            new ProductAssociationStrategy(), new ControlledViewportObservationStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
        return (kernel, world);
    }

    private static PageCaptureObservation Capture(string id, int second)
    {
        var time = T0.AddSeconds(second);
        var source = id.EndsWith("c1", StringComparison.Ordinal);
        return new(id, "cycle:" + id, "session-content", time,
            CoordinateSpace.DeviceViewport(1000, 1000, captureId: id),
            [new("title", "text", "Settings", new UiBounds(0, 0, 900, 80)),
             new("scroll", "scrollable", "main-list", new UiBounds(0, 50, 1000, 960)),
             new("header", "ui.element", "fixed-header", new UiBounds(20, 20, 900, 120)),
             new("alpha", "ui.element", "anchor-alpha", new UiBounds(80, source ? 300 : 100, 800, source ? 360 : 160)),
             new("beta", "ui.element", "anchor-beta", new UiBounds(80, source ? 500 : 300, 800, source ? 560 : 360)),
             new("row", "ui.element", source ? "old-row" : "new-bottom",
                 source ? new UiBounds(80, 190, 800, 240) : new UiBounds(80, 800, 800, 880))])
        { DeviceId = "fixture-device" };
    }

    private static string ObservePage(UniKernel kernel, PageCaptureObservation capture)
    {
        var result = kernel.Process(new ObservationProposal(
            new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, ObservationContext.External,
            new("controlled-layout", capture.CaptureTime, "capture:" + capture.CaptureId, ["fixture:layout"])))
            .Admission;
        var captureEvidenceId = result.EvidenceId!;
        kernel.Process(new ObservationProposal(new(PageObservationCodec.IdentitySubject,
            PageObservationCodec.Encode(new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId,
                capture.SessionCorrelation, capture.CaptureTime, "settings", "Settings"))),
            IngressKind.Observation, ObservationContext.External,
            new("controlled-identity", capture.CaptureTime, "identity:" + capture.CaptureId, ["fixture:identity"]))
        {
            DerivedFromEvidenceIds = [captureEvidenceId],
            TransitiveEvidenceBasis = new HashSet<string> { captureEvidenceId }.ToFrozenSet(StringComparer.Ordinal),
            FusionRule = "controlled-page-identity",
            FusionVersion = "1",
        });
        return captureEvidenceId;
    }

    private static void AddViewportLandscape(UniKernel kernel, PageCaptureObservation capture, string mode)
    {
        kernel.Process(new ObservationProposal(new("test.viewport", mode), IngressKind.Observation,
            ObservationContext.External,
            new("controlled-ui-observation", capture.CaptureTime, "hierarchy:" + capture.CaptureId,
                ["fixture:typed-layout"], new HierarchyCaptureDescriptor(capture.CaptureId, 35,
                    UiHierarchyAcquirerKind.AndroidXUiAutomator, "controlled-v1", UiHierarchyFormat.UiAutomatorXml,
                    "fixture-device", capture.SessionCorrelation, capture.ObservationCycleId, capture.CaptureTime,
                    null, HierarchyCapability.SemanticText, CoverageCompleteness.Partial, "fixture-content",
                    0, null, "text", capture.Space))));
    }

    private sealed class ControlledViewportObservationStrategy : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if (record.Claim.Subject != "test.viewport") return [];
            var owner = previous!.WorldState[WorldModel.CurrentContainerSubject].Value;
            var target = record.Claim.Value is "target" or "weak" or "ambiguous" or "independent";
            var offset = target ? .2 : 0;
            var region = new SpatialLocator(0, .05, 1, .96, "device-viewport");
            var result = new List<ProposedOccurrence>
            {
                new(owner, "scrollable", "main-list", Locator: region, Space: record.Provenance.Hierarchy!.Space),
                new(owner, "ui.element", "fixed-header", "header", new(.02, .02, .9, .12, "device-viewport"),
                    Space: record.Provenance.Hierarchy.Space),
                new(owner, "ui.element", "anchor-alpha", Locator: new(.08, .30-offset, .8, .36-offset, "device-viewport"),
                    Space: record.Provenance.Hierarchy.Space),
                new(owner, "ui.element", "anchor-beta", Locator: new(.08, .50-offset, .8, .56-offset, "device-viewport"),
                    Space: record.Provenance.Hierarchy.Space),
            };
            if (!target)
                result.Add(new(owner, "ui.element", "old-row", "selected",
                    new(.08, .19, .8, .24, "device-viewport"), Space: record.Provenance.Hierarchy.Space));
            else
                result.Add(new(owner, "ui.element", "new-bottom", "ready",
                    new(.08, .80, .8, .88, "device-viewport"), Space: record.Provenance.Hierarchy.Space));
            if (record.Claim.Value == "weak")
                result.RemoveAll(o => o.SemanticDescriptor == "anchor-beta");
            if (record.Claim.Value == "ambiguous")
                result.Add(new(owner, "ui.element", "anchor-alpha", Locator: new(.08, .65, .8, .71, "device-viewport"),
                    Space: record.Provenance.Hierarchy.Space));
            return result;
        }
    }
}
