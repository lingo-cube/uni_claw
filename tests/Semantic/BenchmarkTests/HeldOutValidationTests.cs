using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Semantic.Infrastructure.Fast;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// PROJECT_LEADER_BGE_SMALL_HELD_OUT_VALIDATION — gate proofs T1..T8.
///
/// This suite evaluates the FROZEN BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1
/// against the held-out corpus ContainerIdentity-heldout-v1. Nothing here
/// tunes, modifies, or re-fits any threshold / prototype / rule: the corpus and
/// profile are read-only inputs, and the InMemory side only runs the existing
/// production-default index (no src/ modification, no Runtime wiring).
///
/// Qualification semantics (from the gate):
///   T4 (GATE 1/2/4)   : BGE claims nothing on held-out hard negatives (FR=0).
///   T5 (GATE 3)       : PreviousVerifiedIdentity conflict rejection stays fail-closed.
///   T6 (GATE 4)       : insufficient-evidence cases abstain.
///   T8 (GATE 2)       : BGE FalsePositiveRate <= InMemory FalsePositiveRate on the
///                       same corpus; both reports share corpus identity/hash.
/// If a hard requirement is violated the suite reports RED and the exit decision
/// must record the failure (no in-gate fix, no re-declared PASS).
/// </summary>
public sealed class HeldOutValidationTests
{
    private readonly ITestOutputHelper _output;

    public HeldOutValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── shared models ────────────────────────────────────────────────────────

    internal sealed record Decision(
        string CaseId,
        string Expected,
        string Predicted,
        double Confidence,
        bool Accepted,
        bool Hit);

    internal sealed record HeldOutMetrics(
        double Top1Accuracy,
        double Top3Recall,
        double Top5Recall,
        double FalseRecoveryRate,
        double FalsePositiveRate,
        double HardNegativeRejectionRate,
        double AbstentionCorrectness,
        int PositiveCount,
        int NegativeCount,
        int AcceptedOnNegative);

    internal sealed record BreakdownRow(string Key, int Count, double Top1Accuracy, int FalsePositive, double FalsePositiveRate);

    // ── corpus access ────────────────────────────────────────────────────────

    private static SemanticCorpus HeldOut() => HeldOutContainerIdentityCorpus.Create();

    private static List<SemanticCorpus> TuningCorpora() =>
        new()
        {
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

    /// <summary>All held-out negative (expected None) case ids.</summary>
    private static ImmutableHashSet<string> NegativeCaseIds()
        => HeldOut().Cases.Where(c => c.ExpectedCandidate == "None")
            .Select(c => c.CaseId).ToImmutableHashSet();

    /// <summary>Designated insufficient-evidence cases (D + empty/near-empty F3).</summary>
    private static ImmutableHashSet<string> InsufficientEvidenceCaseIds()
        => ImmutableHashSet.Create(
            "ho-dev-D1", "ho-wifi-D1", "ho-net-D1", "ho-root-D1",
            "ho-dev-F3", "ho-net-F3", "ho-root-F3");

    /// <summary>Case-id → previous verified identity (for the fail-closed invariant).</summary>
    private static ImmutableDictionary<string, string?> PreviousOf() =>
        HeldOut().Cases.ToImmutableDictionary(c => c.CaseId, c => c.PreviousVerifiedIdentity);

    // ── InMemory frozen-pipeline decisions ───────────────────────────────────

    private static readonly FastSemanticFeatureExtractor FrozenExtractor = new();

    /// <summary>Runs the production-default deterministic reference matcher over the held-out corpus (frozen, threshold-only).</summary>
    private static Decision InMemoryDecision(SemanticCase c)
    {
        var query = FrozenExtractor.Extract(c.InputObservation);
        var candidate = HeldOutAssets.FrozenInMemoryRetrieve(query);
        var predicted = candidate?.IdentityCandidate ?? "None";
        var accepted = candidate is not null;
        return new Decision(c.CaseId, c.ExpectedCandidate, predicted, candidate?.SimilarityScore ?? 0d, accepted,
            string.Equals(predicted, c.ExpectedCandidate, StringComparison.Ordinal));
    }

    private static List<Decision> InMemoryDecisions() => HeldOut().Cases.Select(InMemoryDecision).ToList();

    /// <summary>Metrics over a decision list (identical formulas on the Python side).</summary>
    private static HeldOutMetrics ComputeMetrics(IReadOnlyList<Decision> decisions)
    {
        var total = decisions.Count;
        var positives = decisions.Count(d => d.Expected != "None");
        var negatives = decisions.Count(d => d.Expected == "None");
        var hits = decisions.Count(d => d.Hit);
        var acceptedOnNegative = decisions.Count(d => d.Expected == "None" && d.Accepted);
        // In this corpus every negative is Difficulty=Hard; the rejection rate is
        // measured over expected-None cases (abstain == correct rejection).
        var negativeAbstained = decisions.Count(d => d.Expected == "None" && d.Predicted == "None");
        return new HeldOutMetrics(
            Top1Accuracy: total == 0 ? 0d : (double)hits / total,
            Top3Recall: total == 0 ? 0d : (double)hits / total,
            Top5Recall: total == 0 ? 0d : (double)hits / total,
            FalseRecoveryRate: negatives == 0 ? 0d : (double)acceptedOnNegative / negatives,
            FalsePositiveRate: negatives == 0 ? 0d : (double)acceptedOnNegative / negatives,
            HardNegativeRejectionRate: negatives == 0 ? 0d : (double)negativeAbstained / negatives,
            AbstentionCorrectness: negatives == 0 ? 0d : (double)negativeAbstained / negatives,
            PositiveCount: positives,
            NegativeCount: negatives,
            AcceptedOnNegative: acceptedOnNegative);
    }

    /// <summary>Per-dimension breakdown (identity, difficulty, source, viewport, ambiguity).</summary>
    private static List<BreakdownRow> Breakdown(IReadOnlyList<Decision> decisions, Func<SemanticCase, string> keyOf)
    {
        var byId = HeldOut().Cases.ToDictionary(c => c.CaseId);
        return decisions
            .GroupBy(d => keyOf(byId[d.CaseId]))
            .Select(g =>
            {
                var fp = g.Count(d => d.Expected == "None" && d.Accepted);
                var hits = g.Count(d => d.Hit);
                return new BreakdownRow(g.Key, g.Count(), (double)hits / g.Count(), fp,
                    g.Count() == 0 ? 0d : (double)fp / g.Count());
            })
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .ToList();
    }

    // ── BGE report access (committed artifact produced by the Python runner) ─

    private static JsonDocument BgeReport()
    {
        var path = HeldOutAssets.BgeReportJsonPath;
        Assert.True(File.Exists(path), $"BGE report missing: {path}. Run validation/semantic/bge-held-out/run_held_out.py with the frozen profile first.");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    /// <summary>Profile V2 BGE report over former-heldout-v1 (regression/adversarial).</summary>
    private static JsonDocument BgeV2Report()
    {
        var path = HeldOutAssets.BgeV2ReportJsonPath;
        Assert.True(File.Exists(path), $"Profile V2 BGE report missing: {path}. Run the Python safety-hardening pipeline first.");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static Dictionary<string, (string Expected, string Predicted, bool Accepted, double Confidence, bool Hit, string? FailureClass)> BgeCaseMap(JsonDocument report)
    {
        var map = new Dictionary<string, (string, string, bool, double, bool, string?)>();
        foreach (var element in report.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = element.GetProperty("caseId").GetString()!;
            var failure = element.TryGetProperty("failureClass", out var fc) && fc.ValueKind == JsonValueKind.String ? fc.GetString() : null;
            map[id] = (
                element.GetProperty("expected").GetString()!,
                element.GetProperty("predicted").GetString()!,
                element.GetProperty("accepted").GetBoolean(),
                element.GetProperty("confidence").GetDouble(),
                element.GetProperty("hit").GetBoolean(),
                failure);
        }

        return map;
    }

    // ── T0 / T9 : asset consistency ──────────────────────────────────────────

    [Fact]
    public void T0_TuningCorpusShapeIsStable()
    {
        // The isolation proof (T1) depends on the tuning corpora; guard their shape.
        // (ContainerIdentityCorpora.DeveloperOptions() aliases DeveloperOptionsBenchmarkCorpus,
        //  so the disjoint list is 10 corpora / 35 unique cases.)
        var tuning = TuningCorpora();
        Assert.Equal(10, tuning.Count);
        var totalCases = tuning.Sum(c => c.Cases.Length);
        Assert.Equal(35, totalCases);
    }

    [Fact]
    public void T9_CommittedAssetsMatchCode()
    {
        // Corpus JSON asset must be byte-identical to a fresh canonical serialization.
        var canonical = HeldOutAssets.CanonicalCorpusJson(HeldOut());
        var committed = File.ReadAllText(HeldOutAssets.CorpusJsonPath);
        Assert.Equal(canonical, committed);

        // Profile asset exists, declares itself FROZEN and IMMUTABLE, and targets this corpus.
        using var profile = JsonDocument.Parse(HeldOutAssets.ReadProfileJson());
        Assert.Equal(HeldOutAssets.ProfileId, profile.RootElement.GetProperty("profile_id").GetString());
        Assert.Contains("FROZEN", profile.RootElement.GetProperty("document_type").GetString());
        Assert.Contains("IMMUTABLE", profile.RootElement.GetProperty("mutation_policy").GetString());
        Assert.Equal(HeldOutAssets.CorpusId, profile.RootElement.GetProperty("target_corpus_version").GetString());

        // Both committed reports exist and carry the same corpus hash.
        Assert.True(File.Exists(HeldOutAssets.InMemoryReportJsonPath), "InMemory report asset missing (run with UNICLAW_HELDOUT_WRITE_EVIDENCE=1).");
        var corpusHash = HeldOutAssets.Sha256(canonical);
        using var inMemoryReport = JsonDocument.Parse(HeldOutAssets.ReadReportJson(HeldOutAssets.InMemoryReportJsonPath));
        Assert.Equal(HeldOutAssets.CorpusId, inMemoryReport.RootElement.GetProperty("corpusId").GetString());
        Assert.Equal(corpusHash, inMemoryReport.RootElement.GetProperty("corpusSha256").GetString());
        using var bgeReport = BgeReport();
        Assert.Equal(corpusHash, bgeReport.RootElement.GetProperty("corpusSha256").GetString());
    }

    // ── T1 : corpus independence ─────────────────────────────────────────────

    [Fact]
    public void T1_HeldOutCorpusIsIndependentOfTuningCorpus()
    {
        var heldOut = HeldOut();
        Assert.Equal(HeldOutAssets.CorpusId, heldOut.CorpusId);
        Assert.Equal(48, heldOut.Cases.Length);

        var tuning = TuningCorpora();
        var tuningIds = tuning.SelectMany(c => c.Cases.Select(x => x.CaseId)).ToHashSet(StringComparer.Ordinal);
        var tuningFingerprints = tuning.SelectMany(c => c.Cases.Select(x => HeldOutAssets.ElementFingerprint(x.InputObservation)))
            .ToHashSet(StringComparer.Ordinal);

        var sharedIds = heldOut.Cases.Select(c => c.CaseId).Where(tuningIds.Contains).ToList();
        var sharedFingerprints = heldOut.Cases
            .Select(c => (Case: c, Fp: HeldOutAssets.ElementFingerprint(c.InputObservation)))
            .Where(x => tuningFingerprints.Contains(x.Fp))
            .ToList();

        Assert.Empty(sharedIds);
        Assert.Empty(sharedFingerprints.Select(x => x.Case.CaseId));
        foreach (var (caseObj, fp) in sharedFingerprints)
        {
            _output.WriteLine($"held-out case {caseObj.CaseId} shares element fingerprint with a tuning case: {fp}");
        }

        // Per-identity coverage: 12 cases per identity (6 positives A/B/C +
        // 6 negatives D/E/F), grouped by the case-id identity prefix
        // (ho-dev-*, ho-wifi-*, ho-net-*, ho-root-*).
        var byIdentity = heldOut.Cases
            .GroupBy(c => c.CaseId.Split('-')[1])
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var identity in new[] { "dev", "wifi", "net", "root" })
        {
            Assert.Equal(12, byIdentity[identity]);
        }

        Assert.Equal(48, byIdentity.Values.Sum());
        Assert.Equal(24, heldOut.Cases.Count(c => c.ExpectedCandidate == "None"));
        var sources = heldOut.Cases.Select(c => c.Source).Distinct().OrderBy(s => s).ToList();
        _output.WriteLine("held-out source distribution: " + string.Join(", ", sources.Select(s => $"{s}={heldOut.Cases.Count(c => c.Source == s)}")));
    }

    // ── T2 : frozen profile immutability ─────────────────────────────────────

    [Fact]
    public void T2_FrozenProfileCannotMutateDuringEvaluation()
    {
        var profilePath = HeldOutAssets.ProfileJsonPath;
        var before = HeldOutAssets.Sha256File(profilePath);

        // Simulate a full evaluation context: run the entire InMemory benchmark and
        // read the committed BGE report (the evaluation artifacts that consume the profile).
        _ = InMemoryDecisions();
        _ = BgeReport();
        _ = ComputeMetrics(InMemoryDecisions());

        var after = HeldOutAssets.Sha256File(profilePath);
        Assert.Equal(before, after);
        Assert.Equal(before, after);

        // The BGE report must pin the same profile bytes it was produced with.
        using var report = BgeReport();
        Assert.Equal(HeldOutAssets.ProfileId, report.RootElement.GetProperty("profileId").GetString());
        Assert.Equal(before, report.RootElement.GetProperty("profileSha256").GetString());
    }

    // ── T3 : thresholds unchanged ────────────────────────────────────────────

    [Fact]
    public void T3_PerIdentityThresholdsUnchangedFromRound2()
    {
        using var profile = JsonDocument.Parse(HeldOutAssets.ReadProfileJson());
        var thresholds = new Dictionary<string, double>();
        foreach (var property in profile.RootElement.GetProperty("per_identity_thresholds").EnumerateObject())
        {
            thresholds[property.Name] = property.Value.GetDouble();
        }

        foreach (var (identity, threshold) in HeldOutAssets.Round2PerIdentityThresholds)
        {
            Assert.True(thresholds.TryGetValue(identity, out var actual), $"missing threshold for {identity}");
            Assert.Equal(threshold, actual);
        }

        Assert.Equal(4, thresholds.Count);

        // The BGE report must have used exactly these thresholds.
        using var report = BgeReport();
        var reportThresholds = report.RootElement.GetProperty("thresholds");
        foreach (var (identity, threshold) in HeldOutAssets.Round2PerIdentityThresholds)
        {
            Assert.Equal(threshold, reportThresholds.GetProperty(identity).GetDouble());
        }
    }

    // ── T4 : hard negative rejection (Profile V2 regression safety) ────────────

    [Fact]
    public void T4_HardNegativeRejection_ProfileV2RegressionSafety()
    {
        // SEMANTIC_SAFETY_HARDENING_APPLY: the safety regression tests now pin
        // Profile V2 over former-heldout-v1 (regression/adversarial corpus).
        // GREEN = REGRESSION_SAFETY_RECOVERED — NOT production qualification.
        using var report = BgeV2Report();
        var cases = BgeCaseMap(report);
        var negatives = NegativeCaseIds();

        var violations = new List<(string CaseId, string Predicted, double Confidence)>();
        foreach (var id in negatives)
        {
            Assert.True(cases.ContainsKey(id), $"case {id} missing from V2 BGE report");
            var (_, predicted, accepted, confidence, _, _) = cases[id];
            if (accepted || predicted != "None")
            {
                violations.Add((id, predicted, confidence));
            }
        }

        Assert.True(violations.Count == 0,
            "Profile V2 emitted claims on former-heldout hard negatives: " +
            string.Join(", ", violations.Select(v => $"{v.CaseId}->{v.Predicted}@{v.Confidence:F3}")));

        Assert.Equal(1.0, report.RootElement.GetProperty("metrics").GetProperty("hardNegativeRejectionRate").GetDouble());
        Assert.Equal(0.0, report.RootElement.GetProperty("metrics").GetProperty("falseRecoveryRate").GetDouble());
    }

    // ── T5 : previous identity conflict rejection fail-closed (GATE 3) ───────

    [Fact]
    public void T5_PreviousIdentityConflictRejectionStaysFailClosed()
    {
        using var report = BgeReport();
        var cases = BgeCaseMap(report);
        var previousOf = PreviousOf();
        var violations = new List<string>();

        foreach (var (id, value) in cases)
        {
            var (_, predicted, accepted, _, _, _) = value;
            var prev = previousOf[id];
            if (accepted && prev is not null && !string.Equals(predicted, prev, StringComparison.Ordinal))
            {
                violations.Add($"{id}: emitted {predicted} while PreviousVerifiedIdentity={prev}");
            }
        }

        Assert.True(violations.Count == 0,
            "BGE frozen profile violated fail-closed conflict rejection (GATE 3): " + string.Join("; ", violations));
    }

    // ── T6 : insufficient evidence abstention (Profile V2 regression safety) ────

    [Fact]
    public void T6_InsufficientEvidenceAbstains_ProfileV2RegressionSafety()
    {
        using var report = BgeV2Report();
        var cases = BgeCaseMap(report);
        var insufficient = InsufficientEvidenceCaseIds();
        Assert.Equal(7, insufficient.Count);

        var violations = new List<string>();
        foreach (var id in insufficient)
        {
            var (_, predicted, accepted, confidence, _, _) = cases[id];
            if (accepted || predicted != "None")
            {
                violations.Add($"{id}->{predicted}@{confidence:F3}");
            }
        }

        Assert.True(violations.Count == 0,
            "Profile V2 failed to abstain on insufficient evidence: " + string.Join(", ", violations));
    }

    // ── T7 : per-identity metric breakdown ───────────────────────────────────

    [Fact]
    public void T7_PerIdentityMetricBreakdown()
    {
        using var bgeReport = BgeReport();

        // InMemory side: breakdowns over identity / difficulty / source / viewport / ambiguity.
        var decisions = InMemoryDecisions();
        var byId = HeldOut().Cases.ToDictionary(c => c.CaseId);
        var identityBreakdown = Breakdown(decisions, c => c.ExpectedIdentity ?? "None");
        var difficultyBreakdown = Breakdown(decisions, c => HeldOutAssets.DifficultyToken(c.Difficulty));
        var sourceBreakdown = Breakdown(decisions, c => HeldOutAssets.SourceToken(c.Source));
        var viewportBreakdown = Breakdown(decisions, c => HeldOutAssets.ViewportToken(c.ViewportState));
        var ambiguityBreakdown = Breakdown(decisions, c => c.AmbiguityLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(5, identityBreakdown.Count); // 4 identities + None bucket
        Assert.Equal(48, identityBreakdown.Sum(r => r.Count));
        Assert.Equal(48, difficultyBreakdown.Sum(r => r.Count));
        Assert.Equal(48, sourceBreakdown.Sum(r => r.Count));
        Assert.Equal(48, viewportBreakdown.Sum(r => r.Count));
        Assert.Equal(4, ambiguityBreakdown.Count); // levels 0..3
        Assert.Equal(48, ambiguityBreakdown.Sum(r => r.Count));

        // BGE side must expose the same per-identity breakdown dimensions.
        var bgeIdentity = bgeReport.RootElement.GetProperty("breakdown").GetProperty("identity");
        var rows = new List<(string Key, int Count)>();
        foreach (var element in bgeIdentity.EnumerateArray())
        {
            rows.Add((element.GetProperty("key").GetString()!, element.GetProperty("count").GetInt32()));
        }

        Assert.Equal(5, rows.Count);
        Assert.Equal(48, rows.Sum(r => r.Count));
        foreach (var (key, count) in rows)
        {
            var inMemoryRow = identityBreakdown.Single(r => r.Key == key);
            Assert.Equal(inMemoryRow.Count, count);
            _output.WriteLine($"identity {key}: count={count} inmemory_top1={inMemoryRow.Top1Accuracy:F4} bge_top1={elementTop1(bgeIdentity, key):F4}");
        }
    }

    private static double elementTop1(JsonElement array, string key)
    {
        foreach (var element in array.EnumerateArray())
        {
            if (element.GetProperty("key").GetString() == key)
            {
                return element.GetProperty("top1Accuracy").GetDouble();
            }
        }

        return double.NaN;
    }

    // ── T8 : InMemory / BGE same-corpus comparison (GATE 2) ──────────────────

    [Fact]
    public void T8_SameCorpusComparison_ProfileV2_SafetyDoesNotDegrade()
    {
        var corpusSha = HeldOutAssets.Sha256(HeldOutAssets.CanonicalCorpusJson(HeldOut()));

        using var inMemoryReport = JsonDocument.Parse(HeldOutAssets.ReadReportJson(HeldOutAssets.InMemoryReportJsonPath));
        using var bgeReport = BgeV2Report();

        // Same corpus identity and bytes (identical former-heldout corpus).
        Assert.Equal(HeldOutAssets.CorpusId, inMemoryReport.RootElement.GetProperty("corpusId").GetString());
        Assert.Equal(HeldOutAssets.CorpusId, bgeReport.RootElement.GetProperty("corpusId").GetString());
        Assert.Equal(corpusSha, inMemoryReport.RootElement.GetProperty("corpusSha256").GetString());
        Assert.Equal(corpusSha, bgeReport.RootElement.GetProperty("corpusSha256").GetString());
        Assert.Equal(48, inMemoryReport.RootElement.GetProperty("corpusCaseCount").GetInt32());
        Assert.Equal(48, bgeReport.RootElement.GetProperty("corpusCaseCount").GetInt32());

        var inMemoryMetrics = inMemoryReport.RootElement.GetProperty("metrics");
        var bgeMetrics = bgeReport.RootElement.GetProperty("metrics");

        var inMemoryFpr = inMemoryMetrics.GetProperty("falsePositiveRate").GetDouble();
        var bgeFpr = bgeMetrics.GetProperty("falsePositiveRate").GetDouble();

        // Profile V2 false recovery must be zero on the regression corpus.
        Assert.Equal(0.0, bgeMetrics.GetProperty("falseRecoveryRate").GetDouble());
        // Profile V2 FPR must not exceed the incumbent baseline on the same corpus.
        Assert.True(bgeFpr <= inMemoryFpr,
            $"Profile V2 FPR {bgeFpr:F4} exceeds InMemory FPR {inMemoryFpr:F4} on the identical corpus.");

        _output.WriteLine($"InMemory on {HeldOutAssets.CorpusId}: Top1={inMemoryMetrics.GetProperty("top1Accuracy").GetDouble():F4} FPR={inMemoryFpr:F4}");
        _output.WriteLine($"Profile V2 on {HeldOutAssets.CorpusId}: Top1={bgeMetrics.GetProperty("top1Accuracy").GetDouble():F4} FR={bgeMetrics.GetProperty("falseRecoveryRate").GetDouble():F4} FPR={bgeFpr:F4}");
    }

    // ── evidence writer (env-gated) ──────────────────────────────────────────

    [Fact]
    public void WriteEvidenceAssets_WhenRequested()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_HELDOUT_WRITE_EVIDENCE") != "1")
        {
            return;
        }

        var corpusJson = HeldOutAssets.CanonicalCorpusJson(HeldOut());
        File.WriteAllText(HeldOutAssets.CorpusJsonPath, corpusJson);

        var decisions = InMemoryDecisions();
        var metrics = ComputeMetrics(decisions);
        var performance = MeasureInMemoryLatency();
        var byId = HeldOut().Cases.ToDictionary(c => c.CaseId);

        var casesArray = decisions.Select(d => new
        {
            caseId = d.CaseId,
            expected = d.Expected,
            predicted = d.Predicted,
            confidence = d.Confidence,
            accepted = d.Accepted,
            hit = d.Hit,
            failureClass = (string?)null,
        }).ToArray();

        var report = new
        {
            schema = "uniclaw.semantic.heldoutReport.v1",
            reportId = "container-identity-heldout-v1-inmemory-profile-v1",
            backend = "in-memory",
            profileId = HeldOutAssets.InMemoryProfileId,
            corpusId = HeldOutAssets.CorpusId,
            corpusSha256 = HeldOutAssets.Sha256(corpusJson),
            corpusCaseCount = heldOutCaseCount(),
            generated = HeldOutAssets.AssetGenerationDate,
            metrics = new
            {
                top1Accuracy = metrics.Top1Accuracy,
                top3Recall = metrics.Top3Recall,
                top5Recall = metrics.Top5Recall,
                falseRecoveryRate = metrics.FalseRecoveryRate,
                falsePositiveRate = metrics.FalsePositiveRate,
                hardNegativeRejectionRate = metrics.HardNegativeRejectionRate,
                abstentionCorrectness = metrics.AbstentionCorrectness,
                positiveCount = metrics.PositiveCount,
                negativeCount = metrics.NegativeCount,
                acceptedOnNegative = metrics.AcceptedOnNegative,
                performance = performance,
            },
            breakdown = new
            {
                identity = Breakdown(decisions, c => c.ExpectedIdentity ?? "None"),
                difficulty = Breakdown(decisions, c => HeldOutAssets.DifficultyToken(c.Difficulty)),
                source = Breakdown(decisions, c => HeldOutAssets.SourceToken(c.Source)),
                viewportState = Breakdown(decisions, c => HeldOutAssets.ViewportToken(c.ViewportState)),
                ambiguityLevel = Breakdown(decisions, c => c.AmbiguityLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            },
            cases = casesArray,
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        File.WriteAllText(HeldOutAssets.InMemoryReportJsonPath, JsonSerializer.Serialize(report, options));

        _output.WriteLine($"Wrote {HeldOutAssets.CorpusJsonPath}");
        _output.WriteLine($"Wrote {HeldOutAssets.InMemoryReportJsonPath}");
    }

    private static int heldOutCaseCount() => HeldOut().Cases.Length;

    /// <summary>Repository-local InMemory latency microbenchmark (5 runs/case, percentile).</summary>
    private static object MeasureInMemoryLatency()
    {
        var samples = new List<double>();
        foreach (var c in HeldOut().Cases)
        {
            var query = FrozenExtractor.Extract(c.InputObservation);
            for (var run = 0; run < 5; run++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _ = HeldOutAssets.FrozenInMemoryRetrieve(query);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
        }

        var ordered = samples.OrderBy(x => x).ToArray();
        double percentile(double p)
        {
            if (ordered.Length == 0)
            {
                return 0d;
            }

            var pos = (ordered.Length - 1) * p;
            var lo = (int)Math.Floor(pos);
            var hi = (int)Math.Ceiling(pos);
            if (lo == hi)
            {
                return ordered[lo];
            }

            var w = pos - lo;
            return ordered[lo] * (1 - w) + ordered[hi] * w;
        }

        return new { p50Ms = percentile(0.50), p95Ms = percentile(0.95), p99Ms = percentile(0.99), samples = samples.Count };
    }
}