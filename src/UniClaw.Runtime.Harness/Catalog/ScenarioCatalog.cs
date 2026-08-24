using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Runtime.Harness.Replay;

namespace UniClaw.Runtime.Harness.Catalog;

/// <summary>Immutable scenario index — ID lookup, reference integrity, schema validation.</summary>
public sealed class ScenarioCatalog
{
    private readonly ImmutableDictionary<string, ScenarioCatalogEntry> _entries;
    private readonly ImmutableDictionary<string, HarnessAssetManifest> _manifests;

    private ScenarioCatalog(ImmutableDictionary<string, ScenarioCatalogEntry> entries, ImmutableDictionary<string, HarnessAssetManifest>? manifests = null)
    {
        _entries = entries;
        _manifests = manifests ?? ImmutableDictionary<string, HarnessAssetManifest>.Empty;
    }

    /// <summary>Look up a scenario by ID.</summary>
    public ScenarioCatalogEntry? Get(string scenarioId)
        => _entries.GetValueOrDefault(scenarioId);

    /// <summary>Look up a scenario or throw.</summary>
    public ScenarioCatalogEntry GetRequired(string scenarioId)
        => _entries.TryGetValue(scenarioId, out var entry)
            ? entry
            : throw new KeyNotFoundException($"Scenario '{scenarioId}' not in catalog.");

    /// <summary>Resolve a deep-validated scenario and its immutable referenced assets.</summary>
    public ScenarioCatalogResolution ResolveRequired(string scenarioId)
    {
        var entry = GetRequired(scenarioId);
        if (!_manifests.TryGetValue(scenarioId, out var manifest))
            throw new InvalidOperationException($"Scenario '{scenarioId}' was not loaded with deep validation.");
        var scenario = manifest.Scenarios.FirstOrDefault(x => x.ScenarioId == scenarioId)
            ?? throw new InvalidDataException($"Scenario '{scenarioId}' is missing from its validated manifest.");
        var replays = (entry.ReplayIds ?? []).Select(id => manifest.Replays.First(x => x.ReplayId == id)).ToImmutableArray();
        var frames = (entry.FrameIds ?? []).Select(id => manifest.Frames.First(x => x.FrameId == id)).ToImmutableArray();
        return new ScenarioCatalogResolution(entry, manifest, scenario, replays, frames);
    }

    /// <summary>All registered scenario IDs.</summary>
    public ImmutableArray<string> ScenarioIds => [.. _entries.Keys];

    /// <summary>Load catalog from a manifest stream. Fails closed on any validation error.</summary>
    public static (ScenarioCatalog? Catalog, ImmutableArray<string> Errors) Load(Stream manifestStream)
        => LoadCore(manifestStream, catalogRoot: null);

    /// <summary>Load and fail closed against the catalog root and referenced asset manifests.</summary>
    public static (ScenarioCatalog? Catalog, ImmutableArray<string> Errors) Load(Stream manifestStream, string catalogRoot)
        => LoadCore(manifestStream, catalogRoot);

    private static (ScenarioCatalog? Catalog, ImmutableArray<string> Errors) LoadCore(Stream manifestStream, string? catalogRoot)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        try
        {
            var manifest = JsonSerializer.Deserialize<CatalogManifest>(manifestStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            if (manifest is null)
            {
                errors.Add("Catalog manifest deserialized to null.");
                return (null, errors.ToImmutable());
            }

            if (string.IsNullOrWhiteSpace(manifest.CatalogId))
                errors.Add("Catalog manifest missing CatalogId.");
            if (catalogRoot is not null && manifest.SchemaVersion != HarnessAssetSchema.CurrentVersion)
                errors.Add($"Catalog manifest has unsupported schema version {manifest.SchemaVersion}.");
            if (manifest.Scenarios is null or { Length: 0 })
                errors.Add("Catalog manifest has no scenarios.");

            var entries = ImmutableDictionary.CreateBuilder<string, ScenarioCatalogEntry>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            if (manifest.Scenarios is not null)
            {
                foreach (var entry in manifest.Scenarios)
                {
                    if (string.IsNullOrWhiteSpace(entry.ScenarioId))
                    {
                        errors.Add("Scenario entry missing ScenarioId.");
                        continue;
                    }
                    if (!seenIds.Add(entry.ScenarioId))
                    {
                        errors.Add($"Duplicate ScenarioId: '{entry.ScenarioId}'.");
                        continue;
                    }
                    entries[entry.ScenarioId] = entry;
                }
            }

            var manifests = ImmutableDictionary.CreateBuilder<string, HarnessAssetManifest>();
            if (catalogRoot is not null)
                ValidateExternalAssets(entries.Values, catalogRoot, errors, manifests);

            if (errors.Count > 0)
                return (null, errors.ToImmutable());

            return (new ScenarioCatalog(entries.ToImmutable(), manifests.ToImmutable()), ImmutableArray<string>.Empty);
        }
        catch (Exception ex)
        {
            errors.Add($"Catalog load failed: {ex.Message}");
            return (null, errors.ToImmutable());
        }
    }

    private static void ValidateExternalAssets(IEnumerable<ScenarioCatalogEntry> entries, string root, ImmutableArray<string>.Builder errors, ImmutableDictionary<string, HarnessAssetManifest>.Builder manifests)
    {
        var rootFull = Path.GetFullPath(root);
        foreach (var entry in entries)
        {
            var entryErrors = ImmutableArray.CreateBuilder<string>();
            if (string.IsNullOrWhiteSpace(entry.ManifestPath)) { errors.Add($"Scenario '{entry.ScenarioId}' missing manifest path."); continue; }
            if (Path.IsPathRooted(entry.ManifestPath)) { errors.Add($"Scenario '{entry.ScenarioId}' manifest path must be relative."); continue; }
            string path;
            try { path = Path.GetFullPath(Path.Combine(rootFull, entry.ManifestPath)); }
            catch (Exception ex) { errors.Add($"Scenario '{entry.ScenarioId}' manifest path invalid: {ex.Message}"); continue; }
            if (!path.StartsWith(rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            { errors.Add($"Scenario '{entry.ScenarioId}' manifest path escapes catalog root."); continue; }
            if (!File.Exists(path)) { errors.Add($"Scenario '{entry.ScenarioId}' manifest not found: '{entry.ManifestPath}'."); continue; }
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (string.IsNullOrWhiteSpace(entry.ManifestSha256) || !HashMatches(bytes, entry.ManifestSha256))
                { errors.Add($"Scenario '{entry.ScenarioId}' manifest hash mismatch."); continue; }
                var asset = HarnessAssetManifestJson.Deserialize(Encoding.UTF8.GetString(bytes));
                foreach (var error in HarnessAssetManifestValidator.Validate(asset))
                    entryErrors.Add($"Scenario '{entry.ScenarioId}' asset manifest: {error}");
                var scenario = asset.Scenarios.FirstOrDefault(x => x.ScenarioId == entry.ScenarioId);
                if (scenario is null) { errors.Add($"Scenario '{entry.ScenarioId}' missing from asset manifest."); continue; }
                if (entry.SchemaVersion != HarnessAssetSchema.CurrentVersion)
                    entryErrors.Add($"Scenario '{entry.ScenarioId}' has unsupported schema version {entry.SchemaVersion}.");
                if (!IsKnownProvenance(entry.Provenance) || entry.Provenance != scenario.Provenance.ToString() || entry.Provenance == "LiveCapture")
                    entryErrors.Add($"Scenario '{entry.ScenarioId}' provenance is inconsistent.");

                var replays = asset.Replays.ToDictionary(x => x.ReplayId, StringComparer.Ordinal);
                var frames = asset.Frames.ToDictionary(x => x.FrameId, StringComparer.Ordinal);
                foreach (var id in entry.ReplayIds ?? [])
                {
                    if (!replays.TryGetValue(id, out var replay))
                        entryErrors.Add($"Scenario '{entry.ScenarioId}' references missing replay '{id}'.");
                    else if (replay.Provenance.ToString() != entry.Provenance || replay.Provenance == AssetMaturity.LiveCapture)
                        entryErrors.Add($"Scenario '{entry.ScenarioId}' replay '{id}' provenance is inconsistent.");
                }

                var referencedArtifacts = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in entry.FrameIds ?? [])
                {
                    if (!frames.TryGetValue(id, out var frame))
                    {
                        entryErrors.Add($"Scenario '{entry.ScenarioId}' references missing frame '{id}'.");
                        continue;
                    }
                    if (frame.Provenance.ToString() != entry.Provenance || frame.Provenance == AssetMaturity.LiveCapture)
                        entryErrors.Add($"Scenario '{entry.ScenarioId}' frame '{id}' provenance is inconsistent.");
                    referencedArtifacts.UnionWith(frame.ArtifactIds);
                }

                ValidateReferencedArtifactFiles(entry, asset, referencedArtifacts, path, rootFull, entryErrors);
                errors.AddRange(entryErrors);
                if (entryErrors.Count == 0) manifests[entry.ScenarioId] = asset;
            }
            catch (Exception ex) { errors.Add($"Scenario '{entry.ScenarioId}' manifest validation failed: {ex.Message}"); }
        }
    }

    private static void ValidateReferencedArtifactFiles(
        ScenarioCatalogEntry entry,
        HarnessAssetManifest manifest,
        IEnumerable<string> referencedArtifactIds,
        string manifestPath,
        string catalogRoot,
        ImmutableArray<string>.Builder errors)
    {
        var artifacts = manifest.Artifacts.ToDictionary(x => x.ArtifactId, StringComparer.Ordinal);
        var manifestDir = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidDataException("Catalog asset manifest has no parent directory.");
        foreach (var artifactId in referencedArtifactIds)
        {
            if (!artifacts.TryGetValue(artifactId, out var artifact))
                continue; // The manifest validator already reports this reference.
            if (artifact.Provenance.ToString() != entry.Provenance
                || artifact.Provenance == AssetMaturity.LiveCapture)
                errors.Add($"Scenario '{entry.ScenarioId}' artifact '{artifactId}' provenance is inconsistent.");
            if (string.IsNullOrWhiteSpace(artifact.RelativePath))
            {
                errors.Add($"Scenario '{entry.ScenarioId}' artifact '{artifactId}' missing relative path.");
                continue;
            }
            if (Path.IsPathRooted(artifact.RelativePath))
            {
                errors.Add($"Scenario '{entry.ScenarioId}' artifact '{artifactId}' path must be relative.");
                continue;
            }

            var artifactPath = Path.GetFullPath(Path.Combine(manifestDir, artifact.RelativePath));
            if (!IsWithinRoot(artifactPath, catalogRoot))
            {
                errors.Add($"Scenario '{entry.ScenarioId}' artifact '{artifactId}' path escapes catalog root.");
                continue;
            }
            if (!File.Exists(artifactPath))
            {
                errors.Add($"Scenario '{entry.ScenarioId}' artifact '{artifactId}' file was not found.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(artifact.ContentHash)
                || !HashMatches(File.ReadAllBytes(artifactPath), artifact.ContentHash))
                errors.Add($"Scenario '{entry.ScenarioId}' artifact '{artifactId}' hash mismatch.");
        }
    }

    private static bool IsWithinRoot(string path, string root)
        => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static bool HashMatches(byte[] bytes, string expected)
    {
        var value = expected.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? expected[7..] : expected;
        return value.Length == 64 && value.All(Uri.IsHexDigit) &&
            Convert.ToHexString(SHA256.HashData(bytes)).Equals(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownProvenance(string? value) => value is "Synthetic" or "RealitySeeded" or "RecordedReality" or "LiveCapture";

    /// <summary>Validate all entries have resolvable references.</summary>
    public ImmutableArray<string> ValidateReferences()
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        foreach (var (_, entry) in _entries)
        {
            if (entry.ReplayIds is not null)
            {
                foreach (var rid in entry.ReplayIds)
                {
                    // Replay IDs are external references — existence checked at replay time
                    if (string.IsNullOrWhiteSpace(rid))
                        errors.Add($"Scenario '{entry.ScenarioId}' has empty replay ID.");
                }
            }
        }
        return errors.ToImmutable();
    }
}

/// <summary>One entry in a scenario catalog.</summary>
public sealed record ScenarioCatalogEntry
{
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public string ScenarioId { get; init; } = "";
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Provenance { get; init; }
    public string[]? ReplayIds { get; init; }
    public string[]? FrameIds { get; init; }
    public string? ManifestPath { get; init; }
    public string? ManifestSha256 { get; init; }
}

public sealed record ScenarioCatalogResolution(
    ScenarioCatalogEntry Entry,
    HarnessAssetManifest Manifest,
    ScenarioAsset Scenario,
    ImmutableArray<ReplayAsset> Replays,
    ImmutableArray<FrameAsset> Frames);

/// <summary>Catalog manifest — deserialized from JSON.</summary>
file sealed class CatalogManifest
{
    public string? CatalogId { get; init; }
    public int SchemaVersion { get; init; } = HarnessAssetSchema.CurrentVersion;
    public ScenarioCatalogEntry[]? Scenarios { get; init; }
}
