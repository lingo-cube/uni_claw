using UniClaw.Core.UniBrain;

namespace UniClaw.ClaudeProvider;

/// <summary>
/// ClaudeTextUnderstanding — ITextUnderstanding implementation using Claude API.
/// Stub: throws NotImplementedException. Real implementation requires Anthropic SDK.
/// </summary>
public sealed class ClaudeTextUnderstanding : ITextUnderstanding
{
    /// <inheritdoc />
    public Task<TextUnderstandingResult> UnderstandTextAsync(
        TextUnderstandingRequest request,
        CancellationToken ct = default)
        => throw new NotImplementedException("Claude text understanding not yet implemented.");
}
