using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

public sealed class SourceGroundingNormalizerTests
{
    private static Observation Observation(bool primary, bool auxiliary, bool duplicateAux = false)
    {
        var sources = new List<ObservationSourceMetadata>();
        if (primary) sources.Add(new(ObservationSourceTier.PrimaryVision, true, 1, "f1", 100, 100, "vision", "vision"));
        if (auxiliary) sources.Add(new(ObservationSourceTier.AuxiliaryStructured, true, 1, "f1", 100, 100, "adb", "adb"));
        var elements = ImmutableArray.Create(new ObservedElement("Child", null, 0, new ElementBounds(.1f, .1f, .4f, .4f)));
        var structured = auxiliary
            ? ImmutableArray.Create(
                new StructuredElementEvidence(Class: "Button", ResourceId: null, Clickable: true, Checkable: false,
                    Checked: null, Enabled: true, Focusable: true, Bounds: new ElementBounds(.1f, .1f, .4f, .4f), RawText: "Child"),
                duplicateAux
                    ? new StructuredElementEvidence(Class: "Button", ResourceId: null, Clickable: true, Checkable: false,
                        Checked: null, Enabled: true, Focusable: true, Bounds: new ElementBounds(.1f, .1f, .4f, .4f), RawText: "Child")
                    : null).Where(x => x is not null).Cast<StructuredElementEvidence>().ToImmutableArray()
            : ImmutableArray<StructuredElementEvidence>.Empty;
        return new Observation(elements, null, 1) { Sources = sources.ToImmutableArray(), StructuredElements = structured };
    }

    [Fact] public void VisionOnly_IsPrimaryEligible() => Assert.True(SourceGroundingNormalizer.Normalize(Observation(true, false)).Single().EligibleForAuthorization);

    [Fact] public void UniqueAux_IsCorroboration() => Assert.Single(SourceGroundingNormalizer.Normalize(Observation(true, true)).Single().AuxiliarySupports);

    [Fact] public void AmbiguousAux_RemainsSeparateAndIneligible()
    {
        var all = SourceGroundingNormalizer.Normalize(Observation(true, true, true));
        Assert.Contains(all, x => x.Reference.SourceKind == ObservationSourceKind.AuxiliaryStructured && !x.EligibleForAuthorization);
    }

    [Fact] public void AuxOnly_IsNeverEligible()
    {
        var all = SourceGroundingNormalizer.Normalize(Observation(false, true));
        Assert.NotEmpty(all); Assert.All(all, x => Assert.False(x.EligibleForAuthorization));
    }

    [Fact] public void MultiplePrimary_FailsClosed()
    {
        var o = Observation(true, false) with { Sources = [
            new(ObservationSourceTier.PrimaryVision, true, 1, "f1", 100, 100, "v1", "v1"),
            new(ObservationSourceTier.PrimaryVision, true, 1, "f1", 100, 100, "v2", "v2")] };
        Assert.Empty(SourceGroundingNormalizer.Normalize(o));
    }
}
