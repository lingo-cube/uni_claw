using System;
using System.Collections.Immutable;
using System.Linq;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// Tier B S1 normalization remediation — targeted equivalence evidence
/// (remediation §6 A–D), using the SAME admitted-frame stamping recipe the
/// graduated SourceProvenanceContractTests prove with (primary vision source +
/// admitted NavigationCandidate evidence per row). Vision-only tier — exactly
/// the realigned capstone pipeline semantics.
/// </summary>
public sealed class ViewportNormalizationEquivalenceTests
{
    private static Observation AdmittedFrame(long seq, params string[] rowTitles)
    {
        var elements = rowTitles
            .Select((t, i) => new ObservedElement(t, null, i,
                new ElementBounds(0, i * 0.1f, 1, (i + 1) * 0.1f), "menuItem"))
            .ToImmutableArray();
        var stamped = new Observation(elements, "com.uniclaw.fixture", seq) with
        {
            Sources = ImmutableArray.Create(new ObservationSourceMetadata(
                ObservationSourceTier.PrimaryVision, true, seq,
                $"frame-{seq}", 1080, 1920, "vision", "vision")),
        };
        var context = SemanticObservationFactProjector.Project(stamped);
        var manifest = new SemanticCapabilityManifest("fixture", "1", ["navigation"]);
        var evidence = context.Facts
            .Where(f => f.SourceTier == SemanticSourceTier.Primary
                && f.Kind == SemanticObservationFactKind.Text
                && !string.IsNullOrWhiteSpace(f.RawText))
            .Select(f => new SemanticEvidenceV2Envelope(
                $"e:{f.OccurrenceId}",
                new ElementAffordanceCandidateEvidence(
                    f.OccurrenceId,
                    ElementAffordanceKind.NavigationCandidate,
                    new SemanticSymbolReference(manifest.ManifestId, manifest.Version, "navigation"),
                    context.Observation,
                    new SemanticScopeReference(f.OccurrenceId),
                    new SemanticProvenance(f.SourceId, SemanticSourceTier.Primary, f.ProvenanceId, DateTimeOffset.UnixEpoch, f.FrameId),
                    .9, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue)))
            .ToImmutableArray();
        return stamped with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(evidence) };
    }

    // ── A. same source across two frames → equivalent via overlap ─────────

    [Fact]
    public void A_SameSourceTwoFrames_OverlapEquivalence_Resolves()
    {
        var f1 = AdmittedFrame(1, "Child 01", "Child 02", "Child 03");
        var f2 = AdmittedFrame(2, "Child 02", "Child 03", "Child 04");
        var result = SourceEquivalenceNormalizer.Normalize([f1, f2]);
        Assert.True(result.IsResolved);
        Assert.False(result.EquivalenceEvidence.IsDefaultOrEmpty);
    }

    // ── B. distinct near-identical sources are NOT merged ─────────────────

    [Fact]
    public void B_DistinctSimilarTexts_NotMerged_EachKeepsOwnIdentity()
    {
        var f1 = AdmittedFrame(1, "Child 01", "Child 011");
        var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(f1);
        Assert.Equal(2, occurrences.Length);
        Assert.NotEqual(occurrences[0].StructuredSignature, occurrences[1].StructuredSignature);
        var f2 = AdmittedFrame(2, "Child 01", "Child 011");
        var result = SourceEquivalenceNormalizer.Normalize([f1, f2]);
        Assert.True(result.IsResolved);
        Assert.Equal(2, result.UniqueSourceSignatures.Length);
    }

    // ── C. OCR-variant source is a NEW distinct source, never merged ──────

    [Fact]
    public void C_OcrVariantAcrossFrames_DistinctSource_NoFalseMerge()
    {
        var f1 = AdmittedFrame(1, "Child 02", "Child 03");
        var f2 = AdmittedFrame(2, "Child 02", "Child 03", "Child 0AF");
        var result = SourceEquivalenceNormalizer.Normalize([f1, f2]);
        Assert.True(result.IsResolved);
        Assert.Contains(result.UniqueSourceSignatures, s => s.Contains("Child 0AF", StringComparison.Ordinal));
        Assert.Equal(3, result.UniqueSourceSignatures.Length);
    }

    // ── D. full root viewport union → resolved with 8 logical sources ─────

    [Fact]
    public void D_RootViewportUnion_ScrolledFrames_Resolved_AllEightSources()
    {
        var frames = new[]
        {
            AdmittedFrame(1, "Child 01", "Child 02", "Child 03"),
            AdmittedFrame(2, "Child 02", "Child 03", "Child 04"),
            AdmittedFrame(3, "Child 04", "Child 05", "Child 06"),
            AdmittedFrame(4, "Child 05", "Child 06", "Child 07", "Child 08"),
        };
        var result = SourceEquivalenceNormalizer.Normalize([.. frames]);
        Assert.True(result.IsResolved);
        Assert.Equal(8, result.UniqueSourceSignatures.Length);
    }

    // ── Duplicate-in-one-frame stays fail-closed (no weakening) ───────────

    [Fact]
    public void DuplicateInSingleFrame_RemainsFailClosed_NotDedupedByString()
    {
        var dup = AdmittedFrame(1, "Child 01", "Child 01");
        var result = SourceEquivalenceNormalizer.Normalize([dup]);
        Assert.False(result.IsResolved);
    }
}
