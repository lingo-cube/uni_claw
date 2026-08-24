using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
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
}
