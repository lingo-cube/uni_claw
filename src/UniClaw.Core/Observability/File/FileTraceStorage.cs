using System.Text.Json;
using UniClaw.Core.Domain;

namespace UniClaw.Core.Observability;

/// <summary>
/// FileTraceStorage — ITraceStorage implementation writing trace records to JSONL files.
/// Each write appends a JSON line with record_type discriminator to {baseDir}/{traceId}/trace.jsonl.
/// Session metadata stored in {baseDir}/{traceId}/session.json.
/// Uses IFileProvider abstraction to keep Core decoupled from System.IO (D-91).
/// Throws IOException on write failure (D-93). Read methods tolerate corrupted lines (skip).
/// Index methods (GetByNodeId, GetBySpanType) are off-interface (ISP D-2b, same as InMemoryTraceStorage).
/// </summary>
public sealed class FileTraceStorage : ITraceStorage
{
    private readonly IFileProvider _fileProvider;
    private readonly string _baseDir;
    private string? _currentTraceId;

    /// <summary>
    /// Construct FileTraceStorage with IFileProvider and optional baseDir.
    /// </summary>
    public FileTraceStorage(IFileProvider fileProvider, string baseDir = "traces")
    {
        _fileProvider = fileProvider;
        _baseDir = baseDir;
    }

    // ── Helper paths ────────────────────────────────────────

    private string TraceDir => $"{_baseDir}/{_currentTraceId}";
    private string TraceFilePath => $"{TraceDir}/trace.jsonl";
    private string SessionFilePath => $"{TraceDir}/session.json";

    // ── JSONL discriminator constants ────────────────────────

    private const string RecordTypeExecution = "execution";
    private const string RecordTypeTransition = "state_transition";
    private const string RecordTypeError = "error";
    private const string RecordTypePageTransition = "page_transition";
    private const string RecordTypeAICall = "ai_call";
    private const string RecordTypeSpan = "span";  // D-134, trace-span-observability P1

    // ── ITraceStorage: Session lifecycle ─────────────────────

    public TraceSession? CurrentSession
    {
        get
        {
            if (_currentTraceId == null)
                return null;

            var json = _fileProvider.ReadAllText(SessionFilePath);
            if (json == null)
                return null;

            try
            {
                return JsonSerializer.Deserialize<TraceSession>(json, DomainJsonOptions.Default);
            }
            catch (JsonException)
            {
                return null; // Corrupted session.json → null (don't block trace data)
            }
        }
    }

    public void SetSession(TraceSession session)
    {
        _currentTraceId = session.TraceId;
        _fileProvider.EnsureDirectory(TraceDir);

        var json = JsonSerializer.Serialize(session, DomainJsonOptions.Default);
        _fileProvider.AppendLine(SessionFilePath, json);
    }

    public void EndSession()
    {
        if (_currentTraceId == null)
            return;

        var session = CurrentSession;
        if (session != null)
        {
            var ended = session with { EndTime = DateTimeOffset.UtcNow };
            var json = JsonSerializer.Serialize(ended, DomainJsonOptions.Default);

            // Overwrite session.json with updated session (EndTime populated, D-102)
            _fileProvider.WriteAllText(SessionFilePath, json);
        }

        _currentTraceId = null;
    }

    // ── ITraceStorage: Synchronous write ─────────────────────

    public void AddExecution(ExecutionRecord record)
    {
        var line = SerializeWithDiscriminator(record, RecordTypeExecution);
        _fileProvider.AppendLine(TraceFilePath, line);
    }

    public void AddTransition(StateTransition transition)
    {
        var line = SerializeWithDiscriminator(transition, RecordTypeTransition);
        _fileProvider.AppendLine(TraceFilePath, line);
    }

    public void AddError(ErrorRecord record)
    {
        var line = SerializeWithDiscriminator(record, RecordTypeError);
        _fileProvider.AppendLine(TraceFilePath, line);
    }

    public void AddPageTransition(PageTransition transition)
    {
        var line = SerializeWithDiscriminator(transition, RecordTypePageTransition);
        _fileProvider.AppendLine(TraceFilePath, line);
    }

    public void AddAICall(AICallRecord record)
    {
        var line = SerializeWithDiscriminator(record, RecordTypeAICall);
        _fileProvider.AppendLine(TraceFilePath, line);
    }

    // ── ITraceStorage: Synchronous read ──────────────────────

    public IReadOnlyList<ExecutionRecord> GetExecutions()
        => DeserializeByType<ExecutionRecord>(RecordTypeExecution);

    public IReadOnlyList<StateTransition> GetTransitions()
        => DeserializeByType<StateTransition>(RecordTypeTransition);

    public IReadOnlyList<ErrorRecord> GetErrors()
        => DeserializeByType<ErrorRecord>(RecordTypeError);

    public IReadOnlyList<PageTransition> GetPageTransitions()
        => DeserializeByType<PageTransition>(RecordTypePageTransition);

    public IReadOnlyList<AICallRecord> GetAICalls()
        => DeserializeByType<AICallRecord>(RecordTypeAICall);

    public string Export()
    {
        var lines = _fileProvider.ReadAllLines(TraceFilePath);
        // Wrap all lines in a JSON array format compatible with InMemoryTraceStorage.Export()
        // Each line is already a valid JSON object; we join them into an array.
        return "[" + string.Join(",", lines) + "]";
    }

    // ── ITraceStorage: TraceSpan write/read (D-134) ───────

    /// <summary>Open a span — append an open-span line to the JSONL file.</summary>
    public string OpenSpan(string spanType, string spanName, string spanId,
        string? parentSpanId, DateTimeOffset startTime, TraceContext? context,
        Dictionary<string, object>? attributes)
    {
        var span = new TraceSpan(spanId, parentSpanId, spanType, spanName,
            startTime, null, "ok", context, attributes);
        var line = SerializeWithDiscriminator(span, RecordTypeSpan);
        _fileProvider.AppendLine(TraceFilePath, line);
        return spanId;
    }

    /// <summary>Close a span — append a closed-span line (EndTime + Status) to the JSONL file.</summary>
    public void CloseSpan(string spanId, DateTimeOffset endTime, string status,
        Dictionary<string, object>? attributes)
    {
        // With append-only JSONL, close is a second write. Read methods deduplicate by
        // spanId, keeping the last occurrence (the closed version).
        var open = FindSpan(spanId);
        if (open == null) return; // no-op for unknown spanId

        // Merge attributes
        Dictionary<string, object>? merged = null;
        if (attributes != null || open.Attributes != null)
        {
            merged = new(open.Attributes ?? new Dictionary<string, object>());
            if (attributes != null)
            {
                foreach (var kv in attributes)
                    merged[kv.Key] = kv.Value;
            }
            if (merged.Count == 0) merged = null;
        }

        var closed = open with { EndTime = endTime, Status = status, Attributes = merged };
        var line = SerializeWithDiscriminator(closed, RecordTypeSpan);
        _fileProvider.AppendLine(TraceFilePath, line);
    }

    /// <summary>Find a span by its SpanId (latest occurrence wins — close overrides open).</summary>
    public TraceSpan? FindSpan(string spanId)
    {
        return GetAllSpans().LastOrDefault(s => s.SpanId == spanId);
    }

    /// <summary>Get all spans, deduplicated by spanId (last occurrence wins).</summary>
    public IReadOnlyList<TraceSpan> GetAllSpans()
    {
        var spans = DeserializeByType<TraceSpan>(RecordTypeSpan);
        // Deduplicate: keep the last occurrence for each spanId (close overrides open)
        var deduped = new Dictionary<string, TraceSpan>();
        foreach (var span in spans)
            deduped[span.SpanId] = span;
        return deduped.Values.OrderBy(s => s.StartTime).ToList();
    }

    /// <summary>Get all spans matching a dotted spanType string.</summary>
    public IReadOnlyList<TraceSpan> GetSpansByType(string spanType)
    {
        return GetAllSpans().Where(s => s.SpanType == spanType).ToList();
    }

    /// <summary>Get all child spans whose ParentSpanId matches the given id.</summary>
    public IReadOnlyList<TraceSpan> GetChildSpans(string parentSpanId)
    {
        return GetAllSpans().Where(s => s.ParentSpanId == parentSpanId).ToList();
    }

    // ── FileTraceStorage-specific index methods (NOT on ITraceStorage — ISP D-2b) ──

    /// <summary>Get execution records grouped by Context.NodeId (query-time computation, D-94)</summary>
    public IReadOnlyList<ExecutionRecord> GetByNodeId(string nodeId)
    {
        return GetExecutions()
            .Where(r => r.Context?.NodeId == nodeId)
            .ToList();
    }

    /// <summary>Get execution records grouped by SpanType (query-time computation, D-94)</summary>
    public IReadOnlyList<ExecutionRecord> GetBySpanType(SpanType spanType)
    {
        return GetExecutions()
            .Where(r => r.SpanType == spanType)
            .ToList();
    }

    // ── Private helpers ──────────────────────────────────────

    /// <summary>
    /// Serialize a record with record_type discriminator as the first field.
    /// Uses DomainJsonOptions.Default (camelCase + enum-as-string + null skip, D-91).
    /// </summary>
    private static string SerializeWithDiscriminator(object record, string recordType)
    {
        // Serialize the record first, then inject record_type as the first field
        var baseJson = JsonSerializer.Serialize(record, DomainJsonOptions.Default);

        // Insert record_type at the beginning of the JSON object
        // baseJson starts with "{", so we inject after it
        return $"{{\"record_type\":\"{recordType}\",{baseJson.Substring(1)}";
    }

    /// <summary>
    /// Deserialize all lines matching a record_type discriminator.
    /// Skips corrupted/invalid JSON lines. Returns empty collection for nonexistent trace.
    /// </summary>
    private IReadOnlyList<T> DeserializeByType<T>(string expectedRecordType)
    {
        var lines = _fileProvider.ReadAllLines(TraceFilePath);
        if (lines.Count == 0)
            return Array.Empty<T>();

        var results = new List<T>();

        foreach (var line in lines)
        {
            // Quick check: does this line contain the expected record_type?
            // Full deserialize to verify, but skip invalid JSON lines
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (root.TryGetProperty("record_type", out var typeProp)
                    && typeProp.GetString() == expectedRecordType)
                {
                    // Remove record_type discriminator before deserializing to the target type
                    // (record_type is not part of the C# record definition)
                    var cleanJson = RemoveDiscriminator(line);
                    var deserialized = JsonSerializer.Deserialize<T>(cleanJson, DomainJsonOptions.Default);
                    if (deserialized != null)
                        results.Add(deserialized);
                }
            }
            catch (JsonException)
            {
                // Corrupted line — skip (D-93: single corrupted line should not block entire trace read)
            }
        }

        return results;
    }

    /// <summary>
    /// Remove the record_type discriminator field from a JSONL line,
    /// so the remaining JSON can be deserialized to the target C# record type.
    /// </summary>
    private static string RemoveDiscriminator(string jsonLine)
    {
        // Find and remove the "record_type":"xxx" field from the JSON object
        // The record_type is always the first field (as written by SerializeWithDiscriminator)
        // Pattern: {"record_type":"xxx",{rest}}
        // We need to remove the "record_type":"xxx", prefix and restore the {rest}
        const string prefixPattern = "{\"record_type\":\"";
        var prefixEnd = jsonLine.IndexOf("\",{", StringComparison.Ordinal);
        if (prefixEnd < 0)
            return jsonLine; // Fallback: return as-is if pattern not found

        // Extract: {"record_type":"execution",{actual content}}
        // Result: {actual content}
        return jsonLine.Substring(prefixEnd + 2);
    }
}
