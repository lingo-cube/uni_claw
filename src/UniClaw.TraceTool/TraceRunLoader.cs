using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Core.Observability;
using UniClaw.Host.Artifacts;

namespace UniClaw.TraceTool;

public static class TraceRunLoader
{
    /// <summary>
    /// Load a run directory: replay trace.jsonl → InMemoryTraceService (ITraceQuery),
    /// deserialize manifest.json / result.json / issues.jsonl, enumerate step assets.
    /// Read-only — never writes into the run directory.
    /// </summary>
    public static async Task<TraceRun> LoadAsync(
        string runDir,
        CancellationToken cancellationToken = default)
    {
        // 1. Read manifest.json
        var manifestPath = Path.Combine(runDir, "manifest.json");
        RunManifest? manifest = null;
        if (File.Exists(manifestPath))
        {
            var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            manifest = JsonSerializer.Deserialize<RunManifest>(manifestJson, JsonOptions);
        }

        // 1b. Version dispatch: "1" → V1 layout, "2" → V2 layout, missing → V1
        // (backward compat). Unknown → loud failure — the reader cannot interpret
        // the layout, and a silently wrong read would corrupt analysis output.
        var schemaVersion = manifest?.SchemaVersion ?? "1";
        if (!RunLayoutV2.IsKnownVersion(schemaVersion))
        {
            throw new InvalidOperationException(
                RunLayoutV2.UnsupportedVersionMessage(schemaVersion));
        }

        // 2. Read result.json
        var resultPath = Path.Combine(runDir, "result.json");
        RunResult? result = null;
        if (File.Exists(resultPath))
        {
            var resultJson = await File.ReadAllTextAsync(resultPath, cancellationToken);
            result = JsonSerializer.Deserialize<RunResult>(resultJson, JsonOptions);
        }

        // 3. Replay trace via FileTraceStorage → InMemoryTraceStorage → InMemoryTraceService
        // TracePath format (finalized runs): "trace/{runId}/trace.jsonl", which matches
        // FileTraceStorage's {baseDir}/{traceId}/trace.jsonl layout:
        //   baseDir = {runDir}/trace, traceId = {runId}.
        // result.json from an interrupted run may still hold the initial placeholder
        // "trace/trace.jsonl" (no traceId segment) — fall back to the runId-keyed path.
        var tracePath = result?.TracePath ?? "trace/trace.jsonl";
        var traceParts = tracePath.Split('/');
        var traceDir = Path.Combine(runDir, traceParts[0]);
        var traceId = traceParts.Length >= 2 ? traceParts[1] : null;
        var fullTraceFile = traceId != null
            ? Path.Combine(traceDir, traceId, "trace.jsonl")
            : Path.Combine(runDir, tracePath);

        if (!File.Exists(fullTraceFile)
            && result != null
            && traceId != result.RunId)
        {
            // Stale initial TracePath ("trace/trace.jsonl") — the durable trace is
            // always keyed by the runId: {runDir}/trace/{runId}/trace.jsonl.
            fullTraceFile = Path.Combine(traceDir, result.RunId, "trace.jsonl");
            traceId = result.RunId;
        }

        InMemoryTraceService traceService;
        if (File.Exists(fullTraceFile) && traceId != null)
        {
            // Read-only replay: baseDir = the traceId directory, NO SetSession.
            // SetSession would append a session.json line into the run directory
            // (corrupting the session written by the Host run) and throw on read-only
            // artifacts — a read-only analyzer must not write. FileTraceStorage with
            // _currentTraceId == null reads {baseDir}/trace.jsonl, which is exactly
            // the file we want.
            var fileStorage = new FileTraceStorage(
                new PhysicalFileProvider(),
                Path.Combine(traceDir, traceId));

            var memStorage = new InMemoryTraceStorage();

            foreach (var exec in fileStorage.GetExecutions())
                memStorage.AddExecution(exec);
            foreach (var transition in fileStorage.GetTransitions())
                memStorage.AddTransition(transition);
            foreach (var error in fileStorage.GetErrors())
                memStorage.AddError(error);
            foreach (var pageTransition in fileStorage.GetPageTransitions())
                memStorage.AddPageTransition(pageTransition);
            foreach (var aiCall in fileStorage.GetAICalls())
                memStorage.AddAICall(aiCall);
            foreach (var span in fileStorage.GetAllSpans())
                ReplaySpan(memStorage, span);

            traceService = new InMemoryTraceService(memStorage);
        }
        else
        {
            // No trace file — empty service
            traceService = new InMemoryTraceService(new InMemoryTraceStorage());
        }

        // 4. Enumerate step assets (lazy) — version-aware location:
        //    V2 → {runDir}/assets/{runId}/steps/, V1 → {runDir}/steps/.
        var runIdFromManifest = manifest?.RunId ?? result?.RunId ?? "unknown";
        var stepsDir = schemaVersion == "2"
            ? Path.Combine(runDir, "assets", runIdFromManifest, "steps")
            : Path.Combine(runDir, "steps");
        var stepAssets = Directory.Exists(stepsDir)
            ? Directory.GetDirectories(stepsDir)
                .Select(dir => new StepAsset(dir))
                .ToArray()
            : Array.Empty<StepAsset>();

        // 5. Load issues.jsonl (D-192 issue records written by the Host run).
        // Each line is a serialized RunIssue; absence → empty collection, and
        // malformed lines are skipped — a run directory never fails to load,
        // consistent with result.json's degradation semantics.
        var issues = new List<RunIssue>();
        var issuesPath = Path.Combine(runDir, "issues.jsonl");
        if (File.Exists(issuesPath))
        {
            var lines = await File.ReadAllLinesAsync(issuesPath, cancellationToken);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var issue = JsonSerializer.Deserialize<RunIssue>(line, JsonOptions);
                    if (issue is not null)
                        issues.Add(issue);
                }
                catch (JsonException)
                {
                    // Malformed line — skip.
                }
            }
        }

        return new TraceRun(runDir, manifest, result, traceService, stepAssets, issues);
    }

    /// <summary>
    /// Replay a span from the file into the in-memory store: OpenSpan restores the
    /// span with its original spanId and metadata; CloseSpan applies EndTime/Status
    /// when the file holds the closed version (GetAllSpans dedupes by spanId, so the
    /// closed occurrence wins for closed spans).
    /// </summary>
    private static void ReplaySpan(InMemoryTraceStorage storage, TraceSpan span)
    {
        storage.OpenSpan(
            span.SpanType,
            span.SpanName,
            span.SpanId,
            span.ParentSpanId,
            span.StartTime,
            span.Context,
            span.Attributes);
        if (span.EndTime.HasValue)
        {
            storage.CloseSpan(span.SpanId, span.EndTime.Value, span.Status, null);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
