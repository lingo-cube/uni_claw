using System.Collections.Frozen;
using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Simulation.Tests.ComponentReplay;
using Xunit;

namespace UniClaw.Simulation.Tests;

public sealed class ComponentWorldReplayTests
{
    public static IEnumerable<object[]> RecordedCases() => ComponentReplayCases.Rows("layoutSuppliers");

    private static ObservationProposal IdentityProposal(PageIdentityObservation identity, string captureEvidence) =>
        new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(identity)), IngressKind.Observation,
            ObservationContext.External, new("sim014.controlled-identity", identity.CaptureTime,
                "identity:" + identity.CaptureId + ":" + identity.Signature, ["controlled-identity-substitute", "synthetic-replay-clock"]))
        { DerivedFromEvidenceIds = [captureEvidence], TransitiveEvidenceBasis = new[] { captureEvidence }.ToFrozenSet(),
            FusionRule = "sim014.recorded-layout-identity", FusionVersion = "1" };

    [Theory, MemberData(nameof(RecordedCases)), Trait("Scenario", "SCN-REPLAY-009")]
    public void SameWorldConsumerUsesTwoRecordedLayoutSources_SharedCaptureFaultPausesThenRecovers(string id, string supplier)
    {
        var guard = ComponentReplayCases.Guard();
        var setup = guard.Create(ComponentReplayCases.Root, () =>
            (Input: RecordedComponentInputs.Load(id, supplier), Ledger: new EvidenceLedger(),
                World: new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
                    new ProductAssociationStrategy())));
        var oracle = ComponentReplayCases.Oracle(); var expected = oracle.Cases.Single(c => c.Id == id);
        var delivered = new List<object>();
        EvidenceRecord Accept(ObservationProposal proposal)
        {
            var admitted = setup.Ledger.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admitted.Admission.Decision); Assert.NotNull(admitted.Record);
            var relevance = setup.World.JudgeRelevance(admitted.Record);
            setup.World.Reconcile(admitted.Record, relevance);
            delivered.Add(new { admitted.Record.EvidenceId, proposal.Claim.Subject, relevance.IsRelevant, relevance.Reason });
            return admitted.Record;
        }
        var beforeRecord = Accept(RecordedComponentInputs.CaptureProposal(setup.Input.Before));
        Assert.Equal(oracle.World.NeedsIdentityBefore, setup.World.Current!.ObservationNeed is not null);
        Accept(IdentityProposal(setup.Input.BeforeIdentity, beforeRecord.EvidenceId));
        var parent = setup.World.Current!; var parentId = parent.CurrentPageObservation!.ContainerId;
        Assert.NotNull(parentId); Assert.Equal(oracle.World.ContainersBefore, parent.Containers!.Count);
        var parentFrozen = JsonSerializer.Serialize(parent, RecordedComponentInputs.Json);
        var inputFrozen = JsonSerializer.Serialize(setup.Input.AfterIdentity, RecordedComponentInputs.Json);
        var source = Accept(RecordedComponentInputs.CaptureProposal(setup.Input.After));
        var unresolved = setup.World.Current!; var unresolvedFrozen = JsonSerializer.Serialize(unresolved, RecordedComponentInputs.Json);
        Assert.NotNull(unresolved.ObservationNeed); Assert.Null(unresolved.CurrentPageObservation!.ContainerId);
        Assert.All(unresolved.Occurrences!, occurrence => Assert.Null(occurrence.OwningContainerId));

        var fault = new CaptureMismatchFault();
        var wrong = setup.Input.AfterIdentity with { CaptureId = fault.Deliver(setup.Input.After.CaptureId, "page-identity.captureId") };
        Accept(IdentityProposal(wrong, source.EvidenceId));
        Assert.Equal(oracle.World.NeedsIdentityAfterWrong, setup.World.Current!.ObservationNeed is not null);
        Assert.Null(setup.World.Current.CurrentPageObservation!.ContainerId);
        Assert.All(setup.World.Current.Occurrences!, occurrence => Assert.Null(occurrence.OwningContainerId));

        var matching = setup.Input.AfterIdentity with { CaptureId = fault.Deliver(setup.Input.After.CaptureId, "page-identity.captureId") };
        var identityRecord = Accept(IdentityProposal(matching, source.EvidenceId));
        var confirmed = setup.World.Current!; var childId = confirmed.CurrentPageObservation!.ContainerId;
        Assert.Equal(oracle.World.NeedsIdentityAfterMatching, confirmed.ObservationNeed is not null);
        Assert.NotNull(childId); Assert.NotEqual(parentId, childId); Assert.Equal(oracle.World.ContainersAfter, confirmed.Containers!.Count);
        Assert.All(confirmed.Occurrences!, occurrence => Assert.Equal(childId, occurrence.OwningContainerId));
        foreach (var label in expected.Controls) Assert.Contains(confirmed.Occurrences!, o => o.SemanticDescriptor == label && o.OwningContainerId == childId);
        Assert.Same(confirmed, setup.World.Reconcile(identityRecord, setup.World.JudgeRelevance(identityRecord)));
        Assert.Equal(parentFrozen, JsonSerializer.Serialize(parent, RecordedComponentInputs.Json));
        Assert.Equal(unresolvedFrozen, JsonSerializer.Serialize(unresolved, RecordedComponentInputs.Json));
        Assert.Equal(inputFrozen, JsonSerializer.Serialize(setup.Input.AfterIdentity, RecordedComponentInputs.Json));
        Assert.Equal(oracle.World.FaultReceipts, fault.Receipts.Count);
        Assert.Equal(setup.Input.After.CaptureId, Assert.Single(fault.Receipts).Original);

        // A late, explicitly re-enveloped old recorded layout is historical input;
        // it cannot replace the confirmed current capture or assign old controls.
        var late = setup.Input.Before with { CaptureId = "late:" + setup.Input.Before.CaptureId,
            ObservationCycleId = "late:" + setup.Input.Before.ObservationCycleId,
            Space = UniClaw.Kernel.Perception.CoordinateSpace.DeviceViewport(setup.Input.Before.Space.PixelWidth,
                setup.Input.Before.Space.PixelHeight, setup.Input.Before.Space.Rotation, captureId: "late:" + setup.Input.Before.CaptureId) };
        Accept(RecordedComponentInputs.CaptureProposal(late));
        Assert.Equal(setup.Input.After.CaptureId, setup.World.Current!.CurrentPageObservation!.Capture.CaptureId);
        Assert.Null(setup.World.Current.ObservationNeed); Assert.Equal(childId, setup.World.Current.CurrentPageObservation.ContainerId);
        Assert.All(setup.World.Current.Occurrences!, o => Assert.Equal(childId, o.OwningContainerId));
        ComponentReplayCases.Export("component-world-" + id + "-" + supplier, guard,
            new { supplier, setup.Input.SourceHashes, fault = fault.Receipts, delivered,
                beforeContainer = parentId, afterContainer = childId, currentCapture = setup.World.Current.CurrentPageObservation.Capture.CaptureId,
                containers = confirmed.Containers.Count, identityNeeded = setup.World.Current.ObservationNeed is not null,
                scope = "World + real Ledger; Control/Assurance/Effect and goal evaluation not composed" });
    }

    [Theory, InlineData("typed-import"), InlineData("frozen-capture"), Trait("Scenario", "SCN-REPLAY-009")]
    public void ContradictoryIdentityAfterConfirmationBecomesUnknown_OriginalRevisionRemainsUnchanged(string supplier)
    {
        var guard = ComponentReplayCases.Guard();
        var setup = guard.Create(ComponentReplayCases.Root, () =>
            (Input: RecordedComponentInputs.Load("sims", supplier), Ledger: new EvidenceLedger(),
                World: new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
                    new ProductAssociationStrategy())));
        EvidenceRecord Accept(ObservationProposal p)
        {
            var admission = setup.Ledger.Admit(p); Assert.Equal(AdmissionDecision.Accepted, admission.Admission.Decision);
            setup.World.Reconcile(admission.Record!, setup.World.JudgeRelevance(admission.Record!)); return admission.Record!;
        }
        var source = Accept(RecordedComponentInputs.CaptureProposal(setup.Input.After));
        Accept(IdentityProposal(setup.Input.AfterIdentity, source.EvidenceId));
        var confirmed = setup.World.Current!; Assert.NotNull(confirmed.CurrentPageObservation!.ContainerId);
        var frozen = JsonSerializer.Serialize(confirmed, RecordedComponentInputs.Json);
        Accept(IdentityProposal(setup.Input.AfterIdentity with { Signature = "contradictory:other-page" }, source.EvidenceId));
        Assert.NotNull(setup.World.Current!.ObservationNeed); Assert.Null(setup.World.Current.CurrentPageObservation!.ContainerId);
        Assert.Single(setup.World.Current.Conflicts); Assert.All(setup.World.Current.Occurrences!, o => Assert.Null(o.OwningContainerId));
        Assert.Equal(frozen, JsonSerializer.Serialize(confirmed, RecordedComponentInputs.Json));
        ComponentReplayCases.Export("component-world-conflict-" + supplier, guard,
            new { conflictCount = setup.World.Current.Conflicts.Count, identityNeeded = true, ownerless = true });
    }
}
