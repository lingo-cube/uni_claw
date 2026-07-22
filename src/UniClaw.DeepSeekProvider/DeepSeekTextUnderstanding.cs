using UniClaw.Core.UniBrain;

namespace UniClaw.DeepSeekProvider;

/// <summary>
/// DeepSeekTextUnderstanding — ITextUnderstanding implementation using DeepSeek API.
/// Stub: throws NotImplementedException. Real implementation requires DeepSeek SDK.
/// </summary>
public sealed class DeepSeekTextUnderstanding : ITextUnderstanding
{
    /// <inheritdoc />
    public Task<TextUnderstandingResult> UnderstandTextAsync(
        TextUnderstandingRequest request,
        CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek text understanding not yet implemented.");
}
