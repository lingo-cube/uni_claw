namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// Result of <see cref="ScenarioKnowledgeStore.Freeze"/>: the persisted
/// human-readable asset locations and a deterministic content digest.
/// <see cref="ContentSha256"/> is the SHA-256 (lowercase hex) of the
/// records.json BYTES — the same digest the manifest records as
/// recordsSha256, so load-time cross-checking verifies exactly what freeze
/// produced. It is a digest of content, never a timestamp, path, or machine
/// name — freezing the same fixture twice yields the same digest.
/// </summary>
public sealed record FrozenFixture(
    string Directory,
    string RecordsPath,
    string ManifestPath,
    string MarkdownPath,
    int RecordCount,
    string ContentSha256);

/// <summary>
/// One rejected record during <see cref="ScenarioKnowledgeStore.Load"/>.
/// <see cref="RecordId"/> is the stored RecordId when the record was parseable
/// enough to carry one; for a structurally broken record it is a stable
/// placeholder ("&lt;record[N]&gt;", N = array index) so every rejection stays
/// reportable. <see cref="Reason"/> names the failing gate in deterministic
/// wording (missing/invalid field, RecordId mismatch = tampered content, scope
/// mismatch, admission rejection). Load NEVER silently fixes or drops a record:
/// every rejected record appears here.
/// </summary>
public sealed record LoadRejection(string RecordId, string Reason);

/// <summary>
/// Result of <see cref="ScenarioKnowledgeStore.Load"/>: the rebuilt
/// <see cref="ScenarioKnowledgeFixture"/> (revalidated via its admission gate,
/// bound to the expected scope), the count of accepted records, and every
/// rejected record with its reason.
/// </summary>
public sealed record LoadedFixture(
    ScenarioKnowledgeFixture Fixture,
    int RecordsLoaded,
    IReadOnlyList<LoadRejection> RejectedRecords);