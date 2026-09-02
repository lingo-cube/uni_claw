using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// Real-world-oriented validation harness for Fast Semantic Container Identity
/// Recovery using the known DeveloperOptions scroll container. It simulates
/// Runtime authority deterministically and records Text Resolver result, Fast
/// Semantic result, Runtime Validation result, final identity/belief, and
/// SemanticContradiction presence.
/// </summary>
public sealed class FastSemanticContainerIdentityRealWorldValidationTests
{
    private sealed record ValidationRunRecord(
        string Scenario,
        long ObservationSequence,
        string? TextResolverResult,
        int FastSemanticResultCount,
        string? Candidate,
        double? Confidence,
        string RuntimeValidationResult,
        string? FinalContainerIdentity,
        string? FinalBelief,
        bool SemanticContradiction);

    private static readonly SemanticPattern DeveloperOptionsPattern = new(
        "DeveloperOptions",
        "pattern:developer-options-real",
        ImmutableArray.Create("Developer options", "Enable demo mode", "Show demo mode", "Automatic system updates"),
        ImmutableArray.Create("switch"),
        ImmutableArray.Create("type:switch", "switch:True"));

    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    private static ObservedElement El(string text, string? type = "menu_item") =>
        new(text, null, 0, null, type);

    private static FastSemanticContainerIdentityProvider FastProvider() =>
        new(ContainerIdentityPrototypeStore.FromSemanticPatterns(
            ImmutableArray.Create(DeveloperOptionsPattern)));

    private static ValidationRunRecord Run(
        string scenario,
        long seq,
        string? textResult,
        Observation obs,
        string? previousVerifiedIdentity,
        bool observationContinuity,
        double recoveryConfidenceThreshold = 0.6)
    {
        var provider = FastProvider();
        var evidence = provider.ResolveAsync(new ObservationContext(obs, previousVerifiedIdentity))
            .GetAwaiter().GetResult();

        var candidate = evidence.Length > 0 ? evidence[0].Candidate : null;
        var confidence = evidence.Length > 0 ? evidence[0].Confidence : (double?)null;

        // Simulated Runtime authority: Text Resolver first; Semantic candidate may
        // be used only when it passes Runtime validation (confidence, previous
        // identity, observation continuity). Semantic never sets CurrentContainer.
        string runtimeResult;
        string? finalContainer;
        string? finalBelief;
        bool contradiction;

        if (textResult is not null)
        {
            runtimeResult = "TEXT_RESOLVER_SUCCESS";
            finalContainer = textResult;
            finalBelief = textResult;
            contradiction = false;
        }
        else if (candidate is not null
                 && confidence is { } c
                 && c >= recoveryConfidenceThreshold
                 && previousVerifiedIdentity == candidate
                 && observationContinuity)
        {
            runtimeResult = "RUNTIME_VALIDATION_RECOVERED";
            finalContainer = candidate;
            finalBelief = candidate;
            contradiction = false;
        }
        else
        {
            runtimeResult = "FAIL_CLOSED";
            finalContainer = null;
            finalBelief = null;
            contradiction = true;
        }

        return new ValidationRunRecord(
            scenario,
            seq,
            textResult,
            evidence.Length,
            candidate,
            confidence,
            runtimeResult,
            finalContainer,
            finalBelief,
            contradiction);
    }

    // A: Title visible → Text Resolver success, no Semantic dependency.
    [Fact]
    public void A_TitleVisible_TextResolverSuccess()
    {
        var record = Run(
            "A-TitleVisible",
            seq: 1,
            textResult: "DeveloperOptions",
            obs: Obs(1, El("Developer options"), El("Enable demo mode")),
            previousVerifiedIdentity: "DeveloperOptions",
            observationContinuity: true);

        Assert.Equal("DeveloperOptions", record.TextResolverResult);
        Assert.Equal("TEXT_RESOLVER_SUCCESS", record.RuntimeValidationResult);
        Assert.Equal("DeveloperOptions", record.FinalContainerIdentity);
        Assert.False(record.SemanticContradiction);
    }

    // B: Title leaves viewport → Text Resolver null, Fast Semantic candidate,
//    Runtime Validation recovers identity.
[Fact]
public void B_TitleOffscreen_FastCandidate_Recovery()
{
    var record = Run(
        "B-TitleOffscreen",
        seq: 2,
        textResult: null,
        obs: Obs(2, El("Enable demo mode"), El("Show demo mode"), new ObservedElement("Automatic system updates", true, 0, null, "switch")),
        previousVerifiedIdentity: "DeveloperOptions",
        observationContinuity: true);

        Assert.Null(record.TextResolverResult);
        Assert.True(record.FastSemanticResultCount > 0);
        Assert.Equal("DeveloperOptions", record.Candidate);
        Assert.True(record.Confidence >= 0.6);
        Assert.Equal("RUNTIME_VALIDATION_RECOVERED", record.RuntimeValidationResult);
        Assert.Equal("DeveloperOptions", record.FinalContainerIdentity);
        Assert.False(record.SemanticContradiction);
    }

    // C: Bottom random scroll → Semantic miss, old fail-close preserved.
    [Fact]
    public void C_BottomRandomScroll_SemanticMiss_FailClosed()
    {
        var record = Run(
            "C-BottomRandomScroll",
            seq: 3,
            textResult: null,
            obs: Obs(3, El("Unknown option"), El("Some random row")),
            previousVerifiedIdentity: "DeveloperOptions",
            observationContinuity: true);

        Assert.Equal(0, record.FastSemanticResultCount);
        Assert.Null(record.Candidate);
        Assert.Equal("FAIL_CLOSED", record.RuntimeValidationResult);
        Assert.Null(record.FinalContainerIdentity);
        Assert.True(record.SemanticContradiction);
    }

    // D: Wrong page → no false recovery.
    [Fact]
    public void D_WrongPage_NoFalseRecovery()
    {
        var record = Run(
            "D-WrongPage",
            seq: 4,
            textResult: null,
            obs: Obs(4, El("Data usage"), El("Mobile data")),
            previousVerifiedIdentity: "DeveloperOptions",
            observationContinuity: true);

        Assert.Equal(0, record.FastSemanticResultCount);
        Assert.Null(record.Candidate);
        Assert.Equal("FAIL_CLOSED", record.RuntimeValidationResult);
        Assert.NotEqual("DeveloperOptions", record.FinalContainerIdentity);
        Assert.True(record.SemanticContradiction);
    }

    // Baseline vs Fast: Scenario B without provider fails-closed; with provider recovers.
    [Fact]
    public async Task BaselineVsFast_ContradictionRecoveryComparison()
    {
        var obs = Obs(10, El("Enable demo mode"), El("Show demo mode"), new ObservedElement("Automatic system updates", true, 0, null, "switch"));
        var context = new ObservationContext(obs, "DeveloperOptions");

        // Baseline: no semantic provider.
        var baselinePipeline = new SemanticEvidenceFusionPipeline();
        var baseline = await baselinePipeline.ResolveAndFuseAsync(new SemanticEvidenceFusionInput(obs));
        Assert.Empty(baseline.AcceptedEvidence);

        // Fast: provider supplies candidate.
        var fastPipeline = new SemanticEvidenceFusionPipeline(
            provider: FastProvider(),
            fusion: new SemanticEvidenceFusion());
        var fast = await fastPipeline.ResolveAndFuseAsync(new SemanticEvidenceFusionInput(obs));
        var accepted = Assert.Single(fast.AcceptedEvidence);
        Assert.Equal("DeveloperOptions", accepted.Candidate);
    }

    // Safety: low-confidence candidate must not recover.
    [Fact]
    public async Task Safety_LowConfidence_DoesNotRecover()
    {
        var obs = Obs(20, new ObservedElement("Developer options", null, 0, null, "text"));
        var provider = FastProvider();
        var evidence = await provider.ResolveAsync(new ObservationContext(obs, "DeveloperOptions"));

        // Score 1/3 ≈ 0.33 below the 0.6 runtime recovery threshold.
        var confidence = Assert.Single(evidence).Confidence;
        Assert.True(confidence < 0.6);

        var record = Run(
            "Safety-LowConfidence",
            seq: 20,
            textResult: null,
            obs: obs,
            previousVerifiedIdentity: "DeveloperOptions",
            observationContinuity: true);
        Assert.Equal("FAIL_CLOSED", record.RuntimeValidationResult);
        Assert.True(record.SemanticContradiction);
    }

    // Safety: stale evidence must not recover.
    [Fact]
    public async Task Safety_StaleEvidence_DoesNotRecover()
    {
        var oldObs = Obs(30, El("Enable demo mode"), El("Show demo mode"));
        var currentObs = Obs(31, El("Enable demo mode"), El("Show demo mode"));
        var provider = FastProvider();
        var evidence = await provider.ResolveAsync(new ObservationContext(oldObs, "DeveloperOptions"));

        var fusion = new SemanticEvidenceFusion();
        var result = fusion.Fuse(new SemanticEvidenceFusionInput(
            currentObs,
            semanticEvidence: evidence.ToImmutableArray(),
            knownObservationSequences: ImmutableArray.Create(31L)));

        Assert.Empty(result.AcceptedEvidence);
        Assert.Single(result.ValidationReasons);
    }

    // Safety: wrong container (previous identity mismatch) must not recover.
    [Fact]
    public void Safety_WrongContainer_DoesNotRecover()
    {
        var record = Run(
            "Safety-WrongContainer",
            seq: 40,
            textResult: null,
            obs: Obs(40, El("Enable demo mode"), El("Show demo mode")),
            previousVerifiedIdentity: "WifiSettings",
            observationContinuity: true);

        Assert.Equal("DeveloperOptions", record.Candidate);
        Assert.Equal("FAIL_CLOSED", record.RuntimeValidationResult);
        Assert.NotEqual("DeveloperOptions", record.FinalContainerIdentity);
        Assert.True(record.SemanticContradiction);
    }

    // Safety: vector miss preserves old behavior.
    [Fact]
    public async Task Safety_VectorMiss_PreservesOldBehavior()
    {
        var provider = FastProvider();
        var evidence = await provider.ResolveAsync(new ObservationContext(
            Obs(50, El("Unknown row")),
            "DeveloperOptions"));
        Assert.Empty(evidence);
    }
}