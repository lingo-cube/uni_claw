namespace UniClaw.Core.Graph.Models;

/// <summary>
/// 入口策略类型
/// </summary>
public enum EntryStrategy
{
    /// <summary>从主屏幕冷启动</summary>
    ColdLaunch,

    /// <summary>使用深度链接或Intent直接启动</summary>
    DirectDeeplink,

    /// <summary>绑定当前屏幕（假设已在该页面）</summary>
    BindCurrentScreen
}

/// <summary>
/// 入口策略
/// </summary>
/// <param name="Strategy">策略类型</param>
/// <param name="Fallback">备用入口</param>
/// <param name="WaitCondition">期望的屏幕状态</param>
/// <param name="TimeoutSeconds">超时秒数</param>
public sealed record class EntryPolicy(
    EntryStrategy Strategy,
    string? Fallback = null,
    Dictionary<string, object>? WaitCondition = null,
    double TimeoutSeconds = 10.0);

/// <summary>
/// 完成策略类型
/// </summary>
public enum CompletionPolicyType
{
    /// <summary>自然完成</summary>
    None,

    /// <summary>找到目标</summary>
    TargetFound,

    /// <summary>超时</summary>
    Timeout,

    /// <summary>达到最大步数</summary>
    MaxSteps
}

/// <summary>
/// 匹配模式
/// </summary>
public enum MatchMode
{
    /// <summary>精确匹配</summary>
    Exact,

    /// <summary>包含匹配</summary>
    Contains
}

/// <summary>
/// 找到目标后的操作
/// </summary>
public enum TargetFoundAction
{
    /// <summary>标记并停止</summary>
    MarkAndStop,

    /// <summary>执行操作后停止</summary>
    ExecuteThenStop
}

/// <summary>
/// 完成策略
/// </summary>
/// <param name="Type">完成策略类型</param>
/// <param name="TargetName">目标名称（用于TargetFound）</param>
/// <param name="MatchMode">匹配模式</param>
/// <param name="ActionOnFound">找到后的操作</param>
/// <param name="TimeoutSeconds">超时秒数</param>
/// <param name="MaxSteps">最大步数</param>
public sealed record class CompletionPolicy(
    CompletionPolicyType Type = CompletionPolicyType.None,
    string? TargetName = null,
    MatchMode MatchMode = MatchMode.Exact,
    TargetFoundAction ActionOnFound = TargetFoundAction.MarkAndStop,
    double? TimeoutSeconds = null,
    int? MaxSteps = null);

/// <summary>
/// 遍历模式
/// </summary>
public enum TraversalMode
{
    /// <summary>混合模式（静态+动态）</summary>
    Hybrid,

    /// <summary>具体模式（仅预定义路径）</summary>
    Concrete,

    /// <summary>抽象模式（完全动态）</summary>
    Abstract
}

/// <summary>
/// 意图槽位（AI提取）
/// </summary>
/// <param name="TargetApp">目标应用</param>
/// <param name="Scope">范围</param>
/// <param name="Target">目标</param>
/// <param name="Depth">深度</param>
/// <param name="ElementHandling">元素处理方式</param>
/// <param name="Navigation">导航方式</param>
/// <param name="Restore">是否恢复状态</param>
/// <param name="Completion">完成条件</param>
public sealed record class IntentSlots(
    string TargetApp,
    string Scope,
    string? Target = null,
    int? Depth = null,
    string? ElementHandling = null,
    string? Navigation = null,
    bool? Restore = null,
    string? Completion = null);

/// <summary>
/// 遍历计划 - 完整的遍历规范
/// </summary>
/// <param name="EntryApp">目标应用名称</param>
/// <param name="EntryPolicy">入口策略</param>
/// <param name="RootNode">根节点</param>
/// <param name="StaticNodes">静态节点注册表</param>
/// <param name="TemplateRegistry">模板注册表路径</param>
/// <param name="Mode">遍历模式</param>
/// <param name="CompletionPolicy">完成策略</param>
/// <param name="IntentSlots">AI提取的意图</param>
/// <param name="Meta">元数据</param>
public sealed record class TraversalPlan(
    string EntryApp,
    EntryPolicy EntryPolicy,
    TraversalNode? RootNode = null,
    Dictionary<string, TraversalNode>? StaticNodes = null,
    string? TemplateRegistry = null,
    TraversalMode Mode = TraversalMode.Hybrid,
    CompletionPolicy? CompletionPolicy = null,
    IntentSlots? IntentSlots = null,
    Dictionary<string, object>? Meta = null)
{
    /// <summary>
    /// 获取静态节点
    /// </summary>
    public TraversalNode? GetStaticNode(string nodeId) =>
        StaticNodes?.TryGetValue(nodeId, out var node) == true ? node : null;

    /// <summary>
    /// 获取所有静态节点ID
    /// </summary>
    public IEnumerable<string> GetStaticNodeIds() =>
        StaticNodes?.Keys ?? Enumerable.Empty<string>();
}
