using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;

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
public sealed record class EntryPolicy
{
    /// <summary>策略类型</summary>
    public EntryStrategy Strategy { get; init; }

    /// <summary>备用入口</summary>
    public string? Fallback { get; init; }

    /// <summary>期望的屏幕状态</summary>
    public Dictionary<string, object>? WaitCondition { get; init; }

    /// <summary>超时秒数</summary>
    public double TimeoutSeconds { get; init; }

    /// <summary>
    /// 构造 EntryPolicy — 校验 TimeoutSeconds 在 (0, 300]。
    /// </summary>
    public EntryPolicy(
        EntryStrategy Strategy,
        string? Fallback = null,
        Dictionary<string, object>? WaitCondition = null,
        double TimeoutSeconds = 10.0)
    {
        if (TimeoutSeconds <= 0 || TimeoutSeconds > 300)
            throw new DomainValidationException(nameof(TimeoutSeconds), TimeoutSeconds);

        this.Strategy = Strategy;
        this.Fallback = Fallback;
        this.WaitCondition = WaitCondition;
        this.TimeoutSeconds = TimeoutSeconds;
    }
}

/// <summary>
/// 完成策略类型
/// </summary>
public enum CompletionPolicyType
{
    /// <summary>穷尽遍历意图 —— 不意图打断，目标自然耗尽</summary>
    Exhaustive,

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
public sealed record class CompletionPolicy
{
    /// <summary>完成策略类型</summary>
    public CompletionPolicyType Type { get; init; }

    /// <summary>目标名称（用于TargetFound）</summary>
    public string? TargetName { get; init; }

    /// <summary>匹配模式</summary>
    public MatchMode MatchMode { get; init; }

    /// <summary>找到后的操作</summary>
    public TargetFoundAction ActionOnFound { get; init; }

    /// <summary>超时秒数</summary>
    public double? TimeoutSeconds { get; init; }

    /// <summary>最大步数</summary>
    public int? MaxSteps { get; init; }

    /// <summary>
    /// 构造 CompletionPolicy — Type==TargetFound 时 TargetName 非空；
    /// TimeoutSeconds 非 null 时在 (0, 86400]；MaxSteps 非 null 时在 [1, 1000000]。
    /// </summary>
    public CompletionPolicy(
        CompletionPolicyType Type = CompletionPolicyType.Exhaustive,
        string? TargetName = null,
        MatchMode MatchMode = MatchMode.Exact,
        TargetFoundAction ActionOnFound = TargetFoundAction.MarkAndStop,
        double? TimeoutSeconds = null,
        int? MaxSteps = null)
    {
        if (Type == CompletionPolicyType.TargetFound && string.IsNullOrWhiteSpace(TargetName))
            throw new DomainValidationException(nameof(TargetName), TargetName);
        if (TimeoutSeconds.HasValue && (TimeoutSeconds.Value <= 0 || TimeoutSeconds.Value > 86400))
            throw new DomainValidationException(nameof(TimeoutSeconds), TimeoutSeconds);
        if (MaxSteps.HasValue && (MaxSteps.Value < 1 || MaxSteps.Value > 1000000))
            throw new DomainValidationException(nameof(MaxSteps), MaxSteps);

        this.Type = Type;
        this.TargetName = TargetName;
        this.MatchMode = MatchMode;
        this.ActionOnFound = ActionOnFound;
        this.TimeoutSeconds = TimeoutSeconds;
        this.MaxSteps = MaxSteps;
    }
}

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
/// 意图槽位（AI提取）— 每个字段表达一个正交的意图维度（遍历形状 / 交互策略 / 边界 / 约束 / override 各管各的）。
/// </summary>
/// <param name="TargetApp">目标应用（必须非空）</param>
/// <param name="Scope">遍历形状，词表锁 ∈ {full, target_only}（与 D-86 Exact/Subset 1:1 同构：full→Exact 穷尽、target_only→Subset 找目标即停）。legacy 值 full_interaction/menu_only/safe_mode/read_only 属 ElementHandling 词表、target_path 已退役，均不得作 Scope。</param>
/// <param name="Target">目标名称，Scope=target_only 时必须非空（Scope=full 时忽略）</param>
/// <param name="Depth">intent 深度约束：null=无约束（DescendAll）；非空时与 TraversalEngineConfig.MaxDepth 按 priority「紧者胜」(min(config.MaxDepth, IntentSlots.Depth)) 解析 —— config 是部署硬天花板，intent 在内收紧。engine 实际接通在 Change B；≥0。</param>
/// <param name="ElementHandling">交互策略，词表 ∈ TEMPLATE_SETS keys（full_interaction/menu_only/safe_mode/read_only），null 默认 full_interaction</param>
/// <param name="Navigation">导航方式</param>
/// <param name="Restore">是否恢复状态</param>
/// <param name="Completion">完成 override ∈ {max_steps, timeout}，**覆盖** scope 派生的 CompletionPolicy Type（非 side-bound）：max_steps→Type=MaxSteps、timeout→Type=Timeout。引擎 bound 检查以 Type 为门，故 override 必须改 Type 才生效。</param>
/// <param name="Entry">遍历根：null=app-root（整树穷尽）；子菜单穷尽用 Entry=sub-menu-root（边界内禀于 Entry+Back 导航，无需 SingleLevel）</param>
public sealed record class IntentSlots(
    string TargetApp,
    string Scope,
    string? Target = null,
    int? Depth = null,
    string? ElementHandling = null,
    string? Navigation = null,
    bool? Restore = null,
    string? Completion = null,
    string? Entry = null);

/// <summary>
/// 遍历计划 - 完整的遍历规范 (12 字段对齐 Python)。
/// </summary>
/// <param name="EntryApp">目标应用名称（必须非空）</param>
/// <param name="PlanName">计划名称</param>
/// <param name="PlanId">计划ID</param>
/// <param name="EntryPolicy">入口策略</param>
/// <param name="EntryConfig">入口配置（V6.8）</param>
/// <param name="RootNode">根节点</param>
/// <param name="StaticNodes">静态节点注册表</param>
/// <param name="TemplateRegistry">模板注册表路径</param>
/// <param name="Mode">遍历模式</param>
/// <param name="CompletionPolicy">完成策略</param>
/// <param name="IntentSlots">AI提取的意图</param>
/// <param name="Meta">元数据</param>
public sealed record class TraversalPlan
{
    /// <summary>目标应用名称（必须非空）</summary>
    public string EntryApp { get; init; }

    /// <summary>计划名称</summary>
    public string PlanName { get; init; }

    /// <summary>计划ID</summary>
    public string PlanId { get; init; }

    /// <summary>入口策略</summary>
    public EntryPolicy EntryPolicy { get; init; }

    /// <summary>入口配置（V6.8）</summary>
    public EntryConfig? EntryConfig { get; init; }

    /// <summary>根节点</summary>
    public TraversalNode? RootNode { get; init; }

    /// <summary>静态节点注册表</summary>
    public Dictionary<string, TraversalNode>? StaticNodes { get; init; }

    /// <summary>模板注册表路径</summary>
    public string? TemplateRegistry { get; init; }

    /// <summary>遍历模式</summary>
    public TraversalMode Mode { get; init; }

    /// <summary>完成策略</summary>
    public CompletionPolicy? CompletionPolicy { get; init; }

    /// <summary>AI提取的意图</summary>
    public IntentSlots? IntentSlots { get; init; }

    /// <summary>元数据</summary>
    public Dictionary<string, object>? Meta { get; init; }

    /// <summary>
    /// 构造 TraversalPlan — 校验 EntryApp 非空。
    /// </summary>
    public TraversalPlan(
        string EntryApp,
        EntryPolicy EntryPolicy,
        string PlanName = "",
        string PlanId = "",
        EntryConfig? EntryConfig = null,
        TraversalNode? RootNode = null,
        Dictionary<string, TraversalNode>? StaticNodes = null,
        string? TemplateRegistry = null,
        TraversalMode Mode = TraversalMode.Hybrid,
        CompletionPolicy? CompletionPolicy = null,
        IntentSlots? IntentSlots = null,
        Dictionary<string, object>? Meta = null)
    {
        if (string.IsNullOrWhiteSpace(EntryApp))
            throw new DomainValidationException(nameof(EntryApp), EntryApp ?? "(null)");

        // C-4: 根节点校验 — RootNode 可空（TraversalEngine.BuildDefaultRoot 兜底构建默认根），
        // 但若显式提供则必须类型为 Screen/Container、操作为 NoAction，否则构造期 fail-fast。
        if (RootNode is not null)
        {
            if (RootNode.NodeType != NodeType.Screen && RootNode.NodeType != NodeType.Container)
                throw new DomainValidationException("RootNode.NodeType", RootNode.NodeType);
            if (RootNode.Operation.Action != OperationType.NoAction)
                throw new DomainValidationException("RootNode.Operation", RootNode.Operation.Action);
        }

        this.EntryApp = EntryApp;
        this.PlanName = PlanName ?? string.Empty;
        this.PlanId = PlanId ?? string.Empty;
        this.EntryPolicy = EntryPolicy;
        this.EntryConfig = EntryConfig;
        this.RootNode = RootNode;
        this.StaticNodes = StaticNodes;
        this.TemplateRegistry = TemplateRegistry;
        this.Mode = Mode;
        this.CompletionPolicy = CompletionPolicy;
        this.IntentSlots = IntentSlots;
        this.Meta = Meta;
    }

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
