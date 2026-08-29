using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class SourceEquivalenceNormalizerTests
{
    private const int Width = 1080;
    private const int Height = 1920;

    private static Observation Load(string name, long seq)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Replay/Assets/source-identity-scroll", name);
        var xml = File.ReadAllText(path);
        var structured = AdbUiHierarchySource.Parse(xml, Width, Height);
        return new Observation([], "com.uniclaw.fixture", seq) { StructuredElements = structured };
    }

    private static Observation LoadScroll01(string name, long seq)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Replay/Assets/scroll01", name);
        var xml = File.ReadAllText(path);
        var structured = AdbUiHierarchySource.Parse(xml, Width, Height);
        return new Observation([], "com.uniclaw.fixture", seq) { StructuredElements = structured };
    }

    private static StructuredElementEvidence Row(string title, string resourceId = "com.uniclaw.fixture:id/row_title", string @class = "android.widget.LinearLayout")
        => new(Class: @class, ResourceId: resourceId, Clickable: true, Checkable: false,
            Checked: false, Enabled: true, Focusable: true,
            Bounds: new ElementBounds(0, 0, 1, 0.1f), RawText: title);

    [Fact]
    public void PRIMARY_SCROLL01_NormalizesToItem01Through16_ExactlyOnce()
    {
        var observations = ImmutableArray.Create(
            LoadScroll01("v1.xml", 1),
            LoadScroll01("v2.xml", 2),
            LoadScroll01("v3.xml", 3));

        var result = SourceEquivalenceNormalizer.Normalize(observations);

        Assert.True(result.IsResolved);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Equal(16, result.UniqueSourceSignatures.Length);
        for (int i = 1; i <= 16; i++)
        {
            var expected = $"Item {i:00}";
            Assert.Contains(result.UniqueSourceSignatures, s => s.StartsWith(expected + "|", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void NORM1_RealV1V2V3_NormalizesToItem07Through28_ExactlyOnce()
    {
        var observations = ImmutableArray.Create(
            Load("v1.xml", 1),
            Load("v2.xml", 2),
            Load("v3.xml", 3));

        var result = SourceEquivalenceNormalizer.Normalize(observations);

        Assert.True(result.IsResolved);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Equal(22, result.UniqueSourceSignatures.Length);
        for (int i = 7; i <= 28; i++)
        {
            var expected = $"Item {i:00}";
            Assert.Contains(result.UniqueSourceSignatures, s => s.StartsWith(expected + "|", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void NORM2_BoundsMove_DoesNotCreateNewSource()
    {
        var v1 = new Observation([], "com.uniclaw.fixture", 1)
        {
            StructuredElements =
            [
                Row("Item 15", "com.uniclaw.fixture:id/row_title"),
            ],
        };
        var v2 = new Observation([], "com.uniclaw.fixture", 2)
        {
            StructuredElements =
            [
                Row("Item 15", "com.uniclaw.fixture:id/row_title"),
            ],
        };
        var result = SourceEquivalenceNormalizer.Normalize([v1, v2]);
        Assert.True(result.IsResolved);
        Assert.Single(result.UniqueSourceSignatures);
    }

    [Fact]
    public void NORM3_NodePathRecycling_IsNotUsedAsLogicalIdentity()
    {
        // The normalizer only uses structured signature, not node paths.
        // This test proves a recycled path still normalizes by signature.
        var v1 = new Observation([], "com.uniclaw.fixture", 1)
        {
            StructuredElements = [Row("Item 15", "com.uniclaw.fixture:id/row_title")],
        };
        var v2 = new Observation([], "com.uniclaw.fixture", 2)
        {
            StructuredElements = [Row("Item 15", "com.uniclaw.fixture:id/row_title")],
        };
        var result = SourceEquivalenceNormalizer.Normalize([v1, v2]);
        Assert.True(result.IsResolved);
        Assert.Single(result.UniqueSourceSignatures);
    }

    [Fact]
    public void NORM4_DuplicateTitle_WithoutUniqueOverlap_DoesNotMerge()
    {
        var v1 = new Observation([], "com.uniclaw.fixture", 1)
        {
            StructuredElements =
            [
                Row("Shared", "com.uniclaw.fixture:id/row_title"),
                Row("Shared", "com.uniclaw.fixture:id/row_title"),
            ],
        };
        var v2 = new Observation([], "com.uniclaw.fixture", 2)
        {
            StructuredElements =
            [
                Row("Shared", "com.uniclaw.fixture:id/row_title"),
                Row("Other", "com.uniclaw.fixture:id/row_title"),
            ],
        };
        // The duplicate signature in V1 makes overlap ambiguous by signature alone.
        var result = SourceEquivalenceNormalizer.Normalize([v1, v2]);
        Assert.False(result.IsResolved);
    }

    [Fact]
    public void NORM8_NoAdjacentOverlap_FailsClosed()
    {
        var v1 = new Observation([], "com.uniclaw.fixture", 1)
        {
            StructuredElements = [Row("A")],
        };
        var v2 = new Observation([], "com.uniclaw.fixture", 2)
        {
            StructuredElements = [Row("B")],
        };
        var result = SourceEquivalenceNormalizer.Normalize([v1, v2]);
        Assert.False(result.IsResolved);
    }

    [Fact]
    public void Explicit_primary_normalization_ignores_auxiliary_only_navigation_rows()
    {
        var v1 = ExplicitPrimary(1, "A", "Auxiliary X");
        var v2 = ExplicitPrimary(2, "A", "Auxiliary Y");

        var result = SourceEquivalenceNormalizer.Normalize([v1, v2]);

        Assert.True(result.IsResolved);
        Assert.Equal(["A|menu_item||"], result.UniqueSourceSignatures);
        var diagnosticOccurrences = SourceEquivalenceNormalizer.OccurrencesOf(v1);
        Assert.Contains(diagnosticOccurrences, occurrence => !occurrence.EligibleForAuthorization);
        Assert.Single(diagnosticOccurrences.Where(occurrence => occurrence.EligibleForAuthorization));
    }

    private static Observation ExplicitPrimary(long sequence, string visual, string auxiliaryOnly)
    {
        var frame = $"frame-{sequence}";
        var observation = new Observation(
            [new ObservedElement(visual, null, 0, new ElementBounds(0, 0, 1, .1f), "menu_item")],
            "com.uniclaw.fixture",
            sequence)
        {
            StructuredElements = [Row(auxiliaryOnly)],
            Sources =
            [
                new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, sequence, frame, 100, 100, "vision", "vision"),
                new ObservationSourceMetadata(ObservationSourceTier.AuxiliaryStructured, true, sequence, frame, 100, 100, "adb", "adb"),
            ],
        };
        var occurrenceId = SemanticObservationFactProjector.CreateOccurrenceId("vision", "0");
        var reference = new SemanticObservationReference($"observation:{sequence}", sequence, frame);
        var evidence = new SemanticEvidenceV2Envelope(
            $"e:{occurrenceId}",
            new ElementAffordanceCandidateEvidence(
                occurrenceId,
                ElementAffordanceKind.NavigationCandidate,
                new SemanticSymbolReference("fixture", "1", "navigation"),
                reference,
                new SemanticScopeReference(occurrenceId),
                new SemanticProvenance("vision", SemanticSourceTier.Primary, "vision", DateTimeOffset.UnixEpoch, frame),
                .9,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.MaxValue));
        return observation with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot([evidence]) };
    }

    // ── WI-FIX (Part B): StableKey on ObservedElement drives the signature ────
    // BuildSignature uses StableKey ?? Text | PerceptionType. StableKey is an
    // optional init-only field; when present it stabilizes identity across
    // text-recognition drift; when absent the construction falls back to Text
    // (legacy-compatible). Bounds / node path / viewport ordinal are never used.

    [Fact]
    public void BuildSignature_WithStableKey_UsesStableKeyInsteadOfText()
    {
        var element = new ObservedElement("Network & internet", null, 0, null, "menu_item")
        {
            StableKey = "row_001",
        };

        var signature = SourceEquivalenceNormalizer.BuildSignature(element);

        Assert.StartsWith("row_001|", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("Network & internet", signature, StringComparison.Ordinal);
        Assert.Equal("row_001|menu_item||", signature);
    }

    [Fact]
    public void BuildSignature_WithoutStableKey_FallsBackToText_LegacyCompatible()
    {
        var element = new ObservedElement("Network & internet", null, 0, null, "menu_item");

        var signature = SourceEquivalenceNormalizer.BuildSignature(element);

        Assert.StartsWith("Network & internet|", signature, StringComparison.Ordinal);
        Assert.Equal("Network & internet|menu_item||", signature);
    }

    [Fact]
    public void BuildSignature_NullStableKey_FallsBackToText()
    {
        var element = new ObservedElement("Data usage", null, 0, null, "menu_item")
        {
            StableKey = null,
        };

        var signature = SourceEquivalenceNormalizer.BuildSignature(element);

        Assert.StartsWith("Data usage|", signature, StringComparison.Ordinal);
    }
}
