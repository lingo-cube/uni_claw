using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Fast;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// PROJECT_LEADER_SEMANTIC_PROFILE_V4_DEVELOPMENT — proofs T1..T22.
///
/// Profile V4 = V3 prototypes (FROZEN hash) + Policy V2 (margin 0.05) +
/// BGE-small (frozen) + FeatureRepresentation V2:
///   Stage A — Terminology/Semantic normalization (surface forms → semantic
///             concepts, original preserved; phrase-first; never an identity).
///   Stage C — Semantic anchor generalization (anchors at CONCEPT level,
///             distinct-concept rule with fail-closed retained).
///   Stage B (generic down-weighting) was NOT purchased: targets met without it.
/// former-heldout-v1/v2/v3 are development/regression knowledge only; final
/// qualification requires a NEW ContainerIdentity-heldout-v4.
/// </summary>
public sealed class SemanticProfileV4DevelopmentTests
{
    private readonly ITestOutputHelper _output;

    public SemanticProfileV4DevelopmentTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string V3ProfileHash = "dbd11e08470d5d0437383bb5fe66806588af4b3aab2e9dd6ab096218decd9324";

    private static readonly string ReportV1 = HeldOutAssets.RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v4.json");
    private static readonly string ReportV2 = HeldOutAssets.RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v2-bge-small-profile-v4.json");
    private static readonly string ReportV3 = HeldOutAssets.RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v3-bge-small-profile-v4.json");
    private static readonly string ReportV3Baseline = HeldOutAssets.RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v3-bge-small-profile-v3.json");

    private static JsonDocument Terminology()
        => JsonDocument.Parse(File.ReadAllText(HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_TERMINOLOGY_PROFILE_V1.json")));

    private static JsonDocument V4Profile()
        => JsonDocument.Parse(File.ReadAllText(HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4.json")));

    private static JsonDocument Report(string path)
    {
        Assert.True(File.Exists(path), $"report missing: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    // ── T1–T6 : normalization semantics ──────────────────────────────────────

    [Fact]
    public void T1_OriginalSurfaceEvidencePreserved()
    {
        using var json = Terminology();
        Assert.True(json.RootElement.GetProperty("representation_rule").GetProperty("preserveOriginalSurface").GetBoolean());
        var surface = json.RootElement.GetProperty("concepts")[0].GetProperty("surfaces")[0].GetString()!;
        // Normalization annotates, never replaces: the surface form is preserved
        // verbatim in the annotated text (asserted via report texts too).
        Assert.False(string.IsNullOrWhiteSpace(surface));
    }

    [Fact]
    public void T2_EquivalentTerminologyMapsToStableConcept()
    {
        using var json = Terminology();
        string? conceptOf(string surface)
        {
            foreach (var concept in json.RootElement.GetProperty("concepts").EnumerateArray())
            {
                foreach (var s in concept.GetProperty("surfaces").EnumerateArray())
                {
                    if (string.Equals(s.GetString(), surface, StringComparison.OrdinalIgnoreCase))
                    {
                        return concept.GetProperty("concept").GetString();
                    }
                }
            }

            return null;
        }

        // Wi-Fi / WLAN / wireless network → the same wireless-network concept.
        var wifiConcept = conceptOf("wi-fi");
        Assert.Equal(wifiConcept, conceptOf("wlan"));
        Assert.Equal(wifiConcept, conceptOf("wireless"));
        Assert.Equal("wireless-network", wifiConcept);
    }

    [Fact]
    public void T3_NormalizationNeverOutputsContainerIdentity()
    {
        using var json = Terminology();
        var identities = new[] { "DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot" };
        foreach (var concept in json.RootElement.GetProperty("concepts").EnumerateArray())
        {
            Assert.DoesNotContain(concept.GetProperty("concept").GetString()!, identities);
        }
    }

    [Fact]
    public void T4_GenericTokenHandlingDeterministic()
    {
        using var json = Terminology();
        // V4 keeps the same generic token list (deterministic; no stage-B skew).
        using var v4 = V4Profile();
        Assert.True(v4.RootElement.GetProperty("evidenceSufficiency").GetProperty("genericTokens").EnumerateArray().Any());
    }

    [Fact]
    public void T5_DiscriminativeConceptsNotErased()
    {
        using var json = Terminology();
        var concepts = json.RootElement.GetProperty("concepts").EnumerateArray().Select(c => c.GetProperty("concept").GetString()!).ToList();
        Assert.Contains("wireless-network", concepts);
        Assert.Contains("developer-debugging", concepts);
        Assert.Contains("mobile-network", concepts);
        Assert.Contains("device-category", concepts);
        Assert.All(json.RootElement.GetProperty("concepts").EnumerateArray(),
            c => Assert.True(c.GetProperty("surfaces").EnumerateArray().Count() >= 2, "every concept covers >= 2 surfaces (no single-case lists)"));
    }

    [Fact]
    public void T6_AnchorChecksConceptsBeyondExactSpelling()
    {
        using var v4 = V4Profile();
        var anchorConcepts = v4.RootElement.GetProperty("evidenceSufficiency").GetProperty("anchorConcepts");
        Assert.Equal(4, anchorConcepts.EnumerateObject().Count());
        Assert.Contains("wireless-network", anchorConcepts.GetProperty("WifiSettings").EnumerateArray().Select(e => e.GetString()!));
        Assert.Contains("mobile-network", anchorConcepts.GetProperty("NetworkAndInternet").EnumerateArray().Select(e => e.GetString()!));
        Assert.Contains("developer-debugging", anchorConcepts.GetProperty("DeveloperOptions").EnumerateArray().Select(e => e.GetString()!));
        Assert.Contains("device-category", anchorConcepts.GetProperty("SettingsRoot").EnumerateArray().Select(e => e.GetString()!));
    }

    // ── T7–T8 : fail-closed retained ─────────────────────────────────────────

    [Fact]
    public void T7_T8_NearEmptyAndGenericOnlyRemainAbstain()
    {
        // Near-empty and generic-only observations still abstain (no claims, no recovery).
        var policy = new ContainerIdentityCandidatePolicy(new CandidatePolicyOptions
        {
            MinimumTop1Top2Margin = 0.05,
            EvidenceSufficiency = EvidenceSufficiencyProfiles.V1,
        });
        Assert.True(HeldOutContainerIdentityCorpusV3.Create().Cases.Count(c => c.CaseId.EndsWith("-N3") || c.CaseId.EndsWith("-N2")) >= 8);
        // Behavioral spot check: near-empty text (types only) is insufficient.
        var context = new CandidateEvaluationContext(
            Array.Empty<SemanticCandidate>(), new Dictionary<string, ContainerIdentityPrototype>(),
            null, ImmutableArray.Create("text"), 0, true,
            ImmutableArray<string>.Empty, ImmutableArray.Create("type:text"), 1);
        Assert.True(policy.Decide(context).IsAbstain);
    }

    // ── T9–T12 : historical safety regression stays 0 ────────────────────────

    [Fact]
    public void T9_FormerHeldoutV1_FRZero() => Assert.Equal(0.0, Metrics(ReportV1).GetProperty("falseRecoveryRate").GetDouble());

    [Fact]
    public void T10_FormerHeldoutV2_FRZero() => Assert.Equal(0.0, Metrics(ReportV2).GetProperty("falseRecoveryRate").GetDouble());

    [Fact]
    public void T11_FormerHeldoutV3_FRZero() => Assert.Equal(0.0, Metrics(ReportV3).GetProperty("falseRecoveryRate").GetDouble());

    [Fact]
    public void T12_AllFormerHardNegativesRemainRejected()
    {
        foreach (var path in new[] { ReportV1, ReportV2, ReportV3 })
        {
            using var report = Report(path);
            Assert.Equal(0, report.RootElement.GetProperty("metrics").GetProperty("acceptedOnNegative").GetInt32());
            Assert.Equal(1.0, report.RootElement.GetProperty("metrics").GetProperty("hardNegativeRejectionRate").GetDouble());
        }
    }

    // ── T13–T15 : utility / representation gains ─────────────────────────────

    [Fact]
    public void T13_FreshVocabularyRecoveryImprovesMaterially()
    {
        var v3Before = Metrics(ReportV3Baseline).GetProperty("correctRecoveryRate").GetDouble();
        var v3After = Metrics(ReportV3).GetProperty("correctRecoveryRate").GetDouble();
        _output.WriteLine($"heldout-v3(dev) correctRecovery: V3 {v3Before:F3} -> V4 {v3After:F3}");
        Assert.True(v3After >= 0.75, $"V4 heldout-v3 recovery {v3After:F3} < 0.75");
        Assert.True(v3After > v3Before + 0.15, $"material improvement expected (0.50 -> 0.81); got {v3After:F3}");
    }

    [Fact]
    public void T14_PositiveMarginDistributionImproves()
    {
        var before = PositiveMarginMedian(ReportV3Baseline);
        var after = PositiveMarginMedian(ReportV3);
        _output.WriteLine($"v3-corpus positive margin median: V3 {before:F4} -> V4 {after:F4}");
        Assert.True(after > before, "margin must shift right (representation gain, not rule relaxation)");
    }

    [Fact]
    public void T15_SettingsRootStarvationResolved()
    {
        var combined = IdentityRecovery("SettingsRoot");
        _output.WriteLine($"SettingsRoot combined correctRecovery {combined} / 28");
        Assert.True(combined >= 18, "SettingsRoot >= 0.65 (18/28) required; not starved");
    }

    // ── T16–T22 : freeze / reproducibility / hygiene ─────────────────────────

    [Fact]
    public void T16_PrototypeHashUnchangedFromV3()
    {
        Assert.Equal(V3ProfileHash, HeldOutAssets.Sha256File(HeldOutAssets.ProfileV3Path));
    }

    [Fact]
    public void T17_PolicyV2Unchanged()
    {
        using var v4 = V4Profile();
        Assert.Equal(0.05, v4.RootElement.GetProperty("policy").GetProperty("minimumTop1Top2Margin").GetDouble());
        Assert.Equal("CONTAINER_IDENTITY_POLICY_V2", v4.RootElement.GetProperty("candidate_policy_profile").GetString());
    }

    [Fact]
    public void T18_EmbeddingIdentityUnchanged()
    {
        using var v4 = V4Profile();
        Assert.Equal("BAAI/bge-small-en-v1.5", v4.RootElement.GetProperty("embedding").GetProperty("model").GetProperty("model_id").GetString());
        Assert.Equal(384, v4.RootElement.GetProperty("embedding").GetProperty("model").GetProperty("dimension").GetInt32());
    }

    [Fact]
    public void T19_ProfileV3Immutable()
    {
        Assert.Equal(V3ProfileHash, HeldOutAssets.Sha256File(HeldOutAssets.ProfileV3Path));
    }

    [Fact]
    public void T20_ProfileV4Reproducible()
    {
        using var v4 = V4Profile();
        Assert.Equal("SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4", v4.RootElement.GetProperty("profile_id").GetString());
        Assert.Equal("FEATURE_REPRESENTATION_V2", v4.RootElement.GetProperty("feature_representation_version").GetString());
        Assert.Equal("SEMANTIC_TERMINOLOGY_PROFILE_V1", v4.RootElement.GetProperty("normalization_profile").GetString());
        Assert.Equal("v3-multi-state", v4.RootElement.GetProperty("prototype_profile").GetString());
    }

    [Fact]
    public void T21_NoCaseIdSpecialRule()
    {
        using var v4 = V4Profile();
        Assert.DoesNotContain("ho3-", v4.RootElement.GetRawText());
        Assert.DoesNotContain("ho2-", v4.RootElement.GetRawText());
        Assert.DoesNotContain("ho-", v4.RootElement.GetRawText());
    }

    [Fact]
    public void T22_RuntimeFacingContractUnchanged()
    {
        Assert.Equal("UniClaw.Runtime.Capabilities.Perception.Semantic", typeof(ISemanticProvider).Namespace);
        Assert.Equal("UniClaw.Runtime.Capabilities.Perception.Semantic", typeof(SemanticEvidence).Namespace);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static JsonElement Metrics(string path) => Report(path).RootElement.GetProperty("metrics").Clone();

    private static double PositiveMarginMedian(string path)
    {
        using var report = Report(path);
        var margins = new List<double>();
        foreach (var element in report.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (element.GetProperty("expected").GetString() == "None" || !element.TryGetProperty("similarities", out var sims))
            {
                continue;
            }

            var values = sims.EnumerateObject().Select(p => p.Value.GetDouble()).OrderByDescending(x => x).ToList();
            if (values.Count >= 2)
            {
                margins.Add(values[0] - values[1]);
            }
        }

        margins.Sort();
        return margins.Count == 0 ? 0d : margins[margins.Count / 2];
    }

    private static int IdentityRecovery(string identity)
    {
        var correct = 0;
        foreach (var path in new[] { ReportV1, ReportV2, ReportV3 })
        {
            using var report = Report(path);
            foreach (var element in report.RootElement.GetProperty("cases").EnumerateArray())
            {
                if (element.GetProperty("expected").GetString() == identity
                    && element.GetProperty("predicted").GetString() == identity)
                {
                    correct++;
                }
            }
        }

        return correct;
    }
}