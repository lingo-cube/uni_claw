using System.Text.Json;
using UniClaw.Core.Domain;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability.File;

/// <summary>
/// FileTraceStorage tests — validate JSONL write/read/index/error handling via MockFileProvider.
/// </summary>
public class FileTraceStorageTests
{
    private readonly MockFileProvider _provider = new();

    private FileTraceStorage CreateStorage(string baseDir = "traces")
        => new FileTraceStorage(_provider, baseDir);

    private TraceSession CreateSession(string traceId = "test-trace-001")
        => new TraceSession(traceId, DateTimeOffset.UtcNow);

    // ── 4.2: StartSession/EndSession ─────────────────────────

    [Fact]
    public void SetSession_CreatesDirectoryAndWritesSessionJson()
    {
        var storage = CreateStorage();
        var session = CreateSession();

        storage.SetSession(session);

        Assert.True(_provider.DirectoryExists("traces/test-trace-001"));
        Assert.True(_provider.FileExists("traces/test-trace-001/session.json"));
        var content = _provider.ReadAllText("traces/test-trace-001/session.json");
        Assert.Contains("test-trace-001", content);
    }

    [Fact]
    public void EndSession_OverwritesSessionJsonWithEndTime()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());

        storage.EndSession();

        // session.json should have been overwritten with EndTime populated (D-102)
        var content = _provider.ReadAllText("traces/test-trace-001/session.json");
        Assert.NotNull(content);
        Assert.Contains("endTime", content);  // camelCase serialization of EndTime field
    }

    [Fact]
    public void CurrentSession_ReturnsNullBeforeSetSession()
    {
        var storage = CreateStorage();
        Assert.Null(storage.CurrentSession);
    }

    // ── 4.3: Write methods ────────────────────────────────────

    [Fact]
    public void AddExecution_WritesExecutionRecordType()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());

        var record = new ExecutionRecord("click", "success", SpanType.DfsForward);
        storage.AddExecution(record);

        var content = _provider.ReadAllText("traces/test-trace-001/trace.jsonl");
        Assert.Contains("\"record_type\":\"execution\"", content);
        Assert.Contains("\"action\":\"click\"", content);
    }

    [Fact]
    public void AddTransition_WritesStateTransitionRecordType()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());

        var transition = new StateTransition("Idle", "Traversing");
        storage.AddTransition(transition);

        var content = _provider.ReadAllText("traces/test-trace-001/trace.jsonl");
        Assert.Contains("\"record_type\":\"state_transition\"", content);
        Assert.Contains("\"fromState\":\"Idle\"", content);
    }

    [Fact]
    public void AddError_WritesErrorRecordType()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());

        var error = new ErrorRecord("Timeout", "Step exceeded limit", ErrorSeverity.Error);
        storage.AddError(error);

        var content = _provider.ReadAllText("traces/test-trace-001/trace.jsonl");
        Assert.Contains("\"record_type\":\"error\"", content);
        Assert.Contains("\"errorType\":\"Timeout\"", content);
    }

    [Fact]
    public void AddPageTransition_WritesPageTransitionRecordType()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());

        var pt = new PageTransition("home", "wifi", "forward");
        storage.AddPageTransition(pt);

        var content = _provider.ReadAllText("traces/test-trace-001/trace.jsonl");
        Assert.Contains("\"record_type\":\"page_transition\"", content);
        Assert.Contains("\"fromPage\":\"home\"", content);
    }

    [Fact]
    public void AddAICall_WritesAICallRecordType()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());

        var ai = new AICallRecord("vision", "mock", true, 10.5);
        storage.AddAICall(ai);

        var content = _provider.ReadAllText("traces/test-trace-001/trace.jsonl");
        Assert.Contains("\"record_type\":\"ai_call\"", content);
        Assert.Contains("\"capability\":\"vision\"", content);
    }

    // ── 4.4: Read methods ──────────────────────────────────────

    [Fact]
    public void GetExecutions_DeserializesExecutionRecords()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        storage.AddExecution(new ExecutionRecord("click", "success", SpanType.DfsForward));
        storage.AddExecution(new ExecutionRecord("back", "success", SpanType.DfsBacktrack));

        var result = storage.GetExecutions();
        Assert.Equal(2, result.Count);
        Assert.Equal("click", result[0].Action);
        Assert.Equal("back", result[1].Action);
    }

    [Fact]
    public void GetTransitions_DeserializesStateTransitions()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        storage.AddTransition(new StateTransition("Idle", "Traversing"));

        var result = storage.GetTransitions();
        Assert.Single(result);
        Assert.Equal("Idle", result[0].FromState);
    }

    [Fact]
    public void GetErrors_DeserializesErrorRecords()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        storage.AddError(new ErrorRecord("Timeout", "limit exceeded", ErrorSeverity.Error));

        var result = storage.GetErrors();
        Assert.Single(result);
        Assert.Equal("Timeout", result[0].ErrorType);
    }

    [Fact]
    public void GetPageTransitions_DeserializesPageTransitions()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        storage.AddPageTransition(new PageTransition("home", "wifi", "forward"));

        var result = storage.GetPageTransitions();
        Assert.Single(result);
        Assert.Equal("home", result[0].FromPage);
    }

    [Fact]
    public void GetAICalls_DeserializesAICallRecords()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        storage.AddAICall(new AICallRecord("vision", "mock", true, 10.5));

        var result = storage.GetAICalls();
        Assert.Single(result);
        Assert.Equal("vision", result[0].Capability);
    }

    // ── 4.5: Index methods ──────────────────────────────────────

    [Fact]
    public void GetByNodeId_GroupsExecutionRecordsByNodeId()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        var ctx = new TraceContext(NodeId: "wifi_node");
        storage.AddExecution(new ExecutionRecord("click", "success", SpanType.DfsForward, ctx));
        storage.AddExecution(new ExecutionRecord("click", "success", SpanType.DfsForward, new TraceContext(NodeId: "bt_node")));

        var result = storage.GetByNodeId("wifi_node");
        Assert.Single(result);
        Assert.Equal("wifi_node", result[0].Context?.NodeId);
    }

    [Fact]
    public void GetBySpanType_GroupsExecutionRecordsBySpanType()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        storage.AddExecution(new ExecutionRecord("click", "success", SpanType.DfsForward));
        storage.AddExecution(new ExecutionRecord("back", "success", SpanType.DfsBacktrack));

        var forward = storage.GetBySpanType(SpanType.DfsForward);
        Assert.Single(forward);
        Assert.Equal(SpanType.DfsForward, forward[0].SpanType);
    }

    // ── 4.6: JSONL format validation ───────────────────────────

    [Fact]
    public void JsonlLine_UsesCamelCasePropertyNames()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        storage.AddExecution(new ExecutionRecord("click", "success", SpanType.DfsForward));

        var line = _provider.ReadAllLines("traces/test-trace-001/trace.jsonl")[0];
        Assert.Contains("\"record_type\"", line);     // snake_case discriminator (Python compat)
        Assert.Contains("\"spanType\"", line);         // camelCase C# property
        Assert.Contains("\"action\"", line);           // camelCase
    }

    [Fact]
    public void JsonlLine_SerializesEnumAsString()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        storage.AddExecution(new ExecutionRecord("click", "success", SpanType.DfsForward));

        var line = _provider.ReadAllLines("traces/test-trace-001/trace.jsonl")[0];
        // DomainJsonOptions uses JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        Assert.Contains("\"dfsForward\"", line);  // camelCase enum as string, not integer
    }

    // ── 4.7: Error handling ─────────────────────────────────────

    [Fact]
    public void ReadMethods_ReturnEmptyForNonexistentTraceId()
    {
        var storage = CreateStorage();
        // No SetSession called → no trace directory → all reads return empty

        Assert.Empty(storage.GetExecutions());
        Assert.Empty(storage.GetTransitions());
        Assert.Empty(storage.GetErrors());
        Assert.Empty(storage.GetPageTransitions());
        Assert.Empty(storage.GetAICalls());
    }

    [Fact]
    public void ReadMethods_SkipCorruptedJsonlLines()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());

        // Write valid execution record
        storage.AddExecution(new ExecutionRecord("click", "success"));

        // Manually inject a corrupted line via MockFileProvider
        _provider.AppendLine("traces/test-trace-001/trace.jsonl", "this is not json!!!");

        // Write another valid execution record
        storage.AddExecution(new ExecutionRecord("back", "success"));

        var result = storage.GetExecutions();
        Assert.Equal(2, result.Count); // 2 valid, 1 corrupted skipped
    }

    [Fact]
    public void CurrentSession_ReturnsNullForMissingSessionJson()
    {
        var storage = CreateStorage();
        // Set traceId but no session.json written (simulates incomplete initialization)
        Assert.Null(storage.CurrentSession);
    }

    // ── 4.8: ExportTrace compatibility ──────────────────────────

    [Fact]
    public void Export_ReturnsJsonArrayWrappingAllJsonlLines()
    {
        var storage = CreateStorage();
        storage.SetSession(CreateSession());
        storage.AddExecution(new ExecutionRecord("click", "success"));
        storage.AddTransition(new StateTransition("Idle", "Traversing"));

        var exported = storage.Export();

        // Export should be a JSON array containing all lines
        Assert.StartsWith("[", exported);
        Assert.EndsWith("]", exported);
        Assert.Contains("\"record_type\"", exported);
    }
}
