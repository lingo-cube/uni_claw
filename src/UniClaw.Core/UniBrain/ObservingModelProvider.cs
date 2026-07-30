using System.Diagnostics;
using UniClaw.Core.Domain;
using UniClaw.Core.Observability;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// ObservingModelProvider — IModelProvider 观测 decorator (D-E11)。
/// 包裹 inner IModelProvider，每次成功 AI 调用后记录一条 AICallRecord 到 ITraceRecorder。
/// 不吞 inner 异常：inner 抛出则直接向上传播，不记 record。
/// 对齐 OpenSpec change unibrain-modelprovider-vertical-slice task 3.1。
/// </summary>
public sealed class ObservingModelProvider : IModelProvider
{
    private readonly IModelProvider _inner;
    private readonly ITraceRecorder _recorder;

    /// <param name="inner">被包裹的真实 provider</param>
    /// <param name="recorder">追踪记录器</param>
    public ObservingModelProvider(IModelProvider inner, ITraceRecorder recorder)
    {
        _inner = inner ?? throw new DomainValidationException("inner", inner);
        _recorder = recorder ?? throw new DomainValidationException("recorder", recorder);
    }

    /// <summary>Provider 标识，委托给 inner。</summary>
    public string ProviderId => _inner.ProviderId;

    /// <inheritdoc />
    public async Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var resp = await _inner.CompleteTextAsync(request, ct);
        sw.Stop();
        await _recorder.RecordAICallAsync(
            BuildRecord(request.Capability ?? "", resp, "text", sw.Elapsed.TotalMilliseconds),
            cancellationToken: ct);
        return resp;
    }

    /// <inheritdoc />
    public async Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var resp = await _inner.CompleteVisionAsync(request, imageData, ct);
        sw.Stop();
        await _recorder.RecordAICallAsync(
            BuildRecord(request.Capability ?? "", resp, "vision", sw.Elapsed.TotalMilliseconds),
            cancellationToken: ct);
        return resp;
    }

    /// <inheritdoc />
    public async Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var resp = await _inner.CompleteMultimodalAsync(request, imageData, ct);
        sw.Stop();
        await _recorder.RecordAICallAsync(
            BuildRecord(request.Capability ?? "", resp, "multimodal", sw.Elapsed.TotalMilliseconds),
            cancellationToken: ct);
        return resp;
    }

    /// <summary>
    /// 构造 AICallRecord：latency 由调用方 (Stopwatch) 测量后传入；metadata 携带 model/mode，失败时附加 error。
    /// </summary>
    private static AICallRecord BuildRecord(string capability, ModelResponse resp, string mode, double latencyMs)
    {
        var metadata = new Dictionary<string, object> { ["model"] = resp.Model, ["mode"] = mode };
        if (resp.Diagnostics is not null)
        {
            foreach (var diagnostic in resp.Diagnostics)
                metadata[$"transport.{diagnostic.Key}"] = diagnostic.Value;
        }
        if (!resp.Success) metadata["error"] = resp.ErrorMessage ?? "";
        return new AICallRecord(
            capability,
            resp.ProviderId,
            resp.Success,
            latencyMs,
            Context: null,
            Tokens: resp.InputTokens + resp.OutputTokens,
            Timestamp: DateTimeOffset.UtcNow,
            Metadata: metadata);
    }
}
