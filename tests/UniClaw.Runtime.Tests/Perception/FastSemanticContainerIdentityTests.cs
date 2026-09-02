using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;
using UniClaw.Runtime.Model;
using Xunit;
using SemanticEvidence = UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidence;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// T1–T12 for Fast Semantic Container Identity Recovery.
/// Proves the Fast provider/vector-index pipeline produces ContainerIdentity
/// evidence only, does not create Fact/Belief, and preserves fail-closed behavior
/// on vector miss.
/// </summary>
public sealed class FastSemanticContainerIdentityTests
{
    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "Foreground", seq);

    private static ObservedElement El(string text, string? type = "menu_item") =>
        new(text, null, 0, null, type);

    private static readonly SemanticPattern DeveloperOptionsPattern = new(
        "DeveloperOptions",
        "pattern:developer-options",
        ImmutableArray.Create("Enable demo mode", "Show demo mode"),
        ImmutableArray.Create("menu_item"),
        ImmutableArray.Create("type:menu_item"));

    private static readonly SemanticPattern WifiSettingsPattern = new(
        "WifiSettings",
        "pattern:wifi-settings",
        ImmutableArray.Create("Wi-Fi", "Network & internet"),
        ImmutableArray.Create("switch"),
        ImmutableArray.Create("type:switch"));

    private static FastSemanticContainerIdentityProvider Provider(
        params SemanticPattern[] patterns) =>
        new(ContainerIdentityPrototypeStore.FromSemanticPatterns(patterns.ToImmutableArray()));

    // T1: Vector hit returns ContainerIdentity SemanticEvidence
    [Fact]
    public async Task T1_VectorHit_ReturnsContainerIdentityEvidence()
    {
        var provider = Provider(DeveloperOptionsPattern);
        var obs = Obs(5, El("Enable demo mode"));
        var result = await provider.ResolveAsync(new ObservationContext(obs, "DeveloperOptions"));

        var evidence = Assert.Single(result);
        Assert.Equal(SemanticEvidenceKind.ContainerIdentity, evidence.Kind);
        Assert.Equal("DeveloperOptions", evidence.Candidate);
        Assert.Equal(5, evidence.ObservationSequence);
    }

    // T2: Vector miss returns empty evidence
    [Fact]
    public async Task T2_VectorMiss_ReturnsEmptyEvidence()
    {
        var provider = Provider(WifiSettingsPattern);
        var obs = Obs(5, El("Enable demo mode"));
        var result = await provider.ResolveAsync(new ObservationContext(obs));

        Assert.Empty(result);
    }

    // T3: Fast semantic latency bounded
    [Fact]
    public async Task T3_FastSemantic_LatencyBounded()
    {
        var provider = Provider(DeveloperOptionsPattern);
        var obs = Obs(5, El("Enable demo mode"));
        var context = new ObservationContext(obs);

        var sw = Stopwatch.StartNew();
        await provider.ResolveAsync(context);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), "Fast semantic should be bounded.");
    }

    // T4: Semantic candidate does not become Fact
    [Fact]
    public async Task T4_SemanticCandidate_DoesNotBecomeFact()
    {
        var provider = Provider(DeveloperOptionsPattern);
        var obs = Obs(5, El("Enable demo mode"));
        var evidence = Assert.Single(await provider.ResolveAsync(new ObservationContext(obs)));

        foreach (var prop in typeof(SemanticEvidence).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotMatch("(Fact|Belief|CurrentContainer|Action)", prop.Name);
        }
        Assert.Equal("DeveloperOptions", evidence.Candidate);
    }

    // T5: Old container identity requires Runtime validation
    [Fact]
    public async Task T5_OldIdentity_RequiresRuntimeValidation()
    {
        var provider = Provider(DeveloperOptionsPattern);
        var obs = Obs(5, El("Enable demo mode"));
        var evidence = await provider.ResolveAsync(new ObservationContext(obs, "DeveloperOptions"));

        // Provider only produces evidence; it does not set CurrentContainer.
        var fusion = new SemanticEvidenceFusion();
        var result = fusion.Fuse(new SemanticEvidenceFusionInput(obs, semanticEvidence: evidence.ToImmutableArray()));

        var accepted = Assert.Single(result.AcceptedEvidence);
        Assert.Equal("DeveloperOptions", accepted.Candidate);
        // The result is still just evidence; no Fact/CurrentContainer is emitted.
        foreach (var prop in typeof(ValidatedSemanticEvidenceResult).GetProperties())
        {
            Assert.DoesNotMatch("(CurrentContainer|Fact|Belief)", prop.Name);
        }
    }

    // T6: No Vector provider keeps Runtime unchanged
    [Fact]
    public async Task T6_NoVectorProvider_RuntimeUnchanged()
    {
        var pipeline = new SemanticEvidenceFusionPipeline();
        var obs = Obs(5, El("Enable demo mode"));
        var result = await pipeline.ResolveAndFuseAsync(new SemanticEvidenceFusionInput(obs));

        Assert.Empty(result.AcceptedEvidence);
        Assert.Empty(result.RejectedEvidence);
        Assert.Empty(result.ConfidenceWeights);
    }

    // T7: Agent unchanged — provider is only an ISemanticProvider, not an Agent/Planner
    [Fact]
    public void T7_Provider_IsNotAgentOrPlanner()
    {
        var type = typeof(FastSemanticContainerIdentityProvider);
        Assert.True(typeof(ISemanticProvider).IsAssignableFrom(type));
        Assert.DoesNotMatch("Agent|Planner", type.FullName ?? string.Empty);
        Assert.NotEqual("Agent", type.Namespace);
    }

    // T8: Resolver unchanged — no concrete ContainerIdentity resolver replacement exists
    [Fact]
    public void T8_Resolver_Unchanged()
    {
        var iface = typeof(IContainerIdentityEvidenceFusion);
        Assert.True(iface.IsInterface);

        var concrete = iface.Assembly.GetTypes()
            .Where(t => !t.IsInterface && !t.IsAbstract && iface.IsAssignableFrom(t))
            .ToList();
        Assert.Empty(concrete);
    }

    // T9: Scrolled container receives semantic candidate
    [Fact]
    public async Task T9_ScrolledContainer_ReceivesSemanticCandidate()
    {
        // Title is offscreen; only bottom-of-page fragments remain visible.
        var provider = Provider(DeveloperOptionsPattern);
        var obs = Obs(5, El("Show demo mode"));
        var evidence = await provider.ResolveAsync(new ObservationContext(obs, "DeveloperOptions"));

        var single = Assert.Single(evidence);
        Assert.Equal("DeveloperOptions", single.Candidate);
        Assert.Equal(SemanticEvidenceKind.ContainerIdentity, single.Kind);
    }

    // T10: Semantic confidence does not equal Truth
    [Fact]
    public async Task T10_Confidence_DoesNotEqualTruth()
    {
        var provider = Provider(DeveloperOptionsPattern);
        var obs = Obs(5, El("Enable demo mode"));
        var evidence = Assert.Single(await provider.ResolveAsync(new ObservationContext(obs)));

        var fusion = new SemanticEvidenceFusion();
        var result = fusion.Fuse(new SemanticEvidenceFusionInput(obs, semanticEvidence: ImmutableArray.Create(evidence)));

        var weight = Assert.Single(result.ConfidenceWeights);
        Assert.Equal(evidence.Confidence, weight.Weight);
        Assert.True(weight.Weight > 0d && weight.Weight <= 1d);
    }

    // T11: Stale ObservationSequence rejected
    [Fact]
    public async Task T11_StaleObservationSequence_Rejected()
    {
        var provider = Provider(DeveloperOptionsPattern);
        var oldObs = Obs(5, El("Enable demo mode"));
        var currentObs = Obs(6, El("Enable demo mode"));

        var evidence = await provider.ResolveAsync(new ObservationContext(oldObs));
        var fusion = new SemanticEvidenceFusion();
        var input = new SemanticEvidenceFusionInput(
            currentObs,
            semanticEvidence: evidence.ToImmutableArray(),
            knownObservationSequences: ImmutableArray.Create(6L));
        var result = fusion.Fuse(input);

        Assert.Empty(result.AcceptedEvidence);
        var reason = Assert.Single(result.ValidationReasons);
        Assert.Equal(SemanticEvidenceRejectionReason.StaleObservationSequence, reason.Reason);
    }

    // T12: Semantic failure preserves fail-closed behavior
    [Fact]
    public async Task T12_SemanticFailure_PreservesFailClosed()
    {
        var provider = Provider(WifiSettingsPattern); // miss for DeveloperOptions frame
        var obs = Obs(5, El("Enable demo mode"));
        var evidence = await provider.ResolveAsync(new ObservationContext(obs));
        Assert.Empty(evidence);

        var fusion = new SemanticEvidenceFusion();
        var result = fusion.Fuse(new SemanticEvidenceFusionInput(obs, semanticEvidence: evidence.ToImmutableArray()));
        Assert.Empty(result.AcceptedEvidence);
        Assert.Empty(result.RejectedEvidence);
    }
}