using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class PageObservationRecoveryTests
{
    private static PageCaptureObservation Capture(string id = "capture-a") =>
        new(id, "cycle-a", "session-a", new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero),
            CoordinateSpace.DeviceViewport(100, 100, captureId: id),
            new[] { new SemanticLayoutItem("title", "text", "Settings", new UiBounds(0, 0, 80, 20)) })
        { DeviceId = "fixture-device" };

    [Fact]
    public void CodecRoundTripPreservesCaptureCorrelationAndItems()
    {
        var capture = Capture();
        var json = PageObservationCodec.Encode(capture);
        Assert.True(PageObservationCodec.TryReadCapture(json, out var decoded));
        Assert.NotNull(decoded);
        Assert.Equal(capture.CaptureId, decoded!.CaptureId);
        Assert.Equal(capture.ObservationCycleId, decoded.ObservationCycleId);
        Assert.Equal(capture.Space, decoded.Space);
        Assert.Equal(capture.Items, decoded.Items);
    }

    [Fact]
    public void CodecRejectsUnknownMembersAndDuplicateItems()
    {
        var json = PageObservationCodec.Encode(Capture());
        Assert.False(PageObservationCodec.TryReadCapture(json[..^1] + ",\"unexpected\":true}", out _));
        var duplicate = Capture() with { Items = new[] { Capture().Items[0], Capture().Items[0] } };
        Assert.Throws<ArgumentException>(() => PageObservationCodec.Encode(duplicate));
    }

    [Fact]
    public void CaptureNeedBlocksUiUntilCorrelatedIdentityThenRebindsOnlyThatCapture()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen", PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject, "ordinary" }, new ProductAssociationStrategy());
        var at = new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
        EvidenceRecord Admit(ObservationProposal p)
        {
            var result = ledger.Admit(p); Assert.True(result.Admission.Decision == AdmissionDecision.Accepted, result.Admission.ToString());
            var record = result.Record!; world.Reconcile(record, world.JudgeRelevance(record)); return record;
        }
        var pageA = Admit(new(new("ui.screen", "A"), IngressKind.Observation, ObservationContext.External,
            new("test", at, "screen-a", ["screen-a"])));
        var capture = Capture("capture-b") with { CaptureTime = at.AddSeconds(1),
            Items = new[] { Capture().Items[0], new SemanticLayoutItem("row", "ui.element", "Mobile network", new UiBounds(0, 30, 80, 60)) } };
        var captureRecord = Admit(new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, ObservationContext.External,
            new("test", at.AddSeconds(1), "capture-b", ["capture-b" ])));
        Assert.NotNull(world.Current!.ObservationNeed);
        Assert.All(world.Current.Occurrences!, occurrence => Assert.Null(occurrence.OwningContainerId));
        Assert.Equal(CurrentCandidateSetResultKind.ScopeProjectionUnavailable,
            world.ResolveCurrent(new TargetDescriptor("text", "Settings")).Result);

        var identity = new PageIdentityObservation("capture-b", "cycle-a", "session-a", capture.CaptureTime, "B", "Settings");
        foreach (var variant in new[] { identity with { CaptureId = "wrong" }, identity with { ObservationCycleId = "wrong" },
                     identity with { SessionCorrelation = "wrong" }, identity with { CaptureTime = at.AddSeconds(3) },
                     identity with { ObservedTitle = "Other" } })
        {
            var invalidResult = ledger.Admit(IdentityProposal(variant, at.AddSeconds(1), captureRecord.EvidenceId));
            if (invalidResult.Admission.Decision == AdmissionDecision.Accepted)
                world.Reconcile(invalidResult.Record!, world.JudgeRelevance(invalidResult.Record!));
            Assert.Null(world.Current!.CurrentPageObservation!.ContainerId);
            Assert.Equal("A", world.Current.WorldState[WorldModel.SignatureSubjectPrefix +
                world.Current.WorldState[WorldModel.CurrentContainerSubject].Value].Value);
        }
        var noLineage = Admit(new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(identity)),
            IngressKind.Observation, ObservationContext.External,
            new("test", at.AddSeconds(1), "capture-b", ["capture-b"])));
        Assert.Null(world.Current!.CurrentPageObservation!.ContainerId);

        var valid = Admit(IdentityProposal(identity, capture.CaptureTime, captureRecord.EvidenceId));
        var confirmed = world.Current!;
        Assert.NotNull(confirmed.CurrentPageObservation!.ContainerId);
        Assert.Equal("B", confirmed.WorldState[WorldModel.SignatureSubjectPrefix + confirmed.CurrentPageObservation.ContainerId].Value);
        Assert.Null(confirmed.ObservationNeed);
        Assert.All(confirmed.Occurrences!, occurrence => Assert.Equal(confirmed.CurrentPageObservation.ContainerId, occurrence.OwningContainerId));
        Assert.Contains(world.RevisionHistory, revision => revision.RevisionId != confirmed.RevisionId
            && revision.CurrentPageObservation?.ContainerId is null);
        Assert.True(world.Current!.WorldState.ContainsKey(WorldModel.CurrentContainerSubject));
        var duplicate = world.Reconcile(valid, world.JudgeRelevance(valid));
        Assert.Same(confirmed, duplicate);
        Admit(new(new("ordinary", "content"), IngressKind.Observation, ObservationContext.External,
            new("test", at.AddSeconds(2), "ordinary", ["fixture"])));
        Assert.Null(world.Current!.ObservationNeed);
        var lateCapture = capture with { CaptureId = "late", CaptureTime = at,
            Space = CoordinateSpace.DeviceViewport(100, 100, captureId: "late") };
        Admit(new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(lateCapture)), IngressKind.Observation,
            ObservationContext.External, new("test", at, "late", ["fixture"])));
        Assert.Equal("capture-b", world.Current!.CurrentPageObservation!.Capture.CaptureId);
        Assert.Null(world.Current.ObservationNeed);
        _ = pageA; _ = noLineage;
    }

    [Fact]
    public void ContradictionInConfirmedCapturePausesUntilNewObservation_WithoutChangingOldRevision()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject }, new ProductAssociationStrategy());
        var capture = Capture() with { Items = new[] { Capture().Items[0],
            new SemanticLayoutItem("row", "ui.element", "Details", new UiBounds(0, 30, 80, 60)) } };
        EvidenceRecord Process(ObservationProposal proposal)
        {
            var admitted = ledger.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admitted.Admission.Decision);
            world.Reconcile(admitted.Record!, world.JudgeRelevance(admitted.Record!));
            return admitted.Record!;
        }
        var source = Process(new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, ObservationContext.External,
            new("test", capture.CaptureTime, "capture", ["fixture"])));
        var identity = new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId,
            capture.SessionCorrelation, capture.CaptureTime, "A", "Settings");
        Process(IdentityProposal(identity, capture.CaptureTime, source.EvidenceId));
        var confirmed = world.Current!;
        Process(IdentityProposal(identity with { Signature = "B" }, capture.CaptureTime, source.EvidenceId));
        Assert.NotNull(world.Current!.ObservationNeed);
        Assert.Null(world.Current.CurrentPageObservation!.ContainerId);
        Assert.NotNull(confirmed.CurrentPageObservation!.ContainerId);
        Assert.Single(world.Current.Conflicts);
        Assert.All(world.Current.Occurrences!, o => Assert.Null(o.OwningContainerId));
        var next = capture with { CaptureId = "next", ObservationCycleId = "next-cycle", CaptureTime = capture.CaptureTime.AddSeconds(1),
            Space = CoordinateSpace.DeviceViewport(100, 100, captureId: "next") };
        var nextSource = Process(new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(next)),
            IngressKind.Observation, ObservationContext.External, new("test", next.CaptureTime, "next", ["fixture"])));
        Process(IdentityProposal(identity with { CaptureId = next.CaptureId, ObservationCycleId = next.ObservationCycleId,
            CaptureTime = next.CaptureTime }, next.CaptureTime, nextSource.EvidenceId));
        Assert.Null(world.Current!.ObservationNeed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InsufficientLayoutOrAmbiguousAssociationKeepsIdentityUnknown(bool ambiguous)
    {
        var ledger = new EvidenceLedger();
        var capture = Capture();
        if (ambiguous) capture = capture with { Items = new[] { capture.Items[0],
            new SemanticLayoutItem("row", "ui.element", "Details", new UiBounds(0, 30, 80, 60)) } };
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject }, ambiguous ? new Ambiguous() : new ProductAssociationStrategy());
        var source = ledger.Admit(new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, ObservationContext.External,
            new("test", capture.CaptureTime, "capture", ["fixture"]))).Record!;
        world.Reconcile(source, world.JudgeRelevance(source));
        var identity = new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation,
            capture.CaptureTime, "A", "Settings");
        var proposed = ledger.Admit(IdentityProposal(identity, capture.CaptureTime, source.EvidenceId)).Record!;
        world.Reconcile(proposed, world.JudgeRelevance(proposed));
        Assert.NotNull(world.Current!.ObservationNeed);
        Assert.Null(world.Current.CurrentPageObservation!.ContainerId);
        Assert.Empty(world.Current.Containers!);
        Assert.All(world.Current.Occurrences!, o => Assert.Null(o.OwningContainerId));
    }

    private sealed class Ambiguous : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => new(AssociationDispositionKind.Ambiguous,
            null, [], [], "fixture ambiguous identity");
    }

    private static ObservationProposal IdentityProposal(PageIdentityObservation identity, DateTimeOffset time, string evidenceId) =>
        new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(identity)),
            IngressKind.Observation, ObservationContext.External,
            new("test", time, "capture-b:" + identity.CaptureId + ":" + identity.ObservationCycleId + ":" + identity.SessionCorrelation + ":" + identity.ObservedTitle, ["capture:capture-b"]))
        { DerivedFromEvidenceIds = new[] { evidenceId }, TransitiveEvidenceBasis = new HashSet<string> { evidenceId }, FusionRule = "page-identity", FusionVersion = "1" };
}
