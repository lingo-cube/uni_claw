using UniClaw.Runtime.Capabilities.Perception.Semantic;
using Xunit;

namespace UniClaw.Semantic.Tests.RegressionTests;

public sealed class SemanticRegressionTests
{
    [Fact]
    public void SemanticContract_RemainsContainerIdentityOnly()
    {
        // Phase 1 contract must not expand into ElementMeaning / Relation.
        var values = Enum.GetValues<SemanticEvidenceKind>();
        Assert.Single(values);
        Assert.Equal(SemanticEvidenceKind.ContainerIdentity, values[0]);
    }

    [Fact]
    public async Task NoOpProvider_StillReturnsEmpty()
    {
        var provider = new UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion.NoOpSemanticProvider();
        var obs = new UniClaw.Runtime.Model.Observation(
            System.Collections.Immutable.ImmutableArray<UniClaw.Runtime.Model.ObservedElement>.Empty,
            "Foreground",
            1);
        var evidence = await provider.ResolveAsync(new ObservationContext(obs));
        Assert.Empty(evidence);
    }
}