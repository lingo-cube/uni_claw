using System.Collections.Immutable;
using UniClaw.Runtime.Tests.Scenario.Fakes;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// Scenario-neutral world for the generic evidence validation:
///
///   Container A (root)
///     ├─ Node B (navigable child)
///     ├─ Node C (navigable child)
///     └─ Node D (navigable child)
///
/// Every container identity is generic — no Settings/Android/WiFi vocabulary.
/// The fixture declares only screens, relations, and goal signals; the Runtime
/// must discover the graph through observation evidence.
/// </summary>
public static class GenericTreeWorld
{
    public const string App = "generic.app";
    public const string Root = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";

    /// <summary>Goal signal: root screen shows the "complete" marker element.</summary>
    public const string GoalSignalText = "goal-signal";

    public static EvidenceFixture Create() => new(
        RootContainerIdentity: Root,
        Screens:
        [
            new EvidenceScreen(Root, IsLaunchTarget: true,
                [
                    new EvidenceElement(B, TransitionTo: B),
                    new EvidenceElement(C, TransitionTo: C),
                    new EvidenceElement(D, TransitionTo: D),
                    new EvidenceElement(GoalSignalText),
                ], ForegroundApplication: App),
            new EvidenceScreen(B, IsLaunchTarget: false,
                [
                    new EvidenceElement(Root, TransitionTo: Root),
                    new EvidenceElement("B-leaf"),
                ], ForegroundApplication: App),
            new EvidenceScreen(C, IsLaunchTarget: false,
                [
                    new EvidenceElement(Root, TransitionTo: Root),
                    new EvidenceElement("C-leaf"),
                ], ForegroundApplication: App),
            new EvidenceScreen(D, IsLaunchTarget: false,
                [
                    new EvidenceElement(Root, TransitionTo: Root),
                    new EvidenceElement("D-leaf"),
                ], ForegroundApplication: App),
        ],
        ChildRelations:
        [
            new EvidenceRelation(Root, [B, C, D]),
            new EvidenceRelation(B, []),
            new EvidenceRelation(C, []),
            new EvidenceRelation(D, []),
        ],
        GoalSignals:
        [
            new EvidenceGoalSignal(Root, GoalSignalText),
        ]);

    /// <summary>Expected specification: exhaustively cover root A and all three children.</summary>
    public static ExpectedSpecification Specification() => new(
        ApplicationIdentity: App,
        RootContainerIdentity: Root,
        RequiredCoverage: ImmutableHashSet.Create(Root, B, C, D),
        MaximumDepth: 1,
        RequireGoalEvidenceSatisfied: true);
}
