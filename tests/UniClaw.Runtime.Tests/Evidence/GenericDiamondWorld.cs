using System.Collections.Immutable;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// Second scenario-neutral world with a DIFFERENT topology than
/// <see cref="GenericTreeWorld"/>: a two-level diamond (root → two children,
/// one child has a grandchild). Same Runtime, same evidence evaluation
/// semantics — proves the generic capability is scenario-independent.
///
///   Container Root
///     ├─ Node X
///     │    └─ Node X1 (grandchild)
///     └─ Node Y
/// </summary>
public static class GenericDiamondWorld
{
    public const string App = "generic.app";
    public const string Root = "Root";
    public const string X = "X";
    public const string Y = "Y";
    public const string X1 = "X1";

    public const string GoalSignalText = "diamond-goal-signal";

    public static EvidenceFixture Create() => new(
        RootContainerIdentity: Root,
        Screens:
        [
            new EvidenceScreen(Root, IsLaunchTarget: true,
                [
                    new EvidenceElement(X, TransitionTo: X),
                    new EvidenceElement(Y, TransitionTo: Y),
                    new EvidenceElement(GoalSignalText),
                ], ForegroundApplication: App),
            new EvidenceScreen(X, IsLaunchTarget: false,
                [
                    new EvidenceElement(Root, TransitionTo: Root),
                    new EvidenceElement(X1, TransitionTo: X1),
                ], ForegroundApplication: App),
            new EvidenceScreen(Y, IsLaunchTarget: false,
                [
                    new EvidenceElement(Root, TransitionTo: Root),
                    new EvidenceElement("Y-leaf"),
                ], ForegroundApplication: App),
            new EvidenceScreen(X1, IsLaunchTarget: false,
                [
                    new EvidenceElement(X, TransitionTo: X),
                    new EvidenceElement("X1-leaf"),
                ], ForegroundApplication: App),
        ],
        ChildRelations:
        [
            new EvidenceRelation(Root, [X, Y]),
            new EvidenceRelation(X, [X1]),
            new EvidenceRelation(Y, []),
            new EvidenceRelation(X1, []),
        ],
        GoalSignals:
        [
            new EvidenceGoalSignal(Root, GoalSignalText),
        ]);

    /// <summary>Expected specification: exhaustively cover Root, X, Y, X1.</summary>
    public static ExpectedSpecification Specification() => new(
        ApplicationIdentity: App,
        RootContainerIdentity: Root,
        RequiredCoverage: ImmutableHashSet.Create(Root, X, Y, X1),
        MaximumDepth: 2,
        RequireGoalEvidenceSatisfied: true);
}
