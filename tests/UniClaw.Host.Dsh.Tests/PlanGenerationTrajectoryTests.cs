using System.Text.Json;

namespace UniClaw.Host.Dsh.Tests;

public sealed class PlanGenerationTrajectoryTests
{
    private const string SessionId = "session-plan004-fixture";
    private const string RequestId = "dsh-req-00000007";

    [Fact]
    public void Parse_PairsRawSubmitCallsWithResultsAndFiltersByRequestTurns()
    {
        var snapshot = Parse(FixtureLog(), Probe());

        Assert.Equal(new[] { 2, 3 }, snapshot.Turns);
        Assert.Equal(2, snapshot.SubmitCalls.Count);
        Assert.Equal(1, snapshot.SubmitRetryCount);
        Assert.Equal("call-first", snapshot.SubmitCalls[0].CallId);
        Assert.Equal("{ \"task\": { \"payload\": { \"decisionId\": \"d-first\" } } }",
            snapshot.SubmitCalls[0].RawArguments);
        Assert.False(snapshot.SubmitCalls[0].Accepted);
        Assert.Equal("decision-schema-invalid", snapshot.Rejections.Single().Reason);
        Assert.Equal("decision.proposal is missing", snapshot.Rejections.Single().Message);
        Assert.Contains("submit_decision rejected:", snapshot.Rejections.Single().Text, StringComparison.Ordinal);
        Assert.Equal("call-final", snapshot.SubmitCalls[1].CallId);
        Assert.True(snapshot.SubmitCalls[1].Accepted);
        Assert.Equal("bounded actual prompt", snapshot.SystemPrompt);
        Assert.Equal(new[] { "uniagent-prod:product-prompt", "uniagent-prod:skill:android-automotive-ui-testing" }, snapshot.Sections);
        Assert.Equal(SessionId, snapshot.PromptProbeDshSessionId);
        Assert.Equal(1, snapshot.ProviderRetryStartedCount);
        Assert.Equal(1, snapshot.ProviderRetryFailureCount);
        Assert.Equal(1, snapshot.NoSubmitNudgeCount);
        Assert.Equal("complete", snapshot.UsageStatus);
        Assert.Equal(30, snapshot.InputTokenTotal);
        Assert.Equal(6, snapshot.OutputTokenTotal);
        Assert.Equal(36, snapshot.TotalTokenTotal);
        Assert.Equal(2, snapshot.UsageAttempts.Count);
        Assert.All(snapshot.UsageAttempts, usage =>
        {
            Assert.Equal("deepseek-official", usage.Provider);
            Assert.Equal("deepseek-v4-flash", usage.Model);
        });
        Assert.DoesNotContain("unrelated", snapshot.ConsultationRawJsonl, StringComparison.Ordinal);
        Assert.DoesNotContain("call-other", snapshot.ConsultationRawJsonl, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsSessionOrProbeIdentityMismatch()
    {
        var log = FixtureLog();
        var wrongSessionLog = log.Replace(SessionId, "session-other", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => Parse(wrongSessionLog, Probe()));

        var wrongProbe = Probe().Replace(SessionId, "session-other", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => Parse(FixtureLog(), wrongProbe));
    }

    [Fact]
    public void Parse_UsesSessionV4UserPayloadAndSeqBasedSurfaceReplacement()
    {
        var raw = string.Join('\n', new[]
        {
            Header(SessionId),
            Event(1, "turn/start", new { turn = 1 }),
            Event(2, "step/start", new { turn = 1, step = 1 }),
            Event(3, "system/message", new { turn = 1, step = 1, message = SystemMessage("stale prompt") }, "append"),
            Event(4, "system/message", new { turn = 1, step = 1, message = SystemMessage("mounted prompt") },
                new { op = "replace", startSeq = 3, endSeq = 3 }, new long[] { 3 }),
            Event(5, "user/message", UserData(RequestId + "-1700000000000", "request body"), "append"),
            Event(6, "turn/end", new { turn = 1, reason = new { kind = "complete" } }),
        }) + "\n";

        var snapshot = Parse(raw, Probe());

        Assert.Equal(new[] { 1 }, snapshot.Turns);
        Assert.Equal("mounted prompt", snapshot.SystemPrompt);
        Assert.Contains("\"source\":{\"kind\":\"user\",\"rpcId\":\"" + RequestId,
            snapshot.ConsultationRawJsonl, StringComparison.Ordinal);
        Assert.DoesNotContain("stale prompt", snapshot.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MarksUsageUnknownWhenTheTurnHasNoProviderAccounting()
    {
        var raw = string.Join('\n', new[]
        {
            Header(SessionId),
            Event(1, "turn/start", new { turn = 1 }),
            Event(2, "step/start", new { turn = 1, step = 1 }),
            Event(3, "user/message", UserData(RequestId + "-123", "task"), "append"),
            Event(4, "assistant/message", new { turn = 1, step = 1, message = new { source = new { kind = "model", provider = "deepseek-official", model = "deepseek-v4-flash" }, content = new[] { Text("done") } }, stream = Array.Empty<object>() }, "append"),
            Event(5, "step/end", new { turn = 1, step = 1 }),
            Event(6, "turn/end", new { turn = 1, reason = new { kind = "complete" } }),
        }) + "\n";

        var snapshot = PlanGenerationTrajectoryCollector.Parse(SessionId, RequestId, raw, Probe());

        Assert.Equal("unknown", snapshot.UsageStatus);
        Assert.Null(snapshot.InputTokenTotal);
        Assert.Null(snapshot.TotalTokenTotal);
        Assert.Single(snapshot.UsageAttempts);
        Assert.Null(snapshot.UsageAttempts[0].RawUsage);
    }

    private static PlanGenerationTrajectorySnapshot Parse(string jsonl, string probe) =>
        PlanGenerationTrajectoryCollector.Parse(SessionId, RequestId, jsonl, probe);

    private static string FixtureLog()
    {
        var events = new[]
        {
            Event(1, "turn/start", new { turn = 1 }),
            Event(2, "user/message", UserData("previous-request-1", "other private task marker"), "append"),
            Event(3, "turn/end", new { turn = 1, reason = new { kind = "complete" } }),
            Event(4, "turn/start", new { turn = 2 }),
            Event(5, "step/start", new { turn = 2, step = 1 }),
            Event(6, "system/message", new { turn = 2, step = 1, message = SystemMessage("bounded actual prompt") }, "append"),
            Event(7, "user/message", UserData(RequestId + "-1700000000000", "case input"), "append"),
            Event(8, "request/header", new { header = new { config = new { provider = "deepseek-official", model = "deepseek-v4-flash" } }, reason = "initial" }),
            Event(9, "tool/call", new { turn = 2, step = 1, callId = "call-first", name = "submit_decision", arguments = "{ \"task\": { \"payload\": { \"decisionId\": \"d-first\" } } }" }),
            Event(10, "tool/result", new { turn = 2, step = 1, message = ToolMessage("call-first", "submit_decision rejected: decision-schema-invalid; decision.proposal is missing") }, "append"),
            Event(11, "llm/retry-started", new { turn = 2, step = 1 }),
            Event(12, "llm/retry", new { turn = 2, step = 1, reason = "provider transient failure" }),
            Event(13, "assistant/message", AssistantData(2, 1, 10, 2), "append"),
            Event(14, "step/end", new { turn = 2, step = 1 }),
            Event(15, "turn/end", new { turn = 2, reason = new { kind = "complete" } }),
            Event(16, "turn/start", new { turn = 3 }),
            Event(17, "step/start", new { turn = 3, step = 2 }),
            Event(18, "user/message", UserData(RequestId + "-nudge-1700000001000", "submit now"), "append"),
            Event(19, "tool/call", new { turn = 3, step = 2, callId = "call-final", name = "submit_decision", arguments = "{\"task\":{\"payload\":{\"decisionId\":\"d-final\"}}}" }),
            Event(20, "tool/result", new { turn = 3, step = 2, message = ToolMessage("call-final", "submit_decision accepted: decision captured") }, "append"),
            Event(21, "assistant/message", AssistantData(3, 2, 20, 4), "append"),
            Event(22, "step/end", new { turn = 3, step = 2 }),
            Event(23, "turn/end", new { turn = 3, reason = new { kind = "complete" } }),
            Event(24, "turn/start", new { turn = 4 }),
            Event(25, "step/start", new { turn = 4, step = 3 }),
            Event(26, "user/message", UserData("unrelated-request-1", "later unrelated task"), "append"),
            Event(27, "tool/call", new { turn = 4, step = 3, callId = "call-other", name = "submit_decision", arguments = "{}" }),
        };
        return Header(SessionId) + "\n" + string.Join('\n', events) + "\n";
    }

    private static object AssistantData(int turn, int step, int inputTokens, int outputTokens) => new
    {
        turn,
        step,
        message = new
        {
            source = new { kind = "model", provider = "deepseek-official", model = "deepseek-v4-flash" },
            content = new[] { Text("response") },
        },
        usage = new { inputTokens, outputTokens, totalTokens = inputTokens + outputTokens },
        stream = Array.Empty<object>(),
    };

    private static object UserData(string rpcId, string text) => new
    {
        id = "user-" + rpcId,
        role = "user",
        source = new { kind = "user", rpcId },
        content = new[] { Text(text) },
    };

    private static object SystemMessage(string text) => new
    {
        id = "system-prompt",
        source = new { kind = "system-prompt" }, role = "system", content = new[] { Text(text) },
    };

    private static object ToolMessage(string callId, string text) => new
    {
        source = new { kind = "tool", callId },
        toolCallId = callId,
        role = "tool",
        content = new[] { Text(text) },
    };

    private static object Text(string value) => new { type = "text", text = value };

    private static string Header(string id) => JsonSerializer.Serialize(new
    {
        type = "session", version = 4, id, createdAt = 0, isSeeded = false, delegationDepth = 0,
    });

    private static string Event(
        long seq, string type, object data, object? surfaceOp = null, long[]? sourceEventSeqs = null)
    {
        var row = surfaceOp is null
            ? JsonSerializer.Serialize(new { seq, type, time = seq, data })
            : sourceEventSeqs is null
                ? JsonSerializer.Serialize(new { seq, type, time = seq, data, surfaceOp })
                : JsonSerializer.Serialize(new { seq, type, time = seq, data, surfaceOp, sourceEventSeqs });
        return row;
    }

    private static string Probe() => JsonSerializer.Serialize(new
    {
        recorded = true,
        dshSessionId = SessionId,
        sections = new[]
        {
            "uniagent-prod:product-prompt",
            "uniagent-prod:skill:android-automotive-ui-testing",
        },
        contexts = Array.Empty<string>(),
    });
}
