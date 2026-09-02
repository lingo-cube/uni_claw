using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Semantic.Infrastructure.Retrieval;
using UniClaw.Runtime.Model;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// PROJECT_LEADER_SEMANTIC_PIPELINE_RESPONSIBILITY_SEPARATION — proofs T1..T8
/// plus the V1 compatibility proof and the latency record.
///
/// Goal: prove each layer's duty is REAL and independently testable:
/// Feature describes · Embedding represents · Prototype owns known identity
/// representation · Retrieval finds nearest candidates (no acceptance) ·
/// Candidate Policy Accepts/Abstains · Provider forms evidence.
/// NOT in scope: improving accuracy, fixing held-out failures, new safety
/// mechanisms, new models/backends, Runtime contract changes.
/// </summary>
public sealed class SemanticResponsibilitySeparationTests
{
    private readonly ITestOutputHelper _output;

    public SemanticResponsibilitySeparationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static ContainerIdentityPrototypeStore V1Store() =>
        ContainerIdentityPrototypeStore.FromSemanticPatterns(HeldOutAssets.FrozenInMemoryPatterns());

    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    // ── T1 : embedding never accepts ─────────────────────────────────────────

    [Fact]
    public void T1_EmbeddingProviderDoesNotAcceptCandidates()
    {
        // IEmbeddingProvider exposes exactly one capability: representation.
        var methods = typeof(IEmbeddingProvider).GetMethods().Select(m => m.Name).ToHashSet();
        Assert.Single(methods);
        Assert.Equal("Embed", methods.Single());
        Assert.DoesNotContain("Decide", methods);
        Assert.DoesNotContain("Retrieve", methods);

        var provider = new DeterministicSemanticEmbeddingProvider();
        var query = new FastSemanticFeatureExtractor().Extract(
            Obs(1, new ObservedElement("Wi-Fi", null, 0, null, "menu_item")));
        var vector = provider.Embed(query);

        Assert.Equal(64, vector.Dimension);
        Assert.Equal("deterministic-v1", vector.Model.ModelId);
        Assert.Equal(64, vector.Model.Dimension);
        Assert.True(vector.Values.All(v => v >= 0f));
    }

    // ── T2 : vector index has no threshold / acceptance ──────────────────────

    [Fact]
    public void T2_VectorIndexContainsNoThresholdOrPolicy()
    {
        Assert.DoesNotContain(
            typeof(ExactInMemoryVectorIndex).GetProperties(),
            p => p.Name.Contains("Threshold", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Decide",
            typeof(ExactInMemoryVectorIndex).GetMethods().Select(m => m.Name));

        // Behavioral: retrieval returns the FULL ranking, including candidates a
        // policy would reject. A weak query still yields ranked results from the
        // index itself (acceptance is not the index's job).
        var index = new ExactInMemoryVectorIndex(V1Store(), new DeterministicSemanticEmbeddingProvider());
        var weakQuery = new FastSemanticFeatureExtractor().Extract(
            Obs(2, new ObservedElement("Generic row", null, 0, null, "menu_item")));
        var ranked = index.Retrieve(new DeterministicSemanticEmbeddingProvider().Embed(weakQuery));
        Assert.NotNull(ranked);
        Assert.All(ranked, c => Assert.True(c.SimilarityScore is >= 0d and <= 1d));
    }

    // ── T3 : prototype store independent of retrieval ────────────────────────

    [Fact]
    public void T3_PrototypeStoreIsIndependentOfRetrievalBackend()
    {
        var storeMethods = typeof(ContainerIdentityPrototypeStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToHashSet();
        Assert.DoesNotContain("Retrieve", storeMethods);
        Assert.DoesNotContain("Match", storeMethods);
        Assert.DoesNotContain("Decide", storeMethods);

        var store = V1Store();
        Assert.Equal(4, store.All().Count);
        Assert.NotEmpty(store.Resolve("DeveloperOptions"));
        Assert.Empty(store.Resolve("UnknownIdentity"));

        // The same store feeds the reference matcher AND the exact vector index.
        var matcher = new DeterministicSemanticMatcher();
        var index = new ExactInMemoryVectorIndex(store, new DeterministicSemanticEmbeddingProvider());
        var query = new FastSemanticFeatureExtractor().Extract(
            Obs(3,
                new ObservedElement("Developer options", null, 0, null, "text"),
                new ObservedElement("Enable demo mode", null, 1, null, "menu_item")));

        Assert.NotEmpty(matcher.Match(query, store));
        Assert.NotEmpty(index.Retrieve(new DeterministicSemanticEmbeddingProvider().Embed(query)));
        Assert.Equal(4, store.All().Count); // neither backend mutated the store
    }

    // ── T4 : candidate policy independently testable ─────────────────────────

    [Fact]
    public void T4_CandidatePolicyAcceptsAndAbstainsIndependently()
    {
        var store = V1Store();
        var prototypesById = store.All().ToDictionary(p => p.PrototypeId);
        var policy = new ContainerIdentityCandidatePolicy();
        var devPrototypeId = store.All().Single(p => p.IdentityCandidate == "DeveloperOptions").PrototypeId;

        var top = new SemanticCandidate("DeveloperOptions", 0.9, devPrototypeId);

        var acceptContext = new CandidateEvaluationContext(
            new[] { top },
            prototypesById,
            PreviousVerifiedIdentity: "DeveloperOptions",
            ObservedElementTypes: ImmutableArray.Create("text", "menu_item"),
            ObservedTextTokenCount: 2,
            HasAnyEvidence: true);

        var accepted = policy.Decide(acceptContext);
        Assert.False(accepted.IsAbstain);
        Assert.Equal("DeveloperOptions", accepted.AcceptedCandidate!.IdentityCandidate);

        // Conflict rejection: top candidate != previous verified identity.
        var conflictContext = acceptContext with { PreviousVerifiedIdentity = "Security" };
        Assert.True(policy.Decide(conflictContext).IsAbstain);

        // Threshold: below acceptance threshold.
        var lowScoreContext = acceptContext with
        {
            RankedCandidates = new[] { top with { SimilarityScore = 0.1 } },
        };
        Assert.True(policy.Decide(lowScoreContext).IsAbstain);

        // Minimum evidence: no text/types/structural evidence at all.
        var noEvidenceContext = acceptContext with { HasAnyEvidence = false };
        Assert.True(policy.Decide(noEvidenceContext).IsAbstain);
    }

    // ── T5 : retrieval backend swap does not change policy contract ─────────

    [Fact]
    public void T5_RetrievalBackendSwapDoesNotChangePolicyContract()
    {
        var store = V1Store();
        var policy = new ContainerIdentityCandidatePolicy();

        // The policy's only input is CandidateEvaluationContext (backend-agnostic)
        // and its only output is CandidatePolicyResult: swapping the retrieval
        // backend cannot change the policy contract.
        var decide = typeof(IContainerIdentityCandidatePolicy).GetMethod("Decide")!;
        Assert.Equal(typeof(CandidateEvaluationContext), decide.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CandidatePolicyResult), decide.ReturnType);

        // Both backends' candidate outputs flow through the SAME policy instance
        // and produce a valid decision (accept or abstain) without exception.
        var query = new FastSemanticFeatureExtractor().Extract(
            Obs(4, new ObservedElement("Wi-Fi", null, 0, null, "menu_item")));
        var matcherCandidates = new DeterministicSemanticMatcher().Match(query, store);
        var indexCandidates = new ExactInMemoryVectorIndex(store, new DeterministicSemanticEmbeddingProvider())
            .Retrieve(new DeterministicSemanticEmbeddingProvider().Embed(query));

        var prototypesById = store.All().ToDictionary(p => p.PrototypeId);
        var contextFactory = new Func<IReadOnlyList<SemanticCandidate>, CandidateEvaluationContext>(candidates =>
            new CandidateEvaluationContext(candidates, prototypesById, "WifiSettings",
                ImmutableArray.Create("menu_item"), 1, HasAnyEvidence: true));

        foreach (var candidates in new[] { matcherCandidates, indexCandidates })
        {
            var result = policy.Decide(contextFactory(candidates));
            Assert.True(result.IsAbstain != (result.AcceptedCandidate is not null),
                "policy result must be exactly one of accept or abstain");
        }

        // Determinism: the same candidate list always yields the same decision.
        var first = policy.Decide(contextFactory(matcherCandidates));
        var second = policy.Decide(contextFactory(matcherCandidates));
        Assert.Equal(first.IsAbstain, second.IsAbstain);
    }

    // ── T6 : embedding provider swap does not change Runtime contract ───────

    [Fact]
    public async Task T6_EmbeddingProviderSwapDoesNotChangeRuntimeFacingContract()
    {
        Assert.Equal("UniClaw.Runtime.Capabilities.Perception.Semantic", typeof(ISemanticProvider).Namespace);

        var store = V1Store();
        var policy = new ContainerIdentityCandidatePolicy();
        var observation = Obs(5, new ObservedElement("Developer options", null, 0, null, "text"));

        var providerA = new FastSemanticContainerIdentityProvider(
            new DeterministicSemanticEmbeddingProvider(),
            new ExactInMemoryVectorIndex(store, new DeterministicSemanticEmbeddingProvider()),
            store,
            policy);
        var providerB = new FastSemanticContainerIdentityProvider(
            new AlternateDeterministicEmbeddingProvider(),
            new ExactInMemoryVectorIndex(store, new AlternateDeterministicEmbeddingProvider()),
            store,
            policy);

        foreach (var provider in new ISemanticProvider[] { providerA, providerB })
        {
            Assert.IsAssignableFrom<ISemanticProvider>(provider);
            var evidence = await provider.ResolveAsync(new ObservationContext(observation, "DeveloperOptions"));
            Assert.All(evidence, e => Assert.Equal(SemanticEvidenceKind.ContainerIdentity, e.Kind));
        }
    }

    private sealed class AlternateDeterministicEmbeddingProvider : IEmbeddingProvider
    {
        private readonly DeterministicSemanticEmbeddingProvider _inner = new(64);

        public EmbeddingVector Embed(ContainerSemanticQuery query)
        {
            // Deliberately different model identity, same shape.
            var inner = _inner.Embed(query);
            return new EmbeddingVector(inner.Values, new EmbeddingModelIdentity(
                "alternate-deterministic", "v2", 64, "in-process", "none"));
        }
    }

    // ── T7 : profile binds independent component identities ─────────────────

    [Fact]
    public void T7_ProfileBindsIndependentComponentIdentities()
    {
        var profile = SemanticPerceptionProfiles.SeparatedV1;
        Assert.Equal("SEMANTIC_CONTAINER_IDENTITY_PROFILE_V1", profile.ProfileId);
        Assert.Equal("v1-text-plus-type", profile.FeatureExtractionVersion);
        Assert.Equal("DeterministicSemantic", profile.EmbeddingProvider);
        Assert.Equal("deterministic-v1", profile.EmbeddingModel.ModelId);
        Assert.Equal("v1-canonical-signatures", profile.PrototypeProfileVersion);
        Assert.Equal("DeterministicMatcher", profile.RetrievalBackend);
        Assert.Equal("overlap", profile.SimilarityMetric);
        Assert.Equal("v1", profile.CandidatePolicyProfileVersion);

        // SemanticOptions must be able to express each identity independently.
        var options = new SemanticOptions();
        Assert.Equal(profile.ProfileId, options.PipelineProfileId);
        Assert.Equal(SemanticVectorBackend.InMemory, options.VectorBackend);
        Assert.Equal("Deterministic", options.Embedding.Provider);
        Assert.Equal("deterministic-v1", options.Embedding.Model.ModelId);
        Assert.Equal("v1-canonical-signatures", options.Prototype.ProfileVersion);
        Assert.Equal("v1", options.Policy.ProfileVersion);

        var overridden = new SemanticOptions
        {
            VectorBackend = SemanticVectorBackend.Faiss,
            Retrieval = new SemanticRetrievalOptions { Metric = "cosine", TopK = 3 },
            Embedding = new SemanticEmbeddingOptions { Provider = "BgeSmall" },
            Prototype = new SemanticPrototypeOptions { ProfileVersion = "v2-multi-state" },
            Policy = new SemanticPolicyOptions { ProfileVersion = "v2", AcceptanceThreshold = 0.5 },
            PipelineProfileId = "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2",
        };
        Assert.Equal("v2-multi-state", overridden.Prototype.ProfileVersion);
        Assert.Equal("BgeSmall", overridden.Embedding.Provider);
        Assert.Equal("cosine", overridden.Retrieval.Metric);
        Assert.Equal("SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2", overridden.PipelineProfileId);
    }

    // ── T8 : BGE is no longer a vector backend concept ──────────────────────

    [Fact]
    public void T8_BgeIsNotARetrievalBackendConcept()
    {
        Assert.Null(typeof(SemanticVectorBackend).GetField("Bge"));
        Assert.False(SemanticVectorIndexRegistry.IsSupported("BGE"));
        var constants = new[] { SemanticVectorBackend.InMemory, SemanticVectorBackend.Faiss, SemanticVectorBackend.Qdrant, SemanticVectorBackend.Milvus };
        Assert.DoesNotContain("BGE", (IEnumerable<string>)constants);
    }

    // ── compatibility proofs (legacy V1 reproducible via recomposition) ─────

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

    private sealed record Verdict(string CaseId, string Legacy, string Separated);

    private static async Task<List<Verdict>> CompareOnTuning(CandidatePolicyOptions separatedPolicyOptions)
    {
        var store = V1Store();
        var extractor = new FastSemanticFeatureExtractor();
        var matcher = new DeterministicSemanticMatcher();

        // Legacy reference: matcher + acceptance threshold (exact legacy index
        // arithmetic — the legacy profile had NO structural/conflict/min-evidence
        // rules on the C# side).
        var provider = new FastSemanticContainerIdentityProvider(store,
            new ContainerIdentityCandidatePolicy(separatedPolicyOptions));

        var verdicts = new List<Verdict>();
        foreach (var corpus in TuningCorpora())
        {
            foreach (var c in corpus.Cases)
            {
                var legacyCandidates = matcher.Match(extractor.Extract(c.InputObservation), store);
                var legacyTop = legacyCandidates.FirstOrDefault();
                var legacy = legacyTop is not null
                             && legacyTop.SimilarityScore >= HeldOutAssets.InMemoryMatchThreshold
                    ? legacyTop.IdentityCandidate
                    : "None";

                var evidence = await provider.ResolveAsync(
                    new ObservationContext(c.InputObservation, c.PreviousVerifiedIdentity));
                var separated = evidence.Length > 0 ? evidence[0].Candidate : "None";
                verdicts.Add(new Verdict(c.CaseId, legacy, separated));
            }
        }

        return verdicts;
    }

    [Fact]
    public async Task Compat_ThresholdOnlySeparatedReproducesLegacyExactly()
    {
        // Legacy Profile V1 (threshold-only) == separated pipeline with the same
        // threshold-only policy: identical decisions on every tuning case.
        var verdicts = await CompareOnTuning(new CandidatePolicyOptions
        {
            AcceptanceThreshold = HeldOutAssets.InMemoryMatchThreshold,
            StructuralCompatibility = false,
            PreviousIdentityConflictRejection = false,
            MinimumEvidenceAbstention = false,
        });

        var differences = verdicts.Where(v => v.Legacy != v.Separated).ToList();
        Assert.True(differences.Count == 0,
            "Threshold-only separated pipeline diverged from legacy: " +
            string.Join(", ", differences.Select(d => $"{d.CaseId}({d.Legacy}->{d.Separated})")));
        _output.WriteLine($"compat: {verdicts.Count} tuning cases, 0 divergences (threshold-only)");
    }

    [Fact]
    public async Task Compat_SeparatedV1DocumentsRuleDifferences()
    {
        // Separated V1 policy (threshold + structural + conflict + min-evidence)
        // vs legacy: differences are DOCUMENTED — the previously-BGE-profile-only
        // rules are now expressed in the C# policy layer. The set is pinned so no
        // future change moves it silently.
        var verdicts = await CompareOnTuning(new CandidatePolicyOptions
        {
            AcceptanceThreshold = HeldOutAssets.InMemoryMatchThreshold,
        });

        foreach (var d in verdicts.Where(v => v.Legacy != v.Separated))
        {
            _output.WriteLine($"diff {d.CaseId}: legacy={d.Legacy} separated={d.Separated}");
        }

        var differences = verdicts.Where(v => v.Legacy != v.Separated).Select(v => v.CaseId)
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            // Documented differences (tuning corpus, 4-identity canonical store):
            // the separated V1 policy now EXPRESSES the conflict rejection /
            // structural rules that previously lived only in the embedding
            // benchmark. This changes verdicts where legacy (threshold-only)
            // emitted a generic type-only 0.33–0.40 tie candidate or a text
            // match that conflicts with PreviousVerifiedIdentity — separated V1
            // fails closed (ABSTAIN). No held-out case is involved; behavior is
            // never "improved" by this gate, only re-documented.
            "adv-similar-page",
            "dev-D-wrong-page",
            "dev-E-similar-page",
            "dev-golden-D",
            "reg-wrong-container-rejection",
            "root-golden-B",
            "root-golden-C",
            "root-golden-D",
            "root-golden-E",
            "root-negative-001",
            "wifi-golden-D",
            "wifi-negative-001",
        };

        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal), differences);
        _output.WriteLine($"compat: {verdicts.Count} tuning cases; documented rule-driven differences = {differences.Count}");
    }

    // ── latency record (separated vs legacy) ─────────────────────────────────

    [Fact]
    public async Task Perf_SeparatedVsLegacyLatency()
    {
        var store = V1Store();
        var corpus = TuningCorpora().SelectMany(c => c.Cases).ToList();

        var legacyProvider = new FastSemanticContainerIdentityProvider(store,
            new ContainerIdentityCandidatePolicy(new CandidatePolicyOptions
            {
                AcceptanceThreshold = HeldOutAssets.InMemoryMatchThreshold,
                StructuralCompatibility = false,
                PreviousIdentityConflictRejection = false,
                MinimumEvidenceAbstention = false,
            }));
        var separatedProvider = new FastSemanticContainerIdentityProvider(store,
            new ContainerIdentityCandidatePolicy(new CandidatePolicyOptions
            {
                AcceptanceThreshold = HeldOutAssets.InMemoryMatchThreshold,
            }));

        var legacy = await Measure(legacyProvider, corpus);
        var separated = await Measure(separatedProvider, corpus);
        _output.WriteLine($"legacy  p50={legacy.Item1:F4}ms p95={legacy.Item2:F4}ms p99={legacy.Item3:F4}ms");
        _output.WriteLine($"sep     p50={separated.Item1:F4}ms p95={separated.Item2:F4}ms p99={separated.Item3:F4}ms");

        // Responsibility separation must not add significant overhead
        // (loose bound protects against pathological regressions only).
        Assert.True(separated.Item2 < 25.0, $"separated p95 {separated.Item2:F2}ms exceeded loose bound");
        Assert.True(separated.Item3 < 50.0, $"separated p99 {separated.Item3:F2}ms exceeded loose bound");
    }

    private static async Task<(double, double, double)> Measure(
        FastSemanticContainerIdentityProvider provider,
        List<SemanticCase> corpus)
    {
        var samples = new List<double>();
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var c in corpus)
            {
                var sw = Stopwatch.StartNew();
                _ = await provider.ResolveAsync(new ObservationContext(c.InputObservation, c.PreviousVerifiedIdentity));
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
        }

        var ordered = samples.OrderBy(x => x).ToArray();
        static double P(IReadOnlyList<double> sorted, double percentile)
        {
            var pos = (sorted.Count - 1) * percentile;
            var lo = (int)Math.Floor(pos);
            var hi = (int)Math.Ceiling(pos);
            return lo == hi ? sorted[lo] : sorted[lo] * (1 - pos + lo) + sorted[hi] * (pos - lo);
        }

        return (P(ordered, 0.50), P(ordered, 0.95), P(ordered, 0.99));
    }
}