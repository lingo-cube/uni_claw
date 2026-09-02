using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;
using UniClaw.Semantic.Settings;

// FUSION/SEMANTIC FRAGMENT VERDICT REPLAY — evidence collection for
// SEMANTIC_FRAGMENT_VERDICT_CONSISTENCY_DIAGNOSTIC_GATE. Rebuilds a real
// accepted Display-child observation from the campaign stage evidence and
// replays: Project → per-occurrence facts → pattern-by-pattern predicate
// matrix (Pattern-5 IsDuplicatePrimaryRowRendering / LooksLikePreferenceRow /
// Correlate+IsNavigationRowShape / Pattern-7) → capability envelopes.
var stagePath = args.Length > 0 ? args[0] : "/tmp/p26-normalizer-pubfix-r4-stage.json";
long targetSeq = args.Length > 1 ? long.Parse(args[1]) : 10;

using var doc = JsonDocument.Parse(File.ReadAllText(stagePath));
var frames = doc.RootElement.GetProperty("frames").EnumerateArray();
var frame = frames.First(f => f.GetProperty("sequenceNumber").GetInt64() == targetSeq);

var elements = frame.GetProperty("fusedCandidates").EnumerateArray().Select(e =>
{
    var b = e.GetProperty("bounds");
    var bounds = new ElementBounds(
        b.GetProperty("X1").GetSingle(), b.GetProperty("Y1").GetSingle(),
        b.GetProperty("X2").GetSingle(), b.GetProperty("Y2").GetSingle());
    return new ObservedElement(
        e.TryGetProperty("text", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetString() ?? "" : "",
        null, e.GetProperty("index").GetInt32(), bounds,
        e.TryGetProperty("type", out var ty) && ty.ValueKind != JsonValueKind.Null ? ty.GetString() : null)
    {
        StableKey = e.TryGetProperty("rowId", out var r) && r.ValueKind != JsonValueKind.Null ? r.GetString() : null,
    };
}).ToImmutableArray();

var structured = (frame.TryGetProperty("structuredEvidence", out var se) ? se.EnumerateArray() : Array.Empty<JsonElement>().AsEnumerable()).Select(e =>
{
    string? S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
    bool? B(string n) => e.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetBoolean() : null;
    ElementBounds? bounds = null;
    if (e.TryGetProperty("bounds", out var b) && b.ValueKind == JsonValueKind.Object)
    {
        bounds = new ElementBounds(
            b.GetProperty("X1").GetSingle(), b.GetProperty("Y1").GetSingle(),
            b.GetProperty("X2").GetSingle(), b.GetProperty("Y2").GetSingle());
    }
    return new StructuredElementEvidence(
        S("class"), S("resourceId"), B("clickable"), B("checkable"), B("checkedState"),
        B("enabled"), B("focusable"), bounds,
        S("contentDescription"), S("sourceNodeIdentity"), S("rawText"), S("parentSourceNodeIdentity"));
}).ToImmutableArray();

var frameId = $"frame-{targetSeq}";
var observation = new Observation(elements, "com.uniclaw.fixture", targetSeq)
{
    StructuredElements = structured,
    Sources = ImmutableArray.Create(
        new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, targetSeq, frameId, 1080, 2400, "vision", "vision"),
        new ObservationSourceMetadata(ObservationSourceTier.AuxiliaryStructured, true, targetSeq, frameId, 1080, 2400, "adb", "adb")),
};

var context = SemanticObservationFactProjector.Project(observation);
var primaryFacts = context.Facts.Where(f => f.SourceTier == SemanticSourceTier.Primary).ToArray();
var auxFacts = context.Facts.Where(f => f.SourceTier == SemanticSourceTier.Auxiliary).ToArray();

Console.WriteLine($"== frame {targetSeq}: elements={elements.Length} structured={structured.Length} primaryFacts={primaryFacts.Length}");

// --- per-occurrence fact dump + Pattern-5 / LooksLikePreferenceRow / corroboration matrix ---
Console.WriteLine("\n== OCCURRENCE MATRIX (text_block provider rows + menu_item peers) ==");
Console.WriteLine("idx | provider | text | bounds(T,L,W,H) | P5:tm? peerCnt overlap | LooksLike(facts) | Corrob(aux) | rowsLikeNav | P7 ");
foreach (var group in primaryFacts.GroupBy(f => f.OccurrenceId, StringComparer.Ordinal).OrderBy(g => g.First().OccurrenceId))
{
    var facts = group.ToArray();
    var text = string.Join(" ", facts.SelectMany(f => new[] { f.RawText, f.RawContentDescription })).Trim();
    var provider = facts.Select(f => f.RawProviderType).FirstOrDefault(ft => !string.IsNullOrWhiteSpace(ft));
    var geom = facts.FirstOrDefault(f => f.Kind == SemanticObservationFactKind.Geometry)?.Bounds;
    var isTextBlock = string.Equals(provider, "text_block", StringComparison.OrdinalIgnoreCase);
    // resolve element index from the occurrence id (vision:<index> hash) by scanning elements
    var idx = -1;
    for (int i = 0; i < observation.Elements.Length; i++)
    {
        if (string.Equals(SemanticObservationFactProjector.CreateOccurrenceId("vision", i.ToString()), group.Key, StringComparison.Ordinal)) { idx = i; break; }
    }
    if (!isTextBlock) continue; // focus on text_block representation rows

    // ---- Pattern-5: IsDuplicatePrimaryRowRendering (replicated 1:1) ----
    string p5e = "";
    var peers = primaryFacts
        .Where(f => !string.Equals(f.OccurrenceId, group.Key, StringComparison.Ordinal)
            && string.Equals(f.RawText, text, StringComparison.Ordinal)
            && f.Bounds is { } pb && geom is { } gb && Overlaps(pb, gb)
            && (string.Equals(f.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase)))
        .Select(f => f.OccurrenceId).Distinct(StringComparer.Ordinal).ToArray();
    bool p5 = peers.Length == 1 && !string.IsNullOrWhiteSpace(text) && geom is not null;
    p5e = $"(text={!string.IsNullOrWhiteSpace(text)}, peerCnt={peers.Length}, overlap={geom is not null && peers.Length > 0})";

    // ---- LooksLikePreferenceRow (facts only) ----
    bool looksLike = facts.Any(f => !string.IsNullOrWhiteSpace(f.RawText)) &&
        facts.Any(f => f.RawClassName?.Contains("LinearLayout", StringComparison.OrdinalIgnoreCase) == true
            || string.Equals(f.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase)) &&
        facts.Any(f => !string.IsNullOrWhiteSpace(f.RawContentDescription) || f.PrimitiveState is not null || f.Bounds is not null);

    // ---- Corroboration (Correlate + IsNavigationRowShape) ----
    var corr = Correlate(auxFacts, facts.First(f => !string.IsNullOrWhiteSpace(f.RawText) || !string.IsNullOrWhiteSpace(f.RawContentDescription) || true));
    bool corrobNav = corr.Any(IsNavigationRowShape);

    // ---- Pattern-7 (subtitle below a classified preference row) ----
    string p7info = "-";
    bool p7 = false;
    var tt = facts.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.RawText));
    if (tt is not null && geom is { } subB)
    {
        foreach (var row in primaryFacts.GroupBy(f => f.OccurrenceId, StringComparer.Ordinal))
        {
            var rf = row.ToArray();
            if (rf.Any(f => string.Equals(f.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase)))
            {
                var rb = rf.FirstOrDefault(f => f.Kind == SemanticObservationFactKind.Geometry)?.Bounds;
                var rt = rf.Select(f => f.RawText).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                if (rb is null) continue;
                if (string.Equals(tt.RawText?.Trim(), rt?.Trim(), StringComparison.Ordinal)) continue;
                if (Math.Abs(subB.Left - rb.Left) > 0.05) continue;
                var gap = subB.Top - (rb.Top + rb.Height);
                var maxGap = rb.Height * 0.6;
                if (gap >= 0 && gap <= maxGap) { p7 = true; p7info = $"below '{rt}' (gap={gap:F4} max={maxGap:F4})"; break; }
            }
        }
    }

    string b = geom is null ? "-" : $"T={geom.Top:F4} L={geom.Left:F4} W={geom.Width:F4} H={geom.Height:F4}";
    string idxStr = idx < 0 ? "?" : idx.ToString();
    Console.WriteLine(
        $"{idxStr,3} | {provider,-12} | \"{text}\" | {b} | P5={p5}:{p5e} | LL={looksLike} | corrobNav={corrobNav} | P7={p7}:{p7info}");
    // DEBUG: candidate peer occurrence — Geometry fact presence + bounds
    foreach (var gOcc in primaryFacts.Where(f => !string.Equals(f.OccurrenceId, group.Key, StringComparison.Ordinal)
        && string.Equals(f.RawText, text, StringComparison.Ordinal)).GroupBy(f => f.OccurrenceId))
    {
        var gFacts = gOcc.ToArray();
        var gGeom = gFacts.FirstOrDefault(f => f.Kind == SemanticObservationFactKind.Geometry);
        string gi = gGeom?.Bounds is { } gb ? $"T={gb.Top:F4}L={gb.Left:F4}" : "<NO-GEOMETRY>";
        var srcTier = gFacts[0].SourceTier;
        // decode: which observation channel is this occurrence?
        string chan = "?";
        for (int ei = 0; ei < observation.Elements.Length; ei++)
            if (string.Equals(SemanticObservationFactProjector.CreateOccurrenceId("vision", ei.ToString()), gOcc.Key, StringComparison.Ordinal)) { chan = $"vision[{ei}]"; break; }
        for (int ei = 0; ei < observation.StructuredElements.Length; ei++)
            if (string.Equals(SemanticObservationFactProjector.CreateOccurrenceId("adb", ei.ToString()), gOcc.Key, StringComparison.Ordinal)) { chan = $"adb[{ei}]"; break; }
        Console.WriteLine($"      !! peer occurrence tier={srcTier} chan={chan} provider={gFacts[0].RawProviderType ?? "<null>"} geom={gi} textFacts={gFacts.Count(f => f.Kind == SemanticObservationFactKind.Text)}");
    }
}

// --- per-element fact inventory ---
Console.WriteLine("\n== PER-ELEMENT FACT INVENTORY ==");
for (int i = 0; i < observation.Elements.Length; i++)
{
    var el = observation.Elements[i];
    var oid = SemanticObservationFactProjector.CreateOccurrenceId("vision", i.ToString());
    var kinds = primaryFacts.Where(f => string.Equals(f.OccurrenceId, oid, StringComparison.Ordinal))
        .GroupBy(f => f.Kind).ToDictionary(g => g.Key, g => g.Count());
    string ks = string.Join(" ", kinds.Select(k => $"{k.Key}={k.Value}"));
    Console.WriteLine($"pos={i,2} type={el.PerceptionType ?? "-",-13} text=\"{el.Text}\" boundsValid={el.Bounds?.IsValid} {ks}");
}
var capability = new SettingsSemanticCapability();
var envelopes = await capability.InterpretAsync(context);
foreach (var env in envelopes)
{
    string kind = env.Candidate switch
    {
        ElementAffordanceCandidateEvidence a => $"affordance:{a.AffordanceKind} occ={a.OccurrenceId}",
        ContainerRelationCandidateEvidence r => $"relation:{r.RelationKind} occ={r.RelatedOccurrenceId}",
        _ => "other",
    };
    Console.WriteLine(kind);
}
foreach (var env in envelopes)
{
    if (env.Candidate is ElementAffordanceCandidateEvidence el)
    {
        var idx2 = -1;
        for (int i = 0; i < observation.Elements.Length; i++)
        {
            if (string.Equals(SemanticObservationFactProjector.CreateOccurrenceId("vision", i.ToString()), el.OccurrenceId, StringComparison.Ordinal)) { idx2 = i; break; }
        }
        var elm = idx2 >= 0 ? observation.Elements[idx2] : null;
        if (elm is not null && string.Equals(elm.PerceptionType, "text_block", StringComparison.Ordinal))
            Console.WriteLine($"idx={idx2} row={elm.StableKey} tb '{elm.Text}' -> {el.AffordanceKind}");
    }
}

static bool Overlaps(SemanticNormalizedBounds a, SemanticNormalizedBounds b)
    => a.Left <= b.Left + b.Width && b.Left <= a.Left + a.Width
       && a.Top <= b.Top + b.Height && b.Top <= a.Top + a.Height;

static SemanticObservationFact[] Correlate(IReadOnlyCollection<SemanticObservationFact> auxiliary, SemanticObservationFact primary)
{
    if (auxiliary.Count == 0) return [];
    SemanticObservationFact[] matches;
    if (!string.IsNullOrWhiteSpace(primary.RawText))
    {
        matches = auxiliary.Where(c => string.Equals(c.RawText, primary.RawText, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 1) return matches;
    }
    if (primary.Bounds is not null)
    {
        matches = auxiliary.Where(c => c.Bounds is { } cb && Overlaps(cb, primary.Bounds)).ToArray();
        if (matches.Length == 1) return matches;
    }
    return [];
}

static bool IsNavigationRowShape(SemanticObservationFact fact)
    => fact.Clickable == true
       && (fact.RawClassName?.Contains("LinearLayout", StringComparison.OrdinalIgnoreCase) == true
           || string.Equals(fact.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase)
           || string.Equals(fact.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase));
