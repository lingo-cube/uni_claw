using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Simulation;

/// <summary>
/// MockTraversalAdvisor — 返回 GiveUp 决策的 ITraversalAdvisor 实现。
/// 对齐: PRD §9 mock 策略 — 返回 ContextDecisionResult(Result=GiveUp) 或固定决策。
/// </summary>
public sealed class MockTraversalAdvisor : ITraversalAdvisor
{
    /// <inheritdoc />
    public Task<ContainerInference> InferContainerTypeAsync(
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new ContainerInference(
            ContainerType: "unknown",
            Confidence: 0.0));
    }

    /// <inheritdoc />
    public Task<ContextDecisionResult> DecideNextActionAsync(
        string goal,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        int? depth = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new ContextDecisionResult(
            Result: DecisionResult.GiveUp,
            Confidence: 0.0));
    }

    /// <inheritdoc />
    public Task<ContextDecisionResult> HandleExceptionAsync(
        Exception exception,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new ContextDecisionResult(
            Result: DecisionResult.GiveUp,
            Confidence: 0.0));
    }

    /// <inheritdoc />
    public Task<SafetyScreeningResult> ScreenSafetyAsync(
        PageAnalysis pageAnalysis,
        string instruction,
        string? pageType = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new SafetyScreeningResult(
            Evaluations: ImmutableArray<SafetyEvaluation>.Empty,
            PageLevelGuidance: new PageLevelGuidance(
                OverallSafeToProceed: true,
                RecommendedMaxParallel: 1)));
    }
}
