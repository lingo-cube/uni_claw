using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Abstractions;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Graph.Services;

/// <summary>
/// PlanCompiler — 确定性 IntentSlots → TraversalPlan 映射，无 AI 依赖。
/// 派生正确性对齐下游 D-86 Mode 自动分流（ResolveModeAndTarget）预期：
/// Scope={full,target_only} 定 CompletionPolicy.Type；ElementHandling 定 DynamicRules。
/// </summary>
public sealed class PlanCompiler : IPlanCompiler
{
    /// <summary>穷尽安全上界（秒）—— Completion=timeout override 的默认超时。</summary>
    public const double DefaultCompletionTimeoutSeconds = 300;

    /// <summary>穷尽安全上界（步）—— Completion=max_steps override 的默认步数预算。</summary>
    public const int DefaultCompletionMaxSteps = 500;

    /// <summary>入口策略默认超时（秒）—— BuildEntryPolicy 使用。</summary>
    public const double EntryTimeoutSeconds = 10;

    /// <summary>
    /// TEMPLATE_SETS — 4 值对齐 Python source，按 **ElementHandling**（交互策略）键控，
    /// 非 Scope。每个模板集定义该交互策略下动态生成的模板名称列表。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ImmutableArray<string>> TemplateSets =
        new Dictionary<string, ImmutableArray<string>>
        {
            ["full_interaction"] = ImmutableArray.Create("menu_container", "switch_leaf", "slider_leaf", "leaf_action"),
            ["menu_only"] = ImmutableArray.Create("menu_container"),
            ["safe_mode"] = ImmutableArray.Create("menu_container", "switch_leaf", "slider_leaf", "leaf_action"),
            ["read_only"] = ImmutableArray.Create("leaf_info"),
        };

    /// <summary>
    /// 模板匹配条件 — 对齐 Python match_conditions。
    /// menu_container → {"type": "menu_item"}, switch_leaf → {"type": "switch"},
    /// slider_leaf → {"type": "slider"}, leaf_action → {"type": "button"}, leaf_info → {} (match anything)
    /// </summary>
    public static readonly IReadOnlyDictionary<string, MatchCondition> MatchConditions =
        new Dictionary<string, MatchCondition>
        {
            ["menu_container"] = new MatchCondition(Type: "menu_item"),
            ["switch_leaf"] = new MatchCondition(Type: "switch"),
            ["slider_leaf"] = new MatchCondition(Type: "slider"),
            ["leaf_action"] = new MatchCondition(Type: "button"),
            ["leaf_info"] = new MatchCondition(), // Empty condition = match anything
        };

    /// <summary>
    /// compile — 5-step deterministic TraversalPlan generation from IntentSlots：
    /// (1) ValidateSlots → (2) BuildEntryPolicy → (3) BuildRootNode → (4) BuildCompletionPolicy → (5) assemble。
    /// </summary>
    public TraversalPlan Compile(IntentSlots slots)
    {
        // Step 1: validate_slots
        ValidateSlots(slots);

        // Step 2: build_entry_policy
        var entryPolicy = BuildEntryPolicy(slots);

        // Step 3: build_root_node
        var rootNode = BuildRootNode(slots);

        // Step 4: build_completion_policy
        var completionPolicy = BuildCompletionPolicy(slots);

        // Step 5: assemble TraversalPlan
        var templateRegistry = slots.ElementHandling ?? "full_interaction";
        return new TraversalPlan(
            EntryApp: slots.TargetApp,
            EntryPolicy: entryPolicy,
            PlanName: $"{slots.TargetApp}_{slots.Scope}",
            PlanId: $"plan_{slots.TargetApp}_{slots.Scope}",
            RootNode: rootNode,
            TemplateRegistry: templateRegistry,
            CompletionPolicy: completionPolicy,
            IntentSlots: slots);
    }

    private void ValidateSlots(IntentSlots slots)
    {
        if (string.IsNullOrWhiteSpace(slots.TargetApp))
            throw new DomainValidationException(nameof(slots.TargetApp), slots.TargetApp ?? "(null)");

        // P2: Scope 词表锁 ∈ {full, target_only}（拒 legacy element_handling 值 + 已退役的 target_path）。
        if (slots.Scope != "full" && slots.Scope != "target_only")
            throw new DomainValidationException(nameof(slots.Scope), slots.Scope);

        // target_only 必须带 Target（full + Target 取忽略，见 design §5.7 / OQ3）。
        if (slots.Scope == "target_only" && string.IsNullOrWhiteSpace(slots.Target))
            throw new DomainValidationException("scope_target", "scope=target_only requires a target, got " + (slots.Target ?? "(null)"));

        // ElementHandling（若给）必须是 TEMPLATE_SETS key；null → 默认 full_interaction（BuildDynamicRules 处理）。
        if (!string.IsNullOrWhiteSpace(slots.ElementHandling) && !TemplateSets.ContainsKey(slots.ElementHandling))
            throw new DomainValidationException(nameof(slots.ElementHandling), slots.ElementHandling);

        // Depth ≥ 0（null=无约束 DescendAll）。
        if (slots.Depth.HasValue && slots.Depth.Value < 0)
            throw new DomainValidationException(nameof(slots.Depth), slots.Depth.Value);

        // P4: Completion（若给）∈ {max_steps, timeout} —— 非法值 fail-fast throw（原 _ => None 静默吞）。
        if (!string.IsNullOrWhiteSpace(slots.Completion)
            && slots.Completion != "max_steps"
            && slots.Completion != "timeout")
            throw new DomainValidationException(nameof(slots.Completion), slots.Completion);
    }

    private EntryPolicy BuildEntryPolicy(IntentSlots slots)
    {
        // P5: 默认 ColdLaunch / fallback=null（从 DirectDeeplink/cold_launch 改 —— 不预设深链存在）。
        return new EntryPolicy(
            Strategy: EntryStrategy.ColdLaunch,
            Fallback: null,
            TimeoutSeconds: EntryTimeoutSeconds);
    }

    private TraversalNode BuildRootNode(IntentSlots slots)
    {
        // 根节点的合法性（非 null / Screen|Container / NoAction）由 TraversalPlan 构造函数校验 (C-4)。
        // target_path 退役：统一 DYNAMIC_MATCH（不再有 STATIC 分支）。
        var childrenStrategy = new ChildrenStrategy(
            ChildrenStrategyType.DynamicMatch,
            DynamicRules: BuildDynamicRules(slots));

        // RootNode 反映 Entry（默认 app-root = TargetApp）。
        var name = slots.Entry ?? slots.TargetApp;

        return new TraversalNode(
            NodeId: "root",
            Name: name,
            NodeType: NodeType.Screen,
            Operation: new Operation(OperationType.NoAction),
            ChildrenStrategy: childrenStrategy);
    }

    private Dictionary<string, DynamicRule>? BuildDynamicRules(IntentSlots slots)
    {
        // P1: 读 ElementHandling（非 Scope）；null 默认 full_interaction。
        var handling = slots.ElementHandling ?? "full_interaction";
        if (!TemplateSets.TryGetValue(handling, out var templateNames))
            return null;

        var rules = new Dictionary<string, DynamicRule>();
        foreach (var templateName in templateNames)
        {
            if (MatchConditions.TryGetValue(templateName, out var condition))
            {
                rules[templateName] = new DynamicRule(
                    RuleId: templateName,
                    MatchCondition: condition,
                    ChildTemplate: templateName,
                    Action: MatchAction.GenerateChild);
            }
        }

        return rules;
    }

    private CompletionPolicy BuildCompletionPolicy(IntentSlots slots)
    {
        // Completion override 覆盖 Type（非 side-bound）：引擎 bound 检查以 Type 为门
        // （TraversalEngine L315/L323），Type 不变则 bound 失效，故 override 必须改 Type。
        if (!string.IsNullOrEmpty(slots.Completion))
        {
            return slots.Completion switch
            {
                "max_steps" => new CompletionPolicy(CompletionPolicyType.MaxSteps, MaxSteps: DefaultCompletionMaxSteps),
                "timeout" => new CompletionPolicy(CompletionPolicyType.Timeout, TimeoutSeconds: DefaultCompletionTimeoutSeconds),
                // ValidateSlots 已拒非法值；此 arm 防御性 fail-fast，应对未来重构改校验顺序。
                _ => throw new DomainValidationException(nameof(slots.Completion), slots.Completion)
            };
        }

        // P3: Scope 派生默认 Type。
        // target_only → TargetFound(TargetName=Target, MatchMode=Contains, ActionOnFound=ExecuteThenStop)
        // full → Exhaustive（exhaustive intent）
        return slots.Scope == "target_only"
            ? new CompletionPolicy(
                CompletionPolicyType.TargetFound,
                TargetName: slots.Target,
                MatchMode: MatchMode.Contains,
                ActionOnFound: TargetFoundAction.ExecuteThenStop)
            : new CompletionPolicy(CompletionPolicyType.Exhaustive);
    }
}
