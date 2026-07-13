using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Graph.Models;

/// <summary>
/// 文本匹配模式 (M-9: Exact vs Contains)。
/// 默认 Contains — 向后兼容 Python substring 匹配行为。
/// </summary>
public enum TextMatchMode
{
    /// <summary>精确匹配 — string equality</summary>
    Exact,

    /// <summary>包含匹配 — substring match (case-insensitive)</summary>
    Contains
}

/// <summary>
/// 子节点策略类型
/// </summary>
public enum ChildrenStrategyType
{
    /// <summary>预定义子节点列表</summary>
    Static,

    /// <summary>运行时发现+模板匹配</summary>
    DynamicMatch,

    /// <summary>无子节点</summary>
    None
}

/// <summary>
/// 子节点策略
/// </summary>
/// <param name="Type">策略类型</param>
/// <param name="StaticChildren">静态子节点ID列表</param>
/// <param name="DynamicRules">动态匹配规则</param>
/// <param name="MaxChildren">最大子节点数（安全限制）</param>
public sealed record class ChildrenStrategy(
    ChildrenStrategyType Type,
    List<string>? StaticChildren = null,
    Dictionary<string, DynamicRule>? DynamicRules = null,
    int MaxChildren = 100);

/// <summary>
/// 动态匹配规则
/// </summary>
/// <param name="RuleId">规则ID</param>
/// <param name="MatchCondition">匹配条件</param>
/// <param name="ChildTemplate">子节点模板ID</param>
/// <param name="Action">匹配后的操作</param>
public sealed record class DynamicRule(
    string RuleId,
    MatchCondition MatchCondition,
    string ChildTemplate,
    MatchAction Action);

/// <summary>
/// 匹配条件
/// </summary>
/// <param name="Type">UI元素类型</param>
/// <param name="ExpectedAction">期望的操作类型</param>
/// <param name="TextPattern">文本正则表达式</param>
/// <param name="TextMatchMode">文本匹配模式 (M-9): Exact=精确匹配, Contains=包含匹配 (默认)</param>
/// <param name="MinIndex">最小索引</param>
/// <param name="MaxIndex">最大索引</param>
/// <param name="Custom">自定义条件</param>
public sealed record class MatchCondition(
    string? Type = null,
    string? ExpectedAction = null,
    string? TextPattern = null,
    TextMatchMode TextMatchMode = TextMatchMode.Contains,
    int? MinIndex = null,
    int? MaxIndex = null,
    Dictionary<string, object>? Custom = null);

/// <summary>
/// 匹配后的操作
/// </summary>
public enum MatchAction
{
    /// <summary>生成子节点</summary>
    GenerateChild,

    /// <summary>跳过</summary>
    Skip,

    /// <summary>内联执行</summary>
    ExecuteInline
}

/// <summary>
/// 错误策略类型
/// </summary>
public enum ErrorPolicyType
{
    /// <summary>重试</summary>
    Retry,

    /// <summary>跳过</summary>
    Skip,

    /// <summary>中止</summary>
    Abort,

    /// <summary>回退到指定节点</summary>
    Fallback,

    /// <summary>回溯</summary>
    Backtrack
}

/// <summary>
/// 错误策略
/// </summary>
/// <param name="OnError">错误发生时的处理方式</param>
/// <param name="MaxRetries">最大重试次数</param>
/// <param name="FallbackTarget">回退目标节点ID</param>
/// <param name="ContinueOnError">是否继续执行</param>
public sealed record class ErrorPolicy(
    ErrorPolicyType OnError,
    int MaxRetries = 1,
    string? FallbackTarget = null,
    bool ContinueOnError = false);

/// <summary>
/// 退出条件类型
/// </summary>
public enum ExitConditionType
{
    /// <summary>所有子节点都已访问</summary>
    AllChildrenVisited,

    /// <summary>所有子节点已访问 或 已到达滚动末尾</summary>
    AllChildrenVisitedOrScrollEnd,

    /// <summary>达到深度限制</summary>
    DepthLimited,

    /// <summary>仅处理直接子节点</summary>
    SingleLevel
}

/// <summary>
/// 回退操作
/// </summary>
public enum FallbackAction
{
    /// <summary>按返回键</summary>
    Back,

    /// <summary>尝试兄弟菜单或返回</summary>
    AutoEscape,

    /// <summary>跳过</summary>
    Skip,

    /// <summary>中止遍历</summary>
    Abort
}

/// <summary>
/// 退出条件
/// </summary>
/// <param name="Type">退出条件类型</param>
/// <param name="Fallback">回退操作</param>
/// <param name="MaxDepth">最大深度</param>
public sealed record class ExitCondition(
    ExitConditionType Type,
    FallbackAction Fallback = FallbackAction.Back,
    int? MaxDepth = null);

/// <summary>
/// 前置条件
/// </summary>
/// <param name="PageName">期望的页面名称</param>
/// <param name="Path">期望的路径</param>
/// <param name="UiCondition">UI条件表达式</param>
/// <param name="TimeoutSeconds">超时秒数</param>
public sealed record class Precondition(
    string? PageName = null,
    List<string>? Path = null,
    string? UiCondition = null,
    double TimeoutSeconds = 5.0);

/// <summary>
/// 统一的UI元素或操作抽象
/// </summary>
/// <param name="NodeId">节点唯一标识</param>
/// <param name="Name">显示名称</param>
/// <param name="NodeType">节点类型</param>
/// <param name="Operation">要执行的操作</param>
/// <param name="ChildrenStrategy">子节点策略</param>
/// <param name="Precondition">前置条件</param>
/// <param name="ErrorPolicy">错误策略</param>
/// <param name="ExitCondition">退出条件</param>
/// <param name="Meta">元数据</param>
public sealed record class TraversalNode(
    string NodeId,
    string Name,
    NodeType NodeType,
    Operation Operation,
    ChildrenStrategy ChildrenStrategy,
    Precondition? Precondition = null,
    ErrorPolicy? ErrorPolicy = null,
    ExitCondition? ExitCondition = null,
    Dictionary<string, object>? Meta = null) : ITraversalNode
{
    /// <summary>
    /// 是否为容器节点
    /// </summary>
    public bool IsContainer => NodeType == NodeType.Container || NodeType == NodeType.Screen;

    /// <summary>
    /// 是否为叶子节点
    /// </summary>
    public bool IsLeaf => !IsContainer;

    /// <summary>
    /// 获取静态子节点ID列表
    /// </summary>
    public List<string> StaticChildren => ChildrenStrategy.StaticChildren ?? [];
}
