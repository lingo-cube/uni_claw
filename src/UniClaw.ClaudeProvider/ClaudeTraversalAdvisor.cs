using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.UniBrain;

namespace UniClaw.ClaudeProvider;

/// <summary>
/// ClaudeTraversalAdvisor — ITraversalAdvisor implementation using Claude API.
/// Stub: throws NotImplementedException. Real implementation requires Anthropic SDK.
/// </summary>
public sealed class ClaudeTraversalAdvisor : ITraversalAdvisor
{
    /// <inheritdoc />
    public Task<ContainerInference> InferContainerTypeAsync(
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("Claude container inference not yet implemented.");

    /// <inheritdoc />
    public Task<ContextDecisionResult> DecideNextActionAsync(
        string goal,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        int? depth = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("Claude decision not yet implemented.");

    /// <inheritdoc />
    public Task<ContextDecisionResult> HandleExceptionAsync(
        Exception exception,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("Claude exception handling not yet implemented.");

    /// <inheritdoc />
    public Task<SafetyScreeningResult> ScreenSafetyAsync(
        PageAnalysis pageAnalysis,
        string instruction,
        string? pageType = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("Claude safety screening not yet implemented.");
}
