using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.UniBrain;

namespace UniClaw.ClaudeProvider;

/// <summary>
/// ClaudePageAnalyzer — IPageAnalyzer implementation using Claude Vision API.
/// Stub: throws NotImplementedException. Real implementation requires Anthropic SDK.
/// </summary>
public sealed class ClaudePageAnalyzer : IPageAnalyzer
{
    /// <inheritdoc />
    public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Claude Vision page analysis not yet implemented.");

    /// <inheritdoc />
    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
        => throw new NotImplementedException("Claude Vision app entry detection not yet implemented.");

    /// <inheritdoc />
    public Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("Claude Vision page type verification not yet implemented.");
}
