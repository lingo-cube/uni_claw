using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniClaw.Host.Runtime;

/// <summary>
/// Host-owned persistence seam for RuntimeRun snapshots and append-only events.
/// The file adapter is deliberately an implementation detail: callers use the
/// projection and event contracts, never the physical directory layout.
/// </summary>
public sealed class RuntimeRunStore
{
    private const string ProjectionSchema = "uniclaw.workspace.runtime-run-projection.v1";
    private const string EventSchema = "uniclaw.workspace.runtime-run-event.v1";
    private const string ContractVersion = "uniclaw.workspace.contract.v1";
    private readonly string _root;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _json = CreateJsonOptions(writeIndented: true);
    private readonly JsonSerializerOptions _lineJson = CreateJsonOptions(writeIndented: false);

    public RuntimeRunStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("RunStore root is required", nameof(root));
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public RuntimeRunProjection Create(CreateRequest request, DateTimeOffset? observedAt = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = observedAt ?? DateTimeOffset.UtcNow;
        lock (_gate)
        {
            var existing = FindByIdempotencyKeyUnsafe(request.IdempotencyKey)
                ?? FindByLaunchIdUnsafe(request.LaunchId);
            if (existing is not null)
                return existing;

            var runId = "run-" + Guid.NewGuid().ToString("N");
            var projection = new RuntimeRunProjection(
                ProjectionSchema,
                ContractVersion,
                runId,
                request.ProductSessionId ?? "product-session-" + Guid.NewGuid().ToString("N"),
                request.TaskInstanceId ?? "task-instance-" + Guid.NewGuid().ToString("N"),
                HostSessionRef: null,
                request.ProjectRef,
                request.TestSetRef,
                request.TaskRef,
                request.LaunchId,
                request.CorrelationId,
                request.Environment,
                Status: "starting",
                Phase: "admission",
                StartedAt: now,
                EndedAt: null,
                Outcome: null,
                Reason: null,
                Revision: 1,
                ObservedAt: now,
                LastEventSequence: 1,
                Consistency: "current",
                Artifacts: Array.Empty<RuntimeArtifactRef>(),
                IdempotencyKey: request.IdempotencyKey);
            WriteEventUnsafe(new RuntimeRunEvent(
                EventSchema,
                ContractVersion,
                "event-" + Guid.NewGuid().ToString("N"),
                runId,
                "run.accepted",
                "runtime",
                "uniclaw-runtime",
                now,
                now,
                1,
                "Runtime run accepted",
                "runs/" + runId + "/events/1",
                PayloadAvailable: false));
            WriteProjectionUnsafe(projection);
            return projection;
        }
    }

    public RuntimeRunProjection? Get(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        lock (_gate) return ReadProjectionUnsafe(runId);
    }

    public RuntimeRunProjection? FindByIdempotencyKey(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;
        lock (_gate) return FindByIdempotencyKeyUnsafe(idempotencyKey);
    }

    public RuntimeRunProjection? FindByLaunchId(string launchId)
    {
        if (string.IsNullOrWhiteSpace(launchId)) return null;
        lock (_gate) return FindByLaunchIdUnsafe(launchId);
    }

    public RunPage List(string? status = null, string? productSessionId = null, string? cursor = null, int limit = 50)
    {
        lock (_gate)
        {
            var after = DecodeListCursor(cursor);
            var runs = FindAllUnsafe()
                .Where(run => string.IsNullOrWhiteSpace(status) || string.Equals(run.Status, status, StringComparison.OrdinalIgnoreCase))
                .Where(run => string.IsNullOrWhiteSpace(productSessionId) || string.Equals(run.ProductSessionId, productSessionId, StringComparison.Ordinal))
                .OrderByDescending(run => run.StartedAt)
                .ThenByDescending(run => run.RunId, StringComparer.Ordinal)
                .Where(run => after is null || IsBefore(run, after.Value))
                .ToList();
            var boundedLimit = Math.Clamp(limit, 1, 100);
            var page = runs.Take(boundedLimit).ToArray();
            var next = runs.Count > page.Length && page.Length > 0
                ? EncodeListCursor(page[^1])
                : null;
            return new RunPage(page, next);
        }
    }

    public RuntimeRunProjection Transition(
        string runId,
        string status,
        string phase,
        string eventType,
        string source,
        string authority,
        string summary,
        Func<RuntimeRunProjection, RuntimeRunProjection>? mutate = null,
        DateTimeOffset? observedAt = null)
    {
        lock (_gate)
        {
            var current = ReadProjectionUnsafe(runId)
                ?? throw new KeyNotFoundException($"Runtime run '{runId}' was not found");
            if (IsTerminal(current.Status))
                throw new InvalidOperationException($"Runtime run '{runId}' is already terminal");
            var now = observedAt ?? DateTimeOffset.UtcNow;
            var mutated = mutate?.Invoke(current) ?? current;
            var next = mutated with
            {
                Status = status,
                Phase = phase,
                Revision = current.Revision + 1,
                ObservedAt = now,
                LastEventSequence = current.LastEventSequence + 1,
                EndedAt = IsTerminal(status) ? mutated.EndedAt ?? now : mutated.EndedAt,
                Outcome = IsTerminal(status) ? mutated.Outcome ?? OutcomeFor(status) : mutated.Outcome,
                Consistency = "current"
            };
            WriteEventUnsafe(new RuntimeRunEvent(
                EventSchema,
                ContractVersion,
                "event-" + Guid.NewGuid().ToString("N"),
                runId,
                eventType,
                source,
                authority,
                now,
                now,
                next.LastEventSequence,
                summary,
                "runs/" + runId + "/events/" + next.LastEventSequence,
                PayloadAvailable: false));
            WriteProjectionUnsafe(next);
            return next;
        }
    }

    public EventPage ReadEvents(string runId, string? source = null, string? cursor = null, int limit = 50)
    {
        lock (_gate)
        {
            if (ReadProjectionUnsafe(runId) is null)
                throw new KeyNotFoundException($"Runtime run '{runId}' was not found");
            var after = DecodeCursor(cursor);
            var eventsPath = EventsPath(runId);
            if (!File.Exists(eventsPath)) return new EventPage(Array.Empty<RuntimeRunEvent>(), null);
            var events = File.ReadLines(eventsPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize<RuntimeRunEvent>(line, _json)!)
                .Where(item => item is not null && item.Sequence > after)
                .Where(item => string.IsNullOrWhiteSpace(source) || string.Equals(item.Source, source, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Sequence)
                .ToList();
            var boundedLimit = Math.Clamp(limit, 1, 100);
            var page = events.Take(boundedLimit).ToArray();
            var next = events.Count > page.Length && page.Length > 0
                ? EncodeCursor(page[^1].Sequence)
                : null;
            return new EventPage(page, next);
        }
    }

    private RuntimeRunProjection? FindByIdempotencyKeyUnsafe(string key) => FindAllUnsafe()
        .FirstOrDefault(run => string.Equals(run.IdempotencyKey, key, StringComparison.Ordinal));

    private RuntimeRunProjection? FindByLaunchIdUnsafe(string key) => FindAllUnsafe()
        .FirstOrDefault(run => string.Equals(run.LaunchId, key, StringComparison.Ordinal));

    private IEnumerable<RuntimeRunProjection> FindAllUnsafe()
    {
        foreach (var path in Directory.EnumerateFiles(_root, "*.snapshot.json"))
        {
            RuntimeRunProjection? projection;
            try { projection = JsonSerializer.Deserialize<RuntimeRunProjection>(File.ReadAllText(path), _json); }
            catch (JsonException) { continue; }
            if (projection is not null) yield return projection;
        }
    }

    private RuntimeRunProjection? ReadProjectionUnsafe(string runId)
    {
        if (!IsSafeId(runId)) return null;
        var path = ProjectionPath(runId);
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<RuntimeRunProjection>(File.ReadAllText(path), _json); }
        catch (JsonException) { return null; }
    }

    private void WriteProjectionUnsafe(RuntimeRunProjection projection)
    {
        var path = ProjectionPath(projection.RunId);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(projection, _json), Encoding.UTF8);
        File.Move(temp, path, overwrite: true);
    }

    private void WriteEventUnsafe(RuntimeRunEvent @event)
    {
        File.AppendAllText(EventsPath(@event.RunId), JsonSerializer.Serialize(@event, _lineJson) + Environment.NewLine, Encoding.UTF8);
    }

    private string ProjectionPath(string runId) => Path.Combine(_root, runId + ".snapshot.json");
    private string EventsPath(string runId) => Path.Combine(_root, runId + ".events.ndjson");
    private static bool IsSafeId(string value) => value.Length > 0 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.');
    private static bool IsTerminal(string status) => status is "completed" or "failed" or "interrupted";
    private static string? OutcomeFor(string status) => status switch { "completed" => "completion", "failed" => "failure", _ => "unknown" };
    private static JsonSerializerOptions CreateJsonOptions(bool writeIndented) => new(JsonSerializerDefaults.Web)
    {
        WriteIndented = writeIndented,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    private static string EncodeCursor(long sequence) => Convert.ToBase64String(Encoding.UTF8.GetBytes(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    private static long DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return 0;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return long.TryParse(raw, out var sequence) && sequence >= 0 ? sequence : throw new FormatException();
        }
        catch (Exception) { throw new FormatException("cursor must be an opaque cursor returned by Runtime Run events query"); }
    }

    private static bool IsBefore(RuntimeRunProjection run, ListCursor cursor) =>
        run.StartedAt < cursor.StartedAt
        || (run.StartedAt == cursor.StartedAt && string.CompareOrdinal(run.RunId, cursor.RunId) < 0);

    private static string EncodeListCursor(RuntimeRunProjection run) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        run.StartedAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture) + "\n" + run.RunId));

    private static ListCursor? DecodeListCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('\n', 2);
            if (parts.Length != 2 || !DateTimeOffset.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt) || !IsSafeId(parts[1]))
                throw new FormatException();
            return new ListCursor(startedAt, parts[1]);
        }
        catch (Exception) { throw new FormatException("cursor must be an opaque cursor returned by Runtime Run list query"); }
    }

    public sealed record CreateRequest(
        string? ProductSessionId,
        string? TaskInstanceId,
        string LaunchId,
        string IdempotencyKey,
        string CorrelationId,
        JsonElement? ProjectRef,
        JsonElement? TestSetRef,
        JsonElement? TaskRef,
        JsonElement? Environment);

    public sealed record EventPage(IReadOnlyList<RuntimeRunEvent> Events, string? NextCursor);
    public sealed record RunPage(IReadOnlyList<RuntimeRunProjection> Runs, string? NextCursor);
    private readonly record struct ListCursor(DateTimeOffset StartedAt, string RunId);

    public sealed record RuntimeRunProjection(
        string SchemaVersion,
        string ContractVersion,
        string RunId,
        string ProductSessionId,
        string TaskInstanceId,
        HostSessionRef? HostSessionRef,
        JsonElement? ProjectRef,
        JsonElement? TestSetRef,
        JsonElement? TaskRef,
        string LaunchId,
        string CorrelationId,
        JsonElement? Environment,
        string Status,
        string Phase,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        string? Outcome,
        string? Reason,
        long Revision,
        DateTimeOffset ObservedAt,
        long LastEventSequence,
        string Consistency,
        IReadOnlyList<RuntimeArtifactRef> Artifacts,
        string? IdempotencyKey = null);

    public sealed record HostSessionRef(string Host, string SessionId);
    public sealed record RuntimeArtifactRef(string ArtifactId, string Kind, string Availability, string? DetailEndpoint = null);
    public sealed record RuntimeRunEvent(
        string SchemaVersion,
        string ContractVersion,
        string EventId,
        string RunId,
        string EventType,
        string Source,
        string Authority,
        DateTimeOffset OccurredAt,
        DateTimeOffset? ObservedAt,
        long Sequence,
        string Summary,
        string? PayloadRef = null,
        bool PayloadAvailable = false);
}
