using UniClaw.Core.Domain;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Simulation;

/// <summary>
/// MockModelProvider — IModelProvider 的传输层 mock 实现。
/// 按 request.Capability 查 MockModelFixture 预设表返回 ModelResponse；不做真实 AI 调用。
/// 三个 completion 方法 (text/vision/multimodal) 均走 fixture-driven replay：同一份
/// MockModelFixture.Responses (capability → MockModelEntry) 预设表对所有模式通用，
/// 由调用方设定 Mode 区分 ("text" / "vision" / "multimodal")。缺失预设 → DomainValidationException fail-fast。
/// </summary>
public sealed class MockModelProvider : IModelProvider
{
    private readonly MockModelFixture _fixture;
    private readonly string _providerId;

    /// <summary>
    /// 构造器: fixture 为 null → DomainValidationException fail-fast。
    /// </summary>
    public MockModelProvider(MockModelFixture fixture, string providerId = "mock")
    {
        _fixture = fixture
            ?? throw new DomainValidationException(nameof(fixture), fixture);
        _providerId = providerId;
    }

    /// <summary>Provider 标识 (默认 "mock")。</summary>
    public string ProviderId => _providerId;

    /// <summary>
    /// 按 request.Capability 查表返回预设响应。缺失预设 → DomainValidationException fail-fast。
    /// Mode 固定为 "text"。
    /// </summary>
    public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
    {
        var entry = _fixture.Resolve(request.Capability ?? string.Empty);
        if (entry is null)
            throw new DomainValidationException(
                nameof(request.Capability),
                request.Capability,
                $"Mock has no preset for capability '{request.Capability}'.");

        var response = new ModelResponse(
            entry.Content,
            _providerId,
            "text",
            entry.InputTokens,
            entry.OutputTokens,
            entry.LatencyMs) with
        {
            Success = entry.Success,
            ErrorMessage = entry.ErrorMessage,
        };

        return Task.FromResult(response);
    }

    /// <summary>
    /// 按 request.Capability 查表返回预设响应。缺失预设 → DomainValidationException fail-fast。
    /// Mode 固定为 "vision"。imageData 接受但不参与查表 —— mock 预设按 capability 索引、与模式无关
    /// (同一份 MockModelFixture.Responses 同时服务三个 completion 方法，由调用方设定 Mode)。
    /// </summary>
    public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
    {
        // imageData 故意不使用：mock 不分析图像字节，预设以 capability 为键、模式无关。
        var entry = _fixture.Resolve(request.Capability ?? string.Empty);
        if (entry is null)
            throw new DomainValidationException(
                nameof(request.Capability),
                request.Capability,
                $"Mock has no preset for capability '{request.Capability}'.");

        var response = new ModelResponse(
            entry.Content,
            _providerId,
            "vision",
            entry.InputTokens,
            entry.OutputTokens,
            entry.LatencyMs) with
        {
            Success = entry.Success,
            ErrorMessage = entry.ErrorMessage,
        };

        return Task.FromResult(response);
    }

    /// <summary>
    /// 按 request.Capability 查表返回预设响应。缺失预设 → DomainValidationException fail-fast。
    /// Mode 固定为 "multimodal"。imageData 接受但不参与查表 —— 同 CompleteVisionAsync，mock 预设
    /// 以 capability 为键、模式无关 (同一份 fixture 服务三个 completion 方，由调用方设定 Mode)。
    /// </summary>
    public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
    {
        // imageData 故意不使用：mock 不分析图像字节，预设以 capability 为键、模式无关。
        var entry = _fixture.Resolve(request.Capability ?? string.Empty);
        if (entry is null)
            throw new DomainValidationException(
                nameof(request.Capability),
                request.Capability,
                $"Mock has no preset for capability '{request.Capability}'.");

        var response = new ModelResponse(
            entry.Content,
            _providerId,
            "multimodal",
            entry.InputTokens,
            entry.OutputTokens,
            entry.LatencyMs) with
        {
            Success = entry.Success,
            ErrorMessage = entry.ErrorMessage,
        };

        return Task.FromResult(response);
    }
}
