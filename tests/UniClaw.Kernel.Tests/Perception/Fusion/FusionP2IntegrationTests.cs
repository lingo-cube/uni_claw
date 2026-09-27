using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.Fusion;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception.Fusion;

public sealed class FusionP2IntegrationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static ObservationProposal Source(string subject, string value, string producer) =>
        new(new ObservationClaim(subject, value), IngressKind.Observation, ObservationContext.External,
            new Provenance(producer, T0, $"scope:{subject}", new[] { "raw:test" }));

    [Fact]
    public void DerivedFusion_UsesExistingP2AndWorldModelPath()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "wifi.checked" });
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance);

        var hierarchy = ledger.Admit(Source("hierarchy.checked", "checked", "hierarchy")).Record!;
        var visual = ledger.Admit(Source("visual.appearance", "on", "visual")).Record!;
        var captures = new[]
        {
            new FusionSourceEvidence(hierarchy.EvidenceId, FusionSourceKind.Hierarchy,
                "node-1", "checked", "checked", new FusionCapture("h", "run-1", "cycle-1", T0)),
            new FusionSourceEvidence(visual.EvidenceId, FusionSourceKind.Visual,
                "node-1", "checked", "on", new FusionCapture("v", "run-1", "cycle-1", T0.AddMilliseconds(10))),
        };
        var fused = new FusionEngine().Fuse(new FusionRequest(
            "wifi.checked", "checked", captures,
            new AssociationCandidate("h-1", new[] { "v-1" }, Array.Empty<string>(), AssociationDisposition.Unique),
            TimeSpan.FromSeconds(1)));

        Assert.True(fused.IsAdmissible);
        var result = kernel.Process(fused.ToObservationProposal());

        Assert.Equal(AdmissionDecision.Accepted, result.Admission.Decision);
        Assert.NotNull(result.ResultingRevision);
        Assert.Contains(result.Admission.EvidenceId!, result.ResultingRevision!.EvidenceBasis);
        Assert.Contains(hierarchy.EvidenceId, ledger.CanonicalRecords.Keys);
        Assert.Contains(visual.EvidenceId, ledger.CanonicalRecords.Keys);
        var admitted = ledger.CanonicalRecords[result.Admission.EvidenceId!];
        Assert.Equal(fused.DerivedFromEvidenceIds, admitted.DerivedFromEvidenceIds);
        Assert.Equal(fused.TransitiveEvidenceBasis, admitted.TransitiveEvidenceBasis);
    }

    [Fact]
    public void MalformedLineage_IsRejectedBeforeWorldModel()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "wifi.checked" });
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance);
        var source = ledger.Admit(Source("wifi.source", "checked", "hierarchy")).Record!;

        var malformed = new ObservationProposal(
            new ObservationClaim("wifi.checked", "checked"), IngressKind.Observation,
            ObservationContext.External,
            new Provenance("per-011.fusion", T0, "scope:wifi.checked", new[] { "typed-lineage:v1" }))
        {
            DerivedFromEvidenceIds = new[] { source.EvidenceId, "ev-missing-parent" },
            TransitiveEvidenceBasis = new HashSet<string> { source.EvidenceId, "ev-missing-parent" },
            FusionRule = "per-011-authority-v1",
            FusionVersion = "1",
        };

        var result = kernel.Process(malformed);

        Assert.Equal(AdmissionDecision.Rejected, result.Admission.Decision);
        Assert.Equal("MalformedLineage", result.Admission.RejectionReason);
        Assert.Null(result.ResultingRevision);
        Assert.Single(ledger.CanonicalRecords);
        Assert.Null(world.Current);
    }

    [Fact]
    public void BasisMismatch_IsRejectedWithoutTreatingDerivedEvidenceAsIndependent()
    {
        var ledger = new EvidenceLedger();
        var source = ledger.Admit(Source("source", "v", "hierarchy")).Record!;
        var malformed = Source("derived", "v", "per-011.fusion") with
        {
            DerivedFromEvidenceIds = new[] { source.EvidenceId },
            TransitiveEvidenceBasis = new HashSet<string> { "ev-wrong-basis" },
            FusionRule = "per-011-authority-v1",
            FusionVersion = "1",
        };

        var (admission, record) = ledger.Admit(malformed);

        Assert.Equal(AdmissionDecision.Rejected, admission.Decision);
        Assert.Equal("MalformedLineage", admission.RejectionReason);
        Assert.Null(record);
        Assert.Single(ledger.CanonicalRecords);
    }
}
