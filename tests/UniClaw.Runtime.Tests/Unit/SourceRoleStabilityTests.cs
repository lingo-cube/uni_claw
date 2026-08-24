using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>Structured adapters preserve raw facts without scenario roles.</summary>
public sealed class SourceRoleStabilityTests
{
    private static ImmutableArray<StructuredElementEvidence> Parse(string text, string resourceId = "")
    {
        var xml = $"<hierarchy><node index=\"0\" text=\"{text}\" resource-id=\"{resourceId}\" class=\"Widget\" content-desc=\"\" checkable=\"false\" checked=\"false\" clickable=\"true\" enabled=\"true\" focusable=\"true\" bounds=\"[0,0][1080,600]\"/></hierarchy>";
        return AdbUiHierarchySource.Parse(xml, 1080, 1920);
    }

    [Fact]
    public void RawTextIsPreservedWithoutRolePromotion()
    {
        var node = Assert.Single(Parse("Storage", "opaque:id/row"));
        Assert.Equal("Storage", node.RawText);
        Assert.Equal("opaque:id/row", node.ResourceId);
        Assert.Null(node.ParentSourceNodeIdentity);
    }

    [Fact]
    public void MissingRawTextRemainsMissing() => Assert.Null(Assert.Single(Parse("" )).RawText);

    [Fact]
    public void SourceEquivalenceUsesRawIdentity()
    {
        var first = new Observation([], "opaque", 1)
        {
            StructuredElements = ImmutableArray.Create(new StructuredElementEvidence(
                Class: "android.widget.LinearLayout", ResourceId: "opaque:id/row", Clickable: true, Checkable: false,
                Checked: false, Enabled: true, Focusable: true, Bounds: new ElementBounds(0, 0, 1, .2f), RawText: "Storage"))
        };
        var second = new Observation([], "opaque", 2)
        {
            StructuredElements = first.StructuredElements
        };
        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(first, second));
        Assert.True(result.IsResolved);
        Assert.Single(result.UniqueSourceSignatures);
    }

    [Fact]
    public void DuplicateRawIdentityFailsClosed()
    {
        var observation = new Observation([], "opaque", 1)
        {
            StructuredElements = ImmutableArray.Create(
                new StructuredElementEvidence(Class: "Widget", ResourceId: "opaque:id/shared", Clickable: true, Checkable: false, Checked: false, Enabled: true, Focusable: true, Bounds: new ElementBounds(0, 0, 1, .1f), RawText: "Shared"),
                new StructuredElementEvidence(Class: "Widget", ResourceId: "opaque:id/shared", Clickable: true, Checkable: false, Checked: false, Enabled: true, Focusable: true, Bounds: new ElementBounds(0, .1f, 1, .2f), RawText: "Shared"))
        };
        Assert.False(SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(observation)).IsResolved);
    }
}
