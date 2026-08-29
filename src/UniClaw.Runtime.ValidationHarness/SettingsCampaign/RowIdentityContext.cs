using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign;

/// <summary>
/// Run-scoped row identity memory (DESIGN-SPEC D2, AUDITED per
/// UNKNOWN_AFFORDANCE_BYPASS gate): the C# side owns the known-rows list.
/// StableKeys are assigned by TEXT + VERTICAL POSITION BAND — two elements
/// with the same text at DIFFERENT vertical positions are DIFFERENT physical
/// rows and receive DIFFERENT keys (audit finding: 'Appearance' section
/// header at y=0.65 and 'Appearance' label at y=0.70 are distinct rows).
/// Reset per Run.
/// </summary>
public sealed class RowIdentityContext
{
    private readonly Dictionary<string, string> _idToText = new(StringComparer.Ordinal);
    // Key = "normalizedText|positionBand" → row id
    private readonly Dictionary<string, string> _bandToId = new(StringComparer.Ordinal);
    private int _nextId = 1;

    // Vertical position band width: elements within this distance share a band.
    // Real Settings rows are ~0.04-0.06 apart; 0.03 separates distinct rows
    // while tolerating OCR jitter within the same row.
    private const float PositionBandWidth = 0.03f;

    /// <summary>
    /// Stabilizes an observation: for every element with text, finds or creates
    /// a stable row id (keyed by text + vertical band). Returns a NEW
    /// observation with all StableKeys set.
    /// </summary>
    public Observation Stabilize(Observation observation)
    {
        if (observation.Elements.IsDefaultOrEmpty)
            return observation;

        var builder = ImmutableArray.CreateBuilder<ObservedElement>(observation.Elements.Length);
        var changed = false;
        foreach (var element in observation.Elements)
        {
            if (string.IsNullOrWhiteSpace(element.Text))
            {
                builder.Add(element);
                continue;
            }

            if (element.StableKey is { Length: > 0 } existing)
            {
                // Confirmed row (matched by Python) — record it.
                _idToText[existing] = element.Text;
                var band = ComputeBandKey(element);
                _bandToId[band] = existing;
                builder.Add(element);
                continue;
            }

            // Element came back WITHOUT a row_id. Find or create an id keyed
            // by text + position band (same text at different positions =
            // different physical rows = different ids).
            var id = FindOrCreateId(element.Text, element.Bounds);
            builder.Add(element with { StableKey = id });
            changed = true;
        }

        return changed ? observation with { Elements = builder.ToImmutable() } : observation;
    }

    /// <summary>
    /// Finds an existing row id by normalized text + vertical band, or creates
    /// a new one. Same text at a different vertical band → different id.
    /// </summary>
    public string FindOrCreateId(string text, ElementBounds? bounds)
    {
        var norm = NormalizeText(text);
        var band = ComputeBandKey(text, bounds);
        if (_bandToId.TryGetValue(band, out var existing))
            return existing;
        var id = $"row_{_nextId++:D3}";
        _idToText[id] = text;
        _bandToId[band] = id;
        return id;
    }

    /// <summary>
    /// Serializes known rows as the X-Known-Rows header JSON (id → text).
    /// </summary>
    public string? ToHeaderJson()
    {
        if (_idToText.Count == 0)
            return null;
        var list = _idToText.Select(kv => new { id = kv.Key, text = kv.Value }).ToList();
        return JsonSerializer.Serialize(list, JsonOptions);
    }

    /// <summary>Clears all rows (new Run).</summary>
    public void Reset()
    {
        _idToText.Clear();
        _bandToId.Clear();
        _nextId = 1;
    }

    private static string ComputeBandKey(ObservedElement element) =>
        ComputeBandKey(element.Text, element.Bounds);

    private static string ComputeBandKey(string text, ElementBounds? bounds)
    {
        var norm = NormalizeText(text);
        // Band = floor(centerY / bandWidth). Same text in the same band = same row.
        // Same text in different bands = different physical rows.
        var centerY = bounds?.CenterY ?? 0f;
        var band = (int)(centerY / PositionBandWidth);
        return $"{norm}|{band}";
    }

    private static string NormalizeText(string text) =>
        string.Join("", text.ToLowerInvariant().Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
