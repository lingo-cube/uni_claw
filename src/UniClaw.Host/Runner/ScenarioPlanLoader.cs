using System.Text.Json;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Host.Runner;

/// <summary>
/// E6.1 — load a hand-authored plan JSON into an executable <see cref="TraversalPlan"/>.
/// <see cref="TraversalPlan.FromJson"/> round-trips the plan, but the object-typed
/// <c>Target.Value</c> of a coordinate target comes back as a <see cref="JsonElement"/>
/// (the ObjectValueConverter preserves objects as JsonElement). The engine's
/// <c>OperationDispatcher</c> requires a real <see cref="Coordinate"/> for Click/Swipe,
/// so this loader materializes JsonElement coordinates back into <see cref="Coordinate"/>.
/// Plan mode = <c>ChildrenStrategy.Static</c> + <c>StaticNodes</c> whose metadata
/// carries each step's expected change.
/// </summary>
public sealed class ScenarioPlanLoader
{
    public TraversalPlan Load(string planJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planJson);
        var plan = TraversalPlan.FromJson(planJson);

        var root = plan.RootNode is null ? null : MaterializeNode(plan.RootNode);
        Dictionary<string, TraversalNode>? staticNodes = null;
        if (plan.StaticNodes is not null)
        {
            staticNodes = new Dictionary<string, TraversalNode>(StringComparer.Ordinal);
            foreach (var (id, node) in plan.StaticNodes)
                staticNodes[id] = MaterializeNode(node);
        }

        return plan with { RootNode = root, StaticNodes = staticNodes };
    }

    private static TraversalNode MaterializeNode(TraversalNode node) =>
        node with { Operation = MaterializeOperation(node.Operation) };

    private static Operation MaterializeOperation(Operation operation) =>
        operation.Target is null
            ? operation
            : operation with { Target = MaterializeTarget(operation.Target) };

    private static Target MaterializeTarget(Target target)
    {
        if (target.By != TargetType.Coordinate || target.Value is Coordinate)
            return target;
        if (target.Value is JsonElement element
            && element.TryGetProperty("x", out var x)
            && element.TryGetProperty("y", out var y))
        {
            return target with { Value = new Coordinate(x.GetDouble(), y.GetDouble()) };
        }

        return target;
    }
}
