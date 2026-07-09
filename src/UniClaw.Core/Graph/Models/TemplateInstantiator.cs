using UniClaw.Core.Domain.Models.Common;

namespace UniClaw.Core.Graph.Models;

/// <summary>
/// TemplateInstantiator — 7-step instantiate() 流程，从模板定义生成 TraversalNode。
/// 只依赖 PlaceholderResolver (已实现) + Domain/Graph 类型，不依赖 AI/Traversal。
/// </summary>
public sealed class TemplateInstantiator
{
    /// <summary>
    /// instantiate — 7-step flow:
    /// (1) resolve placeholders via PlaceholderResolver
    /// (2) construct Operation via _create_operation
    /// (3) construct Precondition via _create_precondition
    /// (4) construct ChildrenStrategy via _create_children_strategy
    /// (5) construct ErrorPolicy via _create_error_policy
    /// (6) assemble TraversalNode
    /// (7) V6.9 path concatenation — precondition.path = parent_path + [node.name]
    /// </summary>
    public TraversalNode Instantiate(
        Template template,
        Dictionary<string, object> context,
        List<string> parentPath)
    {
        // Step 1: resolve placeholders
        var resolvedOperation = PlaceholderResolver.Resolve(template.Operation, context) as Dictionary<string, object>;
        var resolvedPrecondition = template.Precondition != null
            ? PlaceholderResolver.Resolve(template.Precondition, context) as Dictionary<string, object>
            : null;
        var resolvedChildrenStrategy = template.ChildrenStrategy != null
            ? PlaceholderResolver.Resolve(template.ChildrenStrategy, context) as Dictionary<string, object>
            : null;
        var resolvedErrorPolicy = template.ErrorPolicy != null
            ? PlaceholderResolver.Resolve(template.ErrorPolicy, context) as Dictionary<string, object>
            : null;

        // Step 2: construct Operation
        var operation = CreateOperation(resolvedOperation ?? template.Operation);

        // Step 3: construct Precondition (with V6.9 path concatenation)
        var nodePath = parentPath.Count > 0
            ? parentPath.Concat(new[] { template.TemplateId }).ToList()
            : new List<string> { template.TemplateId };
        var precondition = CreatePrecondition(resolvedPrecondition, nodePath);

        // Step 4: construct ChildrenStrategy
        var childrenStrategy = CreateChildrenStrategy(resolvedChildrenStrategy, template);

        // Step 5: construct ErrorPolicy
        var errorPolicy = CreateErrorPolicy(resolvedErrorPolicy);

        // Step 6: assemble TraversalNode
        return new TraversalNode(
            NodeId: $"dyn_{template.TemplateId}_{context.GetValueOrDefault("item_text", "")}_{context.GetValueOrDefault("parent_node_id", "")}",
            Name: template.TemplateId,
            NodeType: template.NodeType,
            Operation: operation,
            ChildrenStrategy: childrenStrategy,
            Precondition: precondition,
            ErrorPolicy: errorPolicy);
    }

    private Operation CreateOperation(Dictionary<string, object> opDict)
    {
        // Extract action type
        var actionStr = opDict.GetValueOrDefault("action", "click")?.ToString() ?? "click";
        var actionType = actionStr.ToLowerInvariant() switch
        {
            "click" => OperationType.Click,
            "swipe" => OperationType.Swipe,
            "back" => OperationType.Back,
            "input_text" => OperationType.InputText,
            "no_action" => OperationType.NoAction,
            _ => OperationType.Click // Default fallback
        };

        // Extract target
        Target? target = null;
        if (opDict.TryGetValue("target", out var targetObj) && targetObj != null)
        {
            if (targetObj is Dictionary<string, object> targetDict)
            {
                var byStr = targetDict.GetValueOrDefault("by", "text")?.ToString() ?? "text";
                var targetType = byStr.ToLowerInvariant() switch
                {
                    "text" => TargetType.Text,
                    "coordinate" => TargetType.Coordinate,
                    "ui_index" => TargetType.UiIndex,
                    _ => TargetType.Text
                };

                var value = targetDict.GetValueOrDefault("value", "");
                target = new Target(targetType, value ?? "");
            }
            else
            {
                target = new Target(TargetType.Text, targetObj.ToString() ?? "");
            }
        }

        // Extract restore
        RestoreAction? restore = null;
        if (opDict.TryGetValue("restore", out var restoreObj) && restoreObj != null)
        {
            if (restoreObj is Dictionary<string, object> restoreDict)
            {
                var restoreActionStr = restoreDict.GetValueOrDefault("action", "back")?.ToString() ?? "back";
                var restoreActionType = restoreActionStr.ToLowerInvariant() switch
                {
                    "click" => OperationType.Click,
                    "back" => OperationType.Back,
                    _ => OperationType.Back
                };
                restore = new RestoreAction(restoreActionType);
            }
        }

        return new Operation(actionType, target, null, restore);
    }

    private Precondition? CreatePrecondition(Dictionary<string, object>? preDict, List<string> nodePath)
    {
        if (preDict == null)
        {
            // V6.9: Always set path, even without explicit precondition config
            return new Precondition(Path: nodePath);
        }

        // Resolve path from config, falling back to nodePath
        var pageName = preDict.GetValueOrDefault("page_name")?.ToString();
        var timeout = preDict.TryGetValue("timeout_seconds", out var timeoutObj)
            ? Convert.ToDouble(timeoutObj)
            : 5.0;

        return new Precondition(
            PageName: pageName,
            Path: nodePath,
            TimeoutSeconds: timeout);
    }

    private ChildrenStrategy CreateChildrenStrategy(Dictionary<string, object>? csDict, Template template)
    {
        if (csDict == null && template.ChildrenStrategy == null)
            return new ChildrenStrategy(ChildrenStrategyType.None);

        var sourceDict = csDict ?? template.ChildrenStrategy ?? new Dictionary<string, object>();
        var typeStr = sourceDict.GetValueOrDefault("type", "none")?.ToString() ?? "none";

        var csType = typeStr.ToLowerInvariant() switch
        {
            "static" => ChildrenStrategyType.Static,
            "dynamic_match" => ChildrenStrategyType.DynamicMatch,
            "none" => ChildrenStrategyType.None,
            _ => ChildrenStrategyType.None
        };

        // Extract static children list
        List<string>? staticChildren = null;
        if (sourceDict.TryGetValue("static_children", out var scObj) && scObj is List<object> scList)
            staticChildren = scList.Select(s => s?.ToString() ?? "").ToList();

        // Extract max_children
        var maxChildren = sourceDict.TryGetValue("max_children", out var mcObj)
            ? Convert.ToInt32(mcObj)
            : 100;

        return new ChildrenStrategy(csType, staticChildren, MaxChildren: maxChildren);
    }

    private ErrorPolicy CreateErrorPolicy(Dictionary<string, object>? epDict)
    {
        if (epDict == null)
            return new ErrorPolicy(ErrorPolicyType.Retry, MaxRetries: 1);

        var typeStr = epDict.GetValueOrDefault("on_error", "retry")?.ToString() ?? "retry";
        var epType = typeStr.ToLowerInvariant() switch
        {
            "retry" => ErrorPolicyType.Retry,
            "skip" => ErrorPolicyType.Skip,
            "abort" => ErrorPolicyType.Abort,
            "fallback" => ErrorPolicyType.Fallback,
            "backtrack" => ErrorPolicyType.Backtrack,
            _ => ErrorPolicyType.Retry
        };

        var maxRetries = epDict.TryGetValue("max_retries", out var mrObj)
            ? Convert.ToInt32(mrObj)
            : 1;

        var fallbackTarget = epDict.GetValueOrDefault("fallback_target")?.ToString();

        var continueOnError = epDict.TryGetValue("continue_on_error", out var coObj)
            && Convert.ToBoolean(coObj);

        return new ErrorPolicy(epType, maxRetries, fallbackTarget, continueOnError);
    }
}
