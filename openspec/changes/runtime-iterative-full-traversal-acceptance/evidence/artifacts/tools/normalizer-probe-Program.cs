using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

// FAITHFUL probe: rebuild observations (elements + vision source + admitted
// NavigationCandidate envelopes from the stage affordances) then bisect Normalize.
var doc = JsonDocument.Parse(File.ReadAllText("/tmp/p26-push-runU-stage.json"));
var frames = doc.RootElement.GetProperty("frames");
int[] accepted = [5, 8, 11, 14, 17, 20];
var obs = new List<Observation>();
foreach (var seq in accepted)
{
    var fr = frames.EnumerateArray().First(f => f.GetProperty("sequenceNumber").GetInt64() == seq);
    var cand = fr.GetProperty("fusedCandidates");
    var els = cand.EnumerateArray().Select(e =>
    {
        var b = e.GetProperty("bounds");
        return new ObservedElement(
            e.TryGetProperty("text", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetString() ?? "" : "",
            null, e.GetProperty("index").GetInt32(),
            new ElementBounds(b.GetProperty("X1").GetSingle(), b.GetProperty("Y1").GetSingle(),
                b.GetProperty("X2").GetSingle(), b.GetProperty("Y2").GetSingle()),
            e.TryGetProperty("type", out var ty) && ty.ValueKind != JsonValueKind.Null ? ty.GetString() : null)
        { StableKey = e.TryGetProperty("rowId", out var r) && r.ValueKind != JsonValueKind.Null ? r.GetString() : null };
    }).ToImmutableArray();
    var observation = new Observation(els, "com.android.settings", seq)
    {
        Sources = ImmutableArray.Create(new ObservationSourceMetadata(
            ObservationSourceTier.PrimaryVision, true, seq, $"frame-{seq}", 1080, 2400, "vision", "vision")),
    };
    var envelopes = ImmutableArray.CreateBuilder<SemanticEvidenceV2Envelope>();
    foreach (var a in fr.GetProperty("affordances").EnumerateArray())
    {
        if (a.GetProperty("classification").GetString() != "NavigationCandidate") continue;
        if (!a.TryGetProperty("eligibleForAuthorization", out var elig) || !elig.GetBoolean()) continue;
        var idx = a.GetProperty("elementIndex").GetInt32();
        if (idx < 0 || idx >= els.Length) continue;
        var occ = "nav:" + idx;
        envelopes.Add(new SemanticEvidenceV2Envelope(
            "probe:" + occ,
            new ElementAffordanceCandidateEvidence(occ, ElementAffordanceKind.NavigationCandidate,
                new SemanticSymbolReference("probe", "1", "navigation"), new SemanticObservationReference("obs", seq, $"frame-{seq}"),
                new SemanticScopeReference(occ),
                new SemanticProvenance("vision", SemanticSourceTier.Primary, "p", DateTimeOffset.UnixEpoch, $"frame-{seq}"),
                .9, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue)));
    }
    obs.Add(observation with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(envelopes.ToImmutable()) });
}
for (int n = 2; n <= obs.Count; n++)
{
    var r = SourceEquivalenceNormalizer.Normalize(ImmutableArray.CreateRange(obs.Take(n).ToArray()));
    Console.WriteLine($"prefix..seq{accepted[n-1]}: resolved={r.IsResolved} sources={(r.IsResolved ? r.UniqueSourceSignatures.Length : 0)}");
    if (!r.IsResolved)
    {
        foreach (var o in obs.Take(n))
            Console.WriteLine("  win" + o.SequenceNumber + ": " + string.Join(" ", o.Elements
                .Where(e => e.StableKey != null && e.PerceptionType == "menu_item")
                .OrderBy(e => e.Bounds!.CenterY)
                .Select(e => $"{e.StableKey}@{Math.Round(e.Bounds!.CenterY, 3)}")));
    }
}
