using UniClaw.Core.Domain;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Simulation;

/// <summary>
/// MockModelProvider — IModelProvider 的传输层 mock 实现。
/// 按 request.Capability 查 MockModelFixture 预设表返回 ModelResponse；不做真实 AI 调用。
/// 仅实现 CompleteTextAsync；vision/multimodal 留 NotImplementedException (后续切片实现)。
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

    /// <summary>本切片不实现 (NotImplementedException)。</summary>
    public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => throw new NotImplementedException(
            "MockModelProvider does not implement CompleteVisionAsync in this slice.");

    /// <summary>本切片不实现 (NotImplementedException)。</summary>
    public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => throw new NotImplementedException(
            "MockModelProvider does not implement CompleteMultimodalAsync in this slice.");
}
