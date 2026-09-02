using System.Collections.Immutable;
using System.Reflection;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.Tests.Scenario;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class ActiveContainerContextTests
{
    [Fact]
    public void ContextHasExactlyTwoSemanticPropertiesAndStartsKnownEmpty()
    {
        var properties = typeof(ActiveContainerContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "ActiveAncestorPath", "ActiveExecutionContainer" },
            properties);

        var root = Container("SettingsRoot");
        var context = ActiveContainerContext.Create(root);

        Assert.Same(root, context.ActiveExecutionContainer);
        Assert.False(context.ActiveAncestorPath.IsDefault);
        Assert.Empty(context.ActiveAncestorPath);
    }

    [Fact]
    public void EnterChildAppendsAndReturnPopsRootToParentPath()
    {
        var root = Container("SettingsRoot");
        var display = Container("Display");
        var grandchild = Container("Advanced");

        var nested = ActiveContainerContext.Create(root)
            .EnterChild(display, "display-row")
            .EnterChild(grandchild, "advanced-row");

        Assert.Same(grandchild, nested.ActiveExecutionContainer);
        Assert.Equal(
            ["SettingsRoot", "Display"],
            nested.ActiveAncestorPath
                .Select(entry => entry.ParentExecutionContainer.SemanticPageName));
        Assert.True(nested.ContainsSemanticIdentity("SettingsRoot"));
        Assert.True(nested.ContainsSemanticIdentity("Display"));
        Assert.True(nested.ContainsSemanticIdentity("Advanced"));

        Assert.True(nested.TryReturnToParent(out var resumed, out var returnedChild));
        Assert.Same(grandchild, returnedChild);
        Assert.NotNull(resumed);
        Assert.Same(display, resumed!.ActiveExecutionContainer);
        Assert.Equal("display-row", resumed.ActiveAncestorPath[^1].EnteredChildObligationIdentity);

        Assert.True(resumed.TryReturnToParent(out var rootResumed, out var displayReturned));
        Assert.Same(display, displayReturned);
        Assert.NotNull(rootResumed);
        Assert.Same(root, rootResumed!.ActiveExecutionContainer);
        Assert.Empty(rootResumed.ActiveAncestorPath);
    }

    [Fact]
    public void ReplaceExecutionPreservesPathWithoutCreatingAnotherPathTrack()
    {
        var root = Container("SettingsRoot");
        var child = Container("Display");
        var replacement = Container("Display");
        var context = ActiveContainerContext.Create(root).EnterChild(child, "display-row");

        var replaced = context.ReplaceExecution(replacement);

        Assert.Same(replacement, replaced.ActiveExecutionContainer);
        Assert.Single(replaced.ActiveAncestorPath);
        Assert.Same(root, replaced.ActiveAncestorPath[0].ParentExecutionContainer);
    }

    [Fact]
    public void RunEntryResetDropsAbortedPathAndProjectsKnownEmptyRootPath()
    {
        var harness = ScenarioHarness.Create("happy");
        var root = Container("SettingsRoot");
        var child = Container("Display");
        var staleContext = ActiveContainerContext.Create(root).EnterChild(child, "aborted-obligation");

        typeof(RuntimeAgent)
            .GetField("_activeContainerContext", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(harness.Agent, staleContext);
        typeof(RuntimeAgent)
            .GetMethod("StartRunActiveExecutionContext", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.Agent, [root]);

        Assert.Equal("SettingsRoot", harness.Agent.ContainerContext.ActiveExecutionContainer);
        Assert.False(harness.Agent.ContainerContext.ActiveAncestorPath.IsDefault);
        Assert.Empty(harness.Agent.ContainerContext.ActiveAncestorPath);
    }

    private static RuntimeContainer Container(string name)
        => new(name, _ => true, (_, _, _) => new TraversalStepResult.Succeeded());
}
