using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.UniBrain;

namespace UniClaw.DeepSeekProvider;

/// <summary>
/// DeepSeekTraversalAdvisor — ITraversalAdvisor implementation using DeepSeek API.
/// Stub: throws NotImplementedException. Real implementation requires DeepSeek SDK.
/// </summary>
public sealed class DeepSeekTraversalAdvisor : ITraversalAdvisor
{
    /// <inheritdoc />
    public Task<ContainerInference> InferContainerTypeAsync(
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek container inference not yet implemented.");

    /// <inheritdoc />
    public Task<ContextDecisionResult> DecideNextActionAsync(
        string goal,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        int? depth = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek decision not yet implemented.");

    /// <inheritdoc />
    public Task<ContextDecisionResult> HandleExceptionAsync(
        Exception exception,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek exception handling not yet implemented.");

    /// <inheritdoc />
    public Task<SafetyScreeningResult> ScreenSafetyAsync(
        PageAnalysis pageAnalysis,
        string instruction,
        string? pageType = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek safety screening not yet implemented.");
}
