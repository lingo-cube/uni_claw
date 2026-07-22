using UniClaw.Core.UniBrain;

namespace UniClaw.ClaudeProvider;

/// <summary>
/// AnthropicModelProvider — IModelProvider implementation using Anthropic SDK.
/// Stub: throws NotImplementedException. Real implementation requires Anthropic SDK.
/// </summary>
public sealed class AnthropicModelProvider : IModelProvider
{
    /// <inheritdoc />
    public string ProviderId => "claude";

    /// <inheritdoc />
    public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
        => throw new NotImplementedException("Anthropic text completion not yet implemented.");

    /// <inheritdoc />
    public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => throw new NotImplementedException("Anthropic vision completion not yet implemented.");

    /// <inheritdoc />
    public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => throw new NotImplementedException("Anthropic multimodal completion not yet implemented.");
}
