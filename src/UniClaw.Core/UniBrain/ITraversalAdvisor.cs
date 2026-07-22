using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// ITraversalAdvisor — 遍历决策能力。
/// 单一职责: "遍历引擎该怎么做？"
/// 替换: IAIStrategyAdvisor。
/// 关键: 方法参数只用 Domain+BCL 类型，不引用 ITraversalContext (解耦)。
/// Call site 从 ITraversalContext 提取 string/int 值直接传入。
/// </summary>
public interface ITraversalAdvisor
{
    /// <summary>推断页面容器类型</summary>
    Task<ContainerInference> InferContainerTypeAsync(
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default);

    /// <summary>决策下一步操作</summary>
    Task<ContextDecisionResult> DecideNextActionAsync(
        string goal,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        int? depth = null,
        CancellationToken ct = default);

    /// <summary>处理异常 — 恢复规划</summary>
    Task<ContextDecisionResult> HandleExceptionAsync(
        Exception exception,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        CancellationToken ct = default);

    /// <summary>安全筛选</summary>
    Task<SafetyScreeningResult> ScreenSafetyAsync(
        PageAnalysis pageAnalysis,
        string instruction,
        string? pageType = null,
        CancellationToken ct = default);
}
