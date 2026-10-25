using System.Text.Json;
using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.World;

/// <summary>Storage of original World operation outputs; no reconciliation or current authority.</summary>
public interface IWorldJournal
{
    bool CanInitialize { get; }
    string RecordingId { get; }
    IReadOnlyList<WorldHistoryRecord> ReadRecords();
    void Append(WorldHistoryRecord record);
    /// <summary>
    /// Commit one complete outer operation. Data excludes revisions; immutable typed outputs
    /// are supplied separately so native adapters need not serialize the full cumulative state.
    /// The default preserves the original full-record contract for existing adapters.
    /// </summary>
    void AppendOperation(WorldHistoryRecord operation, IReadOnlyList<WorldBeliefRevision> revisions)
    {
        var data = new Dictionary<string, object?>
        { ["input"] = operation.Data.GetProperty("input"), ["revisions"] = revisions };
        foreach (var field in operation.Data.EnumerateObject())
            if (field.Name != "input") data.Add(field.Name, field.Value);
        Append(operation with { Data = JsonSerializer.SerializeToElement(data, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
    }
}

/// <summary>Actual commit acknowledgement of a complete immutable World operation.
/// Cancellation before acceptance may reject; accepted writes must settle before returning.</summary>
public interface IAsyncWorldJournal : IWorldJournal
{
    /// <summary>Original Owner initialization. Compatibility adapters may retain their synchronous entry; production supplies native confirmation.</summary>
    ValueTask InitializeAsync(WorldHistoryRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); Append(record); return ValueTask.CompletedTask;
    }

    ValueTask AppendOperationAsync(WorldHistoryRecord operation, IReadOnlyList<WorldBeliefRevision> revisions,
        CancellationToken cancellationToken = default);
}

/// <summary>Original storage sources, fixed before the first World operation.</summary>
public sealed record WorldHistoryRecording(IWorldJournal Journal, string RunRecordingId, string EvidenceRecordingId);

public sealed record WorldHistoryPosition(string RecordingId, int Sequence);

/// <summary>Frozen original JSON outputs, deliberately distinct from live WorldBelief or WorldModel.</summary>
public sealed record WorldHistoryRecord(int Sequence, string Operation, JsonElement Data);

internal sealed class WorldPersistenceException(string message, Exception? inner = null) : IOException(message, inner);

/// <summary>Original historical data only. This type has no current/grounding/execution interface.</summary>
public sealed class WorldHistoryRead
{
    public WorldHistoryPosition Position { get; private init; } = null!;
    public string RunRecordingId { get; private init; } = "";
    public string EvidenceRecordingId { get; private init; } = "";
    public JsonElement Configuration { get; private init; }
    public IReadOnlyList<WorldHistoryRecord> Operations { get; private init; } = [];
    public IReadOnlyList<JsonElement> HistoricalRevisions { get; private init; } = [];
    public bool ContinuationSupported => false;

    public static WorldHistoryRead ReadOriginal(IWorldJournal journal, string expectedWorldRecordingId,
        string expectedRunRecordingId, EvidenceLedger originalEvidence)
    {
        try { return ReadValidated(journal, expectedWorldRecordingId, expectedRunRecordingId, originalEvidence); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new InvalidDataException("world-history-invalid-shape", ex); }
    }

    private static WorldHistoryRead ReadValidated(IWorldJournal journal, string worldId, string runId, EvidenceLedger evidence)
    {
        if (journal.CanInitialize || !evidence.IsReadOnly || !Guid.TryParseExact(worldId, "N", out _)
            || !Guid.TryParseExact(runId, "N", out _) || journal.RecordingId != worldId)
            throw new InvalidDataException("world-history-original-readonly-sources-required");
        var rows = journal.ReadRecords().Select(r => r with { Data = r.Data.Clone() }).ToArray();
        if (rows.Length == 0 || rows[0].Sequence != 1 || rows[0].Operation != "initialize")
            throw new InvalidDataException("world-history-initialization-missing");
        var header = rows[0].Data;
        if (header.GetProperty("schema").GetString() != "uniclaw.world.owner.v1"
            || header.GetProperty("runRecordingId").GetString() != runId
            || header.GetProperty("evidenceRecordingId").GetString() != evidence.HistoryPosition?.RecordingId)
            throw new InvalidDataException("world-history-origin-mismatch");
        var configuration = header.GetProperty("configuration");
        foreach (var key in new[] { "relevanceScope", "associationStrategy", "observationStrategy", "continuityStrategy", "nativeProfiles" })
            _ = configuration.GetProperty(key);
        _ = configuration.GetProperty("relevanceScope").EnumerateArray();
        _ = configuration.GetProperty("nativeProfiles").EnumerateArray();
        if (!Guid.TryParseExact(configuration.GetProperty("worldModuleVersionId").GetString(), "N", out _))
            throw new InvalidDataException("world-history-implementation-version-missing");
        var processed = new HashSet<string>(StringComparer.Ordinal);
        var revoked = new HashSet<string>(StringComparer.Ordinal);
        var revisions = new List<JsonElement>();
        string? parent = null;
        for (var i = 1; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row.Sequence != i + 1 || !OperationNames.Contains(row.Operation))
                throw new InvalidDataException("world-history-operation-sequence");
            var data = row.Data;
            _ = data.GetProperty("input");
            foreach (var key in ArrayFields) _ = data.GetProperty(key).EnumerateArray();
            var time = data.GetProperty("latestPageObservationTime");
            if (time.ValueKind != JsonValueKind.Null) _ = time.GetDateTimeOffset();
            foreach (var id in data.GetProperty("processedEvidenceIds").EnumerateArray())
            {
                var value = id.GetString()!;
                RequireAccepted(value, evidence);
                if (!processed.Add(value)) throw new InvalidDataException("world-history-processed-reference-duplicate");
            }
            foreach (var judgment in data.GetProperty("relevanceLog").EnumerateArray())
                RequireAccepted(judgment.GetProperty("evidenceId").GetString()!, evidence);
            if (row.Operation == "relevance")
            {
                var judgments = data.GetProperty("relevanceLog");
                if (judgments.GetArrayLength() != 1 || judgments[0].GetProperty("evidenceId").GetString()
                    != data.GetProperty("input").GetProperty("evidenceId").GetString())
                    throw new InvalidDataException("world-history-relevance-input-mismatch");
            }
            foreach (var revision in data.GetProperty("revisions").EnumerateArray())
            {
                if (RevisionFields.Any(field => !revision.TryGetProperty(field, out _)))
                    throw new InvalidDataException("world-history-revision-fields");
                var number = revisions.Count + 1;
                if (revision.GetProperty("revisionNumber").GetInt32() != number
                    || revision.GetProperty("revisionId").GetString() != $"rev-{number}"
                    || revision.GetProperty("parentRevisionId").GetString() != parent)
                    throw new InvalidDataException("world-history-revision-parent");
                foreach (var id in revision.GetProperty("evidenceBasis").EnumerateArray())
                    if (!processed.Contains(id.GetString()!)) throw new InvalidDataException("world-history-unprocessed-basis");
                ValidateRevisionReferences(revision, evidence);
                parent = revision.GetProperty("revisionId").GetString();
                revisions.Add(revision.Clone());
            }
            ValidateDecisionReferences(data, evidence, processed);
            foreach (var source in data.GetProperty("containerObservations").EnumerateArray())
            {
                _ = source.GetProperty("containerId").GetString();
                if (!processed.Contains(source.GetProperty("evidenceId").GetString()!))
                    throw new InvalidDataException("world-history-container-source-missing");
            }
            foreach (var correction in data.GetProperty("pageCorrections").EnumerateArray())
                foreach (var id in correction.GetProperty("revokedInterpretationEvidenceIds").EnumerateArray())
                    revoked.Add(id.GetString()!);
            var snapshot = data.GetProperty("revokedInterpretations").EnumerateArray().Select(v => v.GetString()!).ToArray();
            if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length || !revoked.SetEquals(snapshot))
                throw new InvalidDataException("world-history-revocation-log-mismatch");
            var demandIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var demand in data.GetProperty("continuityDemands").EnumerateArray())
                if (!demandIds.Add(demand.GetProperty("demandId").GetString()!))
                    throw new InvalidDataException("world-history-duplicate-demand");
        }
        return new()
        {
            Position = new(worldId, rows.Length), RunRecordingId = runId,
            EvidenceRecordingId = evidence.HistoryPosition!.RecordingId, Configuration = configuration.Clone(),
            Operations = Array.AsReadOnly(rows), HistoricalRevisions = revisions.AsReadOnly(),
        };
    }

    private static readonly HashSet<string> OperationNames = new(StringComparer.Ordinal)
    { "relevance", "reconcile", "navigation", "viewport", "conflict-resolution", "demand-register", "demand-revoke", "continuity" };
    private static readonly string[] ArrayFields = ["revisions", "relevanceLog", "associationLog", "continuityLog", "claimEvolutionLog",
        "conflictResolutions", "pageCorrections", "contextDecisions", "semanticVerifications", "continuityDemands",
        "processedEvidenceIds", "containerObservations", "revokedInterpretations"];
    private static readonly string[] RevisionFields = ["revisionId", "parentRevisionId", "revisionNumber", "worldState", "worldGraph",
        "evidenceBasis", "freshnessBasis", "uncertainty", "conflicts", "containers", "relations", "occurrences", "logicalItems",
        "currentPageObservation", "observationNeed", "semanticKnowledge", "viewportTransitions"];

    private static void ValidateDecisionReferences(JsonElement data, EvidenceLedger evidence, HashSet<string> processed)
    {
        string Source(JsonElement log, string field)
        {
            var id = log.GetProperty(field).GetString()!;
            RequireAccepted(id, evidence);
            if (!processed.Contains(id)) throw new InvalidDataException("world-history-unprocessed-log-source");
            return id;
        }
        void Dependency(JsonElement log, string field)
        {
            if (log.GetProperty(field).ValueKind != JsonValueKind.Null) _ = Source(log, field);
        }
        void Dependencies(JsonElement log, string field)
        {
            foreach (var id in log.GetProperty(field).EnumerateArray())
            {
                RequireAccepted(id.GetString()!, evidence);
                if (!processed.Contains(id.GetString()!)) throw new InvalidDataException("world-history-unprocessed-log-dependency");
            }
        }
        foreach (var log in data.GetProperty("associationLog").EnumerateArray())
            // Candidates are original strategy proposals, including rejected/unconsumed
            // candidates. Only the resulting belief bases are accepted dependencies.
            _ = Source(log, "evidenceId");
        foreach (var log in data.GetProperty("claimEvolutionLog").EnumerateArray())
        { _ = Source(log, "establishingEvidenceId"); Dependency(log, "supersededEvidenceId"); }
        foreach (var log in data.GetProperty("contextDecisions").EnumerateArray()) _ = Source(log, "evidenceId");
        foreach (var log in data.GetProperty("pageCorrections").EnumerateArray())
        {
            var source = evidence.CanonicalRecords[Source(log, "correctionEvidenceId")];
            var parsed = PageInterpretationCodec.TryRead(source.Claim.Value, out var proposal);
            if (source.Claim.Subject != PageInterpretationCodec.Subject
                || log.GetProperty("targetCaptureEvidenceId").GetString() != (parsed ? proposal!.TargetCaptureEvidenceId : "unknown")
                || log.GetProperty("supersededIdentityEvidenceId").GetString() != (parsed ? proposal!.SupersededIdentityEvidenceId : "unknown")
                || log.GetProperty("processingSourceRef").GetString() != (parsed ? proposal!.ProcessingSourceRef : "not_collected")
                || !JsonElement.DeepEquals(log.GetProperty("replacementIdentity"), JsonSerializer.SerializeToElement(parsed ? proposal!.ReplacementIdentity : null, HistoryJson))
                || !log.GetProperty("supportingEvidenceIds").EnumerateArray().Select(v => v.GetString())
                    .SequenceEqual(parsed ? proposal!.SupportingEvidenceIds : Array.Empty<string>()))
                throw new InvalidDataException("world-history-correction-original-proposal-mismatch");
            // Unapplied proposals may legitimately name nonexistent sources. Preserve
            // those original unknowns; never promote them into accepted dependencies.
            if (log.GetProperty("applied").GetBoolean())
            { Dependency(log, "targetCaptureEvidenceId"); Dependency(log, "supersededIdentityEvidenceId"); Dependencies(log, "supportingEvidenceIds"); }
            Dependencies(log, "revokedInterpretationEvidenceIds");
        }
        foreach (var log in data.GetProperty("semanticVerifications").EnumerateArray())
        {
            var source = evidence.CanonicalRecords[Source(log, "evidenceId")];
            _ = SemanticVerificationCodec.TryRead(source.Claim.Value, out var proposal);
            var observation = log.GetProperty("observation");
            if (source.Claim.Subject != SemanticVerificationCodec.Subject
                || !JsonElement.DeepEquals(observation, JsonSerializer.SerializeToElement(proposal, HistoryJson)))
                throw new InvalidDataException("world-history-semantic-original-proposal-mismatch");
            if (log.GetProperty("applied").GetBoolean())
                foreach (var field in new[] { "semanticEvidenceId", "sourceCaptureEvidenceId", "sourceIdentityEvidenceId", "nativeContextEvidenceId" })
                    Dependency(observation, field);
        }
    }

    private static readonly JsonSerializerOptions HistoryJson = new(JsonSerializerDefaults.Web);
    // Traverse typed schema positions, never arbitrary property names in subject maps.
    private static void ValidateRevisionReferences(JsonElement revision, EvidenceLedger evidence)
    {
        void Reference(JsonElement value, string field)
        {
            var id = value.GetProperty(field);
            if (id.ValueKind != JsonValueKind.Null) RequireAccepted(id.GetString()!, evidence);
        }
        void Basis(JsonElement value, string field)
        {
            var ids = value.GetProperty(field);
            if (ids.ValueKind != JsonValueKind.Null)
                foreach (var id in ids.EnumerateArray()) RequireAccepted(id.GetString()!, evidence);
        }
        IEnumerable<JsonElement> Items(string field)
        {
            var values = revision.GetProperty(field);
            return values.ValueKind == JsonValueKind.Null ? [] : values.EnumerateArray().ToArray();
        }
        Basis(revision, "evidenceBasis");
        foreach (var claim in revision.GetProperty("worldState").EnumerateObject())
        { Reference(claim.Value, "evidenceId"); Basis(claim.Value, "supersededEvidenceIds"); }
        foreach (var conflict in Items("conflicts"))
        { Reference(conflict, "establishedEvidenceId"); Reference(conflict, "challengingEvidenceId"); }
        foreach (var field in new[] { "containers", "relations", "occurrences", "logicalItems" })
            foreach (var item in Items(field)) Basis(item, "evidenceBasis");
        var page = revision.GetProperty("currentPageObservation");
        if (page.ValueKind != JsonValueKind.Null)
        { Reference(page, "evidenceId"); Reference(page, "identityEvidenceId"); }
        foreach (var knowledge in Items("semanticKnowledge"))
        {
            Reference(knowledge, "currentCaptureEvidenceId"); Reference(knowledge, "originalSemanticEvidenceId");
            Reference(knowledge, "verificationEvidenceId"); Basis(knowledge, "correspondenceEvidenceIds");
        }
        foreach (var transition in Items("viewportTransitions"))
        { Basis(transition, "sourceEvidenceIds"); Basis(transition, "targetEvidenceIds"); }
    }

    private static void RequireAccepted(string id, EvidenceLedger evidence)
    {
        if (id is null || !evidence.CanonicalRecords.ContainsKey(id))
            throw new InvalidDataException("world-history-original-evidence-missing");
    }
}
