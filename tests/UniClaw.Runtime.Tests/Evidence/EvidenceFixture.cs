using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// Scenario-neutral observed-evidence fixture for the Evidence + Specification
/// driven validation model.
///
/// A fixture supplies ONLY the external world and its observable evidence:
///   - screen graph (container identities + navigable child relations)
///   - per-screen element/affordance evidence
///   - semantic role classification (via the shared fixture capability)
///   - optional auxiliary structured evidence
///
/// A fixture NEVER supplies:
///   - an execution path / click sequence / expected action order
///   - hidden answers / expected navigation routes
///   - scenario knowledge that the Runtime must discover on its own
///
/// All names are generic (Container A, Node B...) — no Settings/Android/WiFi
/// vocabulary lives in the shared model.
/// </summary>
public sealed record EvidenceFixture(
    string RootContainerIdentity,
    IReadOnlyList<EvidenceScreen> Screens,
    IReadOnlyList<EvidenceRelation> ChildRelations,
    IReadOnlyList<EvidenceGoalSignal> GoalSignals)
{
    /// <summary>All container identities declared by this fixture (root + children).</summary>
    public ImmutableHashSet<string> ContainerIdentities =>
        Screens.Select(s => s.SemanticIdentity).ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>Screen graph → ScriptedEnvironment (deterministic fake world).</summary>
    public ScriptedEnvironment ToScriptedEnvironment()
    {
        var launch = Screens.FirstOrDefault(s => s.IsLaunchTarget);
        var initial = Screens.FirstOrDefault(s => s.SemanticIdentity == RootContainerIdentity)
            ?? throw new InvalidOperationException($"Fixture has no root screen '{RootContainerIdentity}'.");
        return new ScriptedEnvironment(
            initial.Identity,
            launch?.Identity ?? initial.Identity,
            Screens.Select(s => s.ToScreenConfig()));
    }

    /// <summary>Child relation for a container (may be empty for leaf containers).</summary>
    public IReadOnlyList<string> ChildrenOf(string containerIdentity) =>
        ChildRelations.FirstOrDefault(r => r.ContainerIdentity == containerIdentity)?.Children
        ?? [];

    /// <summary>Goal-signal element text declared for a container (goal evidence target).</summary>
    public string? GoalSignalOf(string containerIdentity) =>
        GoalSignals.FirstOrDefault(g => g.ContainerIdentity == containerIdentity)?.ElementText;
}

/// <summary>One screen/container in the fixture graph. <see cref="Identity"/> is
/// the screen name (unique, referenced by transitions); <see cref="ContainerIdentity"/>
/// is the semantic container identity (may be shared by OFF/ON variants of the
/// same container — switch state never changes container identity).</summary>
public sealed record EvidenceScreen(
    string Identity,
    bool IsLaunchTarget,
    ImmutableArray<EvidenceElement> Elements,
    string? ForegroundApplication = null,
    string? ContainerIdentity = null)
{
    /// <summary>Semantic container identity (defaults to the screen name).</summary>
    public string SemanticIdentity => ContainerIdentity ?? Identity;

    public ScreenConfig ToScreenConfig() => new(
        Identity,
        ForegroundApplication,
        Elements.Select((e, i) => new ElementConfig(
            e.Text,
            e.SwitchState,
            e.TransitionTo is null
                ? null
                : new TransitionConfig(
                    e.TransitionAction ?? ScreenTransitionAction.Tap,
                    e.TransitionTo,
                    e.TransitionToState),
            e.Bounds ?? new ElementBounds(0, i * 0.08f, 1, (i + 1) * 0.08f),
            e.PerceptionType)).ToImmutableArray());
}

/// <summary>One element on a screen with optional transition (navigation/state) evidence.</summary>
public sealed record EvidenceElement(
    string Text,
    bool? SwitchState = null,
    string? TransitionTo = null,
    ScreenTransitionAction? TransitionAction = null,
    bool? TransitionToState = null,
    ElementBounds? Bounds = null,
    string? PerceptionType = null);

/// <summary>Declared navigable child relation: container → its authorized children.</summary>
public sealed record EvidenceRelation(string ContainerIdentity, IReadOnlyList<string> Children);

/// <summary>Declared goal-evidence signal: a container shows an element that satisfies the goal.</summary>
public sealed record EvidenceGoalSignal(string ContainerIdentity, string ElementText);
