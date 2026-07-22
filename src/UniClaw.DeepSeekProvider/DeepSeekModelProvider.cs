using UniClaw.Core.UniBrain;

namespace UniClaw.DeepSeekProvider;

/// <summary>
/// DeepSeekModelProvider — IModelProvider implementation using DeepSeek API.
/// Stub: throws NotImplementedException. Real implementation requires DeepSeek SDK.
/// </summary>
public sealed class DeepSeekModelProvider : IModelProvider
{
    /// <inheritdoc />
    public string ProviderId => "deepseek";

    /// <inheritdoc />
    public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek text completion not yet implemented.");

    /// <inheritdoc />
    public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek vision completion not yet implemented.");

    /// <inheritdoc />
    public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek multimodal completion not yet implemented.");
}
