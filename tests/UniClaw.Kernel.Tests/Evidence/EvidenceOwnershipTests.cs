using UniClaw.Kernel.Evidence;
using Xunit;

namespace UniClaw.Kernel.Tests.Evidence;

public sealed class EvidenceOwnershipTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static ObservationProposal Source(IReadOnlyList<string> lineage) =>
        new(new ObservationClaim("wifi.checked", "checked"), IngressKind.Observation,
            ObservationContext.External,
            new Provenance("ownership-source", CapturedAt, "wifi", lineage));

    [Fact]
    public void AdmittedProvenanceKeepsItsContentWhenProducerChangesLineage()
    {
        var ledger = new EvidenceLedger();
        var lineage = new List<string> { "capture:original" };
        var accepted = ledger.Admit(Source(lineage));
        Assert.Equal(AdmissionDecision.Accepted, accepted.Admission.Decision);
        var id = accepted.Record!.EvidenceId;

        lineage[0] = "capture:rewritten";
        lineage.Add("unadmitted-transform");

        var canonical = ledger.CanonicalRecords[id];
        Assert.Equal(new[] { "capture:original" }, canonical.Provenance.TransformationLineage);
        Assert.Same(canonical, ledger.Admit(Source(new[] { "capture:original" })).Record);
        if (canonical.Provenance.TransformationLineage is IList<string> writable)
            Assert.Throws<NotSupportedException>(() => writable[0] = "rewritten-through-read-view");
        Assert.Equal(new[] { "capture:original" }, canonical.Provenance.TransformationLineage);
    }

    [Fact]
    public void ProducerCannotRewriteAcceptedAncestryOrBreakLaterDerivedAdmission()
    {
        var ledger = new EvidenceLedger();
        var source = ledger.Admit(Source(new[] { "capture:original" })).Record!;
        var parents = new List<string> { source.EvidenceId };
        var basis = new HashSet<string>(StringComparer.Ordinal) { source.EvidenceId };
        var derivedProposal = Source(new[] { "derive:v1" }) with
        {
            DerivedFromEvidenceIds = parents,
            TransitiveEvidenceBasis = basis,
            FusionRule = "ownership-fusion",
            FusionVersion = "1",
        };
        var accepted = ledger.Admit(derivedProposal);
        Assert.Equal(AdmissionDecision.Accepted, accepted.Admission.Decision);
        var derived = accepted.Record!;

        parents.Clear();
        basis.Clear();

        Assert.Equal(new[] { source.EvidenceId }, derived.DerivedFromEvidenceIds);
        Assert.True(derived.TransitiveEvidenceBasis.SetEquals(new[] { source.EvidenceId }));
        if (derived.DerivedFromEvidenceIds is IList<string> writableParents)
            Assert.Throws<NotSupportedException>(() => writableParents.Clear());
        if (derived.TransitiveEvidenceBasis is ISet<string> writableBasis)
            Assert.Throws<NotSupportedException>(() => writableBasis.Clear());
        var child = Source(new[] { "derive:v2" }) with
        {
            DerivedFromEvidenceIds = new[] { derived.EvidenceId },
            TransitiveEvidenceBasis = new HashSet<string> { source.EvidenceId },
            FusionRule = "ownership-fusion",
            FusionVersion = "2",
        };
        Assert.Equal(AdmissionDecision.Accepted, ledger.Admit(child).Admission.Decision);
        Assert.Same(derived, ledger.Admit(derivedProposal with
        {
            DerivedFromEvidenceIds = new[] { source.EvidenceId },
            TransitiveEvidenceBasis = new HashSet<string> { source.EvidenceId },
        }).Record);
    }
}
