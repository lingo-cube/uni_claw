using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.AI;

/// <summary>
/// AI决策结果
/// </summary>
public enum DecisionResult
{
    /// <summary>成功决策</summary>
    Success,

    /// <summary>不确定</summary>
    Unsure,

    /// <summary>放弃决策</summary>
    GiveUp
}

/// <summary>
/// 节点数据（AI决策返回）
/// </summary>
/// <param name="NodeId">节点ID</param>
/// /// <param name="Action">操作</param>
/// /// <param name="Target">目标</param>
/// /// <param name="Reasoning">推理说明</param>
public sealed record class NodeData(
    string? NodeId = null,
    string? Action = null,
    object? Target = null,
    string? Reasoning = null);

/// <summary>
/// 容器推断结果
/// </summary>
/// <param name="ContainerType">容器类型</param>
/// <param name="Confidence">置信度 (0-1)</param>
/// <param name="MatchedTemplate">匹配的模板</param>
public sealed record class ContainerInference(
    string ContainerType,
    double Confidence,
    string? MatchedTemplate = null);

/// <summary>
/// 页面类型验证结果
/// </summary>
/// <param name="IsMatch">是否匹配</param>
/// <param name="Confidence">置信度</param>
/// <param name="ActualType">实际类型</param>
/// <param name="Reasoning">推理说明</param>
/// <param name="MismatchDetails">不匹配详情</param>
/// <param name="Suggestion">建议</param>
public sealed record class PageTypeVerification(
    bool IsMatch,
    double Confidence,
    string ActualType,
    string Reasoning,
    MismatchDetails? MismatchDetails = null,
    Suggestion? Suggestion = null);

/// <summary>
/// 不匹配详情
/// </summary>
/// <param name="ExpectedType">期望类型</param>
/// <param name="ActualElements">实际元素列表</param>
/// <param name="MissingElements">缺失元素</param>
public sealed record class MismatchDetails(
    string ExpectedType,
    List<string> ActualElements,
    List<string>? MissingElements = null);

/// <summary>
/// 建议
/// </summary>
/// <param name="Action">建议操作</param>
/// <param name="Target">目标</param>
/// <param name="Reason">原因</param>
public sealed record class Suggestion(
    string Action,
    object? Target = null,
    string? Reason = null);

/// <summary>
/// 安全筛选结果
/// </summary>
/// <param name="Evaluations">元素评估列表</param>
/// <param name="PageLevelGuidance">页面级指导</param>
public sealed record class SafetyScreeningResult(
    List<SafetyEvaluation> Evaluations,
    PageLevelGuidance? PageLevelGuidance = null);

/// <summary>
/// 安全评估
/// </summary>
/// <param name="Name">元素名称</param>
/// <param name="SafetyTag">安全标签</param>
/// <param name="Confidence">置信度</param>
/// <param name="Reason">原因</param>
/// <param name="ContextDependency">上下文依赖</param>
/// <param name="TaskRelevance">任务相关性</param>
public sealed record class SafetyEvaluation(
    string Name,
    SafetyTag SafetyTag,
    double Confidence,
    string Reason,
    string? ContextDependency = null,
    string? TaskRelevance = null);

/// <summary>
/// 安全标签
/// </summary>
public enum SafetyTag
{
    /// <summary>安全</summary>
    Safe,

    /// <summary>谨慎</summary>
    Caution,

    /// <summary>跳过</summary>
    Skip,

    /// <summary>未知</summary>
    Unknown
}

/// <summary>
/// 页面级指导
/// </summary>
/// <param name="OverallSafeToProceed">整体是否安全</param>
/// <param name="RecommendedMaxParallel">推荐最大并行数</param>
public sealed record class PageLevelGuidance(
    bool OverallSafeToProceed,
    int? RecommendedMaxParallel = null);

/// <summary>
/// 页面分析（简化版）
/// </summary>
/// <param name="FlattenedScreen">扁平化屏幕</param>
/// <param name="Path">路径</param>
/// <param name="PopupInfo">弹窗信息</param>
public sealed record class PageAnalysis(
    FlattenedScreen FlattenedScreen,
    List<string> Path,
    PopupInfo? PopupInfo = null);

/// <summary>
/// 弹窗信息
/// </summary>
/// <param name="Detected">是否检测到</param>
/// <param name="CloseButton">关闭按钮位置</param>
/// <param name="Message">弹窗消息</param>
public sealed record class PopupInfo(
    bool Detected,
    (double X, double Y)? CloseButton = null,
    string? Message = null);

/// <summary>
/// 上下文决策结果
/// </summary>
/// <param name="Result">决策结果</param>
/// <param name="Action">操作</param>
/// <param name="Target">目标</param>
/// <param name="Confidence">置信度</param>
public sealed record class ContextDecisionResult(
    DecisionResult Result,
    string? Action = null,
    object? Target = null,
    double Confidence = 0.0);

/// <summary>
/// AI策略顾问接口
/// </summary>
public interface IAIStrategyAdvisor
{
    /// <summary>
    /// 推断页面容器类型
    /// </summary>
    Task<ContainerInference> InferContainerTypeAsync(
        PageAnalysis pageAnalysis,
        ITraversalContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 决策下一步操作
    /// </summary>
    Task<(DecisionResult Result, NodeData? NodeData)> DecideNextActionAsync(
        string goal,
        PageAnalysis pageAnalysis,
        ITraversalContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理异常
    /// </summary>
    Task<(DecisionResult Result, NodeData? NodeData)> HandleExceptionAsync(
        Exception exception,
        PageAnalysis pageAnalysis,
        ITraversalContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证页面类型
    /// </summary>
    Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 安全筛选
    /// </summary>
    Task<SafetyScreeningResult> ScreenSafetyAsync(
        PageAnalysis pageAnalysis,
        string instruction,
        string? pageType = null,
        CancellationToken cancellationToken = default);
}
