using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Corpus;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// PROJECT_LEADER_SEMANTIC_PROFILE_V4_HELD_OUT_QUALIFICATION — proofs Q1..Q22.
///
/// Profile V4 frozen (profile / terminology / anchor / prototype hashes pinned
/// in the receipt BEFORE the corpus run). heldout-v4 is a fresh corpus that
/// never participated in any design; it receives only PASS or FAIL. Historical
/// qualification REDs (V2, V3) are preserved as evidence — this suite is the
/// CURRENT qualification.
/// </summary>
public sealed class SemanticProfileV4QualificationTests
{
    private readonly ITestOutputHelper _output;

    public SemanticProfileV4QualificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Frozen identity hashes (recorded before the corpus run).
    private const string ProfileHash = "09d9e058c8ee818227e0a17a2f64f64c4239e42ad188ec36d0e963e605b5f434";
    private const string TerminologyHash = "110a29477d8d9b277e8940ce4d6a54121c703adebb0661653eeda7ff96374818";
    private const string AnchorHash = "c7e18eec6ccbb058f35cceb1c98ce7ae1774e1a66c6ee34e16d50cda580a8e36"; // raw anchor section (canonical form pinned in the receipt BEFORE the run)
    private const string PrototypeHash = "dbd11e08470d5d0437383bb5fe66806588af4b3aab2e9dd6ab096218decd9324";

    // ── Q1 : corpus isolation ────────────────────────────────────────────────

    [Fact]
    public void Q1_HeldOutV4IsolationValid()
    {
        var heldOut = HeldOutContainerIdentityCorpusV4.Create();
        Assert.Equal("ContainerIdentity-heldout-v4", heldOut.CorpusId);
        Assert.Equal(96, heldOut.Cases.Length);
        Assert.Equal(56, heldOut.Cases.Count(c => c.ExpectedCandidate != "None"));
        Assert.Equal(40, heldOut.Cases.Count(c => c.ExpectedCandidate == "None"));

        using var manifest = JsonDocument.Parse(File.ReadAllText(HeldOutAssets.ManifestV4JsonPath));
        Assert.Equal(96, manifest.RootElement.GetProperty("caseCount").GetInt32());
        Assert.Contains("did not participate", manifest.RootElement.GetProperty("isolationStatement").GetString());

        // Fingerprint disjointness vs tuning + former-heldout-v1/v2/v3.
        var protectedFingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var corpus in ProtectedCorpora())
        {
            foreach (var c in corpus.Cases)
            {
                protectedFingerprints.Add(HeldOutAssets.ElementFingerprint(c.InputObservation));
            }
        }

        var collisions = heldOut.Cases
            .Select(c => (Case: c, Fp: HeldOutAssets.ElementFingerprint(c.InputObservation)))
            .Where(x => protectedFingerprints.Contains(x.Fp))
            .Select(x => x.Case.CaseId).ToList();
        Assert.True(collisions.Count == 0, "instance reuse vs tuning/v1/v2/v3: " + string.Join(", ", collisions));
        _output.WriteLine($"Q1: 96 cases isolated (0 fingerprint collisions)");
    }

    private static List<SemanticCorpus> ProtectedCorpora()
    {
        var list = new List<SemanticCorpus>
        {
            HeldOutContainerIdentityCorpus.Create(), HeldOutContainerIdentityCorpusV2.Create(),
            HeldOutContainerIdentityCorpusV3.Create(),
            DeveloperOptionsBenchmarkCorpus.Create(),
            ContainerIdentityCorpora.WifiSettings(), ContainerIdentityCorpora.NetworkAndInternet(),
            ContainerIdentityCorpora.SettingsRoot(),
        };
        list.AddRange(ExpandedContainerIdentityCorpora.AllGolden());
        list.Add(ExpandedContainerIdentityCorpora.RegressionCorpus());
        list.Add(ExpandedContainerIdentityCorpora.AdversarialCorpus());
        return list;
    }

    // ── Q2–Q7 : freeze ───────────────────────────────────────────────────────

    [Fact]
    public void Q2_ProfileV4Frozen() => Assert.Equal(ProfileHash, HeldOutAssets.Sha256File(HeldOutAssets.ProfileV4Path));

    [Fact]
    public void Q3_TerminologyProfileFrozen() => Assert.Equal(TerminologyHash, HeldOutAssets.Sha256File(
        HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_TERMINOLOGY_PROFILE_V1.json")));

    [Fact]
    public void Q4_AnchorProfileFrozen() => Assert.Equal(AnchorHash, HeldOutAssets.Sha256(AnchorSectionJson()));

    [Fact]
    public void Q5_PrototypeHashUnchanged() => Assert.Equal(PrototypeHash, HeldOutAssets.Sha256File(HeldOutAssets.ProfileV3Path));

    [Fact]
    public void Q6_EmbeddingIdentityUnchanged()
    {
        using var v4 = V4Profile();
        Assert.Equal("BAAI/bge-small-en-v1.5", v4.RootElement.GetProperty("embedding").GetProperty("model").GetProperty("model_id").GetString());
    }

    [Fact]
    public void Q7_PolicyUnchanged()
    {
        using var v4 = V4Profile();
        Assert.Equal(0.05, v4.RootElement.GetProperty("policy").GetProperty("minimumTop1Top2Margin").GetDouble());
        Assert.Equal("CONTAINER_IDENTITY_POLICY_V2", v4.RootElement.GetProperty("candidate_policy_profile").GetString());
    }

    // ── Q8–Q12 : primary safety gates ────────────────────────────────────────

    [Fact]
    public void Q8_FalseRecoveryIsZero() => Assert.Equal(0.0, Metrics().GetProperty("falseRecoveryRate").GetDouble());

    [Fact]
    public void Q9_InsufficientEvidenceAdmissionIsZero() => Assert.Equal(0, Metrics().GetProperty("insufficientEvidenceAdmitted").GetInt32());

    [Fact]
    public void Q10_HardNegativeRejectionIsOne() => Assert.Equal(1.0, Metrics().GetProperty("hardNegativeRejectionRate").GetDouble());

    [Fact]
    public void Q11_ConflictViolationIsZero()
    {
        var previousOf = HeldOutContainerIdentityCorpusV4.Create().Cases.ToDictionary(c => c.CaseId, c => c.PreviousVerifiedIdentity);
        foreach (var (id, row) in CaseRows())
        {
            if (row.Accepted && previousOf[id] is { } prev && row.Predicted != prev)
            {
                Assert.Fail($"{id}: {row.Predicted} conflicts with prev {prev}");
            }
        }
    }

    [Fact]
    public void Q12_StructuralViolationIsZero()
    {
        Assert.Empty(CaseRows().Where(kv => kv.Value.Accepted && kv.Value.StructuralRejected).Select(kv => kv.Key));
    }

    // ── Q13–Q16 : utility gates ──────────────────────────────────────────────

    [Fact]
    public void Q13_CorrectRecoveryAtLeastPointSeven()
    {
        var r = Metrics().GetProperty("correctRecoveryRate").GetDouble();
        _output.WriteLine($"Q13 correctRecovery={r:F4}");
        Assert.True(r >= 0.70, $"CorrectRecovery {r:F3} < 0.70");
    }

    [Fact]
    public void Q14_AbstentionRateBelowPointNine()
        => Assert.True(Metrics().GetProperty("abstentionRate").GetDouble() < 0.90);

    [Fact]
    public void Q15_PerIdentityCorrectRecoveryAtLeastPointFive()
    {
        foreach (var (identity, (count, total)) in IdentityRecovery())
        {
            var rate = (double)count / total;
            _output.WriteLine($"identity {identity}: {count}/{total} = {rate:F3}");
            Assert.True(rate >= 0.50, $"{identity} {rate:F3} < 0.50");
        }
    }

    [Fact]
    public void Q16_NoIdentityStarvation()
    {
        Assert.All(IdentityRecovery(), kv => Assert.True(kv.Value.Item1 >= 7, $"{kv.Key} starved: {kv.Value.Item1}/14"));
    }

    // ── Q17–Q19 : vocabulary generalization / concept collision ─────────────

    [Fact]
    public void Q17_LexicallyNovelPositiveRecoveryAtLeastPointSixFive()
    {
        var r = Metrics().GetProperty("lexicallyNovelPositiveRecovery").GetDouble();
        _output.WriteLine($"Q17 lexicallyNovelPositiveRecovery={r:F4}");
        Assert.True(r >= 0.65, $"LexicallyNovelPositiveRecovery {r:F3} < 0.65");
    }

    [Fact]
    public void Q18_ConceptCollisionNegative_FalseRecoveryZero()
        => Assert.Equal(0.0, Metrics().GetProperty("conceptCollisionNegativeFalseRecovery").GetDouble());

    [Fact]
    public void Q19_ConceptCollisionNegative_HardNegativeRejectionOne()
        => Assert.Equal(1.0, Metrics().GetProperty("conceptCollisionNegativeHardNegativeRejection").GetDouble());

    // ── Q20–Q22 : receipt / immutability / leakage ───────────────────────────

    [Fact]
    public void Q20_ReceiptReproducible()
    {
        using var receipt = Receipt();
        using var report = Report();
        Assert.Equal(ProfileHash, receipt.RootElement.GetProperty("profileSha256").GetString());
        Assert.Equal(TerminologyHash, receipt.RootElement.GetProperty("terminologyProfileHash").GetString());
        Assert.Equal(receipt.RootElement.GetProperty("corpusSha256").GetString(), report.RootElement.GetProperty("corpusSha256").GetString());
        Assert.Equal("ContainerIdentity-heldout-v4", report.RootElement.GetProperty("corpusId").GetString());
        Assert.Equal(96, report.RootElement.GetProperty("corpusCaseCount").GetInt32());
    }

    [Fact]
    public void Q21_ProfileAndFeatureAssetsUnchangedAfterRun()
    {
        var runStart = Receipt().RootElement.GetProperty("profileHashAtRunStart").GetString();
        Assert.Equal(runStart, HeldOutAssets.Sha256File(HeldOutAssets.ProfileV4Path));
        Assert.Equal(TerminologyHash, HeldOutAssets.Sha256File(
            HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_TERMINOLOGY_PROFILE_V1.json")));
    }

    [Fact]
    public void Q22_NoInstanceLeakage()
    {
        var v4Json = File.ReadAllText(HeldOutAssets.ProfileV4Path)
                      + File.ReadAllText(HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_TERMINOLOGY_PROFILE_V1.json"));
        Assert.DoesNotContain("ho4-", v4Json);
        Assert.DoesNotContain("ho3-", v4Json);
        Assert.DoesNotContain("ho2-", v4Json);
        Assert.DoesNotContain("ho-", v4Json);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed record CaseRow(string Predicted, bool Accepted, bool StructuralRejected);

    private static JsonDocument V4Profile()
        => JsonDocument.Parse(File.ReadAllText(HeldOutAssets.ProfileV4Path));

    private static string AnchorSectionJson()
    {
        using var v4 = V4Profile();
        return v4.RootElement.GetProperty("evidenceSufficiency").GetRawText();
    }

    private static JsonDocument Report()
    {
        Assert.True(File.Exists(HeldOutAssets.QualificationReportV4Path), "qualification report missing");
        return JsonDocument.Parse(File.ReadAllText(HeldOutAssets.QualificationReportV4Path));
    }

    private static JsonDocument Receipt()
    {
        Assert.True(File.Exists(HeldOutAssets.QualificationReceiptV4Path), "qualification receipt missing");
        return JsonDocument.Parse(File.ReadAllText(HeldOutAssets.QualificationReceiptV4Path));
    }

    private static JsonElement Metrics() => Report().RootElement.GetProperty("metrics").Clone();

    private static Dictionary<string, CaseRow> CaseRows()
    {
        using var report = Report();
        var map = new Dictionary<string, CaseRow>();
        foreach (var element in report.RootElement.GetProperty("cases").EnumerateArray())
        {
            map[element.GetProperty("caseId").GetString()!] = new CaseRow(
                element.GetProperty("predicted").GetString()!, element.GetProperty("accepted").GetBoolean(),
                element.TryGetProperty("structuralRejected", out var sr) && sr.GetBoolean());
        }

        return map;
    }

    private static Dictionary<string, (int, int)> IdentityRecovery()
    {
        using var report = Report();
        var result = new Dictionary<string, (int, int)>
        {
            ["DeveloperOptions"] = (0, 0), ["WifiSettings"] = (0, 0),
            ["NetworkAndInternet"] = (0, 0), ["SettingsRoot"] = (0, 0),
        };
        foreach (var element in report.RootElement.GetProperty("cases").EnumerateArray())
        {
            var expected = element.GetProperty("expected").GetString()!;
            if (expected == "None" || !result.ContainsKey(expected)) continue;
            var (count, total) = result[expected];
            result[expected] = (count + (element.GetProperty("predicted").GetString() == expected ? 1 : 0), total + 1);
        }

        return result;
    }

    // ── evidence writer (env-gated, BEFORE the corpus run) ───────────────────

    [Fact]
    public void WriteQualificationV4Assets_WhenRequested()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_QUALIFICATION_V4_WRITE") != "1")
        {
            return;
        }

        var corpus = HeldOutContainerIdentityCorpusV4.Create();
        var corpusJson = HeldOutAssets.CanonicalCorpusJson(corpus);
        File.WriteAllText(HeldOutAssets.CorpusV4JsonPath, corpusJson);

        var profileSha = HeldOutAssets.Sha256File(HeldOutAssets.ProfileV4Path);
        var terminologySha = HeldOutAssets.Sha256File(HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_TERMINOLOGY_PROFILE_V1.json"));
        var anchorSha = HeldOutAssets.Sha256(AnchorSectionJson());

        var manifest = new
        {
            schema = "uniclaw.semantic.heldoutCorpus.manifest.v1",
            corpusId = corpus.CorpusId,
            corpusVersion = "1",
            creationDate = "2026-08-30",
            caseCount = corpus.Cases.Length,
            positiveCount = corpus.Cases.Count(c => c.ExpectedCandidate != "None"),
            negativeCount = corpus.Cases.Count(c => c.ExpectedCandidate == "None"),
            profileId = "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4",
            profileHashAtCreation = profileSha,
            terminologyProfileHash = terminologySha,
            anchorProfileHash = anchorSha,
            prototypeProfileHash = PrototypeHash,
            identityDistribution = corpus.Cases.GroupBy(c => c.CaseId.Split('-')[1]).ToDictionary(g => g.Key, g => g.Count()),
            difficultyDistribution = corpus.Cases.GroupBy(c => c.Difficulty).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            sourceDistribution = corpus.Cases.GroupBy(c => c.Source).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            vocabularyNoveltyDistribution = new { lexicallyNovelPositive = HeldOutContainerIdentityCorpusV4.LexicallyNovelPositiveIds.Count },
            conceptCollisionDistribution = new { conceptCollisionNegative = HeldOutContainerIdentityCorpusV4.ConceptCollisionNegativeIds.Count },
            lexicallyNovelPositiveIds = HeldOutContainerIdentityCorpusV4.LexicallyNovelPositiveIds.OrderBy(x => x).ToArray(),
            conceptCollisionNegativeIds = HeldOutContainerIdentityCorpusV4.ConceptCollisionNegativeIds.OrderBy(x => x).ToArray(),
            designatedInsufficientEvidenceIds = new[]
            {
                "ho4-dev-N2", "ho4-dev-N3", "ho4-wifi-N2", "ho4-wifi-N3",
                "ho4-net-N2", "ho4-net-N3", "ho4-root-N2", "ho4-root-N3",
            },
            generatorMethod = "Independently authored fresh Settings-app observations: lexically-novel rows (Window animation scale, Wi-Fi charging, Preferred network type, SIM card lock, Quick tap, Now Playing, ...) that belong to known CONCEPT families but appear in no prototype/terminology/development corpus; fresh sponsor compositions; fresh concept-collision, generic, sibling, structural negatives. RealTrace = verbatim contiguous subsets of the captured root-scrolled frame (truth.json); Manual = fresh compositions; Synthetic = independent adversarial. Limited real-trace availability recorded honestly.",
            isolationStatement = "Created AFTER SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4 freeze. The corpus did not participate in terminology / concept / anchor / prototype / policy / embedding design, V1-V4 debugging or hardening. Automatic isolation: instance fingerprints disjoint from tuning+v1+v2+v3 (Q1); lexically-novel positives are surface-disjoint from the terminology profile (concept overlap is the exam, instance reuse is banned).",
            knownTerminologySurfaceOverlap = 0,
        };
        File.WriteAllText(HeldOutAssets.ManifestV4JsonPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        var receipt = new
        {
            schema = "uniclaw.semantic.qualificationReceipt.v1",
            profileId = "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4",
            profileSha256 = profileSha,
            profileHashAtRunStart = profileSha,
            featureRepresentationVersion = "FEATURE_REPRESENTATION_V2",
            terminologyProfileHash = terminologySha,
            semanticAnchorProfileHash = anchorSha,
            prototypeProfileHash = PrototypeHash,
            corpusId = corpus.CorpusId,
            corpusSha256 = HeldOutAssets.Sha256(corpusJson),
            embeddingModel = new { modelId = "BAAI/bge-small-en-v1.5", revision = "pinned-by-fastembed", dimension = 384, runtime = "fastembed+onnxruntime", precision = "fp32" },
            candidatePolicyProfile = "CONTAINER_IDENTITY_POLICY_V2",
            evidenceSufficiencyProfile = "EVIDENCE_SUFFICIENCY_PROFILE_V3",
            retrievalBackend = "exact-in-memory-cosine-identity-max",
            benchmarkRunnerVersion = "run_held_out.py --profile v4 --corpus v4 (qualification mode)",
            testRevision = "SemanticProfileV4QualificationTests (this file)",
            timestamp = "2026-08-30",
            result = "PENDING",
        };
        File.WriteAllText(HeldOutAssets.QualificationReceiptV4Path, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
    }
}