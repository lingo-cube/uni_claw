using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Simulation.Tests.ComponentReplay;

internal sealed record RecordedPagePair(string Id, PageCaptureObservation Before, PageCaptureObservation After,
    PageIdentityObservation BeforeIdentity, PageIdentityObservation AfterIdentity, SemanticPageCandidate Page,
    SemanticPageCandidate PreviousPage, IReadOnlyDictionary<string, string> SourceHashes);

/// <summary>Two buyers use the same recorded layout inputs. The adapter owns no
/// expected outputs, canonical World IDs, title heuristics or checked authority.
/// Capture time/API metadata are the explicitly synthetic SIM-012 replay context.</summary>
internal static class RecordedComponentInputs
{
    internal const string FixtureRoot = "tests/UniClaw.Simulation.Tests/Fixtures/ComponentReplay/";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private sealed record Asset(string Path, string Sha256);
    private sealed record Source(string Path, string Sha256, string CaptureId, DateTimeOffset ReplayTime);
    private sealed record Imported(UiHierarchyObservation Observation, ProposedOccurrence[] Candidates);
    private sealed record Pair(Asset Before, Asset After);
    private sealed record Identity(string Signature, string Title);
    private sealed record Identities(Identity Before, Identity After);
    private sealed record Case(string Id, Source Before, Source After, Pair Imported, Identities IdentityInput);
    private sealed record Corpus(int Width, int Height, Case[] Cases);

    internal static RecordedPagePair Load(string id, string layoutSource)
    {
        var root = GoldenPaths.RepoRoot();
        var corpus = JsonSerializer.Deserialize<Corpus>(File.ReadAllText(Path.Combine(root,
            "tests/UniClaw.Simulation.Tests/Fixtures/Replay/recent-recordings.json")), Json)!;
        var input = corpus.Cases.Single(c => c.Id == id);
        var hashes = new Dictionary<string, string>();
        string Read(Asset asset)
        {
            var bytes = File.ReadAllBytes(Path.Combine(root, asset.Path));
            if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != asset.Sha256)
                throw new InvalidDataException("recorded-source-hash-mismatch:" + asset.Path);
            hashes[asset.Path] = asset.Sha256;
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        PageCaptureObservation Capture(Source source, Asset asset, string stage)
        {
            Read(new(source.Path, source.Sha256));
            var imported = JsonSerializer.Deserialize<Imported>(Read(asset), Json)!;
            var m = imported.Observation.Metadata;
            if (m.CaptureId != source.CaptureId || m.CaptureTimestamp != source.ReplayTime)
                throw new InvalidDataException("recorded-capture-mismatch");
            var items = new List<SemanticLayoutItem>();
            foreach (var node in imported.Observation.Nodes)
            {
                if (!node.Bounds.TryGetObserved(out var b) || !b.IsValid || b.X1 < 0 || b.Y1 < 0
                    || b.X2 > corpus.Width || b.Y2 > corpus.Height) continue;
                var text = node.Text.TryGetObserved(out var t) && !string.IsNullOrWhiteSpace(t) ? t
                    : node.ContentDescription.TryGetObserved(out var d) ? d : null;
                if (!string.IsNullOrWhiteSpace(text)) items.Add(new("text:" + node.OccurrenceRef.LocalIndex, "text", text, b));
            }
            foreach (var (candidate, index) in imported.Candidates.Select((o, i) => (o, i)))
            {
                if (candidate.OwningContainerId is not null || candidate.SupportingEvidenceIds is { Count: > 0 })
                    throw new InvalidDataException("recorded-input-contains-canonical-owner-or-basis");
                if (candidate.Locator is not { } b || string.IsNullOrWhiteSpace(candidate.SemanticDescriptor)) continue;
                items.Add(new("candidate:" + index, candidate.Role, candidate.SemanticDescriptor, new(
                    (int)Math.Round(b.X1 * corpus.Width), (int)Math.Round(b.Y1 * corpus.Height),
                    (int)Math.Round(b.X2 * corpus.Width), (int)Math.Round(b.Y2 * corpus.Height))));
            }
            var derived = new PageCaptureObservation(m.CaptureId, m.ObservationCycleId!, m.SessionCorrelation,
                m.CaptureTimestamp, m.Space!, items);
            if (layoutSource == "typed-import") return derived;
            if (layoutSource != "frozen-capture") throw new ArgumentException("unknown layout source", nameof(layoutSource));
            var relative = FixtureRoot + "captures/" + id + "-" + stage + ".json";
            var bytes = File.ReadAllBytes(Path.Combine(root, relative));
            hashes[relative] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!PageObservationCodec.TryReadCapture(System.Text.Encoding.UTF8.GetString(bytes), out var frozen)
                || PageObservationCodec.Encode(derived) != PageObservationCodec.Encode(frozen!))
                throw new InvalidDataException("frozen-capture-differs-from-recorded-input:" + relative);
            return frozen!;
        }
        var before = Capture(input.Before, input.Imported.Before, "before");
        var after = Capture(input.After, input.Imported.After, "after");
        PageIdentityObservation IdentityFor(PageCaptureObservation capture, Identity identity) => new(capture.CaptureId,
            capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime, identity.Signature, identity.Title);
        var beforeIdentity = IdentityFor(before, input.IdentityInput.Before);
        var afterIdentity = IdentityFor(after, input.IdentityInput.After);
        return new(id, before, after, beforeIdentity, afterIdentity,
            new(id, afterIdentity.ObservedTitle!, "Controlled semantic catalogue for " + afterIdentity.ObservedTitle),
            new("previous:" + id, beforeIdentity.ObservedTitle!, "Controlled previous-page catalogue"), hashes);
    }

    internal static ObservationProposal CaptureProposal(PageCaptureObservation capture) => new(
        new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
        ObservationContext.External, new("sim014.recorded-layout", capture.CaptureTime,
            "capture:" + capture.CaptureId, ["recorded-xml-layout", "synthetic-replay-clock"]));

    internal static SemanticPerceptionRequest SemanticRequest(RecordedPagePair pair, EvidenceLedger ledger)
    {
        string Admit(PageCaptureObservation capture)
        {
            var admitted = ledger.Admit(CaptureProposal(capture));
            if (admitted.Admission.Decision != AdmissionDecision.Accepted || admitted.Record is null)
                throw new InvalidDataException("capture-not-admitted");
            return admitted.Record.EvidenceId;
        }
        var beforeId = Admit(pair.Before); var afterId = Admit(pair.After);
        SemanticObservationContext Context(PageCaptureObservation c, string basis) => new(c.CaptureId,
            c.ObservationCycleId, c.SessionCorrelation, c.CaptureTime, c.Space, c.Items, [basis]);
        return new("semantic:" + pair.Id,
            new(pair.After.SessionCorrelation, captureId: pair.After.CaptureId, observationCycleId: pair.After.ObservationCycleId),
            Context(pair.After, afterId), [pair.PreviousPage, pair.Page], Context(pair.Before, beforeId),
            Intent: new(pair.Page.Key, "Controlled caller intent, never an observation"));
    }
}
