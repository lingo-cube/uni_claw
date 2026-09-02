using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Agent;

/// <summary>
/// The Agent-owned, run-local execution context.  The value is immutable:
/// changing execution or the active recursive chain replaces the single
/// Agent slot rather than mutating two independently owned tracks.
/// </summary>
internal sealed class ActiveContainerContext
{
    private ActiveContainerContext(
        RuntimeContainer activeExecutionContainer,
        ImmutableArray<ActiveAncestorPathEntry> activeAncestorPath)
    {
        ActiveExecutionContainer = activeExecutionContainer
            ?? throw new ArgumentNullException(nameof(activeExecutionContainer));
        ActiveAncestorPath = activeAncestorPath;
    }

    /// <summary>The Container whose execution/completeness obligation is active.</summary>
    public RuntimeContainer ActiveExecutionContainer { get; }

    /// <summary>
    /// Ordered root-to-immediate-parent entries used only for verified return.
    /// </summary>
    public ImmutableArray<ActiveAncestorPathEntry> ActiveAncestorPath { get; }

    public static ActiveContainerContext Create(RuntimeContainer activeExecutionContainer)
        => new(activeExecutionContainer, ImmutableArray<ActiveAncestorPathEntry>.Empty);

    public ActiveContainerContext ReplaceExecution(RuntimeContainer activeExecutionContainer)
        => new(activeExecutionContainer, ActiveAncestorPath);

    public ActiveContainerContext EnterChild(
        RuntimeContainer child,
        string enteredChildObligationIdentity,
        ContainerEntryContext? parentEntryContext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(enteredChildObligationIdentity);
        return new(
            child,
            ActiveAncestorPath.Add(new ActiveAncestorPathEntry(
            ActiveExecutionContainer,
                enteredChildObligationIdentity,
                parentEntryContext)));
    }

    public bool TryReturnToParent(
        out ActiveContainerContext? resumed,
        out RuntimeContainer? returnedChild)
    {
        if (ActiveAncestorPath.IsDefaultOrEmpty)
        {
            resumed = null;
            returnedChild = null;
            return false;
        }

        var parentEntry = ActiveAncestorPath[^1];
        resumed = new(
            parentEntry.ParentExecutionContainer,
            ActiveAncestorPath.RemoveAt(ActiveAncestorPath.Length - 1));
        returnedChild = ActiveExecutionContainer;
        return true;
    }

    public bool ContainsSemanticIdentity(string semanticIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticIdentity);
        return string.Equals(
                   ActiveExecutionContainer.SemanticPageName,
                   semanticIdentity,
                   StringComparison.Ordinal)
               || ActiveAncestorPath.Any(entry => string.Equals(
                   entry.ParentExecutionContainer.SemanticPageName,
                   semanticIdentity,
                   StringComparison.Ordinal));
    }

}

/// <summary>
/// Existing parent Container and the identity of the child obligation entered
/// from it.  This is an element of the immutable active path, not a second
/// mutable execution-state owner.
/// </summary>
internal sealed record ActiveAncestorPathEntry(
    RuntimeContainer ParentExecutionContainer,
    string EnteredChildObligationIdentity,
    ContainerEntryContext? ParentEntryContext = null);
