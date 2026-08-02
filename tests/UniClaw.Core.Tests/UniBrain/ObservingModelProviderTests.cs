using UniClaw.Core.Domain;
using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// ObservingModelProvider 单元测试 — task 3.1: 4 场景对照 spec。
/// 成功记 record / 失败记 error metadata / ProviderId 委托 / Vision mode 标签。
/// 对齐 OpenSpec change unibrain-modelprovider-vertical-slice。
/// </summary>
public class ObservingModelProviderTests
{
    /// <summary>Spy IModelProvider：持预设响应，记录被调次数。</summary>
    private sealed class SpyModelProvider : IModelProvider
    {
        private readonly ModelResponse _response;

        public int CallCount { get; private set; }
        public string ProviderId => "spy";

        public SpyModelProvider(ModelResponse response) => _response = response;

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_response);
        }

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_response);
        }

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_response);
        }
    }

    /// <summary>Spy ITraceRecorder：仅存储 AICallRecord，其它 6 方法 throw NotImplementedException。</summary>
    private sealed class SpyTraceRecorder : ITraceRecorder
    {
        public List<AICallRecord> Records { get; } = new();

        public Task RecordAICallAsync(AICallRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task<TraceSession> StartSessionAsync(
            string traceId, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task EndSessionAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task RecordExecutionAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task RecordTransitionAsync(StateTransition transition, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task RecordErrorAsync(ErrorRecord record, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task RecordPageTransitionAsync(PageTransition transition, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<string> StartSpanAsync(string spanType, string spanName,
            string? parentSpanId = null, Dictionary<string, object>? attributes = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task EndSpanAsync(string spanId, string status = "ok",
            Dictionary<string, object>? attributes = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task CompleteTextAsync_Success_RecordsCallWithTokens()
    {
        var expected = new ModelResponse("ok", "spy", "text", 10, 20, 5.0);
        var spy = new SpyModelProvider(expected);
        var recorder = new SpyTraceRecorder();
        var provider = new ObservingModelProvider(spy, recorder);

        var request = new ModelRequest("prompt", Capability: "parse_instruction");
        var resp = await provider.CompleteTextAsync(request);

        Assert.Same(expected, resp);
        var record = Assert.Single(recorder.Records);
        Assert.True(record.Success);
        Assert.Equal(30, record.Tokens);
        Assert.Equal("parse_instruction", record.Capability);
        Assert.Equal("spy", record.ProviderId);
        Assert.Equal("text", record.Metadata!["mode"]);
        Assert.Equal(1, spy.CallCount);
    }

    [Fact]
    public async Task CompleteTextAsync_Failure_RecordsErrorMetadata()
    {
        var failed = new ModelResponse("", "spy", "text", 0, 0, 0) with { Success = false, ErrorMessage = "boom" };
        var spy = new SpyModelProvider(failed);
        var recorder = new SpyTraceRecorder();
        var provider = new ObservingModelProvider(spy, recorder);

        await provider.CompleteTextAsync(new ModelRequest("prompt"));

        var record = Assert.Single(recorder.Records);
        Assert.False(record.Success);
        Assert.Equal("boom", record.Metadata!["error"]);
    }

    [Fact]
    public void ProviderId_Delegates_To_Inner()
    {
        var spy = new SpyModelProvider(new ModelResponse("", "spy", "text", 0, 0, 0));
        var provider = new ObservingModelProvider(spy, new SpyTraceRecorder());
        Assert.Equal("spy", provider.ProviderId);
    }

    [Fact]
    public async Task CompleteVisionAsync_Records_Vision_Mode()
    {
        var expected = new ModelResponse("ok", "spy", "vision", 5, 5, 1.0);
        var spy = new SpyModelProvider(expected);
        var recorder = new SpyTraceRecorder();
        var provider = new ObservingModelProvider(spy, recorder);

        await provider.CompleteVisionAsync(new ModelRequest("prompt"), new byte[] { 1, 2, 3 });

        var visionRecord = Assert.Single(recorder.Records);
        Assert.Equal("vision", visionRecord.Metadata!["mode"]);
    }

    [Fact]
    public async Task CompleteVisionAsync_PropagatesSafeTransportDiagnostics()
    {
        var expected = new ModelResponse("ok", "sensenova", "vision", 1, 2, 30.0)
        {
            Diagnostics = new Dictionary<string, object>
            {
                ["headersMs"] = 29.0,
                ["bodyMs"] = 1.0,
                ["attempt"] = 1,
            },
        };
        var recorder = new SpyTraceRecorder();
        var provider = new ObservingModelProvider(
            new SpyModelProvider(expected),
            recorder);

        await provider.CompleteVisionAsync(
            new ModelRequest("prompt"),
            new byte[] { 1, 2, 3 });

        var metadata = Assert.Single(recorder.Records).Metadata!;
        Assert.Equal(29.0, metadata["transport.headersMs"]);
        Assert.Equal(1.0, metadata["transport.bodyMs"]);
        Assert.Equal(1, metadata["transport.attempt"]);
    }
}
