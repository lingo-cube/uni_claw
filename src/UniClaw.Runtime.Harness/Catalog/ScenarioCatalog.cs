using System.Collections.Immutable;
using System.Text.Json;

namespace UniClaw.Runtime.Harness.Catalog;

/// <summary>Immutable scenario index — ID lookup, reference integrity, schema validation.</summary>
public sealed class ScenarioCatalog
{
    private readonly ImmutableDictionary<string, ScenarioCatalogEntry> _entries;

    private ScenarioCatalog(ImmutableDictionary<string, ScenarioCatalogEntry> entries)
    {
        _entries = entries;
    }

    /// <summary>Look up a scenario by ID.</summary>
    public ScenarioCatalogEntry? Get(string scenarioId)
        => _entries.GetValueOrDefault(scenarioId);

    /// <summary>Look up a scenario or throw.</summary>
    public ScenarioCatalogEntry GetRequired(string scenarioId)
        => _entries.TryGetValue(scenarioId, out var entry)
            ? entry
            : throw new KeyNotFoundException($"Scenario '{scenarioId}' not in catalog.");

    /// <summary>All registered scenario IDs.</summary>
    public ImmutableArray<string> ScenarioIds => [.. _entries.Keys];

    /// <summary>Load catalog from a manifest stream. Fails closed on any validation error.</summary>
    public static (ScenarioCatalog? Catalog, ImmutableArray<string> Errors) Load(Stream manifestStream)
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

            if (errors.Count > 0)
                return (null, errors.ToImmutable());

            return (new ScenarioCatalog(entries.ToImmutable()), ImmutableArray<string>.Empty);
        }
        catch (Exception ex)
        {
            errors.Add($"Catalog load failed: {ex.Message}");
            return (null, errors.ToImmutable());
        }
    }

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
    public string ScenarioId { get; init; } = "";
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Provenance { get; init; }
    public string[]? ReplayIds { get; init; }
    public string[]? FrameIds { get; init; }
}

/// <summary>Catalog manifest — deserialized from JSON.</summary>
file sealed class CatalogManifest
{
    public string? CatalogId { get; init; }
    public ScenarioCatalogEntry[]? Scenarios { get; init; }
}
