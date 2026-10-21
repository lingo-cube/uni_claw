using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UniClaw.Agent.Dsh;

namespace UniClaw.Host.Dsh.Tests;

/// <summary>Reads the authenticated DSH session log for one registered generation request.</summary>
public static class PlanGenerationTrajectoryCollector
{
    private const string SessionExportPath = "/api/session.export";
    private const string PromptProbePath = "/api/uniclaw-agent/prompt-probe";
    private static readonly Regex SessionLogName = new("^session(?:\\.v[0-9]+)?\\.jsonl$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Download one DSH session export and prompt probe, then retain raw and request-scoped evidence.
    /// </summary>
    /// <param name="endpoint">The configured DSH web service endpoint.</param>
    /// <param name="sessionId">The exact DSH session id returned by the successful handshake.</param>
    /// <param name="requestId">The consultation request id whose prompt rpcId starts with this value.</param>
    /// <param name="outputDirectory">A new, trial-owned output directory; existing evidence is never overwritten.</param>
    /// <param name="cancellationToken">Cancellation for both authenticated GET requests.</param>
    /// <returns>The raw calls, validation feedback, prompt, and provider usage for the selected turn set.</returns>
    public static async Task<PlanGenerationTrajectorySnapshot> CaptureAsync(
        DshServiceEndpoint endpoint,
        string sessionId,
        string requestId,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        endpoint.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (endpoint.BaseUri.IsLoopback && endpoint.BaseUri.Port == 3080)
            throw new InvalidOperationException("Refusing trajectory capture from the owner's DSH port 3080.");

        var credential = DshWebCredential.Mint(endpoint);
        using var client = new HttpClient { BaseAddress = endpoint.BaseUri, Timeout = TimeSpan.FromSeconds(30) };
        var exportBytes = await GetBytesAsync(client, credential.CookieHeader,
            SessionExportPath + "?sessionId=" + Uri.EscapeDataString(sessionId) + "&includeDescendants=false",
            cancellationToken).ConfigureAwait(false);
        var sessionLog = ReadRootSessionLog(exportBytes, sessionId);
        var probeJson = await GetTextAsync(client, credential.CookieHeader, PromptProbePath, cancellationToken)
            .ConfigureAwait(false);
        var snapshot = Parse(sessionId, requestId, sessionLog, probeJson);

        Directory.CreateDirectory(outputDirectory);
        var archivePath = Path.Combine(outputDirectory, "dsh-session-export.zip");
        var sessionLogPath = Path.Combine(outputDirectory, "dsh-session.raw.jsonl");
        var requestLogPath = Path.Combine(outputDirectory, "dsh-consultation.raw.jsonl");
        var promptProbePath = Path.Combine(outputDirectory, "dsh-prompt-probe.raw.json");
        WriteNew(archivePath, exportBytes);
        WriteNew(sessionLogPath, Encoding.UTF8.GetBytes(sessionLog));
        WriteNew(requestLogPath, Encoding.UTF8.GetBytes(snapshot.ConsultationRawJsonl));
        WriteNew(promptProbePath, Encoding.UTF8.GetBytes(probeJson));

        return snapshot with
        {
            ExportArchivePath = archivePath,
            SessionLogPath = sessionLogPath,
            ConsultationLogPath = requestLogPath,
            PromptProbePath = promptProbePath,
        };
    }

    internal static PlanGenerationTrajectorySnapshot Parse(
        string expectedSessionId,
        string requestId,
        string sessionLogJsonl,
        string promptProbeJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(sessionLogJsonl);
        ArgumentNullException.ThrowIfNull(promptProbeJson);

        var lines = sessionLogJsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) throw new InvalidDataException("DSH session export has no event rows.");
        using var headerDocument = JsonDocument.Parse(lines[0]);
        var header = headerDocument.RootElement;
        if (GetString(header, "type") != "session" || GetString(header, "id") != expectedSessionId)
            throw new InvalidDataException("DSH export header id does not match the requested session id.");

        var events = new List<ParsedEvent>(lines.Length - 1);
        foreach (var line in lines.Skip(1))
        {
            using var document = JsonDocument.Parse(line);
            events.Add(new ParsedEvent(line, document.RootElement.Clone()));
        }

        var requestPrefix = requestId + "-";
        var matchedPrompts = new List<(ParsedEvent Item, string RpcId, int Turn)>();
        var eventTurns = new Dictionary<ParsedEvent, int>();
        int? activeTurn = null;
        foreach (var item in events)
        {
            var type = GetString(item.Value, "type");
            var data = GetProperty(item.Value, "data");
            if (type == "turn/start")
            {
                activeTurn = GetInt32(data, "turn")
                    ?? throw new InvalidDataException("DSH turn/start event has no turn identity.");
            }
            if (activeTurn.HasValue) eventTurns[item] = activeTurn.Value;
            if (type == "user/message")
            {
                // In Session v4, user/message.data is the UserMessage itself, and it has no turn field.
                var rpcId = GetNestedString(data, "source", "rpcId");
                if (rpcId?.StartsWith(requestPrefix, StringComparison.Ordinal) == true)
                {
                    if (!activeTurn.HasValue)
                        throw new InvalidDataException("Matched DSH user message occurs outside an open turn.");
                    matchedPrompts.Add((item, rpcId, activeTurn.Value));
                }
            }
            else if (type == "turn/end")
            {
                activeTurn = null;
            }
        }
        if (matchedPrompts.Count == 0)
            throw new InvalidDataException($"DSH session contains no user prompt for request id '{requestId}'.");

        var turns = matchedPrompts.Select(item => item.Turn).Distinct().Order().ToArray();
        var turnSet = turns.ToHashSet();
        var selected = events.Where(item => eventTurns.TryGetValue(item, out var turn) && turnSet.Contains(turn)).ToArray();
        var selectedLines = selected.Select(item => item.Line).ToArray();

        var calls = new List<MutableSubmitCall>();
        var callsById = new Dictionary<string, MutableSubmitCall>(StringComparer.Ordinal);
        var rejections = new List<PlanGenerationRejection>();
        var usageAttempts = new List<PlanGenerationUsageAttempt>();
        var providerRetryStarted = 0;
        var providerRetryFailures = 0;
        var latestRoute = (Provider: (string?)null, Model: (string?)null);

        foreach (var item in selected)
        {
            var root = item.Value;
            var type = GetString(root, "type");
            var data = GetProperty(root, "data");
            if (type == "request/header")
            {
                var config = GetProperty(GetProperty(data, "header"), "config");
                latestRoute = (GetString(config, "provider"), GetString(config, "model"));
            }
            else if (type == "request/context")
            {
                latestRoute = (GetString(data, "provider") ?? latestRoute.Provider,
                    GetString(data, "model") ?? latestRoute.Model);
            }
            else if (type == "tool/call" && GetString(data, "name") == "submit_decision")
            {
                var callId = GetString(data, "callId") ?? throw new InvalidDataException("submit_decision call has no callId.");
                var call = new MutableSubmitCall(
                    GetInt64(root, "seq") ?? -1,
                    GetNestedInt32(data, "turn") ?? -1,
                    GetNestedInt32(data, "step") ?? -1,
                    callId,
                    GetString(data, "arguments") ?? throw new InvalidDataException("submit_decision call has no raw arguments."));
                calls.Add(call);
                callsById.Add(callId, call);
            }
            else if (type == "tool/result")
            {
                var message = GetProperty(data, "message");
                var callId = GetNestedString(message, "source", "callId")
                    ?? GetString(message, "toolCallId");
                if (callId is null || !callsById.TryGetValue(callId, out var call)) continue;
                var text = MessageText(message);
                call.ResultSeq = GetInt64(root, "seq");
                call.ResultText = text;
                call.Accepted = text.StartsWith("submit_decision accepted:", StringComparison.Ordinal);
                if (text.StartsWith("submit_decision rejected: ", StringComparison.Ordinal))
                {
                    var feedback = text["submit_decision rejected: ".Length..];
                    var separator = feedback.IndexOf("; ", StringComparison.Ordinal);
                    var reason = separator < 0 ? feedback : feedback[..separator];
                    var messageText = separator < 0 ? null : feedback[(separator + 2)..];
                    rejections.Add(new PlanGenerationRejection(callId, call.Seq, call.ResultSeq, text, reason, messageText));
                }
            }
            else if (type is "assistant/message" or "assistant/attempt")
            {
                var turn = GetNestedInt32(data, "turn") ?? -1;
                var step = GetNestedInt32(data, "step") ?? -1;
                var message = GetProperty(data, "message");
                var source = GetProperty(message, "source");
                var provider = GetString(source, "provider") ?? latestRoute.Provider;
                var model = GetString(source, "model") ?? latestRoute.Model;
                var usage = ExtractUsage(data, type);
                usageAttempts.Add(new PlanGenerationUsageAttempt(
                    GetInt64(root, "seq") ?? -1, type, turn, step, provider, model, usage));
            }
            else if (type == "llm/retry-started") providerRetryStarted++;
            else if (type == "llm/retry") providerRetryFailures++;
        }

        using var probeDocument = JsonDocument.Parse(promptProbeJson);
        var probe = probeDocument.RootElement;
        if (GetString(probe, "dshSessionId") != expectedSessionId)
            throw new InvalidDataException("DSH prompt-probe session id does not match the requested session id.");
        if (GetProperty(probe, "recorded").ValueKind != JsonValueKind.True)
            throw new InvalidDataException("DSH prompt-probe did not record a completed assembly.");
        if (GetString(probe, "error") is { Length: > 0 } probeError)
            throw new InvalidDataException("DSH prompt-probe assembly failed: " + probeError);
        var sections = GetStringArray(probe, "sections");
        var systemPrompt = ReadCurrentSystemPrompt(events,
            selected.Max(item => GetInt64(item.Value, "seq") ?? -1));
        var orderedUsage = usageAttempts.OrderBy(attempt => attempt.Seq).ToArray();
        var attemptsWithUsage = orderedUsage.Where(attempt => attempt.RawUsage.HasValue).ToArray();
        var usageStatus = attemptsWithUsage.Length == 0 ? "unknown"
            : attemptsWithUsage.Length == orderedUsage.Length ? "complete" : "partial";
        var inputTotal = SumUsage(attemptsWithUsage, "inputTokens");
        var outputTotal = SumUsage(attemptsWithUsage, "outputTokens");
        var totalTotal = SumUsage(attemptsWithUsage, "totalTokens");
        if (attemptsWithUsage.Length != orderedUsage.Length)
        {
            inputTotal = null;
            outputTotal = null;
            totalTotal = null;
        }

        return new PlanGenerationTrajectorySnapshot(
            expectedSessionId,
            requestId,
            turns,
            calls.Select(call => call.ToRecord()).ToArray(),
            rejections,
            systemPrompt,
            sections,
            expectedSessionId,
            orderedUsage,
            providerRetryStarted,
            providerRetryFailures,
            matchedPrompts.Count(item => item.RpcId.Contains("-nudge-", StringComparison.Ordinal)),
            usageStatus,
            inputTotal,
            outputTotal,
            totalTotal,
            string.Join('\n', selectedLines) + "\n",
            null,
            null,
            null,
            null);
    }

    private static async Task<byte[]> GetBytesAsync(
        HttpClient client, string cookie, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException($"Authenticated DSH session export was rejected ({(int)response.StatusCode}).");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> GetTextAsync(
        HttpClient client, string cookie, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException($"Authenticated DSH prompt probe was rejected ({(int)response.StatusCode}).");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ReadRootSessionLog(byte[] exportBytes, string sessionId)
    {
        using var stream = new MemoryStream(exportBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var rootLogs = archive.Entries.Where(entry => !entry.FullName.Contains("/", StringComparison.Ordinal)
            && SessionLogName.IsMatch(entry.Name)).ToArray();
        if (rootLogs.Length != 1) throw new InvalidDataException("DSH export must contain exactly one root session JSONL log.");
        using var entryStream = rootLogs[0].Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var jsonl = reader.ReadToEnd();
        using var firstRow = JsonDocument.Parse(jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0]);
        if (GetString(firstRow.RootElement, "id") != sessionId)
            throw new InvalidDataException("DSH export root log id does not match the requested session id.");
        return jsonl;
    }

    private static string? ReadCurrentSystemPrompt(IReadOnlyList<ParsedEvent> events, long throughSeq)
    {
        var surface = new List<(long Seq, string Type, string Text)>();
        foreach (var item in events)
        {
            var root = item.Value;
            var seq = GetInt64(root, "seq") ?? -1;
            if (seq > throughSeq) break;
            var type = GetString(root, "type");
            if (type is not ("system/message" or "developer/message" or "user/message" or "assistant/message" or "tool/result"))
                continue;
            var op = GetProperty(root, "surfaceOp");
            if (op.ValueKind == JsonValueKind.String && op.GetString() == "append")
            {
                surface.Add((seq, type!, MessageText(EventMessage(root, type!))));
                continue;
            }
            if (op.ValueKind != JsonValueKind.Object || GetString(op, "op") != "replace")
                throw new InvalidDataException($"Surface event {seq} has no supported surface operation.");
            var start = GetInt64(op, "startSeq") ?? GetInt64(op, "start")
                ?? throw new InvalidDataException("Surface replacement has no startSeq.");
            var end = GetInt64(op, "endSeq") ?? GetInt64(op, "end")
                ?? throw new InvalidDataException("Surface replacement has no endSeq.");
            var first = surface.FindIndex(node => node.Seq == start);
            var last = surface.FindIndex(node => node.Seq == end);
            if (first < 0 || last < first) throw new InvalidDataException("Surface replacement references absent nodes.");
            surface.RemoveRange(first, last - first + 1);
            surface.Insert(first, (seq, type!, MessageText(EventMessage(root, type!))));
        }
        var prompts = surface.Where(node => node.Type == "system/message").Select(node => node.Text).ToArray();
        return prompts.Length == 0 ? null : string.Join("\n", prompts);
    }

    private static string MessageText(JsonElement message)
    {
        var content = GetProperty(message, "content");
        if (content.ValueKind != JsonValueKind.Array) return "";
        return string.Concat(content.EnumerateArray()
            .Where(block => GetString(block, "type") == "text")
            .Select(block => GetString(block, "text") ?? ""));
    }

    private static JsonElement EventMessage(JsonElement root, string type)
    {
        var data = GetProperty(root, "data");
        return type == "user/message" ? data : GetProperty(data, "message");
    }

    private static JsonElement? ExtractUsage(JsonElement data, string eventType)
    {
        var direct = GetProperty(data, "usage");
        if (direct.ValueKind == JsonValueKind.Object) return direct.Clone();
        var stream = GetProperty(data, "stream");
        if (stream.ValueKind != JsonValueKind.Array) return null;
        foreach (var entry in stream.EnumerateArray().Reverse())
        {
            if (GetString(entry, "type") != "chunk") continue;
            var chunk = GetProperty(entry, "chunk");
            if (GetString(chunk, "type") == "usage" && GetProperty(chunk, "usage").ValueKind == JsonValueKind.Object)
                return GetProperty(chunk, "usage").Clone();
        }
        return null;
    }

    private static long? SumUsage(IReadOnlyList<PlanGenerationUsageAttempt> attempts, string field)
    {
        long total = 0;
        foreach (var attempt in attempts)
        {
            if (!attempt.RawUsage.HasValue || !TryGetInt64(attempt.RawUsage.Value, field, out var value)) return null;
            total = checked(total + value);
        }
        return total;
    }

    private static string[] GetStringArray(JsonElement parent, string name)
    {
        var value = GetProperty(parent, name);
        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!).ToArray()
            : [];
    }

    private static JsonElement GetProperty(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value : default;

    private static string? GetString(JsonElement parent, string name)
    {
        var value = GetProperty(parent, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string? GetNestedString(JsonElement parent, params string[] path)
    {
        foreach (var part in path) parent = GetProperty(parent, part);
        return parent.ValueKind == JsonValueKind.String ? parent.GetString() : null;
    }

    private static int? GetNestedInt32(JsonElement parent, params string[] path)
    {
        foreach (var part in path) parent = GetProperty(parent, part);
        return parent.ValueKind == JsonValueKind.Number && parent.TryGetInt32(out var value) ? value : null;
    }

    private static int? GetInt32(JsonElement parent, string name)
    {
        var value = GetProperty(parent, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
    }

    private static long? GetInt64(JsonElement parent, string name)
    {
        var value = GetProperty(parent, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;
    }

    private static bool TryGetInt64(JsonElement parent, string name, out long value)
    {
        var field = GetProperty(parent, name);
        if (field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out value)) return true;
        value = 0;
        return false;
    }

    private static void WriteNew(string path, byte[] content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(content);
    }

    private sealed record ParsedEvent(string Line, JsonElement Value);

    private sealed class MutableSubmitCall(long seq, int turn, int step, string callId, string rawArguments)
    {
        public long Seq { get; } = seq;
        public int Turn { get; } = turn;
        public int Step { get; } = step;
        public string CallId { get; } = callId;
        public string RawArguments { get; } = rawArguments;
        public long? ResultSeq { get; set; }
        public string? ResultText { get; set; }
        public bool? Accepted { get; set; }
        public PlanGenerationSubmitCall ToRecord() => new(Seq, Turn, Step, CallId, RawArguments, ResultSeq, ResultText, Accepted);
    }
}

/// <summary>One raw submitted Product decision and its paired tool result, if any.</summary>
public sealed record PlanGenerationSubmitCall(
    long Seq, int Turn, int Step, string CallId, string RawArguments,
    long? ResultSeq, string? ResultText, bool? Accepted);

/// <summary>One validation rejection returned to the model for correction.</summary>
public sealed record PlanGenerationRejection(
    string CallId, long CallSeq, long? ResultSeq, string Text, string Reason, string? Message);

/// <summary>Usage evidence for one settled or failed model attempt.</summary>
public sealed record PlanGenerationUsageAttempt(
    long Seq, string EventType, int Turn, int Step, string? Provider, string? Model, JsonElement? RawUsage);

/// <summary>Auditable trajectory extracted from one DSH session and one request-id prefix.</summary>
public sealed record PlanGenerationTrajectorySnapshot(
    string DshSessionId,
    string RequestId,
    IReadOnlyList<int> Turns,
    IReadOnlyList<PlanGenerationSubmitCall> SubmitCalls,
    IReadOnlyList<PlanGenerationRejection> Rejections,
    string? SystemPrompt,
    IReadOnlyList<string> Sections,
    string? PromptProbeDshSessionId,
    IReadOnlyList<PlanGenerationUsageAttempt> UsageAttempts,
    int ProviderRetryStartedCount,
    int ProviderRetryFailureCount,
    int NoSubmitNudgeCount,
    string UsageStatus,
    long? InputTokenTotal,
    long? OutputTokenTotal,
    long? TotalTokenTotal,
    string ConsultationRawJsonl,
    string? ExportArchivePath,
    string? SessionLogPath,
    string? ConsultationLogPath,
    string? PromptProbePath)
{
    /// <summary>Subsequent submit_decision calls after the first submitted draft.</summary>
    public int SubmitRetryCount => Math.Max(0, SubmitCalls.Count - 1);
}
