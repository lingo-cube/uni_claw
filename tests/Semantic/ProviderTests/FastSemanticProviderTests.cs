using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Semantic.Tests.ProviderTests;

public sealed class FastSemanticProviderTests
{
    private static readonly SemanticPattern DeveloperOptionsPattern = new(
        "DeveloperOptions",
        "pattern:dev",
        ImmutableArray.Create("Enable demo mode", "Show demo mode"),
        ImmutableArray.Create("switch"),
        ImmutableArray.Create("type:switch", "switch:True"));

    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    [Fact]
    public async Task Provider_ReturnsContainerIdentityEvidence()
    {
        var provider = new FastSemanticContainerIdentityProvider(
            new InMemoryVectorSemanticIndex(ImmutableArray.Create(DeveloperOptionsPattern)));
        var obs = Obs(1,
            new ObservedElement("Enable demo mode", null, 0, null, "menu_item"),
            new ObservedElement("Show demo mode", null, 1, null, "menu_item"),
            new ObservedElement("Automatic system updates", true, 2, null, "switch"));

        var evidence = await provider.ResolveAsync(new ObservationContext(obs, "DeveloperOptions"));

        var single = Assert.Single(evidence);
        Assert.Equal(SemanticEvidenceKind.ContainerIdentity, single.Kind);
        Assert.Equal("DeveloperOptions", single.Candidate);
    }
}