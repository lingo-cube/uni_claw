using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign;

/// <summary>
/// Run-local, CONTAINER-DOMAIN-SCOPED row identity memory
/// (PROJECT_LEADER_STABLEKEY_CONTAINER_DOMAIN_MINIMAL_REPAIR_GATE +
/// ROW_IDENTITY_UNRESOLVED_CONTAINER_TRANSITION_DOMAIN_SAFETY +
/// TRANSITION_SIGNAL_REFINEMENT gates).
///
/// StableKey remains a candidate correlation key — it is NOT SameSource proof —
/// and its legal correlation scope is ONE container domain. Only the ACTIVE
/// domain participates in matching:
///
///   * <see cref="BeginContainer"/> — driven ONLY by the VERIFIED container
///     identity (the runtime's own page resolution). Null identity keeps the
///     CURRENT domain (same-container title-off / interaction continuity); a
///     NEW identity opens a fresh empty domain (no parent row inheritance);
///     re-entry of a known identity reactivates the preserved domain (verified
///     parent return restores original keys). BEGIN_CONTAINER !=
///     CLEAR_ALL_HISTORY.
///   * <see cref="ToHeaderJson"/> — exports ONLY the active domain's known
///     rows; a candidate can never obtain a key belonging to another
///     container.
///   * <see cref="Stabilize"/> — accepts a python-confirmed StableKey ONLY
///     when it belongs to the active domain; a foreign (cross-container) key is
///     rejected and re-keyed inside the active domain (Z4 bug class closed).
///
/// P26-V2 run 6 residual 1 (cross-frame sticky label demotion): the header
/// additionally exports each known row's LATEST upstream PerceptionType
/// (additive ``type`` field).  The perception engine consumes it to keep a
/// previously-demoted section label NonInteractive across frames when
/// per-frame detection-height jitter would briefly recompose it as a phantom
/// menu row.  The type is caller-side memory only — it never changes row
/// identity, never grants actionability, and consumers that ignore the field
/// behave exactly as before.
///
/// TRANSITION_SIGNAL_REFINEMENT: no action-type-driven transition-pending
/// heuristic exists here (a Tap is NOT a container transition). Upstream
/// unresolved-location frames keep the current domain; the NULL_LOCATION-vs-
/// transition-pending distinction requires an authoritative execution fact
/// (ContainerTransition / ActiveContainerContext) that is Agent-internal and
/// NOT observable by this ValidationHarness seam at decision time — until such
/// a read-only seam is exposed, the unresolved-first-frame-of-a-child-entry
/// edge (parent rows still offered during a null frame) is a documented,
/// fail-closed-adjacent residual, NOT silently hidden by heuristics.
/// </summary>
public sealed class RowIdentityContext
{
    // Run-local registry: row id → canonical text (ids stay unique for the run;
    // their legal correlation scope changes, not their format).
    private readonly Dictionary<string, string> _idToText = new(StringComparer.Ordinal);

    // Run-local registry: row id → LATEST upstream PerceptionType (P26-V2 run 6
    // residual 1).  Additive memory only — never participates in identity
    // matching; exported so the perception engine can keep previously-demoted
    // section labels NonInteractive across frames (sticky label demotion).
    private readonly Dictionary<string, string?> _idToType = new(StringComparer.Ordinal);

    // Container domain → its local reconciliation state.
    private readonly Dictionary<string, ContainerDomain> _domains = new(StringComparer.Ordinal);

    private string? _activeDomain;
    private int _nextId = 1;

    // Vertical position band width: elements within this distance share a band.
    private const float PositionBandWidth = 0.03f;

    private sealed class ContainerDomain
    {
        internal readonly HashSet<string> KnownIds = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> BandToId = new(StringComparer.Ordinal);
    }

    /// <summary>CONTAINER-DOMAIN switch, driven by the VERIFIED container
    /// identity (runtime page resolution). Null identity keeps the CURRENT
    /// domain (same-container continuity); a NEW identity opens a FRESH domain;
    /// re-entry of a preserved identity reactivates it (verified parent return
    /// restores original keys).</summary>
    public void BeginContainer(string? identity)
    {
        if (identity is null)
            return;
        if (string.Equals(identity, _activeDomain, StringComparison.Ordinal))
            return;
        if (!_domains.TryGetValue(identity, out _))
            _domains[identity] = new ContainerDomain();
        _activeDomain = identity;
    }

    /// <summary>The active domain, or a legacy fallback single domain when no
    /// container switch was ever driven (callers before this gate).</summary>
    private ContainerDomain ActiveDomain
    {
        get
        {
            if (_activeDomain is null)
            {
                _activeDomain = "";
                if (!_domains.TryGetValue("", out var d))
                    d = _domains[""] = new ContainerDomain();
                return d;
            }
            return _domains[_activeDomain];
        }
    }

    /// <summary>
    /// Stabilizes an observation within the ACTIVE domain: for every element
    /// with text, finds or creates a stable row id keyed by text + vertical
    /// band. A python-confirmed StableKey is honored ONLY when it belongs to
    /// the active domain; a foreign container's key is rejected and re-keyed
    /// inside the active domain (cross-container inheritance = 0).
    /// </summary>
    public Observation Stabilize(Observation observation)
    {
        if (observation.Elements.IsDefaultOrEmpty)
            return observation;

        var domain = ActiveDomain;
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
                // Python-confirmed key: MUST belong to the active domain.
                if (domain.KnownIds.Contains(existing))
                {
                    _idToText[existing] = element.Text;
                    _idToType[existing] = element.PerceptionType;
                    var band = ComputeBandKey(element);
                    domain.BandToId[band] = existing;
                    builder.Add(element);
                    continue;
                }
                // fall through: re-key in the active domain
            }

            var id = FindOrCreateId(element.Text, element.Bounds);
            _idToType[id] = element.PerceptionType;
            builder.Add(element with { StableKey = id });
            changed = true;
        }

        return changed ? observation with { Elements = builder.ToImmutable() } : observation;
    }

    /// <summary>Finds an existing ACTIVE-DOMAIN row id by normalized text +
    /// vertical band, or creates a new one. Same text at a different vertical
    /// band → different id; different containers can never merge.</summary>
    public string FindOrCreateId(string text, ElementBounds? bounds)
    {
        var norm = NormalizeText(text);
        var band = ComputeBandKey(text, bounds);
        var domain = ActiveDomain;
        if (domain.BandToId.TryGetValue(band, out var existing))
            return existing;
        var id = $"row_{_nextId++:D3}";
        _idToText[id] = text;
        domain.KnownIds.Add(id);
        domain.BandToId[band] = id;
        return id;
    }

    /// <summary>Serializes ONLY the ACTIVE container domain's known rows as the
    /// X-Known-Rows header JSON (id → text → latest upstream type). Rows of
    /// other containers are never offered, so a candidate cannot text-match its
    /// way into another container's identity. The additive ``type`` field
    /// (P26-V2 run 6 residual 1) carries the row's LATEST upstream
    /// PerceptionType — sticky-label-demotion memory for the perception
    /// engine; consumers that ignore it behave exactly as before.</summary>
    public string? ToHeaderJson()
    {
        var domain = ActiveDomain;
        if (domain.KnownIds.Count == 0)
            return null;
        var list = _idToText
            .Where(kv => domain.KnownIds.Contains(kv.Key))
            .Select(kv => new
            {
                id = kv.Key,
                text = kv.Value,
                type = _idToType.TryGetValue(kv.Key, out var t) ? t : null,
            })
            .ToList();
        return list.Count == 0 ? null : JsonSerializer.Serialize(list, JsonOptions);
    }

    /// <summary>Clears ALL run-local state (new Run).</summary>
    public void Reset()
    {
        _idToText.Clear();
        _idToType.Clear();
        _domains.Clear();
        _activeDomain = null;
        _nextId = 1;
    }

    private static string ComputeBandKey(ObservedElement element) =>
        ComputeBandKey(element.Text, element.Bounds);

    private static string ComputeBandKey(string text, ElementBounds? bounds)
    {
        var norm = NormalizeText(text);
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