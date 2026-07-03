using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Graph.Models;

/// <summary>
/// PlanCompiler — 确定性 IntentSlots → TraversalPlan 映射，无 AI 依赖。
/// </summary>
public sealed class PlanCompiler
{
    /// <summary>
    /// TEMPLATE_SETS — 4 值对齐 Python source。
    /// 每个模板集定义了该 scope 下使用的模板名称列表。
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
    /// compile — 6-step deterministic TraversalPlan generation from IntentSlots。
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
        var templateRegistry = slots.Scope;
        var plan = new TraversalPlan(
            EntryApp: slots.TargetApp,
            EntryPolicy: entryPolicy,
            PlanName: $"{slots.TargetApp}_{slots.Scope}",
            PlanId: $"plan_{slots.TargetApp}_{slots.Scope}",
            RootNode: rootNode,
            StaticNodes: slots.Scope == "target_path" ? BuildStaticNodes(slots) : null,
            TemplateRegistry: templateRegistry,
            CompletionPolicy: completionPolicy,
            IntentSlots: slots);

        // Step 6: build_static_nodes (only for target_path scope)
        // Already included in Step 5

        return plan;
    }

    private void ValidateSlots(IntentSlots slots)
    {
        if (string.IsNullOrWhiteSpace(slots.TargetApp))
            throw new DomainValidationException(nameof(slots.TargetApp), slots.TargetApp ?? "(null)");

        // Validate scope/target combination
        if (slots.Scope == "target_path" && string.IsNullOrWhiteSpace(slots.Target))
            throw new DomainValidationException("scope_target", "scope=target_path requires a target, got " + (slots.Target ?? "(null)"));

        // Validate depth legality
        if (slots.Depth.HasValue && slots.Depth.Value < 0)
            throw new DomainValidationException(nameof(slots.Depth), slots.Depth.Value);
    }

    private EntryPolicy BuildEntryPolicy(IntentSlots slots)
    {
        return new EntryPolicy(
            Strategy: EntryStrategy.DirectDeeplink,
            Fallback: "cold_launch",
            TimeoutSeconds: 10.0);
    }

    private TraversalNode BuildRootNode(IntentSlots slots)
    {
        var isStatic = slots.Scope == "target_path";

        var childrenStrategy = isStatic
            ? new ChildrenStrategy(ChildrenStrategyType.Static)
            : new ChildrenStrategy(ChildrenStrategyType.DynamicMatch, DynamicRules: BuildDynamicRules(slots));

        return new TraversalNode(
            NodeId: "root",
            Name: slots.TargetApp,
            NodeType: NodeType.Screen,
            Operation: new Operation(OperationType.NoAction),
            ChildrenStrategy: childrenStrategy);
    }

    private Dictionary<string, DynamicRule>? BuildDynamicRules(IntentSlots slots)
    {
        var scope = slots.Scope ?? "full_interaction";
        if (!TemplateSets.TryGetValue(scope, out var templateNames))
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
        if (slots.Completion != null)
        {
            // Map completion override to CompletionPolicy
            return slots.Completion switch
            {
                "timeout" => new CompletionPolicy(CompletionPolicyType.Timeout, TimeoutSeconds: 60.0),
                "max_steps" => new CompletionPolicy(CompletionPolicyType.MaxSteps, MaxSteps: 500),
                _ => new CompletionPolicy(CompletionPolicyType.None)
            };
        }

        // Default based on scope
        return slots.Scope == "target_path"
            ? new CompletionPolicy(CompletionPolicyType.TargetFound, TargetName: slots.Target)
            : new CompletionPolicy(CompletionPolicyType.None);
    }

    private Dictionary<string, TraversalNode> BuildStaticNodes(IntentSlots slots)
    {
        // For target_path scope, construct static path nodes from target
        var nodes = new Dictionary<string, TraversalNode>();

        if (!string.IsNullOrWhiteSpace(slots.Target))
        {
            // Create a leaf node for the target
            var targetNode = new TraversalNode(
                NodeId: $"static_{slots.Target}",
                Name: slots.Target,
                NodeType: NodeType.Target,
                Operation: new Operation(OperationType.Click,
                    Target: new Target(TargetType.Text, slots.Target)),
                ChildrenStrategy: new ChildrenStrategy(ChildrenStrategyType.None));

            nodes[targetNode.NodeId] = targetNode;
        }

        return nodes;
    }
}
