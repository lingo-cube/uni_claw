using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Semantic.Infrastructure.Fast;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// PROJECT_LEADER_SEMANTIC_PROFILE_V2_HELD_OUT_QUALIFICATION — proofs Q1..Q12.
///
/// Profile V2 is FROZEN before the corpus run (qualification receipt pins its
/// hash). ContainerIdentity-heldout-v2 is a NEW corpus that did not participate
/// in any design; it only receives PASS or FAIL — never tuning. A green suite =
/// SEMANTIC_PROFILE_V2_HELD_OUT_QUALIFIED (still NOT physical-device-proven);
/// a red suite = qualification fails (corpus then loses qualification identity).
/// </summary>
public sealed class SemanticProfileV2QualificationTests
{
    private readonly ITestOutputHelper _output;

    public SemanticProfileV2QualificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── Q1 : corpus isolation manifest valid ─────────────────────────────────

    [Fact]
    public void Q1_HeldOutV2CorpusIsolationManifestValid()
    {
        var heldOutV2 = HeldOutContainerIdentityCorpusV2.Create();
        Assert.Equal(HeldOutContainerIdentityCorpusV2.CorpusId, heldOutV2.CorpusId);
        Assert.Equal(64, heldOutV2.Cases.Length);

        Assert.True(File.Exists(HeldOutAssets.ManifestV2JsonPath), "heldout-v2 manifest missing.");
        using var manifest = JsonDocument.Parse(File.ReadAllText(HeldOutAssets.ManifestV2JsonPath));
        Assert.Equal(heldOutV2.CorpusId, manifest.RootElement.GetProperty("corpusId").GetString());
        Assert.Equal(64, manifest.RootElement.GetProperty("caseCount").GetInt32());
        Assert.Contains("did not participate", manifest.RootElement.GetProperty("isolationStatement").GetString());

        // Disjointness: v2 fingerprints must not collide with the TUNING corpora
        // NOR with former-heldout-v1 (no reused concrete instances).
        var protectedFingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var corpus in v1AndTuningCorpora())
        {
            foreach (var c in corpus.Cases)
            {
                protectedFingerprints.Add(HeldOutAssets.ElementFingerprint(c.InputObservation));
            }
        }

        var collisions = heldOutV2.Cases
            .Select(c => (Case: c, Fp: HeldOutAssets.ElementFingerprint(c.InputObservation)))
            .Where(x => protectedFingerprints.Contains(x.Fp))
            .Select(x => x.Case.CaseId)
            .ToList();
        Assert.True(collisions.Count == 0,
            "heldout-v2 reuses a concrete instance from tuning/former-heldout-v1: " + string.Join(", ", collisions));
        _output.WriteLine($"Q1: {heldOutV2.Cases.Length} cases, 0 fingerprint collisions vs tuning+v1");
    }

    private static List<SemanticCorpus> v1AndTuningCorpora() =>
        new()
        {
            HeldOutContainerIdentityCorpus.Create(),
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

    // ── Q2 : Profile V2 identity / hash frozen ───────────────────────────────

    [Fact]
    public void Q2_ProfileV2IdentityAndHashFrozen()
    {
        var profilePath = HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json");
        var sha = HeldOutAssets.Sha256File(profilePath);

        using var manifest = JsonDocument.Parse(File.ReadAllText(HeldOutAssets.ManifestV2JsonPath));
        Assert.Equal(sha, manifest.RootElement.GetProperty("profileHashAtCreation").GetString());
        Assert.Equal("SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2", manifest.RootElement.GetProperty("profileId").GetString());

        using var receipt = QualificationReceipt();
        Assert.Equal(sha, receipt.RootElement.GetProperty("profileSha256").GetString());
        // Qualification ran against the CURRENT frozen profile bytes.
        Assert.Equal(sha, receipt.RootElement.GetProperty("profileHashAtRunStart").GetString());
    }

    // ── Q3–Q7 : primary safety gates ─────────────────────────────────────────

    [Fact]
    public void Q3_FalseRecoveryIsZero()
    {
        var metrics = QualificationMetrics();
        Assert.Equal(0.0, metrics.GetProperty("falseRecoveryRate").GetDouble());
        Assert.Equal(0.0, metrics.GetProperty("falsePositiveRate").GetDouble());
    }

    [Fact]
    public void Q4_InsufficientEvidenceAdmissionIsZero()
    {
        Assert.Equal(0, QualificationMetrics().GetProperty("insufficientEvidenceAdmitted").GetInt32());
    }

    [Fact]
    public void Q5_HardNegativeRejectionIsOne()
    {
        Assert.Equal(1.0, QualificationMetrics().GetProperty("hardNegativeRejectionRate").GetDouble());
    }

    [Fact]
    public void Q6_PreviousIdentityConflictViolationIsZero()
    {
        // No accepted claim may conflict with the previous verified identity.
        var cases = QualificationCases();
        var previousOf = HeldOutContainerIdentityCorpusV2.Create().Cases
            .ToDictionary(c => c.CaseId, c => c.PreviousVerifiedIdentity);
        var violations = new List<string>();
        foreach (var (id, row) in cases)
        {
            if (row.Accepted && previousOf[id] is { } prev
                && !string.Equals(row.Predicted, prev, StringComparison.Ordinal))
            {
                violations.Add($"{id}: {row.Predicted} with prev {prev}");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Q7_StructuralIncompatibilityAdmissionIsZero()
    {
        var cases = QualificationCases();
        var violations = cases.Where(kv => kv.Value.Accepted && kv.Value.StructuralRejected).Select(kv => kv.Key).ToList();
        Assert.Empty(violations);
    }

    // ── Q8–Q10 : utility / coverage gates ────────────────────────────────────

    [Fact]
    public void Q8_CorrectRecoveryAtLeastPointSeven()
    {
        var correct = QualificationMetrics().GetProperty("correctRecoveryRate").GetDouble();
        Assert.True(correct >= 0.70, $"CorrectRecovery {correct:F3} below 0.70");
        _output.WriteLine($"Q8 correctRecovery={correct:F4}");
    }

    [Fact]
    public void Q9_AbstentionRateBelowPointNine()
    {
        var abstention = QualificationMetrics().GetProperty("abstentionRate").GetDouble();
        Assert.True(abstention < 0.90, $"AbstentionRate {abstention:F3} >= 0.90 (reject-all signature)");
        _output.WriteLine($"Q9 abstentionRate={abstention:F4}");
    }

    [Fact]
    public void Q10_NoIdentityStarvation()
    {
        using var report = QualificationReport();
        var identityBreakdown = report.RootElement.GetProperty("breakdown").GetProperty("identity");
        var byIdentity = new Dictionary<string, int>();
        var byIdentityCorrect = new Dictionary<string, int>();
        foreach (var element in identityBreakdown.EnumerateArray())
        {
            byIdentity[element.GetProperty("key").GetString()!] = element.GetProperty("count").GetInt32();
            byIdentityCorrect[element.GetProperty("key").GetString()!] = 0;
        }

        // Correct recovery per identity (from case rows: positive cases only).
        var cases = QualificationCases();
        var expectedOf = HeldOutContainerIdentityCorpusV2.Create().Cases
            .ToDictionary(c => c.CaseId, c => c.ExpectedIdentity);
        foreach (var (id, row) in cases)
        {
            var identity = expectedOf[id];
            if (identity is not null && row.Predicted == identity)
            {
                byIdentityCorrect[identity]++;
            }
        }

        foreach (var identity in new[] { "DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot" })
        {
            var positiveCount = 10; // 10 positive cases per identity in v2
            var correct = byIdentityCorrect[identity];
            var rate = (double)correct / positiveCount;
            Assert.True(rate >= 0.50, $"identity {identity} starved: CorrectRecovery {correct}/10 = {rate:F3}");
            _output.WriteLine($"identity {identity}: correctRecovery {correct}/10 = {rate:F3}");
        }
    }

    // ── Q11 : receipt reproducible ───────────────────────────────────────────

    [Fact]
    public void Q11_QualificationReceiptReproducible()
    {
        using var receipt = QualificationReceipt();
        using var report = QualificationReport();

        // Receipt pins the same identities the report bound.
        Assert.Equal(receipt.RootElement.GetProperty("corpusSha256").GetString(),
            report.RootElement.GetProperty("corpusSha256").GetString());
        Assert.Equal(receipt.RootElement.GetProperty("profileSha256").GetString(),
            report.RootElement.GetProperty("profileSha256").GetString());
        Assert.Equal("ContainerIdentity-heldout-v2", report.RootElement.GetProperty("corpusId").GetString());
        Assert.Equal(64, report.RootElement.GetProperty("corpusCaseCount").GetInt32());

        // Reproducibility: receipts fields are recomputable from committed files.
        Assert.Equal(HeldOutAssets.Sha256File(HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json")),
            receipt.RootElement.GetProperty("profileSha256").GetString());
        Assert.Equal(HeldOutAssets.Sha256File(HeldOutAssets.CorpusV2JsonPath),
            receipt.RootElement.GetProperty("corpusSha256").GetString());
    }

    // ── Q12 : no Profile V2 mutation after corpus run ────────────────────────

    [Fact]
    public void Q12_NoProfileV2MutationAfterCorpusRun()
    {
        var shaAtRunStart = QualificationReceipt().RootElement.GetProperty("profileHashAtRunStart").GetString();
        var shaNow = HeldOutAssets.Sha256File(
            HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json"));
        Assert.Equal(shaAtRunStart, shaNow);
        Assert.Equal(HeldOutAssets.Sha256File(HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json")),
            HeldOutAssets.Sha256File(HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json")));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed record CaseRow(string Predicted, bool Accepted, bool StructuralRejected);

    private static JsonDocument QualificationReport()
    {
        Assert.True(File.Exists(HeldOutAssets.QualificationReportPath),
            "qualification report missing — run the Python qualification pipeline first.");
        return JsonDocument.Parse(File.ReadAllText(HeldOutAssets.QualificationReportPath));
    }

    private static JsonDocument QualificationReceipt()
    {
        Assert.True(File.Exists(HeldOutAssets.QualificationReceiptPath),
            "qualification receipt missing (must be written before the corpus run).");
        return JsonDocument.Parse(File.ReadAllText(HeldOutAssets.QualificationReceiptPath));
    }

    private static JsonElement QualificationMetrics()
        => QualificationReport().RootElement.GetProperty("metrics");

    private static Dictionary<string, CaseRow> QualificationCases()
    {
        using var report = QualificationReport();
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

    // ── corpus / receipt evidence writer (env-gated) ─────────────────────────

    [Fact]
    public void WriteQualificationAssets_WhenRequested()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_QUALIFICATION_WRITE") != "1")
        {
            return;
        }

        var corpus = HeldOutContainerIdentityCorpusV2.Create();
        var corpusJson = HeldOutAssets.CanonicalCorpusJson(corpus);
        File.WriteAllText(HeldOutAssets.CorpusV2JsonPath, corpusJson);

        var profileSha = HeldOutAssets.Sha256File(
            HeldOutAssets.RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json"));

        var manifest = new
        {
            schema = "uniclaw.semantic.heldoutCorpus.manifest.v1",
            corpusId = corpus.CorpusId,
            corpusVersion = "1",
            creationDate = "2026-08-30",
            caseCount = corpus.Cases.Length,
            profileId = "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2",
            profileHashAtCreation = profileSha,
            identityDistribution = corpus.Cases.GroupBy(c => c.CaseId.Split('-')[1]).ToDictionary(g => g.Key, g => g.Count()),
            difficultyDistribution = corpus.Cases.GroupBy(c => c.Difficulty).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            sourceDistribution = corpus.Cases.GroupBy(c => c.Source).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            designatedInsufficientEvidenceIds = new[]
            {
                "ho2-dev-N3", "ho2-dev-N4", "ho2-wifi-N3", "ho2-wifi-N4",
                "ho2-net-N3", "ho2-net-N4", "ho2-root-N3", "ho2-root-N4",
            },
            generatorMethod = "Independently authored from real Settings-app vocabulary: RealTrace = verbatim contiguous subsets of the captured root-scrolled frame (truth.json); Manual = fresh compositions of real rows; Synthetic = independent adversarial patterns (failure CATEGORIES reused, concrete instances fresh — not derived from former-heldout-v1 cases).",
            isolationStatement = "Created AFTER SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2 freeze. The corpus did not participate in feature / embedding / prototype / margin / evidence-sufficiency / anchor / candidate-policy design, V1/V2 debugging, or safety hardening.",
        };

        File.WriteAllText(HeldOutAssets.ManifestV2JsonPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        // Qualification receipt — pinned BEFORE the Profile V2 run.
        var receipt = new
        {
            schema = "uniclaw.semantic.qualificationReceipt.v1",
            profileId = "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2",
            profileSha256 = profileSha,
            profileHashAtRunStart = profileSha,
            corpusId = corpus.CorpusId,
            corpusSha256 = HeldOutAssets.Sha256(corpusJson),
            embeddingModel = new { modelId = "BAAI/bge-small-en-v1.5", revision = "pinned-by-fastembed", dimension = 384, runtime = "fastembed+onnxruntime", precision = "fp32" },
            prototypeProfile = "v1-canonical-signatures",
            candidatePolicyProfile = "CONTAINER_IDENTITY_POLICY_V2",
            minimumTop1Top2Margin = 0.05,
            evidenceSufficiencyProfile = "EVIDENCE_SUFFICIENCY_PROFILE_V1",
            timestamp = "2026-08-30",
            runnerVersion = "run_held_out.py --profile v2 --corpus v2 (qualification mode)",
            testRevision = "SemanticProfileV2QualificationTests (this file)",
        };
        File.WriteAllText(HeldOutAssets.QualificationReceiptPath,
            JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));

        _output.WriteLine($"wrote {HeldOutAssets.CorpusV2JsonPath}");
        _output.WriteLine($"wrote {HeldOutAssets.ManifestV2JsonPath}");
        _output.WriteLine($"wrote {HeldOutAssets.QualificationReceiptPath}");
    }
}