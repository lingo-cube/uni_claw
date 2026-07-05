using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.Architecture;

/// <summary>
/// Defensive enum value count assertions — prevents accidental addition of enum values.
/// Each enum's value count is locked by constitution/locked-enums.md;
/// these tests enforce the constraints as CI-blocking guards.
/// See docs/system/charter-specification.md §6 for the full guard design.
/// </summary>
public class EnumValueGuardTests
{
    // --- Phase 2.1 locked enums (10) ---
    [Fact]
    public void TraversalState_Has8Values()
        => Assert.Equal(8, Enum.GetValues<TraversalState>().Length);

    [Fact]
    public void GlobalState_Has8Values()
        => Assert.Equal(8, Enum.GetValues<GlobalState>().Length);

    [Fact]
    public void NodeType_Has8Values()
        => Assert.Equal(8, Enum.GetValues<NodeType>().Length);

    [Fact]
    public void ErrorType_Has6Values()
        => Assert.Equal(6, Enum.GetValues<ErrorType>().Length);

    [Fact]
    public void ErrorStrategy_Has5Values()
        => Assert.Equal(5, Enum.GetValues<ErrorStrategy>().Length);

    [Fact]
    public void PopupType_Has5Values()
        => Assert.Equal(5, Enum.GetValues<PopupType>().Length);

    [Fact]
    public void DismissStrategy_Has4Values()
        => Assert.Equal(4, Enum.GetValues<DismissStrategy>().Length);

    [Fact]
    public void UrgencyLevel_Has3Values()
        => Assert.Equal(3, Enum.GetValues<UrgencyLevel>().Length);

    [Fact]
    public void BlockingType_Has3Values()
        => Assert.Equal(3, Enum.GetValues<BlockingType>().Length);

    [Fact]
    public void FallbackAction_Has4Values()
        => Assert.Equal(4, Enum.GetValues<FallbackAction>().Length);

    // --- Phase 1 Domain locked enums (2) ---
    [Fact]
    public void TypeHint_Has8Values()
        => Assert.Equal(8, Enum.GetValues<TypeHint>().Length);

    [Fact]
    public void SelectionState_Has3Values()
        => Assert.Equal(3, Enum.GetValues<SelectionState>().Length);
}

/// <summary>
/// Dependency direction guard — ensures Graph layer does not depend on StateMachine layer,
/// that interface ownership boundaries are respected,
/// and that Domain layer has zero upward references (C-4: Domain is the bottom layer).
/// Extracted from Phase2EnumGuardTests.cs.
/// </summary>
public class DependencyDirectionGuardTests
{
    // --- C-4: Domain layer must not reference any upper layer ---
    [Fact]
    public void Domain_DoesNotReferenceAnyUpperLayer()
    {
        // Domain is the bottom layer — it must not reference Graph, StateMachine,
        // Traversal, AI, or Observability (constitution C-4, verified 2026-07-05)
        var domainDir = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "Domain");
        if (!Directory.Exists(domainDir))
            return;

        var forbiddenNamespaces = new[]
        {
            "UniClaw.Core.Graph",
            "UniClaw.Core.StateMachine",
            "UniClaw.Core.Traversal",
            "UniClaw.Core.AI",
            "UniClaw.Core.Observability",
        };

        foreach (var file in Directory.GetFiles(domainDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            foreach (var ns in forbiddenNamespaces)
                Assert.DoesNotContain($"using {ns}", source);
        }
    }

    // --- C-5: Graph layer must not reference StateMachine layer ---
    [Fact]
    public void TraversalNode_DoesNotReferenceStateMachineNamespace()
    {
        var sourcePath = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "Graph", "Models", "TraversalNode.cs");
        if (!File.Exists(sourcePath))
            return; // Skip if file not found in test environment

        var source = File.ReadAllText(sourcePath);
        Assert.DoesNotContain("using UniClaw.Core.StateMachine", source);
    }

    [Fact]
    public void ITraversalNode_ResidesInGraphModelsNamespace()
    {
        // Verify ITraversalNode is defined in Graph.Models, not StateMachine
        var sourcePath = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "Graph", "Models", "ITraversalNode.cs");
        if (!File.Exists(sourcePath))
            return;

        var source = File.ReadAllText(sourcePath);
        Assert.Contains("namespace UniClaw.Core.Graph.Models", source);
        Assert.Contains("interface ITraversalNode", source);
        Assert.Contains("interface IStackFrame", source);
    }

    [Fact]
    public void TraversalState_DoesNotContainITraversalNodeOrIStackFrame()
    {
        var sourcePath = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "StateMachine", "TraversalState.cs");
        if (!File.Exists(sourcePath))
            return;

        var source = File.ReadAllText(sourcePath);
        Assert.DoesNotContain("interface ITraversalNode", source);
        Assert.DoesNotContain("interface IStackFrame", source);
    }

    private static string FindSourceRoot()
    {
        // Walk up from test bin directory to find project root
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "src", "UniClaw.Core.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
