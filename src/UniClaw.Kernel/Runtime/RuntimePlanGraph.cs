using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniClaw.Kernel.Runtime;

/// <summary>Closed native history format, separate from the Agent wire codec.
/// Freeze one object graph; execution frames keep its node/list instances.</summary>
internal static class RuntimePlanGraph
{
    internal const string Schema = "uniclaw.runtime.plan.graph.v1";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
    };
    private sealed record Node(string Path, string Kind, JsonElement Payload, Dictionary<string, string> Children);
    private sealed record Block(string Path, string[] Items);
    private sealed record Graph(string Schema, string? PlanId, int Version, string? Justification,
        string? StageExpectation, int MaxLocalOperations, Node[] Nodes, Block[] Blocks);

    internal static AgentPlanProposal Freeze(AgentPlanProposal input)
    {
        if (input?.Items is null) throw new PlanGraphFormatException("plan:invalid-block");
        if (input.Items.Count > 16) throw new PlanGraphFormatException("plan:too-many-items");
        var count = 0;
        var ancestors = new HashSet<PlanItem>(ReferenceEqualityComparer.Instance);
        return input with { Items = CopyBlock(input.Items, 0) };

        IReadOnlyList<PlanItem> CopyBlock(IReadOnlyList<PlanItem>? items, int depth)
        {
            if (items is null || items.Count > 16 || depth > 8) throw new PlanGraphFormatException("plan:invalid-block");
            var copied = new List<PlanItem>();
            foreach (var item in items)
            {
                if (item is null || ++count > 128 || !ancestors.Add(item))
                    throw new PlanGraphFormatException("plan:invalid-control-structure");
                copied.Add(item switch
                {
                    PlanItem.ActItem value => value with { },
                    PlanItem.ObserveItem value => value with { },
                    PlanItem.ControlItem value => value with { },
                    PlanItem.CheckpointItem value => value with { },
                    PlanItem.ForEachItem value => value with { Body = value.Body is null ? null! : CopyBlock(value.Body, depth + 1) },
                    PlanItem.RepeatItem value => value with { Body = value.Body is null ? null! : CopyBlock(value.Body, depth + 1) },
                    PlanItem.IfItem value => value with { Then = CopyBlock(value.Then, depth + 1), Else = CopyBlock(value.Else, depth + 1) },
                    PlanItem.TraverseItem value => value with
                    {
                        Rules = value.Rules is null ? null! : Array.AsReadOnly(value.Rules.Select(rule => rule is null ? null! : rule with
                        {
                            Operation = rule.Operation is null ? null! : rule.Operation with
                            {
                                Before = rule.Operation.Before is null ? null! : Array.AsReadOnly(rule.Operation.Before.ToArray()),
                                AfterVerified = rule.Operation.AfterVerified is null ? null! : Array.AsReadOnly(rule.Operation.AfterVerified.ToArray()),
                            },
                        }).ToArray()),
                    },
                    _ => throw new PlanGraphFormatException("plan:unknown-item"),
                });
                ancestors.Remove(item);
            }
            return copied.AsReadOnly();
        }
    }

    internal static JsonElement Capture(AgentPlanProposal frozen)
    {
        var nodes = new List<Node>(); var blocks = new List<Block>();
        AddBlock(frozen.Items, "items");
        return JsonSerializer.SerializeToElement(new Graph(Schema, frozen.PlanId, frozen.Version,
            frozen.Justification, frozen.StageExpectation, frozen.MaxLocalOperations, nodes.ToArray(), blocks.ToArray()), Json);

        void AddBlock(IReadOnlyList<PlanItem> items, string path)
        {
            blocks.Add(new(path, Enumerable.Range(0, items.Count).Select(n => path + "/" + n).ToArray()));
            for (var index = 0; index < items.Count; index++)
            {
                var nodePath = path + "/" + index; var item = items[index];
                var children = new Dictionary<string, string>(StringComparer.Ordinal);
                PlanItem payload = item;
                if (item is PlanItem.ForEachItem loop) { children.Add("body", nodePath + "/body"); payload = loop with { Body = [] }; }
                if (item is PlanItem.RepeatItem repeat) { children.Add("body", nodePath + "/body"); payload = repeat with { Body = [] }; }
                if (item is PlanItem.IfItem branch)
                {
                    children.Add("then", nodePath + "/then"); children.Add("else", nodePath + "/else");
                    payload = branch with { Then = [], Else = [] };
                }
                nodes.Add(new(nodePath, item.GetType().Name, JsonSerializer.SerializeToElement(payload, payload.GetType(), Json), children));
                if (item is PlanItem.ForEachItem each) AddBlock(each.Body, children["body"]);
                if (item is PlanItem.RepeatItem repeated) AddBlock(repeated.Body, children["body"]);
                if (item is PlanItem.IfItem selected) { AddBlock(selected.Then, children["then"]); AddBlock(selected.Else, children["else"]); }
            }
        }
    }

    internal static (string? PlanId, int Version) Validate(JsonElement element)
    {
        try
        {
            var graph = element.Deserialize<Graph>(Json) ?? throw new InvalidDataException("runtime-plan-graph-missing");
            if (graph.Schema != Schema || graph.Version < 1 || graph.MaxLocalOperations < 1
                || graph.PlanId is not null && string.IsNullOrWhiteSpace(graph.PlanId)
                || graph.Nodes is null || graph.Blocks is null || graph.Nodes.Length is < 1 or > 128 || graph.Blocks.Length > 257)
                throw new InvalidDataException("runtime-plan-graph-format");
            var nodes = graph.Nodes.ToDictionary(node => node.Path, StringComparer.Ordinal);
            var blocks = graph.Blocks.ToDictionary(block => block.Path, StringComparer.Ordinal);
            var visitedNodes = new HashSet<string>(StringComparer.Ordinal); var visitedBlocks = new HashSet<string>(StringComparer.Ordinal);
            VisitBlock("items", 0, nonempty: true);
            if (visitedNodes.Count != nodes.Count || visitedBlocks.Count != blocks.Count)
                throw new InvalidDataException("runtime-plan-graph-unreachable");
            return (graph.PlanId, graph.Version);

            void VisitBlock(string path, int depth, bool nonempty)
            {
                if (depth > 8 || !blocks.TryGetValue(path, out var block) || !visitedBlocks.Add(path)
                    || block.Items is null || block.Items.Length > 16 || nonempty && block.Items.Length == 0)
                    throw new InvalidDataException("runtime-plan-graph-block");
                for (var index = 0; index < block.Items.Length; index++)
                {
                    var nodePath = path + "/" + index;
                    if (block.Items[index] != nodePath || !nodes.TryGetValue(nodePath, out var node)
                        || !visitedNodes.Add(nodePath) || node.Children is null || node.Payload.ValueKind != JsonValueKind.Object)
                        throw new InvalidDataException("runtime-plan-graph-node");
                    var expectedChildren = ValidatePayload(node);
                    if (node.Children.Count != expectedChildren.Length
                        || expectedChildren.Any(label => !node.Children.TryGetValue(label, out var target) || target != nodePath + "/" + label))
                        throw new InvalidDataException("runtime-plan-graph-node-reference");
                    if ((expectedChildren.Length > 0 || node.Kind is nameof(PlanItem.CheckpointItem) or nameof(PlanItem.TraverseItem))
                        && string.IsNullOrWhiteSpace(graph.PlanId))
                        throw new InvalidDataException("runtime-plan-graph-composable-identity");
                    foreach (var label in expectedChildren) VisitBlock(node.Children[label], depth + 1, node.Kind != nameof(PlanItem.IfItem));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException or KeyNotFoundException or NotSupportedException)
        { throw new InvalidDataException("runtime-plan-graph-invalid", ex); }
    }

    private static string[] ValidatePayload(Node node)
    {
        switch (node.Kind)
        {
            case nameof(PlanItem.ActItem):
                var act = node.Payload.Deserialize<PlanItem.ActItem>(Json)!;
                if (string.IsNullOrWhiteSpace(act.TargetRole) || string.IsNullOrWhiteSpace(act.EffectClass)) throw new InvalidDataException("runtime-plan-act");
                break;
            case nameof(PlanItem.ObserveItem): _ = node.Payload.Deserialize<PlanItem.ObserveItem>(Json); break;
            case nameof(PlanItem.CheckpointItem): _ = node.Payload.Deserialize<PlanItem.CheckpointItem>(Json); break;
            case nameof(PlanItem.ControlItem):
                var control = node.Payload.Deserialize<PlanItem.ControlItem>(Json)!;
                if (!Enum.IsDefined(control.Kind)) throw new InvalidDataException("runtime-plan-control");
                break;
            case nameof(PlanItem.ForEachItem):
                var loop = node.Payload.Deserialize<PlanItem.ForEachItem>(Json)!;
                if (string.IsNullOrWhiteSpace(loop.Source) || loop.MaxIterations < 1 || loop.Body is null || loop.Body.Count != 0) throw new InvalidDataException("runtime-plan-foreach");
                return ["body"];
            case nameof(PlanItem.RepeatItem):
                var repeat = node.Payload.Deserialize<PlanItem.RepeatItem>(Json)!;
                if (repeat.MaxIterations < 1 || repeat.Body is null || repeat.Body.Count != 0) throw new InvalidDataException("runtime-plan-repeat");
                return ["body"];
            case nameof(PlanItem.IfItem):
                var branch = node.Payload.Deserialize<PlanItem.IfItem>(Json)!;
                if (string.IsNullOrWhiteSpace(branch.Condition) || branch.Then is null || branch.Else is null || branch.Then.Count != 0 || branch.Else.Count != 0) throw new InvalidDataException("runtime-plan-if");
                return ["then", "else"];
            case nameof(PlanItem.TraverseItem):
                var traversal = node.Payload.Deserialize<PlanItem.TraverseItem>(Json)!;
                if (string.IsNullOrWhiteSpace(traversal.Source) || traversal.Rules is null || traversal.Rules.Count is < 1 or > 16
                    || traversal.MaxDepth is < 0 or > 16 || traversal.ReturnStep is null
                    || !ValidStep(traversal.ReturnStep)
                    || traversal.Rules.Any(rule => rule is null || string.IsNullOrWhiteSpace(rule.RuleId)
                        || string.IsNullOrWhiteSpace(rule.Condition) || rule.Operation is null || !Enum.IsDefined(rule.Operation.Kind)
                        || string.IsNullOrWhiteSpace(rule.Operation.OperationId)
                        || rule.Operation.Kind == TraversalOperationKind.ReadOnly && rule.Operation.Step is not null
                        || rule.Operation.Kind == TraversalOperationKind.Navigate && !ValidStep(rule.Operation.Step)
                        || !ValidHandlers(rule.Operation.Before) || !ValidHandlers(rule.Operation.AfterVerified))
                    || traversal.Rules.Select(rule => rule.RuleId).Distinct(StringComparer.Ordinal).Count() != traversal.Rules.Count)
                    throw new InvalidDataException("runtime-plan-traverse");
                break;
            default: throw new InvalidDataException("runtime-plan-unknown-node");
        }
        return [];
    }

    private static bool ValidStep(AgentActionStep? step) => step is not null
        && !string.IsNullOrWhiteSpace(step.TargetRole) && !string.IsNullOrWhiteSpace(step.EffectClass);

    private static bool ValidHandlers(IReadOnlyList<PlanLanguageHandler>? handlers) => handlers is { Count: <= 8 }
        && handlers.All(handler => handler is not null && !string.IsNullOrWhiteSpace(handler.HandlerId)
            && !string.IsNullOrWhiteSpace(handler.CapabilityId) && !string.IsNullOrWhiteSpace(handler.ProtocolVersion)
            && !string.IsNullOrWhiteSpace(handler.ExpectedLanguage));
}

internal sealed class PlanGraphFormatException(string reason) : Exception(reason);
