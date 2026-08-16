using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Deterministic mapping between DriverHost read models and their wire DTOs
/// (Kernel-fact copies), plus JSON encode/decode for the newline-delimited
/// JSON-RPC transport. Pure and side-effect free: no I/O, no mutable state.
/// </summary>
public static class UniClawWireCodec
{
    /// <summary>Shared JSON options: camelCase, omit nulls, enums as camelCase strings.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Build the ping DTO.</summary>
    public static UniClawPingDto ToDto(string service, int protocolVersion, string baselineChange)
        => new(service, protocolVersion, baselineChange);

    /// <summary>Build the run-list DTO.</summary>
    public static UniClawRunListDto ToDto(ImmutableArray<string> runIds) => new(runIds);

    /// <summary>Map a RunSnapshot to its immutable wire copy, preserving every field's classification.</summary>
    public static UniClawRunSnapshotDto ToDto(RunSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new UniClawRunSnapshotDto(
            snapshot.RunId,
            Field(snapshot.RunState, v => JsonValue.Create(CamelEnum(v.ToString()))),
            Field(snapshot.CurrentSemanticPage, v => v is null ? null : JsonValue.Create(v)),
            Field(snapshot.ActiveTrap, v => v is null ? null : JsonSerializer.SerializeToNode(ToDto(v), JsonOptions)),
            Field(snapshot.CurrentGoal, v => v is null ? null : JsonSerializer.SerializeToNode(ToDto(v), JsonOptions)),
            Field(snapshot.LastDecision, v => v is null ? null : JsonSerializer.SerializeToNode(ToDto(v), JsonOptions)),
            Field(snapshot.LastAction, v => v is null ? null : JsonSerializer.SerializeToNode(ToDto(v), JsonOptions)),
            Field(snapshot.RecoveryState, v => v is null ? null : JsonSerializer.SerializeToNode(ToDto(v), JsonOptions)),
            Field(snapshot.LatestGoalEvidence, v => v is null ? null : JsonSerializer.SerializeToNode(ToDto(v), JsonOptions)),
            Field(snapshot.CurrentObservationSequence, v => v is null ? null : JsonValue.Create(v.Value)),
            Field(snapshot.CurrentContainerSummary, v => v is null ? null : JsonValue.Create(v)),
            Field(snapshot.BindingsSummary, v => v is null ? null : JsonValue.Create(v)),
            Field(snapshot.StateBeliefsSummary, v => v is null ? null : JsonValue.Create(v)),
            snapshot.Diagnostics);
    }

    /// <summary>Map an InspectTrapResult to its immutable wire copy.</summary>
    public static UniClawTrapResultDto ToDto(InspectTrapResult result)
        => new(
            result.RunId,
            result.Found,
            result.Trap is null ? null : Field(result.Trap, v => v is null ? null : JsonSerializer.SerializeToNode(ToDto(v), JsonOptions)),
            result.Diagnostic);

    /// <summary>Map a RuntimeEventPage to its immutable wire copy (cursor semantics preserved).</summary>
    public static UniClawRuntimeEventPageDto ToDto(RuntimeEventPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new UniClawRuntimeEventPageDto(
            page.RunId,
            [.. page.Events.Select(ToDto)],
            page.NextCursor is null ? null : new UniClawEventCursorDto(page.NextCursor.RunId, page.NextCursor.LastSequence),
            page.HasMore,
            page.Diagnostics);
    }

    /// <summary>Map one RuntimeEventEnvelope to its immutable wire copy.</summary>
    public static UniClawRuntimeEventDto ToDto(RuntimeEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return new UniClawRuntimeEventDto(
            envelope.EventId,
            envelope.RunId,
            envelope.Sequence,
            envelope.Kind.ToString(),
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.ObservationSequence,
            [.. envelope.EvidenceRefs.Select(ToDto)],
            envelope.Payload is null ? null : JsonSerializer.SerializeToNode(envelope.Payload, envelope.Payload.GetType(), JsonOptions));
    }

    /// <summary>Map one EvidenceRef to its immutable wire copy.</summary>
    public static UniClawEvidenceRefDto ToDto(EvidenceRef evidenceRef)
    {
        ArgumentNullException.ThrowIfNull(evidenceRef);
        return new UniClawEvidenceRefDto(
            evidenceRef.EvidenceId,
            evidenceRef.Kind.ToString(),
            evidenceRef.RunId,
            evidenceRef.ObservationSequence,
            evidenceRef.ContentIdentity,
            evidenceRef.Maturity.ToString(),
            evidenceRef.SizeBytes,
            evidenceRef.Locator);
    }

    /// <summary>Map an EvidenceResolution to its immutable wire copy (metadata only).</summary>
    public static UniClawEvidenceResolutionDto ToDto(EvidenceResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return new UniClawEvidenceResolutionDto(
            resolution.Found,
            resolution.Ref is null ? null : ToDto(resolution.Ref),
            resolution.CaptureSessionId,
            resolution.Record is null ? null : new UniClawCaptureRecordDto(
                resolution.Record.Order,
                resolution.Record.Kind.ToString(),
                resolution.Record.SequenceNumber,
                resolution.Record.FrameId,
                resolution.Record.ActionId,
                resolution.Record.ResultOutcome,
                resolution.Record.Info),
            resolution.Artifact is null ? null : new UniClawCaptureArtifactDto(
                resolution.Artifact.ArtifactId,
                resolution.Artifact.FrameId,
                resolution.Artifact.FileName,
                resolution.Artifact.ContentHash,
                resolution.Artifact.ByteCount),
            resolution.Diagnostic);
    }

    /// <summary>Map a ControlSupportResult to its immutable wire copy.</summary>
    public static UniClawControlSupportDto ToDto(ControlSupportResult result)
        => new(result.Operation, result.Supported, result.Reason, result.Evidence, result.ReadOnly);

    /// <summary>Map a Trap to its immutable wire copy (no live references).</summary>
    public static UniClawTrapDto ToDto(Trap trap)
    {
        ArgumentNullException.ThrowIfNull(trap);
        return new UniClawTrapDto(
            trap.Kind.ToString(),
            trap.Scope.ToString(),
            trap.Expected,
            trap.Observed,
            trap.Source,
            trap.Evidence,
            trap.LastAction is null ? null : DeviceActionText.Describe(trap.LastAction));
    }

    /// <summary>Map a GoalSummary to its immutable wire copy.</summary>
    public static UniClawGoalDto ToDto(GoalSummary summary) => new(summary.Goal);

    /// <summary>Map a DecisionSummary to its immutable wire copy.</summary>
    public static UniClawDecisionDto ToDto(DecisionSummary summary)
        => new(summary.Reason, summary.ActionId, summary.StepId, summary.ContainerId);

    /// <summary>Map an ActionSummary to its immutable wire copy.</summary>
    public static UniClawActionDto ToDto(ActionSummary summary)
        => new(summary.ActionId, summary.StepId, summary.ContainerId, summary.ActionDescription);

    /// <summary>Map a RecoverySummary to its immutable wire copy.</summary>
    public static UniClawRecoveryDto ToDto(RecoverySummary summary)
        => new(summary.RecoveryId, summary.Reason, summary.ContainerId, summary.StepId);

    /// <summary>Map a GoalEvidenceSummary to its immutable wire copy.</summary>
    public static UniClawGoalEvidenceDto ToDto(GoalEvidenceSummary summary)
        => new(summary.Satisfied, summary.Reason, summary.SourceObservationSequence, summary.IsPartial);

    /// <summary>
    /// Build one classified field DTO, preserving classification and truth source.
    /// A plainly unavailable field (no partial value) is never invented on the
    /// wire: its value maps to null even when the source field carries a type
    /// default (e.g. an enum's zero member). Partial unavailable fields keep
    /// their partial value with the isPartial flag.
    /// </summary>
    private static UniClawFieldDto Field<T>(SnapshotField<T> field, Func<T?, JsonNode?> valueMapper)
    {
        ArgumentNullException.ThrowIfNull(field);
        var value = field.Classification == SnapshotFieldClassification.NotCurrentlyAvailable && !field.IsPartial
            ? null
            : valueMapper(field.Value);
        return new UniClawFieldDto(
            value,
            CamelEnum(field.Classification.ToString()),
            field.TruthSource,
            field.IsPartial);
    }

    /// <summary>Wire enum convention: camelCase strings (protocol baseline §11).</summary>
    private static string CamelEnum(string enumName) => JsonNamingPolicy.CamelCase.ConvertName(enumName);

    // ---- JSON encode / decode ---------------------------------------------

    /// <summary>Serialize a DTO to compact JSON (never pretty, never newlines inside).</summary>
    public static string Serialize(object dto) => JsonSerializer.Serialize(dto, dto.GetType(), JsonOptions);

    /// <summary>Parse one request/response line; throws JsonException on malformed JSON.</summary>
    public static JsonObject ParseObject(string line)
        => JsonNode.Parse(line) as JsonObject
           ?? throw new JsonException("message is not a JSON object");

    /// <summary>Build a JSON-RPC success response line.</summary>
    public static string SerializeResponse(object? id, object result)
        => Serialize(new { jsonrpc = "2.0", id, result });

    /// <summary>Build a JSON-RPC error response line with a typed error code.</summary>
    public static string SerializeError(object? id, string code, string message)
        => Serialize(new { jsonrpc = "2.0", id, error = new { code, message } });

    // ---- parameter extraction (bad_request safe) ---------------------------

    /// <summary>Try to read a non-empty string parameter.</summary>
    public static bool TryGetString(JsonObject obj, string key, out string value)
    {
        value = string.Empty;
        if (obj.TryGetPropertyValue(key, out var node) && node is JsonValue v
            && v.TryGetValue<string>(out var s) && !string.IsNullOrEmpty(s))
        {
            value = s;
            return true;
        }
        return false;
    }

    /// <summary>Try to read a long parameter (accepts int-backed JsonValues too).</summary>
    public static bool TryGetLong(JsonObject obj, string key, out long value)
    {
        value = 0;
        if (obj.TryGetPropertyValue(key, out var node) && node is JsonValue v)
        {
            if (v.TryGetValue<long>(out var l))
            {
                value = l;
                return true;
            }

            if (v.TryGetValue<int>(out var i))
            {
                value = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>Parse an optional cursor param: { runId, lastSequence }; null when absent or malformed.</summary>
    public static EventCursor? ParseCursor(JsonObject? obj)
    {
        if (obj is null) return null;
        if (!TryGetString(obj, "runId", out var runId) || !TryGetLong(obj, "lastSequence", out var lastSequence))
        {
            return null;
        }
        return new EventCursor(runId, lastSequence);
    }

    /// <summary>Parse an EvidenceRef param (logical locator only — never a path).</summary>
    public static EvidenceRef ParseEvidenceRef(JsonObject obj)
    {
        if (!TryGetString(obj, "locator", out var locator) || !TryGetString(obj, "runId", out var runId))
        {
            throw new ArgumentException("evidenceRef requires non-empty 'locator' and 'runId'");
        }
        return new EvidenceRef
        {
            EvidenceId = locator,
            Kind = EvidenceKind.TraceFragment,
            RunId = runId,
            Locator = locator,
        };
    }
}
