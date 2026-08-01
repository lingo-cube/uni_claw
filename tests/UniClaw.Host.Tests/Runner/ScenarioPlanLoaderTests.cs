using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Host.Runner;
using Xunit;

namespace UniClaw.Host.Tests.Runner;

/// <summary>
/// E6.1 — plan JSON → <see cref="TraversalPlan"/>: <c>ChildrenStrategy.Static</c>
/// + <c>StaticNodes</c> carrying operation, target, and
/// <c>Meta["expected_change"]</c>. The loader materializes the coordinate target
/// (JSON round-trip leaves object-typed <c>Target.Value</c> as a JsonElement,
/// which the engine's dispatcher rejects).
/// </summary>
public class ScenarioPlanLoaderTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory,
        "Plans",
        "locate-static.v1.json");

    [Fact]
    public void Load_HandAuthoredPlanJson_ProducesExecutableStaticPlan()
    {
        var plan = new ScenarioPlanLoader().Load(File.ReadAllText(FixturePath));

        Assert.Equal("com.android.settings", plan.EntryApp);
        Assert.Equal(TraversalMode.Concrete, plan.Mode);
        Assert.NotNull(plan.RootNode);
        Assert.Equal(ChildrenStrategyType.Static, plan.RootNode!.ChildrenStrategy.Type);

        var child = plan.StaticNodes!["step-about"];
        Assert.Equal(NodeType.LeafAction, child.NodeType);
        Assert.Equal(OperationType.Click, child.Operation.Action);
        Assert.Equal(TargetType.Coordinate, child.Operation.Target!.By);

        // Coordinate materialized from the JSON object (dispatcher requires a real Coordinate).
        var coordinate = Assert.IsType<Coordinate>(child.Operation.Target.Value);
        Assert.Equal(0.5, coordinate.X);
        Assert.Equal(0.7, coordinate.Y);

        Assert.Equal("change", child.Meta!["expected_change"]);
    }
}
