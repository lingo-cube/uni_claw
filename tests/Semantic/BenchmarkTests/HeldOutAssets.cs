using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// Held-out validation asset plumbing: repository-root resolution, canonical
/// deterministic JSON serialization of the held-out corpus, frozen profile
/// access, and the frozen InMemory comparison index (production default,
/// 4-identity pattern set). All assets under semantic-assets/ are read-only
/// evidence; nothing here touches Runtime or modifies any src/ file.
/// </summary>
public static class HeldOutAssets
{
    // ── frozen constants ─────────────────────────────────────────────────────

    /// <summary>Corpus id, must equal HeldOutContainerIdentityCorpus.CorpusId.</summary>
    public const string CorpusId = "ContainerIdentity-heldout-v1";

    /// <summary>Frozen profile id (file semantic-assets/profiles/BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1.json).</summary>
    public const string ProfileId = "BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1";

    /// <summary>Frozen InMemory comparison profile (production default index, no tuning).</summary>
    public const string InMemoryProfileId = "INMEMORY_PRODUCTION_DEFAULT_PROFILE_V1";

    /// <summary>Recovery/acceptance threshold used by the frozen InMemory index (existing default).</summary>
    public const double InMemoryMatchThreshold = 0.3;

    /// <summary>Generation date stamped into the canonical corpus JSON (deterministic asset).</summary>
    public const string AssetGenerationDate = "2026-08-30";

    /// <summary>Round-2 per-identity threshold table (docs/benchmarks/semantic-embedding-round2.md), frozen by T3.</summary>
    public static readonly ImmutableDictionary<string, double> Round2PerIdentityThresholds =
        ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, double>("DeveloperOptions", 0.30),
            new KeyValuePair<string, double>("WifiSettings", 0.30),
            new KeyValuePair<string, double>("NetworkAndInternet", 0.65),
            new KeyValuePair<string, double>("SettingsRoot", 0.30),
        });

    // ── repository root ──────────────────────────────────────────────────────

    /// <summary>Resolves the repository root by walking up from the test output directory.</summary>
    public static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                && Directory.Exists(Path.Combine(directory.FullName, "semantic-assets")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Repository root not found from " + AppContext.BaseDirectory);
    }

    /// <summary>Absolute path of a repo-relative asset path (forward slashes).</summary>
    public static string RepoPath(string relative)
        => Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Path of the committed held-out corpus JSON asset.</summary>
    public static string CorpusJsonPath => RepoPath("semantic-assets/heldout/ContainerIdentity-heldout-v1.json");

    /// <summary>Path of the committed frozen profile JSON asset.</summary>
    public static string ProfileJsonPath => RepoPath("semantic-assets/profiles/BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1.json");

    /// <summary>Path of the committed BGE report JSON asset (produced by the Python frozen-profile runner).</summary>
    public static string BgeReportJsonPath => RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v1.json");

    /// <summary>Path of the committed Profile V2 BGE report (safety-hardening runner, former-heldout-v1 as regression/adversarial).</summary>
    public static string BgeV2ReportJsonPath => RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v2.json");

    /// <summary>Path of the committed held-out v2 corpus JSON asset.</summary>
    public static string CorpusV2JsonPath => RepoPath("semantic-assets/heldout/ContainerIdentity-heldout-v2.json");

    /// <summary>Path of the held-out v2 manifest (isolation + profile-freeze record).</summary>
    public static string ManifestV2JsonPath => RepoPath("semantic-assets/heldout/manifest-heldout-v2.json");

    /// <summary>Path of the Profile V2 qualification receipt (pinned before the v2 run).</summary>
    public static string QualificationReceiptPath => RepoPath("semantic-assets/heldout/reports/profile-v2-qualification-receipt.json");

    /// <summary>Path of the Profile V2 qualification report over heldout-v2.</summary>
    public static string QualificationReportPath => RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v2-bge-small-profile-v2.json");

    /// <summary>Path of the committed held-out v3 corpus JSON asset.</summary>
    public static string CorpusV3JsonPath => RepoPath("semantic-assets/heldout/ContainerIdentity-heldout-v3.json");

    /// <summary>Path of the held-out v3 manifest (isolation + profile-freeze record).</summary>
    public static string ManifestV3JsonPath => RepoPath("semantic-assets/heldout/manifest-heldout-v3.json");

    /// <summary>Path of the Profile V3 qualification receipt (pinned before the v3 run).</summary>
    public static string QualificationReceiptV3Path => RepoPath("semantic-assets/heldout/reports/profile-v3-qualification-receipt.json");

    /// <summary>Path of the Profile V3 qualification report over heldout-v3.</summary>
    public static string QualificationReportV3Path => RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v3-bge-small-profile-v3.json");

    /// <summary>Path of the frozen Profile V3 asset.</summary>
    public static string ProfileV3Path => RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3.json");

    /// <summary>Path of the frozen Profile V4 asset.</summary>
    public static string ProfileV4Path => RepoPath("semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4.json");

    /// <summary>Path of the committed held-out v4 corpus JSON asset.</summary>
    public static string CorpusV4JsonPath => RepoPath("semantic-assets/heldout/ContainerIdentity-heldout-v4.json");

    /// <summary>Path of the held-out v4 manifest.</summary>
    public static string ManifestV4JsonPath => RepoPath("semantic-assets/heldout/manifest-heldout-v4.json");

    /// <summary>Path of the Profile V4 qualification receipt (pinned before the run).</summary>
    public static string QualificationReceiptV4Path => RepoPath("semantic-assets/heldout/reports/profile-v4-qualification-receipt.json");

    /// <summary>Path of the Profile V4 qualification report over heldout-v4.</summary>
    public static string QualificationReportV4Path => RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v4-bge-small-profile-v4.json");

    /// <summary>Path of the committed InMemory report JSON asset (produced by this test project).</summary>
    public static string InMemoryReportJsonPath => RepoPath("semantic-assets/heldout/reports/container-identity-heldout-v1-inmemory-profile-v1.json");

    // ── sha256 ───────────────────────────────────────────────────────────────

    /// <summary>SHA-256 hex of UTF-8 bytes.</summary>
    public static string Sha256(string utf8Content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(utf8Content))).ToLowerInvariant();

    /// <summary>SHA-256 hex of a file.</summary>
    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    // ── canonical corpus JSON ────────────────────────────────────────────────

    // DTOs keep declaration order = JSON order for byte-stable serialization.
    public sealed record CorpusElementDto(string Text, bool? SwitchState, int Index, string? PerceptionType);

    public sealed record CorpusCaseDto(
        string CaseId,
        string Package,
        long Sequence,
        string ExpectedCandidate,
        string? ExpectedIdentity,
        string Source,
        string Difficulty,
        string ViewportState,
        string AnchorState,
        int NoiseLevel,
        int AmbiguityLevel,
        double ScrollPosition,
        string? PreviousVerifiedIdentity,
        ImmutableArray<CorpusElementDto> Elements);

    public sealed record HeldOutCorpusDto(
        string Schema,
        string CorpusId,
        string CorpusVersion,
        string Gate,
        string Generated,
        int CaseCount,
        string TuningExclusionNote,
        ImmutableArray<CorpusCaseDto> Cases);

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Source enum → canonical JSON token.</summary>
    public static string SourceToken(SemanticCaseSource source) => source switch
    {
        SemanticCaseSource.RealTrace => "RealTrace",
        SemanticCaseSource.Manual => "Manual",
        SemanticCaseSource.Synthetic => "Synthetic",
        SemanticCaseSource.Regression => "Regression",
        _ => "RealTrace",
    };

    /// <summary>Viewport enum → canonical JSON token.</summary>
    public static string ViewportToken(SemanticViewportState state) => state switch
    {
        SemanticViewportState.TitleVisible => "TitleVisible",
        SemanticViewportState.TitleOffscreen => "TitleOffscreen",
        SemanticViewportState.Partial => "Partial",
        SemanticViewportState.WrongPage => "WrongPage",
        _ => "Unknown",
    };

    /// <summary>Anchor enum → canonical JSON token.</summary>
    public static string AnchorToken(SemanticVisibleAnchorState state) => state switch
    {
        SemanticVisibleAnchorState.AnchorVisible => "AnchorVisible",
        SemanticVisibleAnchorState.AnchorMissing => "AnchorMissing",
        _ => "Unknown",
    };

    /// <summary>Difficulty enum → canonical JSON token.</summary>
    public static string DifficultyToken(SemanticCaseDifficulty difficulty) => difficulty switch
    {
        SemanticCaseDifficulty.Easy => "Easy",
        SemanticCaseDifficulty.Medium => "Medium",
        _ => "Hard",
    };

    /// <summary>Serializes the held-out corpus to its canonical JSON text (byte-stable).</summary>
    public static string CanonicalCorpusJson(SemanticCorpus corpus)
    {
        var cases = corpus.Cases
            .Select(c => new CorpusCaseDto(
                c.CaseId,
                c.InputObservation.ForegroundApplication ?? "com.android.settings",
                c.InputObservation.SequenceNumber,
                c.ExpectedCandidate,
                c.ExpectedIdentity,
                SourceToken(c.Source),
                DifficultyToken(c.Difficulty),
                ViewportToken(c.ViewportState),
                AnchorToken(c.VisibleAnchorState),
                c.NoiseLevel,
                c.AmbiguityLevel,
                c.ScrollPosition,
                c.PreviousVerifiedIdentity,
                c.InputObservation.Elements
                    .Select(e => new CorpusElementDto(e.Text, e.SwitchState, e.Index, e.PerceptionType))
                    .ToImmutableArray()))
            .ToImmutableArray();

        var dto = new HeldOutCorpusDto(
            "uniclaw.semantic.heldoutCorpus.v1",
            corpus.CorpusId,
            "1",
            "PROJECT_LEADER_BGE_SMALL_HELD_OUT_VALIDATION",
            AssetGenerationDate,
            cases.Length,
            "Independent validation corpus. Excluded from every tuning/benchmark helper selection. " +
            "See HeldOutValidationTests.T1 for the disjointness proof against the tuning corpora.",
            cases);

        return JsonSerializer.Serialize(dto, CanonicalJsonOptions);
    }

    /// <summary>Reads the frozen profile JSON text.</summary>
    public static string ReadProfileJson()
        => File.ReadAllText(ProfileJsonPath);

    /// <summary>Reads a committed report JSON text.</summary>
    public static string ReadReportJson(string path)
        => File.ReadAllText(path);

    // ── frozen InMemory comparison (reference matcher + threshold) ────────────

    private static readonly Lazy<(DeterministicSemanticMatcher Matcher, ContainerIdentityPrototypeStore Store)> FrozenInMemory =
        new(() => new(
            new DeterministicSemanticMatcher(),
            ContainerIdentityPrototypeStore.FromSemanticPatterns(FrozenInMemoryPatterns())));

    /// <summary>
    /// Production-default style deterministic reference matcher: 4-identity
    /// SemanticPattern signature set mirroring the tuning corpora, overlap
    /// scoring + threshold. FROZEN — reproduces the committed InMemory report
    /// arithmetic exactly (the legacy index semantics: matcher + threshold,
    /// no structural/conflict rules on this side).
    /// </summary>
    public static SemanticCandidate? FrozenInMemoryRetrieve(ContainerSemanticQuery query)
    {
        var (matcher, store) = FrozenInMemory.Value;
        var top = matcher.Match(query, store).FirstOrDefault();
        return top is not null && top.SimilarityScore >= InMemoryMatchThreshold ? top : null;
    }

    public static ImmutableArray<SemanticPattern> FrozenInMemoryPatterns() =>
        ImmutableArray.Create(
                new SemanticPattern(
                    "DeveloperOptions",
                    "pattern:heldout:DeveloperOptions",
                    ImmutableArray.Create("Developer options", "Enable demo mode", "Show demo mode", "Automatic system updates"),
                    ImmutableArray.Create("text", "menu_item", "switch"),
                    ImmutableArray.Create("type:switch", "switch:True")),
                new SemanticPattern(
                    "WifiSettings",
                    "pattern:heldout:WifiSettings",
                    ImmutableArray.Create("Wi-Fi", "Connected", "AndroidWifi"),
                    ImmutableArray.Create("menu_item", "text_block"),
                    ImmutableArray.Create("type:text_block")),
                new SemanticPattern(
                    "NetworkAndInternet",
                    "pattern:heldout:NetworkAndInternet",
                    ImmutableArray.Create("Network & internet", "Cellular", "SIM cards"),
                    ImmutableArray.Create("menu_item"),
                    ImmutableArray.Create("type:menu_item")),
                new SemanticPattern(
                    "SettingsRoot",
                    "pattern:heldout:SettingsRoot",
                    ImmutableArray.Create("Settings", "Search settings", "Network & internet", "Connected devices", "Apps", "Notifications", "Battery", "Storage"),
                    ImmutableArray.Create("text", "text_block", "menu_item"),
                    ImmutableArray.Create("type:text", "type:menu_item")));

    /// <summary>Element-tuple fingerprint of an observation: (text, perception type, switch state) sorted.</summary>
    public static string ElementFingerprint(Observation observation)
    {
        var tuples = observation.Elements
            .Select(e => $"{e.Text}::{e.PerceptionType ?? "null"}::{e.SwitchState?.ToString() ?? "null"}")
            .OrderBy(x => x, StringComparer.Ordinal);
        return string.Join("|", tuples);
    }

    /// <summary>
    /// A single per-case decision produced by a frozen pipeline, in a schema
    /// shared by the InMemory (C#) and BGE (Python) report writers.
    /// </summary>
    public sealed record CaseDecision(
        string CaseId,
        string Expected,
        string Predicted,
        double Confidence,
        bool Accepted,
        bool Hit,
        string? FailureClass);
}