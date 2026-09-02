using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Semantic.Infrastructure.Retrieval;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// PROJECT_LEADER_SEMANTIC_SAFETY_HARDENING_APPLY — proofs T1..T14.
///
/// Mechanisms (both configurable, profile-bound, rollbackable):
///   A. Margin-based abstention (top1−top2 ambiguity).
///   B. Evidence sufficiency (generic vs identity-discriminative evidence).
///
/// Constraints: no Runtime / SemanticEvidence / ISemanticProvider change; no
/// embedding / retrieval / prototype change; former-heldout-v1 serves ONLY as a
/// regression/adversarial corpus; T4/T6/T8 becoming GREEN is the recorded
/// REGRESSION_SAFETY_RECOVERED outcome, NOT production qualification.
/// </summary>
public sealed class SemanticSafetyHardeningTests
{
    private readonly ITestOutputHelper _output;

    public SemanticSafetyHardeningTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly string V2ReportPath =
        HeldOutAssets.RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v2.json");

    private static readonly string V2ProfilePath =
        HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json");

    // ── fixtures ─────────────────────────────────────────────────────────────

    private static ContainerIdentityPrototypeStore V1Store() =>
        ContainerIdentityPrototypeStore.FromSemanticPatterns(HeldOutAssets.FrozenInMemoryPatterns());

    private static SemanticCandidate Cand(string identity, double score, string prototypeRef)
        => new(identity, score, prototypeRef);

    private static CandidateEvaluationContext Context(
        IReadOnlyList<SemanticCandidate> ranked,
        string? prev = null,
        ImmutableArray<string>? types = null,
        IReadOnlyList<string>? texts = null,
        IReadOnlyList<string>? structural = null,
        string? claimedIdentity = null)
    {
        var store = V1Store();
        var prototypesById = store.All().ToDictionary(p => p.PrototypeId);
        var t = types ?? ImmutableArray.Create("menu_item");
        var txt = texts ?? ImmutableArray<string>.Empty;
        var strukt = structural ?? ImmutableArray<string>.Empty;
        _ = claimedIdentity;
        return new CandidateEvaluationContext(
            ranked, prototypesById, prev, t, txt.Count,
            txt.Count > 0 || t.Length > 0 || strukt.Count > 0,
            ImmutableArray.CreateRange(txt), ImmutableArray.CreateRange(strukt), txt.Count);
    }

    private static IContainerIdentityCandidatePolicy V2Policy(double margin = 0.05)
        => CandidatePolicies.V2(margin);

    // ── A. margin-based abstention ───────────────────────────────────────────

    [Fact]
    public void T1_MarginSufficient_AllowsCandidateEvaluation()
    {
        var policy = V2Policy(0.06);
        var devRef = V1Store().All().Single(p => p.IdentityCandidate == "DeveloperOptions").PrototypeId;
        var ranked = new[]
        {
            Cand("DeveloperOptions", 0.90, devRef),
            Cand("WifiSettings", 0.70, V1Store().All().Single(p => p.IdentityCandidate == "WifiSettings").PrototypeId),
        };
        var result = policy.Decide(Context(ranked, prev: "DeveloperOptions",
            texts: new[] { "developer options", "enable demo mode" }));
        Assert.False(result.IsAbstain);
        Assert.Equal("DeveloperOptions", result.AcceptedCandidate!.IdentityCandidate);
    }

    [Fact]
    public void T2_MarginInsufficient_Abstains()
    {
        var policy = V2Policy(0.06);
        var (devRef, wifiRef, _) = Refs();
        var ranked = new[]
        {
            Cand("DeveloperOptions", 0.90, devRef),
            Cand("WifiSettings", 0.89, wifiRef), // margin 0.01 < 0.06
        };
        var result = policy.Decide(Context(ranked, prev: "DeveloperOptions",
            texts: new[] { "developer options", "enable demo mode" }));
        Assert.True(result.IsAbstain, "ambiguous top1/top2 margin must abstain");
    }

    // ── B. evidence sufficiency ──────────────────────────────────────────────

    [Fact]
    public void T3_NearEmptyObservation_Abstains()
    {
        var policy = V2Policy();
        var (devRef, _, _) = Refs();
        var result = policy.Decide(Context(
            new[] { Cand("DeveloperOptions", 0.90, devRef) },
            prev: "DeveloperOptions",
            types: ImmutableArray.Create("text", "menu_item"),
            texts: Array.Empty<string>(),
            structural: new[] { "type:text", "type:menu_item" }));
        Assert.True(result.IsAbstain, "near-empty (no text fragments) must abstain");
    }

    [Fact]
    public void T4_GenericOnlyEvidence_Abstains()
    {
        var policy = V2Policy();
        var (_, _, rootRef) = Refs();
        var result = policy.Decide(Context(
            new[] { Cand("SettingsRoot", 0.90, rootRef) },
            prev: "SettingsRoot",
            texts: new[] { "system", "settings" },
            structural: new[] { "type:menu_item" }));
        Assert.True(result.IsAbstain, "generic-only UI vocabulary must abstain");
    }

    [Fact]
    public void T5_SufficientDiscriminativeEvidence_AllowsCandidate()
    {
        var policy = V2Policy();
        var (_, wifiRef, _) = Refs();
        var result = policy.Decide(Context(
            new[] { Cand("WifiSettings", 0.90, wifiRef) },
            prev: "WifiSettings",
            texts: new[] { "wi-fi", "connected", "androidwifi" },
            structural: new[] { "type:menu_item", "type:text_block" }));
        Assert.False(result.IsAbstain);
        Assert.Equal("WifiSettings", result.AcceptedCandidate!.IdentityCandidate);
    }

    // ── retained fail-closed rules inside V2 ─────────────────────────────────

    [Fact]
    public void T6_PreviousIdentityConflict_StaysFailClosed()
    {
        var result = V2Policy().Decide(Context(
            new[] { Cand("DeveloperOptions", 0.90, Refs().dev) },
            prev: "WifiSettings",
            texts: new[] { "developer options", "enable demo mode" }));
        Assert.True(result.IsAbstain);
    }

    [Fact]
    public void T7_StructuralIncompatibility_StaysFailClosed()
    {
        var (devRef, _, _) = Refs();
        var store = V1Store();
        var devPrototype = store.All().Single(p => p.PrototypeId == devRef);
        // Observation with element types that overlap NO prototype type.
        var result = V2Policy().Decide(Context(
            new[] { Cand("DeveloperOptions", 0.90, devRef) },
            prev: "DeveloperOptions",
            types: ImmutableArray.Create("text_block", "toggle"),
            texts: new[] { "developer options" }));
        _ = devPrototype;
        Assert.True(result.IsAbstain);
    }

    // ── profile selection / rollback ─────────────────────────────────────────

    [Fact]
    public void T8_PolicyV1AndV2SelectableByProfile()
    {
        var ambiguous = CandidateEvaluationContextAmbiguous();

        // V1 policy: no margin / no sufficiency → generic "system" evidence is accepted.
        var v1Result = CandidatePolicies.V1().Decide(ambiguous);
        Assert.False(v1Result.IsAbstain);

        // V2 policy: same inputs → evidence sufficiency abstains.
        var v2Result = CandidatePolicies.V2().Decide(ambiguous);
        Assert.True(v2Result.IsAbstain);

        // Profile-bound selection through the factory (rollback v2 → v1 via config).
        var optionsV1 = new UniClaw.Semantic.Infrastructure.Configuration.SemanticOptions
        {
            Policy = new UniClaw.Semantic.Infrastructure.Configuration.SemanticPolicyOptions { ProfileVersion = "v1" },
        };
        var optionsV2 = optionsV1 with
        {
            Policy = optionsV1.Policy with { ProfileVersion = "v2" },
        };
        Assert.NotNull(FastSemanticPipelineFactory.CreateFromOptions(optionsV1, V1Store()));
        Assert.NotNull(FastSemanticPipelineFactory.CreateFromOptions(optionsV2, V1Store()));
    }

    // ── layer isolation ──────────────────────────────────────────────────────

    [Fact]
    public void T9_VectorIndexDoesNotKnowMarginPolicy()
    {
        var members = typeof(IVectorSemanticIndex).GetMethods().Select(m => m.Name).ToArray();
        Assert.DoesNotContain("Decide", members);
        Assert.DoesNotContain("Margin", typeof(ExactInMemoryVectorIndex).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public void T10_EmbeddingProviderDoesNotKnowSafetyPolicy()
    {
        var methods = typeof(IEmbeddingProvider).GetMethods().Select(m => m.Name).ToArray();
        Assert.Single(methods);
        Assert.Equal("Embed", methods.Single());
    }

    [Fact]
    public void T11_SemanticEvidenceContractUnchanged()
    {
        var evidenceType = typeof(SemanticEvidence);
        Assert.Equal("UniClaw.Runtime.Capabilities.Perception.Semantic", evidenceType.Namespace);
        // The contract surface is exactly the pre-hardening one.
        var expected = new[]
        {
            "EvidenceId", "Version", "Source", "Kind", "Candidate", "Confidence",
            "Scope", "ObservationSequence", "CreatedAt", "ValidUntil", "References",
        };
        var actual = evidenceType.GetProperties().Select(p => p.Name).OrderBy(x => x).ToArray();
        Assert.Equal(expected.OrderBy(x => x), actual);
        Assert.Equal("UniClaw.Runtime.Capabilities.Perception.Semantic", typeof(ISemanticProvider).Namespace);
    }

    // ── profile reproducibility ──────────────────────────────────────────────

    [Fact]
    public void T12_V2ConfigurationIdentityReproducible()
    {
        var profile = SemanticPerceptionProfiles.V2;
        Assert.Equal("SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2", profile.ProfileId);
        Assert.Equal("CONTAINER_IDENTITY_POLICY_V2", profile.CandidatePolicyProfileVersion);
        Assert.Equal(384, profile.EmbeddingModel.Dimension);
        Assert.Equal("BAAI/bge-small-en-v1.5", profile.EmbeddingModel.ModelId);

        // The committed V2 profile JSON is the SSOT for the executed parameters;
        // its policy section must match the C# V2 policy binding.
        Assert.True(File.Exists(V2ProfilePath), $"V2 profile JSON missing: {V2ProfilePath} (run the Python hardening pipeline first).");
        using var json = JsonDocument.Parse(File.ReadAllText(V2ProfilePath));
        var policy = json.RootElement.GetProperty("policy");
        Assert.Equal("CONTAINER_IDENTITY_POLICY_V2", policy.GetProperty("profileVersion").GetString());
        Assert.Equal(0.05, policy.GetProperty("minimumTop1Top2Margin").GetDouble());
        var sufficiency = json.RootElement.GetProperty("evidenceSufficiency");
        Assert.True(sufficiency.GetProperty("enabled").GetBoolean());
        Assert.Equal(4, sufficiency.GetProperty("identityAnchors").EnumerateObject().Count());
    }

    // ── former-heldout regression safety (Profile V2, BGE pipeline) ─────────

    [Fact]
    public void T13_FormerHeldoutRegressionSafetyRecovered()
    {
        Assert.True(File.Exists(V2ReportPath), $"V2 report missing: {V2ReportPath} (run the Python hardening pipeline first).");
        using var report = JsonDocument.Parse(File.ReadAllText(V2ReportPath));
        var metrics = report.RootElement.GetProperty("metrics");

        // Historical safety failures eliminated on former-heldout-v1 (as
        // regression/adversarial): FR=0, FPR=0, HNR=1.0, IE-admission=0.
        Assert.Equal(0.0, metrics.GetProperty("falseRecoveryRate").GetDouble());
        Assert.Equal(0.0, metrics.GetProperty("falsePositiveRate").GetDouble());
        Assert.Equal(1.0, metrics.GetProperty("hardNegativeRejectionRate").GetDouble());
        Assert.Equal(0, metrics.GetProperty("insufficientEvidenceAdmitted").GetInt32());

        _output.WriteLine($"T13: FR={metrics.GetProperty("falseRecoveryRate").GetDouble():F4} " +
                          $"HNR={metrics.GetProperty("hardNegativeRejectionRate").GetDouble():F4} " +
                          $"IEAdm={metrics.GetProperty("insufficientEvidenceAdmitted").GetInt32()} " +
                          $"Top1={metrics.GetProperty("top1Accuracy").GetDouble():F4}");
    }

    // ── degenerate reject-all guard ──────────────────────────────────────────

    [Fact]
    public void T14_DegenerateRejectAllGuard()
    {
        Assert.True(File.Exists(V2ReportPath), $"V2 report missing: {V2ReportPath}");
        using var report = JsonDocument.Parse(File.ReadAllText(V2ReportPath));
        var metrics = report.RootElement.GetProperty("metrics");
        var correctRecovery = metrics.GetProperty("correctRecoveryRate").GetDouble();
        var abstention = metrics.GetProperty("abstentionRate").GetDouble();

        // Not reject-all: recovery must remain acceptable, abstention bounded.
        Assert.True(correctRecovery >= 0.70, $"CorrectRecovery {correctRecovery:F3} collapsed below guard floor");
        Assert.True(abstention < 0.90, $"Abstention {abstention:F3} indicates degenerate reject-all");

        // The guard itself flags a hypothetical reject-everything profile.
        var degenerate = SafetyHardeningAssessment.FromCounts(
            correctRecovery: 0.0, abstentionRate: 1.0, falseRecoveryRate: 0.0);
        Assert.True(degenerate.IsOverRejecting);
        Assert.False(SafetyHardeningAssessment.FromCounts(correctRecovery, abstention, 0.0).IsOverRejecting);
    }

    // ── V1 failure record preserved (no silent mutation) ─────────────────────

    [Fact]
    public void T15_V1ProfileFailureRecordPreserved()
    {
        using var v1 = JsonDocument.Parse(HeldOutAssets.ReadReportJson(HeldOutAssets.BgeReportJsonPath));
        var metrics = v1.RootElement.GetProperty("metrics");
        Assert.Equal(0.4167, metrics.GetProperty("falseRecoveryRate").GetDouble(), 4);
        Assert.Equal(0.5833, metrics.GetProperty("hardNegativeRejectionRate").GetDouble(), 4);
        Assert.Equal(0.75, metrics.GetProperty("top1Accuracy").GetDouble(), 4);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static (string dev, string wifi, string root) Refs()
    {
        var all = V1Store().All();
        return (
            all.Single(p => p.IdentityCandidate == "DeveloperOptions").PrototypeId,
            all.Single(p => p.IdentityCandidate == "WifiSettings").PrototypeId,
            all.Single(p => p.IdentityCandidate == "SettingsRoot").PrototypeId);
    }

    private static CandidateEvaluationContext CandidateEvaluationContextAmbiguous()
    {
        var (dev, wifi, _) = Refs();
        return Context(
            new[]
            {
                Cand("DeveloperOptions", 0.90, dev),
                Cand("WifiSettings", 0.87, wifi),
            },
            prev: "DeveloperOptions",
            texts: new[] { "system" });
    }
}

/// <summary>
/// Degenerate reject-all guard: ensures safety wins are not achieved by
/// rejecting everything. Reports CorrectRecoveryRate, AbstentionRate, Coverage
/// and flags over-rejection when recovery collapses.
/// </summary>
public sealed record SafetyHardeningAssessment(
    double CorrectRecoveryRate,
    double AbstentionRate,
    bool IsOverRejecting)
{
    public const double MinAcceptableCorrectRecovery = 0.70;
    public const double MaxAcceptableAbstention = 0.90;

    public static SafetyHardeningAssessment FromCounts(
        double correctRecovery, double abstentionRate, double falseRecoveryRate)
    {
        // FR=0 with zero correct recovery is the degenerate reject-all signature.
        var overRejecting = falseRecoveryRate <= 0d
                            && (correctRecovery < MinAcceptableCorrectRecovery
                                || abstentionRate >= MaxAcceptableAbstention);
        return new SafetyHardeningAssessment(correctRecovery, abstentionRate, overRejecting);
    }
}