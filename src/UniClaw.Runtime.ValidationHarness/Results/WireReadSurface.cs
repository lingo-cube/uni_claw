using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Wire;

namespace UniClaw.Runtime.ValidationHarness.Results;

/// <summary>
/// <see cref="IRuntimeReadSurface"/> over the REAL loopback wire transport
/// (WI-EVH-003 4.1): every op is one frozen DriverHost wire method
/// (<c>run.snapshot.get</c> / <c>run.events.after</c> / <c>run.events.drain</c>
/// / <c>run.trap.get</c> / <c>evidence.get</c>). Responses are rehydrated back
/// into the frozen typed read models (a harness-local reverse of the DriverHost
/// codec). No new wire method is added — this is pure client-side typing of the
/// frozen surface. Read failures surface as exceptions (fail-closed; never a
/// fabricated value).
/// </summary>
public sealed class WireReadSurface : IRuntimeReadSurface
{
    private readonly int _port;
    private int _nextRequestId;

    /// <summary>Create the wire surface dialing the bound loopback port.</summary>
    public WireReadSurface(int port)
    {
        if (port <= 0)
            throw new ArgumentOutOfRangeException(nameof(port));
        _port = port;
    }

    /// <inheritdoc />
    public async Task<RunSnapshot> GetRunSnapshotAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var result = await RequestAsync("run.snapshot.get", $"{{\"runId\":\"{runId}\"}}", cancellationToken).ConfigureAwait(false);
        try
        {
            return SnapshotWireMapper.ToRunSnapshot(result.Result);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"run.snapshot.get response was not a valid snapshot: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<SurfaceEventPage> GetRuntimeEventsAfterAsync(string runId, EventCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var cursorJson = cursor is null
            ? string.Empty
            : $",\"cursor\":{{\"runId\":\"{cursor.RunId}\",\"lastSequence\":{cursor.LastSequence}}}";
        var result = await RequestAsync("run.events.after", $"{{\"runId\":\"{runId}\"{cursorJson}}}", cancellationToken).ConfigureAwait(false);
        return SnapshotWireMapper.ToEventPage(result.Result);
    }

    /// <inheritdoc />
    public async Task<SurfaceEventPage> DrainRuntimeEventsAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var result = await RequestAsync("run.events.drain", $"{{\"runId\":\"{runId}\"}}", cancellationToken).ConfigureAwait(false);
        return SnapshotWireMapper.ToEventPage(result.Result);
    }

    /// <inheritdoc />
    public async Task<InspectTrapResult> GetRunTrapAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var result = await RequestAsync("run.trap.get", $"{{\"runId\":\"{runId}\"}}", cancellationToken).ConfigureAwait(false);
        return SnapshotWireMapper.ToTrapResult(result.Result);
    }

    /// <inheritdoc />
    public async Task<EvidenceResolution> GetEvidenceAsync(EvidenceRef evidenceRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidenceRef);
        var refJson = $"{{\"locator\":\"{evidenceRef.Locator}\",\"runId\":\"{evidenceRef.RunId}\"}}";
        var result = await RequestAsync("evidence.get", $"{{\"evidenceRef\":{refJson}}}", cancellationToken).ConfigureAwait(false);
        return SnapshotWireMapper.ToEvidenceResolution(result.Result);
    }

    private record WireResult(JsonObject Result);

    private async Task<WireResult> RequestAsync(string method, string parameters, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        var requestLine =
            $"{{\"jsonrpc\":\"2.0\",\"id\":{System.Threading.Interlocked.Increment(ref _nextRequestId)},\"method\":\"{method}\",\"params\":{parameters}}}";
        var response = await LoopbackWireClient.RequestAsync(_port, requestLine, cancellationToken).ConfigureAwait(false);
        if (response["error"] is JsonObject error)
        {
            var message = error["message"]?.GetValue<string>() ?? "unrecognized RPC error";
            throw new InvalidOperationException($"{method} failed on the wire: {message}");
        }

        var result = response["result"] as JsonObject
            ?? throw new InvalidOperationException($"{method} returned no result object on the wire.");
        return new WireResult(result);
    }
}

/// <summary>
/// Harness-local reverse mapping of the frozen DriverHost wire DTOs back into
/// the typed read models (mirror of <c>UniClawWireCodec.ToDto</c>). Values and
/// classifications are copied verbatim — nothing is invented, nothing is
/// dropped except what the wire itself does not carry (e.g. a Trap's
/// <c>LastAction</c> object — the wire carries only its description).
/// </summary>
internal static class SnapshotWireMapper
{
    private static readonly JsonSerializerOptions Options = UniClawWireCodec.JsonOptions;

    public static RunSnapshot ToRunSnapshot(JsonObject wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        var runId = ReadString(wire, "runId");
        return new RunSnapshot
        {
            RunId = runId,
            RunState = ToField(wire, "runState", value => value is null ? default : ParseEnumName<RunState>(value.GetValue<string>())),
            CurrentSemanticPage = ToField(wire, "currentSemanticPage", value => value?.GetValue<string>()),
            ActiveTrap = ToField(wire, "activeTrap", value => value is null ? null : ToTrap(value.AsObject())),
            CurrentGoal = ToField(wire, "currentGoal", value => value is null ? null : new GoalSummary(ReadString(value.AsObject(), "goal"))),
            LastDecision = ToField(wire, "lastDecision", value => value is null ? null : ToDecision(value.AsObject())),
            LastAction = ToField(wire, "lastAction", value => value is null ? null : ToAction(value.AsObject())),
            RecoveryState = ToField(wire, "recoveryState", value => value is null ? null : ToRecovery(value.AsObject())),
            LatestGoalEvidence = ToField(wire, "latestGoalEvidence", value => value is null ? null : ToGoalEvidence(value.AsObject())),
            CurrentObservationSequence = ToField(wire, "currentObservationSequence", value => value?.GetValue<long>()),
            CurrentContainerSummary = ToField(wire, "currentContainerSummary", value => value?.GetValue<string>()),
            BindingsSummary = ToField(wire, "bindingsSummary", value => value?.GetValue<string>()),
            StateBeliefsSummary = ToField(wire, "stateBeliefsSummary", value => value?.GetValue<string>()),
            Diagnostics = ReadStrings(wire, "diagnostics"),
        };
    }

    public static SurfaceEventPage ToEventPage(JsonObject wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        var events = ImmutableArray.CreateBuilder<SurfaceRuntimeEvent>();
        if (wire["events"] is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not JsonObject eventObject)
                {
                    continue;
                }

                var kindName = ReadString(eventObject, "kind");
                var kind = ParseEnumName<RuntimeEventKind>(kindName);
                events.Add(new SurfaceRuntimeEvent(
                    EventId: ReadString(eventObject, "eventId"),
                    Kind: kindName,
                    Sequence: ReadLong(eventObject, "sequence"),
                    SourceClassification: RuntimeEventKindTable.For(kind).Classification.ToString(),
                    ObservationSequence: ReadNullableLong(eventObject, "observationSequence"),
                    Reason: ReadEventReason(kind, eventObject["payload"] as JsonObject),
                    EvidenceRefs: ReadEvidenceRefs(eventObject)));
            }
        }

        return new SurfaceEventPage(events.ToImmutable(), ReadStrings(wire, "diagnostics"));
    }

    public static InspectTrapResult ToTrapResult(JsonObject wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return new InspectTrapResult(
            RunId: ReadString(wire, "runId"),
            Found: ReadBool(wire, "found"),
            Trap: ToField(wire, "trap", value => value is null ? null : ToTrap(value.AsObject())),
            Diagnostic: ReadNullableString(wire, "diagnostic"));
    }

    public static EvidenceResolution ToEvidenceResolution(JsonObject wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return new EvidenceResolution
        {
            Found = ReadBool(wire, "found"),
            Ref = wire["ref"] is JsonObject refObject ? ToEvidenceRef(refObject) : null,
            CaptureSessionId = ReadNullableString(wire, "captureSessionId"),
            Record = wire["record"] is JsonObject recordObject ? ToCaptureRecord(recordObject) : null,
            Artifact = wire["artifact"] is JsonObject artifactObject ? ToCaptureArtifact(artifactObject) : null,
            Diagnostic = ReadNullableString(wire, "diagnostic"),
        };
    }

    // ---- field DTO → SnapshotField<T> --------------------------------------

    private static SnapshotField<T> ToField<T>(JsonObject wire, string name, Func<JsonNode?, T?> projectValue)
    {
        if (wire[name] is not JsonObject field)
        {
            return SnapshotField<T>.Unavailable($"run.snapshot.get: field '{name}' absent from the wire result.");
        }

        var classificationName = ReadString(field, "classification");
        var classification = classificationName switch
        {
            "directPublicProjection" => SnapshotFieldClassification.DirectPublicProjection,
            "derivedReadModel" => SnapshotFieldClassification.DerivedReadModel,
            "notCurrentlyAvailable" => SnapshotFieldClassification.NotCurrentlyAvailable,
            _ => throw new InvalidOperationException($"run.snapshot.get: unrecognized field classification '{classificationName}' for '{name}'."),
        };
        var truthSource = ReadString(field, "truthSource");
        var isPartial = ReadBool(field, "isPartial");
        var value = field["value"];

        if (classification == SnapshotFieldClassification.NotCurrentlyAvailable && !isPartial)
        {
            return new SnapshotField<T>(default, classification, truthSource);
        }

        return new SnapshotField<T>(
            projectValue(value),
            classification,
            truthSource,
            isPartial);
    }

    // ---- typed value rehydration -------------------------------------------

    private static Trap ToTrap(JsonObject dto) => new(
        ParseEnumName<TrapKind>(ReadString(dto, "kind")),
        ParseEnumName<TrapScope>(ReadString(dto, "scope")),
        ReadNullableLong(dto, "expected"),
        ReadNullableLong(dto, "observed"),
        ReadString(dto, "source"),
        ReadString(dto, "evidence"),
        lastAction: null); // the frozen wire carries the action DESCRIPTION only.

    private static DecisionSummary ToDecision(JsonObject dto) => new(
        ReadNullableString(dto, "reason"),
        ReadNullableString(dto, "actionId"),
        ReadNullableString(dto, "stepId"),
        ReadNullableString(dto, "containerId"));

    private static ActionSummary ToAction(JsonObject dto) => new(
        ReadString(dto, "actionId"),
        ReadNullableString(dto, "stepId"),
        ReadNullableString(dto, "containerId"),
        ReadString(dto, "actionDescription"));

    private static RecoverySummary ToRecovery(JsonObject dto) => new(
        ReadString(dto, "recoveryId"),
        ReadNullableString(dto, "reason"),
        ReadNullableString(dto, "containerId"),
        ReadNullableString(dto, "stepId"));

    private static GoalEvidenceSummary ToGoalEvidence(JsonObject dto) => new(
        ReadBool(dto, "satisfied"),
        ReadNullableString(dto, "reason"),
        ReadNullableLong(dto, "sourceObservationSequence"),
        ReadBool(dto, "isPartial"));

    private static EvidenceRef ToEvidenceRef(JsonObject dto) => new()
    {
        EvidenceId = ReadString(dto, "evidenceId"),
        Kind = ParseEnumName<EvidenceKind>(ReadString(dto, "kind")),
        RunId = ReadString(dto, "runId"),
        ObservationSequence = ReadNullableLong(dto, "observationSequence"),
        ContentIdentity = ReadNullableString(dto, "contentIdentity"),
        Maturity = ParseEnumName<AssetMaturity>(ReadString(dto, "maturity")),
        SizeBytes = ReadNullableInt(dto, "sizeBytes"),
        Locator = ReadString(dto, "locator"),
    };

    private static CaptureRecord ToCaptureRecord(JsonObject dto) => new()
    {
        Order = ReadInt(dto, "order"),
        Kind = ParseEnumName<CaptureRecordKind>(ReadString(dto, "kind")),
        SequenceNumber = ReadLong(dto, "sequenceNumber"),
        FrameId = ReadNullableString(dto, "frameId"),
        ActionId = ReadNullableString(dto, "actionId"),
        ResultOutcome = ReadNullableString(dto, "resultOutcome"),
        Info = ReadNullableString(dto, "info"),
    };

    private static CaptureArtifact ToCaptureArtifact(JsonObject dto) => new()
    {
        ArtifactId = ReadString(dto, "artifactId"),
        FrameId = ReadNullableString(dto, "frameId"),
        FileName = ReadNullableString(dto, "fileName"),
        ContentHash = ReadNullableString(dto, "contentHash"),
        ByteCount = ReadInt(dto, "byteCount"),
    };

    private static ImmutableArray<EvidenceRef> ReadEvidenceRefs(JsonObject eventObject)
    {
        if (eventObject["evidenceRefs"] is not JsonArray array)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<EvidenceRef>();
        foreach (var item in array)
        {
            if (item is JsonObject refObject)
            {
                builder.Add(ToEvidenceRef(refObject));
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Payload reason for the reason-bearing B-class kinds; null for all others.</summary>
    private static string? ReadEventReason(RuntimeEventKind kind, JsonObject? payload)
    {
        if (payload is null)
        {
            return null;
        }

        return kind switch
        {
            RuntimeEventKind.GoalEvidenceProduced or RuntimeEventKind.RunCompleted or RuntimeEventKind.RunFailed
                => ReadNullableString(payload, "reason"),
            _ => null,
        };
    }

    // ---- wire enum conventions ---------------------------------------------

    private static TEnum ParseEnumName<TEnum>(string name)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(name, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"wire enum '{name}' is not a defined {typeof(TEnum).Name}.");

    // ---- json primitive readers (fail-closed) -------------------------------

    private static string ReadString(JsonObject obj, string name)
        => obj[name]?.GetValue<string>()
           ?? throw new InvalidOperationException($"wire response is missing string field '{name}'.");

    private static string? ReadNullableString(JsonObject obj, string name)
        => obj[name] is null ? null : obj[name]!.GetValue<string>();

    private static bool ReadBool(JsonObject obj, string name)
        => obj[name]?.GetValue<bool>()
           ?? throw new InvalidOperationException($"wire response is missing bool field '{name}'.");

    private static long ReadLong(JsonObject obj, string name)
        => obj[name]?.GetValue<long>()
           ?? throw new InvalidOperationException($"wire response is missing long field '{name}'.");

    private static long? ReadNullableLong(JsonObject obj, string name)
        => obj[name] is null ? null : obj[name]!.GetValue<long>();

    private static int ReadInt(JsonObject obj, string name)
        => obj[name]?.GetValue<int>()
           ?? throw new InvalidOperationException($"wire response is missing int field '{name}'.");

    private static int? ReadNullableInt(JsonObject obj, string name)
        => obj[name] is null ? null : obj[name]!.GetValue<int>();

    private static ImmutableArray<string> ReadStrings(JsonObject obj, string name)
    {
        if (obj[name] is not JsonArray array)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var item in array)
        {
            if (item is JsonValue value)
            {
                builder.Add(value.GetValue<string>());
            }
        }

        return builder.ToImmutable();
    }
}