using UniClaw.Core.Domain;
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
public sealed record class ChildrenStrategy
{
    /// <summary>策略类型</summary>
    public ChildrenStrategyType Type { get; init; }

    /// <summary>静态子节点ID列表</summary>
    public List<string>? StaticChildren { get; init; }

    /// <summary>动态匹配规则</summary>
    public Dictionary<string, DynamicRule>? DynamicRules { get; init; }

    /// <summary>最大子节点数（安全限制）</summary>
    public int MaxChildren { get; init; }

    /// <summary>
    /// 构造 ChildrenStrategy — 校验 MaxChildren 在 [0, 10000]。
    /// </summary>
    public ChildrenStrategy(
        ChildrenStrategyType Type,
        List<string>? StaticChildren = null,
        Dictionary<string, DynamicRule>? DynamicRules = null,
        int MaxChildren = 100)
    {
        if (MaxChildren < 0 || MaxChildren > 10000)
            throw new DomainValidationException(nameof(MaxChildren), MaxChildren);

        this.Type = Type;
        this.StaticChildren = StaticChildren;
        this.DynamicRules = DynamicRules;
        this.MaxChildren = MaxChildren;
    }
}

/// <summary>
/// 动态匹配规则
/// </summary>
public sealed record class DynamicRule
{
    /// <summary>规则ID</summary>
    public string RuleId { get; init; }

    /// <summary>匹配条件</summary>
    public MatchCondition MatchCondition { get; init; }

    /// <summary>子节点模板ID</summary>
    public string ChildTemplate { get; init; }

    /// <summary>匹配后的操作</summary>
    public MatchAction Action { get; init; }

    /// <summary>
    /// 构造 DynamicRule — 校验 RuleId/ChildTemplate 非空。
    /// </summary>
    public DynamicRule(
        string RuleId,
        MatchCondition MatchCondition,
        string ChildTemplate,
        MatchAction Action)
    {
        if (string.IsNullOrWhiteSpace(RuleId))
            throw new DomainValidationException(nameof(RuleId), RuleId);
        if (string.IsNullOrWhiteSpace(ChildTemplate))
            throw new DomainValidationException(nameof(ChildTemplate), ChildTemplate);

        this.RuleId = RuleId;
        this.MatchCondition = MatchCondition;
        this.ChildTemplate = ChildTemplate;
        this.Action = Action;
    }
}

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
public sealed record class ErrorPolicy
{
    /// <summary>错误发生时的处理方式</summary>
    public ErrorPolicyType OnError { get; init; }

    /// <summary>最大重试次数</summary>
    public int MaxRetries { get; init; }

    /// <summary>回退目标节点ID</summary>
    public string? FallbackTarget { get; init; }

    /// <summary>是否继续执行</summary>
    public bool ContinueOnError { get; init; }

    /// <summary>
    /// 构造 ErrorPolicy — 校验 MaxRetries 在 [0, 100]。
    /// </summary>
    public ErrorPolicy(
        ErrorPolicyType OnError,
        int MaxRetries = 1,
        string? FallbackTarget = null,
        bool ContinueOnError = false)
    {
        if (MaxRetries < 0 || MaxRetries > 100)
            throw new DomainValidationException(nameof(MaxRetries), MaxRetries);

        this.OnError = OnError;
        this.MaxRetries = MaxRetries;
        this.FallbackTarget = FallbackTarget;
        this.ContinueOnError = ContinueOnError;
    }
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
/// 前置条件
/// </summary>
public sealed record class Precondition
{
    /// <summary>期望的页面名称</summary>
    public string? PageName { get; init; }

    /// <summary>期望的路径</summary>
    public List<string>? Path { get; init; }

    /// <summary>UI条件表达式</summary>
    public string? UiCondition { get; init; }

    /// <summary>超时秒数</summary>
    public double TimeoutSeconds { get; init; }

    /// <summary>
    /// 构造 Precondition — 校验 TimeoutSeconds 在 (0, 300]。
    /// </summary>
    public Precondition(
        string? PageName = null,
        List<string>? Path = null,
        string? UiCondition = null,
        double TimeoutSeconds = 5.0)
    {
        if (TimeoutSeconds <= 0 || TimeoutSeconds > 300)
            throw new DomainValidationException(nameof(TimeoutSeconds), TimeoutSeconds);

        this.PageName = PageName;
        this.Path = Path;
        this.UiCondition = UiCondition;
        this.TimeoutSeconds = TimeoutSeconds;
    }
}

/// <summary>
/// 统一的UI元素或操作抽象
/// </summary>
public sealed record class TraversalNode : ITraversalNode
{
    /// <summary>节点唯一标识</summary>
    public string NodeId { get; init; }

    /// <summary>显示名称</summary>
    public string Name { get; init; }

    /// <summary>节点类型</summary>
    public NodeType NodeType { get; init; }

    /// <summary>要执行的操作</summary>
    public Operation Operation { get; init; }

    /// <summary>子节点策略</summary>
    public ChildrenStrategy ChildrenStrategy { get; init; }

    /// <summary>前置条件</summary>
    public Precondition? Precondition { get; init; }

    /// <summary>错误策略</summary>
    public ErrorPolicy? ErrorPolicy { get; init; }

    /// <summary>元数据</summary>
    public Dictionary<string, object>? Meta { get; init; }

    /// <summary>
    /// 构造 TraversalNode — 校验 NodeId/Name 非空。
    /// </summary>
    public TraversalNode(
        string NodeId,
        string Name,
        NodeType NodeType,
        Operation Operation,
        ChildrenStrategy ChildrenStrategy,
        Precondition? Precondition = null,
        ErrorPolicy? ErrorPolicy = null,
        Dictionary<string, object>? Meta = null)
    {
        if (string.IsNullOrWhiteSpace(NodeId))
            throw new DomainValidationException(nameof(NodeId), NodeId);
        if (string.IsNullOrWhiteSpace(Name))
            throw new DomainValidationException(nameof(Name), Name);

        this.NodeId = NodeId;
        this.Name = Name;
        this.NodeType = NodeType;
        this.Operation = Operation;
        this.ChildrenStrategy = ChildrenStrategy;
        this.Precondition = Precondition;
        this.ErrorPolicy = ErrorPolicy;
        this.Meta = Meta;
    }

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
