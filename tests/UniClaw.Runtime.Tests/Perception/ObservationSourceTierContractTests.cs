using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

public sealed class ObservationSourceTierContractTests
{
    [Fact]
    public void Metadata_TracksPrimaryAndAuxiliaryProvenance()
    {
        var primary = new ObservationSourceMetadata(
            ObservationSourceTier.PrimaryVision, true, 1, "frame:1", 1080, 1920, "vision", "primary-vision");
        var auxiliary = new ObservationSourceMetadata(
            ObservationSourceTier.AuxiliaryStructured, false, 1, "frame:1", 1080, 1920, "adb", "auxiliary-structured");

        Assert.True(primary.Available);
        Assert.False(auxiliary.Available);
        Assert.Equal(ObservationSourceTier.AuxiliaryStructured, auxiliary.Tier);
        Assert.Equal("primary-vision", primary.SourceId);
        Assert.NotEqual(primary.SourceId, auxiliary.SourceId);
        Assert.Equal(primary.FrameReference, auxiliary.FrameReference);
        Assert.Equal(primary.ObservationSequence, auxiliary.ObservationSequence);
    }

    [Fact]
    public void AdbParser_EmitsOnlyPrimitiveInteractionEvidence()
    {
        const string xml = "<hierarchy><node class='android.widget.Button' resource-id='pkg:id/action' text='Scenario label' clickable='true' enabled='true' bounds='[0,0][100,100]' /></hierarchy>";
        var result = AdbUiHierarchySource.Parse(xml, 100, 100);

        var evidence = Assert.Single(result);
        Assert.Equal("pkg:id/action", evidence.ResourceId);
        Assert.Equal("Scenario label", evidence.RawText);
        Assert.Null(evidence.ContentDescription);
        Assert.Null(evidence.Checkable);
    }

    [Fact]
    public void AdbParser_PreservesHierarchyAndTreatsClassAsOpaque()
    {
        const string xml = "<hierarchy><node index='0' class='android.widget.LinearLayout' bounds='[0,0][100,100]'><node index='1' class='android.widget.Switch' text='raw' bounds='[1,1][20,20]' /></node></hierarchy>";
        var result = AdbUiHierarchySource.Parse(xml, 100, 100);

        Assert.Equal(2, result.Length);
        var child = result.Single(e => e.RawText == "raw");
        Assert.Equal("0/0/1", child.SourceNodeIdentity);
        Assert.Equal("0/0", child.ParentSourceNodeIdentity);
        Assert.Equal("android.widget.Switch", child.Class);
    }

    [Fact]
    public void AdbParser_DropsOnlyInvalidBounds()
    {
        const string xml = "<hierarchy><node class='opaque' text='bad' bounds='[10,10][2,2]' /><node class='opaque' text='ok' bounds='[0,0][10,10]' /></hierarchy>";
        var result = AdbUiHierarchySource.Parse(xml, 100, 100);

        var evidence = Assert.Single(result);
        Assert.Equal("ok", evidence.RawText);
    }
}
