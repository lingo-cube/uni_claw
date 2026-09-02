using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Corpus;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// PROJECT_LEADER_SEMANTIC_PROFILE_V3_HELD_OUT_QUALIFICATION — proofs Q1..Q16.
///
/// Profile V3 is FROZEN before the heldout-v3 run (receipt pins profile +
/// prototype hashes). heldout-v3 is a NEW corpus that did not participate in
/// any design; it receives only PASS or FAIL. Historical qualification RED
/// (Profile V2 Q8/Q10) is preserved as evidence — this suite is the CURRENT
/// qualification.
/// </summary>
public sealed class SemanticProfileV3QualificationTests
{
    private readonly ITestOutputHelper _output;

    public SemanticProfileV3QualificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string ProvenProfileHash = "dbd11e08470d5d0437383bb5fe66806588af4b3aab2e9dd6ab096218decd9324";
    private const string ProvenPrototypeHash = "3223eca713003a3de2212dc556db665e3cd23ec8c980071d7e628c083f45469c"; // raw identity_prototypes section (canonical form used by the receipt writer)

    // ── Q1 : corpus isolation ────────────────────────────────────────────────

    [Fact]
    public void Q1_HeldOutV3IsolationValid()
    {
        var heldOutV3 = HeldOutContainerIdentityCorpusV3.Create();
        Assert.Equal(HeldOutContainerIdentityCorpusV3.CorpusId, heldOutV3.CorpusId);
        Assert.Equal(80, heldOutV3.Cases.Length);
        Assert.Equal(48, heldOutV3.Cases.Count(c => c.ExpectedCandidate != "None"));
        Assert.Equal(32, heldOutV3.Cases.Count(c => c.ExpectedCandidate == "None"));

        using var manifest = JsonDocument.Parse(File.ReadAllText(HeldOutAssets.ManifestV3JsonPath));
        Assert.Equal(80, manifest.RootElement.GetProperty("caseCount").GetInt32());
        Assert.Equal(48, manifest.RootElement.GetProperty("positiveCount").GetInt32());
        Assert.Equal(32, manifest.RootElement.GetProperty("negativeCount").GetInt32());
        Assert.Contains("did not participate", manifest.RootElement.GetProperty("isolationStatement").GetString());

        // Fingerprint disjointness vs tuning + former-heldout-v1/v2.
        var protectedFingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var corpus in ProtectedCorpora())
        {
            foreach (var c in corpus.Cases)
            {
                protectedFingerprints.Add(HeldOutAssets.ElementFingerprint(c.InputObservation));
            }
        }

        var collisions = heldOutV3.Cases
            .Select(c => (Case: c, Fp: HeldOutAssets.ElementFingerprint(c.InputObservation)))
            .Where(x => protectedFingerprints.Contains(x.Fp))
            .Select(x => x.Case.CaseId)
            .ToList();
        Assert.True(collisions.Count == 0,
            "heldout-v3 reuses a concrete instance from tuning/v1/v2: " + string.Join(", ", collisions));
        _output.WriteLine($"Q1: 80 cases, 0 fingerprint collisions (tuning+v1+v2)");
    }

    private static List<SemanticCorpus> ProtectedCorpora() =>
        new()
        {
            HeldOutContainerIdentityCorpus.Create(),
            HeldOutContainerIdentityCorpusV2.Create(),
            DeveloperOptionsBenchmarkCorpus.Create(),
            ContainerIdentityCorpora.WifiSettings(),
            ContainerIdentityCorpora.NetworkAndInternet(),
            ContainerIdentityCorpora.SettingsRoot(),
            ExpandedContainerIdentityCorpora.DeveloperOptionsGolden(),
            ExpandedContainerIdentityCorpora.WifiSettingsGolden(),
            ExpandedContainerIdentityCorpora.NetworkAndInternetGolden(),
            ExpandedContainerIdentityCorpora.SettingsRootGolden(),
            ExpandedContainerIdentityCorpora.RegressionCorpus(),
            ExpandedContainerIdentityCorpora.AdversarialCorpus(),
        };

    // ── Q2–Q3 : freeze hashes ────────────────────────────────────────────────

    [Fact]
    public void Q2_ProfileV3FreezeHashValid()
    {
        Assert.Equal(ProvenProfileHash, HeldOutAssets.Sha256File(HeldOutAssets.ProfileV3Path));
        using var receipt = Receipt();
        Assert.Equal(ProvenProfileHash, receipt.RootElement.GetProperty("profileSha256").GetString());
        Assert.Equal(ProvenProfileHash, receipt.RootElement.GetProperty("profileHashAtRunStart").GetString());
    }

    [Fact]
    public void Q3_PrototypeAssetsFreezeHashValid()
    {
        var prototypesHash = HeldOutAssets.Sha256(PrototypeSectionJson());
        Assert.Equal(ProvenPrototypeHash, prototypesHash);
        Assert.Equal(ProvenPrototypeHash, Receipt().RootElement.GetProperty("prototypeProfileHash").GetString());
    }

    private static string PrototypeSectionJson()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(HeldOutAssets.ProfileV3Path));
        return json.RootElement.GetProperty("identity_prototypes").GetRawText();
    }

    // ── Q4–Q8 : primary safety gates ─────────────────────────────────────────

    [Fact]
    public void Q4_FalseRecoveryIsZero() => Assert.Equal(0.0, Metrics().GetProperty("falseRecoveryRate").GetDouble());

    [Fact]
    public void Q5_InsufficientEvidenceAdmissionIsZero() => Assert.Equal(0, Metrics().GetProperty("insufficientEvidenceAdmitted").GetInt32());

    [Fact]
    public void Q6_HardNegativeRejectionIsOne() => Assert.Equal(1.0, Metrics().GetProperty("hardNegativeRejectionRate").GetDouble());

    [Fact]
    public void Q7_ConflictViolationIsZero()
    {
        var previousOf = HeldOutContainerIdentityCorpusV3.Create().Cases
            .ToDictionary(c => c.CaseId, c => c.PreviousVerifiedIdentity);
        var violations = new List<string>();
        foreach (var (id, row) in Cases())
        {
            if (row.Accepted && previousOf[id] is { } prev && row.Predicted != prev)
            {
                violations.Add($"{id}: {row.Predicted} vs prev {prev}");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Q8_StructuralViolationIsZero()
    {
        var violations = Cases().Where(kv => kv.Value.Accepted && kv.Value.StructuralRejected).Select(kv => kv.Key).ToList();
        Assert.Empty(violations);
    }

    // ── Q9–Q12 : utility gates ───────────────────────────────────────────────

    [Fact]
    public void Q9_CorrectRecoveryAtLeastPointSeven()
    {
        var correct = Metrics().GetProperty("correctRecoveryRate").GetDouble();
        _output.WriteLine($"Q9 correctRecovery={correct:F4}");
        Assert.True(correct >= 0.70, $"CorrectRecovery {correct:F3} < 0.70");
    }

    [Fact]
    public void Q10_AbstentionRateBelowPointNine()
    {
        var abstention = Metrics().GetProperty("abstentionRate").GetDouble();
        Assert.True(abstention < 0.90, $"AbstentionRate {abstention:F3} >= 0.90");
    }

    [Fact]
    public void Q11_PerIdentityCorrectRecoveryAtLeastPointFive()
    {
        var rates = IdentityRecovery();
        foreach (var (identity, (count, total)) in rates)
        {
            var rate = (double)count / total;
            _output.WriteLine($"identity {identity}: {count}/{total} = {rate:F3}");
            Assert.True(rate >= 0.50, $"{identity} CorrectRecovery {rate:F3} < 0.50");
        }
    }

    [Fact]
    public void Q12_NoIdentityStarvation()
    {
        var rates = IdentityRecovery();
        // Starvation marker: an identity below 0.50 on a 12-positive corpus.
        Assert.All(rates, kv => Assert.True(kv.Value.Item1 >= 6, $"{kv.Key} starved: {kv.Value.Item1}/12"));
    }

    // ── Q13–Q16 : receipt / immutability / leakage ───────────────────────────

    [Fact]
    public void Q13_QualificationReceiptReproducible()
    {
        using var receipt = Receipt();
        using var report = Report();
        Assert.Equal(receipt.RootElement.GetProperty("corpusSha256").GetString(),
            report.RootElement.GetProperty("corpusSha256").GetString());
        Assert.Equal(receipt.RootElement.GetProperty("profileSha256").GetString(),
            report.RootElement.GetProperty("profileSha256").GetString());
        Assert.Equal("ContainerIdentity-heldout-v3", report.RootElement.GetProperty("corpusId").GetString());
        Assert.Equal(80, report.RootElement.GetProperty("corpusCaseCount").GetInt32());
        Assert.Equal(ProvenProfileHash, receipt.RootElement.GetProperty("profileSha256").GetString());
    }

    [Fact]
    public void Q14_ProfileUnchangedAfterRun()
    {
        var runStart = Receipt().RootElement.GetProperty("profileHashAtRunStart").GetString();
        Assert.Equal(runStart, HeldOutAssets.Sha256File(HeldOutAssets.ProfileV3Path));
    }

    [Fact]
    public void Q15_PrototypeAssetsUnchangedAfterRun()
    {
        var runStart = Receipt().RootElement.GetProperty("prototypeProfileHash").GetString();
        Assert.Equal(runStart, HeldOutAssets.Sha256(PrototypeSectionJson()));
    }

    [Fact]
    public void Q16_NoCaseOrPrototypeInstanceLeakage()
    {
        var corpusIdSet = HeldOutContainerIdentityCorpusV3.Create().Cases.Select(c => c.CaseId).ToHashSet();
        // No prior-case ids referenced; no old-case instance text sets reused.
        foreach (var id in corpusIdSet)
        {
            Assert.StartsWith("ho3-", id);
        }

        // v3 profile does not reference any corpus case ids.
        var v3Json = File.ReadAllText(HeldOutAssets.ProfileV3Path);
        Assert.DoesNotContain("ho3-", v3Json);
        Assert.DoesNotContain("ho2-", v3Json);
        Assert.DoesNotContain("ho-", v3Json);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed record CaseRow(string Predicted, bool Accepted, bool StructuralRejected);

    private static JsonDocument Report()
    {
        Assert.True(File.Exists(HeldOutAssets.QualificationReportV3Path),
            "qualification report missing — run the Python qualification pipeline first.");
        return JsonDocument.Parse(File.ReadAllText(HeldOutAssets.QualificationReportV3Path));
    }

    private static JsonDocument Receipt()
    {
        Assert.True(File.Exists(HeldOutAssets.QualificationReceiptV3Path),
            "qualification receipt missing (must be written before the corpus run).");
        return JsonDocument.Parse(File.ReadAllText(HeldOutAssets.QualificationReceiptV3Path));
    }

    private static JsonElement Metrics() => Report().RootElement.GetProperty("metrics").Clone();

    private static Dictionary<string, CaseRow> Cases()
    {
        using var report = Report();
        var map = new Dictionary<string, CaseRow>();
        foreach (var element in report.RootElement.GetProperty("cases").EnumerateArray())
        {
            map[element.GetProperty("caseId").GetString()!] = new CaseRow(
                element.GetProperty("predicted").GetString()!,
                element.GetProperty("accepted").GetBoolean(),
                element.TryGetProperty("structuralRejected", out var sr) && sr.GetBoolean());
        }

        return map;
    }

    private static Dictionary<string, (int Count, int Total)> IdentityRecovery()
    {
        using var report = Report();
        var result = new Dictionary<string, (int, int)>
        {
            ["DeveloperOptions"] = (0, 0),
            ["WifiSettings"] = (0, 0),
            ["NetworkAndInternet"] = (0, 0),
            ["SettingsRoot"] = (0, 0),
        };
        foreach (var element in report.RootElement.GetProperty("cases").EnumerateArray())
        {
            var expected = element.GetProperty("expected").GetString()!;
            if (expected == "None" || !result.ContainsKey(expected))
            {
                continue;
            }

            var (count, total) = result[expected];
            result[expected] = (count + (element.GetProperty("predicted").GetString() == expected ? 1 : 0), total + 1);
        }

        return result;
    }

    // ── evidence writer (env-gated, BEFORE the corpus run) ───────────────────

    [Fact]
    public void WriteQualificationV3Assets_WhenRequested()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_QUALIFICATION_V3_WRITE") != "1")
        {
            return;
        }

        var corpus = HeldOutContainerIdentityCorpusV3.Create();
        var corpusJson = HeldOutAssets.CanonicalCorpusJson(corpus);
        File.WriteAllText(HeldOutAssets.CorpusV3JsonPath, corpusJson);

        var profileSha = HeldOutAssets.Sha256File(HeldOutAssets.ProfileV3Path);
        var prototypeSha = HeldOutAssets.Sha256(PrototypeSectionJson());

        var manifest = new
        {
            schema = "uniclaw.semantic.heldoutCorpus.manifest.v1",
            corpusId = corpus.CorpusId,
            corpusVersion = "1",
            creationDate = "2026-08-30",
            caseCount = corpus.Cases.Length,
            positiveCount = corpus.Cases.Count(c => c.ExpectedCandidate != "None"),
            negativeCount = corpus.Cases.Count(c => c.ExpectedCandidate == "None"),
            profileId = "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3",
            profileHashAtCreation = profileSha,
            prototypeProfileHash = prototypeSha,
            identityDistribution = corpus.Cases.GroupBy(c => c.CaseId.Split('-')[1]).ToDictionary(g => g.Key, g => g.Count()),
            difficultyDistribution = corpus.Cases.GroupBy(c => c.Difficulty).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            sourceDistribution = corpus.Cases.GroupBy(c => c.Source).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            designatedInsufficientEvidenceIds = new[]
            {
                "ho3-dev-N2", "ho3-dev-N3", "ho3-wifi-N2", "ho3-wifi-N3",
                "ho3-net-N2", "ho3-net-N3", "ho3-root-N2", "ho3-root-N3",
            },
            generatorMethod = "Independently authored fresh Settings-app observations: new wordings (SSIDs such as HomeNet-5G, Access Point Names, Digital wellbeing, System update, simulated displays), new element combinations and scroll compositions, fresh generic/sibling ambiguity instances. NOT derived from tuning / former-heldout-v1 / v2 concrete instances; failure CATEGORIES reused, instances fresh.",
            isolationStatement = "Created AFTER SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3 freeze. The corpus did not participate in feature / embedding / prototype / anchor / margin / evidence-sufficiency / candidate-policy design, V1/V2/V3 debugging, prototype pruning, or safety hardening. Automatic isolation: element fingerprints disjoint from tuning+v1+v2 (Q1). Prototype semantic-concept overlap is expected; instance-level copying is prohibited (Q16).",
        };
        File.WriteAllText(HeldOutAssets.ManifestV3JsonPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        var receipt = new
        {
            schema = "uniclaw.semantic.qualificationReceipt.v1",
            profileId = "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3",
            profileSha256 = profileSha,
            profileHashAtRunStart = profileSha,
            prototypeProfileHash = prototypeSha,
            corpusId = corpus.CorpusId,
            corpusSha256 = HeldOutAssets.Sha256(corpusJson),
            embeddingModel = new { modelId = "BAAI/bge-small-en-v1.5", revision = "pinned-by-fastembed", dimension = 384, runtime = "fastembed+onnxruntime", precision = "fp32" },
            policyProfile = "CONTAINER_IDENTITY_POLICY_V2",
            evidenceSufficiencyProfile = "EVIDENCE_SUFFICIENCY_PROFILE_V2",
            retrievalBackend = "exact-in-memory-cosine-identity-max",
            minimumTop1Top2Margin = 0.05,
            benchmarkRunnerVersion = "run_held_out.py --profile v3 --corpus v3 (qualification mode)",
            testRevision = "SemanticProfileV3QualificationTests (this file)",
            timestamp = "2026-08-30",
            result = "PENDING",
        };
        File.WriteAllText(HeldOutAssets.QualificationReceiptV3Path,
            JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
    }
}