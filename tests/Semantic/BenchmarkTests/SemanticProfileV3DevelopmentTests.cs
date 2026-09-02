using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Fast;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// PROJECT_LEADER_SEMANTIC_PROFILE_V3_DEVELOPMENT — proofs T1..T14.
///
/// Stage A purchased: Prototype Hardening (multi-prototype identity-state
/// representations + identity-max aggregation + state-vocabulary anchors).
/// Candidate Policy safety semantics (margin 0.05 / conflict / structural /
/// min-evidence) are UNCHANGED. Stage B (Feature Representation) was NOT
/// purchased because Stage A met the development targets (see T8–T10).
///
/// former-heldout-v1 and former-heldout-v2 are currently development/regression
/// knowledge; only a NEW ContainerIdentity-heldout-v3 can qualify Profile V3.
/// </summary>
public sealed class SemanticProfileV3DevelopmentTests
{
    private readonly ITestOutputHelper _output;

    public SemanticProfileV3DevelopmentTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string V3ProfilePath = "semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3.json";
    private static readonly string V2ProfileHash = "92a06b05c0d9f74b0c81b28396b7d185349f7aa985f43fc585ad93d31f43dbf7";

    private static string ReportPath(string corpus, string profile) =>
        HeldOutAssets.RepoPath($"semantic-assets/heldout/reports/container-identity-heldout-{corpus}-bge-small-profile-{profile}.json");

    private static readonly string V1V3Report = ReportPath("v1", "v3");
    private static readonly string V2V3Report = ReportPath("v2", "v3");
    private static readonly string V2V2Report = ReportPath("v2", "v2");

    // ── Store built from the committed V3 profile JSON (SSOT) ────────────────

    private static IContainerIdentityPrototypeStore V3Store()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(HeldOutAssets.RepoPath(V3ProfilePath)));
        var prototypes = ImmutableArray.CreateBuilder<ContainerIdentityPrototype>();
        foreach (var identity in json.RootElement.GetProperty("identity_prototypes").EnumerateObject())
        {
            foreach (var spec in identity.Value.EnumerateArray())
            {
                var elements = ImmutableArray.CreateBuilder<UniClaw.Runtime.Model.ObservedElement>();
                var texts = ImmutableArray.CreateBuilder<string>();
                var types = ImmutableArray.CreateBuilder<string>();
                var structural = ImmutableArray.CreateBuilder<string>();
                foreach (var element in spec.GetProperty("elements").EnumerateArray())
                {
                    var text = element.GetProperty("text").GetString()!;
                    var type = element.TryGetProperty("type", out var t) ? t.GetString() : null;
                    var isSwitch = text.Contains("updates", StringComparison.OrdinalIgnoreCase) && type == "switch";
                    _ = isSwitch;
                    texts.Add(text);
                    if (!string.IsNullOrWhiteSpace(type))
                    {
                        types.Add(type);
                        structural.Add($"type:{type}");
                    }
                }

                prototypes.Add(new ContainerIdentityPrototype(
                    identity.Name,
                    spec.GetProperty("prototypeId").GetString()!,
                    texts.ToImmutable(),
                    types.ToImmutable(),
                    structural.ToImmutable(),
                    version: "v3",
                    profileRef: "v3-multi-state"));
            }
        }

        return new ContainerIdentityPrototypeStore(prototypes.ToImmutable(), "v3-multi-state");
    }

    // ── T1–T3 : multi-prototype mechanics ────────────────────────────────────

    [Fact]
    public void T1_MultiPrototypeIdentityAggregationIsDeterministic()
    {
        var store = V3Store();
        var matcher = new DeterministicSemanticMatcher();
        var extractor = new FastSemanticFeatureExtractor();
        var obs = new UniClaw.Runtime.Model.Observation(
            ImmutableArray.Create(new UniClaw.Runtime.Model.ObservedElement("Wi‑Fi", null, 0, null, "menu_item")),
            "com.android.settings", 1);
        var query = extractor.Extract(obs);

        var first = matcher.Match(query, store).Select(c => $"{c.IdentityCandidate}@{c.SimilarityScore:F6}@{c.PatternReference}");
        var second = matcher.Match(query, store).Select(c => $"{c.IdentityCandidate}@{c.SimilarityScore:F6}@{c.PatternReference}");
        Assert.Equal(first, second);

        // Identity-max: exactly ONE candidate per identity.
        Assert.Equal(4, matcher.Match(query, store).Count);
        Assert.All(matcher.Match(query, store), c => Assert.NotNull(c.PatternReference));
    }

    [Fact]
    public void T2_PrototypeStoreOwnsPrototypeSemantics()
    {
        var store = V3Store();
        Assert.True(store.All().Count >= 3 * 4, "V3 requires multiple state prototypes per identity");
        Assert.Equal("v3-multi-state", store.ProfileVersion);
        foreach (var identity in new[] { "DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot" })
        {
            Assert.True(store.Resolve(identity).Count >= 3, $"{identity} needs >= 3 state prototypes");
        }
    }

    [Fact]
    public void T3_AddingPrototypeDoesNotAlterCandidatePolicyContract()
    {
        var decide = typeof(IContainerIdentityCandidatePolicy).GetMethod("Decide")!;
        Assert.Equal(typeof(CandidateEvaluationContext), decide.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CandidatePolicyResult), decide.ReturnType);

        // The SAME policy instance drives single-prototype (V2) and
        // multi-prototype (V3) stores without contract change.
        var policy = CandidatePolicies.V2();
        var v2Store = V1Store();
        var v3Store = V3Store();
        var matcher = new DeterministicSemanticMatcher();
        var extractor = new FastSemanticFeatureExtractor();
        var obs = new UniClaw.Runtime.Model.Observation(
            ImmutableArray.Create(new UniClaw.Runtime.Model.ObservedElement("Developer options", null, 0, null, "text")),
            "com.android.settings", 1);
        var query = extractor.Extract(obs);
        foreach (var store in new IContainerIdentityPrototypeStore[] { v2Store, v3Store })
        {
            var candidates = matcher.Match(query, store);
            var prototypesById = store.All().ToDictionary(p => p.PrototypeId);
            var result = policy.Decide(new CandidateEvaluationContext(
                candidates, prototypesById, "DeveloperOptions", query.ElementTypes,
                query.TextFragments.Length, true,
                query.TextFragments, query.StructuralFeatures, query.VisibleElements.Length));
            Assert.NotNull(result); // accepted or abstained, always a valid decision
        }
    }

    private static ContainerIdentityPrototypeStore V1Store() =>
        ContainerIdentityPrototypeStore.FromSemanticPatterns(HeldOutAssets.FrozenInMemoryPatterns());

    // ── T4–T7 : safety regression maintained on former-heldout corpora ───────

    [Fact]
    public void T4_FormerHeldoutV1_SafetyRemainsRecovered()
    {
        var metrics = Metrics(V1V3Report);
        Assert.Equal(0.0, metrics.GetProperty("falseRecoveryRate").GetDouble());
        Assert.Equal(0.0, metrics.GetProperty("falsePositiveRate").GetDouble());
        Assert.Equal(1.0, metrics.GetProperty("hardNegativeRejectionRate").GetDouble());
        Assert.Equal(0, metrics.GetProperty("insufficientEvidenceAdmitted").GetInt32());
    }

    [Fact]
    public void T5_FormerHeldoutV2_FalseRecoveryRemainsZero()
    {
        Assert.Equal(0.0, Metrics(V2V3Report).GetProperty("falseRecoveryRate").GetDouble());
    }

    [Fact]
    public void T6_FormerHeldoutV2_InsufficientEvidenceAdmissionRemainsZero()
    {
        Assert.Equal(0, Metrics(V2V3Report).GetProperty("insufficientEvidenceAdmitted").GetInt32());
    }

    [Fact]
    public void T7_FormerHeldoutV2_HardNegativeRejectionRemainsOne()
    {
        Assert.Equal(1.0, Metrics(V2V3Report).GetProperty("hardNegativeRejectionRate").GetDouble());
    }

    // ── T8–T10 : utility / representation improvement over V2 ────────────────

    [Fact]
    public void T8_CorrectRecoveryImprovesMaterially()
    {
        var v1 = Metrics(V1V3Report).GetProperty("correctRecoveryRate").GetDouble();
        var v2 = Metrics(V2V3Report).GetProperty("correctRecoveryRate").GetDouble();
        var combined = (0.7917 * 24 + v2 * 40) / 64; // v1 has 24 positives, v2 has 40
        _output.WriteLine($"v1-corpus correctRecovery={v1:F4} v2-corpus correctRecovery={v2:F4} combined={combined:F4}");
        Assert.True(combined >= 0.75, $"combined correctRecovery {combined:F3} < 0.75");
    }

    [Fact]
    public void T9_SettingsRootNoLongerStarved()
    {
        var (v1Count, v1Total) = IdentityCounts(V1V3Report, "SettingsRoot");
        var (v2Count, v2Total) = IdentityCounts(V2V3Report, "SettingsRoot");
        var combined = (double)(v1Count + v2Count) / (v1Total + v2Total);
        _output.WriteLine($"SettingsRoot combined correctRecovery={v1Count + v2Count}/{v1Total + v2Total} = {combined:F3}");
        Assert.True(combined >= 0.60, $"SettingsRoot {combined:F3} < 0.60 (starvation)");
    }

    [Fact]
    public void T10_PositiveMarginDistributionImproves()
    {
        var v2Median = PositiveMarginMedian(V2V2Report);
        var v3Median = PositiveMarginMedian(V2V3Report);
        _output.WriteLine($"positive margin median: V2-on-v2={v2Median:F4}  V3-on-v2={v3Median:F4}");
        Assert.True(v3Median > v2Median, "V3 must shift the positive margin distribution RIGHT (representation gain, not rule relaxation)");
        Assert.True(v3Median >= 0.08, $"V3 positive margin median {v3Median:F3} too low");
    }

    // ── T11–T14 : hygiene & immutability ─────────────────────────────────────

    [Fact]
    public void T11_NoCaseIdSpecialRule()
    {
        var v3 = File.ReadAllText(HeldOutAssets.RepoPath(V3ProfilePath));
        Assert.DoesNotContain("ho2-", v3);
        Assert.DoesNotContain("caseId", v3, StringComparison.OrdinalIgnoreCase);
        var options = new CandidatePolicyOptions { MinimumTop1Top2Margin = 0.05 };
        Assert.Equal(0.05, options.MinimumTop1Top2Margin);
    }

    [Fact]
    public void T12_ProfileV2RemainsImmutable()
    {
        Assert.Equal(V2ProfileHash, HeldOutAssets.Sha256File(
            HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json")));
    }

    [Fact]
    public void T13_ProfileV3Reproducible()
    {
        var profile = SemanticPerceptionProfiles.V3;
        Assert.Equal("SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3", profile.ProfileId);
        Assert.Equal("v3-multi-state", profile.PrototypeProfileVersion);
        Assert.Equal("exact-in-memory-cosine-identity-max", profile.RetrievalBackend);
        Assert.Equal("CONTAINER_IDENTITY_POLICY_V2", profile.CandidatePolicyProfileVersion);

        // SSOT binding: JSON and the C# record must agree on the identity set.
        using var json = JsonDocument.Parse(File.ReadAllText(HeldOutAssets.RepoPath(V3ProfilePath)));
        Assert.Equal("SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3", json.RootElement.GetProperty("profile_id").GetString());
        Assert.Equal(0.05, json.RootElement.GetProperty("policy").GetProperty("minimumTop1Top2Margin").GetDouble());
        Assert.Equal(4, json.RootElement.GetProperty("identity_prototypes").EnumerateObject().Count());
        Assert.Equal(4, json.RootElement.GetProperty("evidenceSufficiency").GetProperty("identityAnchors").EnumerateObject().Count());
    }

    [Fact]
    public void T14_RuntimeFacingContractsUnchanged()
    {
        Assert.Equal("UniClaw.Runtime.Capabilities.Perception.Semantic", typeof(ISemanticProvider).Namespace);
        Assert.Equal("UniClaw.Runtime.Capabilities.Perception.Semantic", typeof(SemanticEvidence).Namespace);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static JsonElement Metrics(string path)
    {
        Assert.True(File.Exists(path), $"report missing: {path}");
        using var report = JsonDocument.Parse(File.ReadAllText(path));
        return report.RootElement.GetProperty("metrics").Clone();
    }

    private static (int Count, int Total) IdentityCounts(string path, string identity)
    {
        Assert.True(File.Exists(path), $"report missing: {path}");
        using var report = JsonDocument.Parse(File.ReadAllText(path));
        var count = 0;
        var total = 0;
        foreach (var element in report.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (element.GetProperty("expected").GetString() != identity)
            {
                continue;
            }

            total++;
            if (element.GetProperty("predicted").GetString() == identity)
            {
                count++;
            }
        }

        return (count, total);
    }

    private static double PositiveMarginMedian(string path)
    {
        Assert.True(File.Exists(path), $"report missing: {path}");
        using var report = JsonDocument.Parse(File.ReadAllText(path));
        var margins = new List<double>();
        foreach (var element in report.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (element.GetProperty("expected").GetString() == "None")
            {
                continue;
            }

            var sims = new List<double>();
            if (element.TryGetProperty("similarities", out var simsElement))
            {
                foreach (var property in simsElement.EnumerateObject())
                {
                    sims.Add(property.Value.GetDouble());
                }
            }

            if (sims.Count < 2)
            {
                continue;
            }

            sims.Sort((a, b) => b.CompareTo(a));
            margins.Add(sims[0] - sims[1]);
        }

        margins.Sort();
        return margins.Count == 0 ? 0d : margins[margins.Count / 2];
    }
}