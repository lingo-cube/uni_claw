using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-U2-MUS-001 deterministic external world. The fixture owns only visible
/// screens, transitions, dispatch outcomes, and Observation sequencing. All
/// semantic identity, inventory, authorization, progress, and Goal decisions
/// remain injected criteria or production Runtime responsibilities.
/// </summary>
internal sealed class U2OpenWorldSettingsFixture
{
    internal const string DefaultRunId = "sc-u2-mus-001-run";
    internal const string RootPage = "SettingsRoot";
    internal const string BranchA = "Safe section A";
    internal const string BranchB = "Safe section B";
    internal const string DangerousCandidate = "Factory reset";
    internal const string DeeperCandidate = "Nested advanced page";

    private U2OpenWorldSettingsFixture(string runId, ScriptedEnvironment environment)
    {
        RunId = runId;
        Environment = environment;
    }

    internal string RunId { get; }

    internal ScriptedEnvironment Environment { get; }

    internal static U2OpenWorldSettingsFixture Positive(string runId = DefaultRunId)
        => Create(runId, U2FixtureVariant.Positive);

    internal static U2OpenWorldSettingsFixture UnresolvedRoot(string runId = DefaultRunId)
        => Create(runId, U2FixtureVariant.UnresolvedRoot);

    internal static U2OpenWorldSettingsFixture AmbiguousParentReturn(string runId = DefaultRunId)
        => Create(runId, U2FixtureVariant.AmbiguousParentReturn);

    internal static U2OpenWorldSettingsFixture WrongParentReturn(string runId = DefaultRunId)
        => Create(runId, U2FixtureVariant.WrongParentReturn);

    internal static U2OpenWorldSettingsFixture StaleChildObservation(string runId = DefaultRunId)
        => Create(runId, U2FixtureVariant.StaleChildObservation);

    internal static TypeLevelTraversalSpecification Specification()
        => new(
            new TypeLevelTaskScope("Settings", RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(
                ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary("Settings", RootPage));

    internal static BranchInventoryEvidence EvaluateInventory(
        ImmutableArray<Observation> observations,
        int semanticDepth)
    {
        if (observations.IsDefaultOrEmpty)
            return new BranchInventoryEvidence(null, "No accepted Container evidence is available.");

        var current = observations[^1];
        if (Has(current, "Unresolved inventory marker"))
        {
            return new BranchInventoryEvidence(
                null,
                "Fresh root evidence does not prove a complete in-scope inventory.");
        }

        if (semanticDepth == 0 && Has(current, BranchA) && Has(current, BranchB))
        {
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty
                    .Add(BranchA, current.SequenceNumber)
                    .Add(BranchB, current.SequenceNumber),
                "Fresh root evidence proves the complete bounded inventory {A, B}.");
        }

        if (semanticDepth == 1 && (Has(current, "A leaf marker") || Has(current, "B leaf marker")))
        {
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                "No child is required inside depth <= 1; visible deeper work is outside the bounded scope, not discovered-world exhaustion.");
        }

        return new BranchInventoryEvidence(
            null,
            $"Evidence at seq={current.SequenceNumber} is insufficient for semantic depth {semanticDepth}.");
    }

    internal static CandidateAuthorizationEvidence EvaluateAuthorization(
        Observation observation,
        ObservedElement candidate)
    {
        if (!observation.Elements.Contains(candidate))
            throw new ArgumentException("Candidate must belong to the supplied Observation.", nameof(candidate));

        return candidate.Text is BranchA or BranchB or RootPage
            ? new CandidateAuthorizationEvidence(true, "Navigation-only candidate is explicitly authorized.")
            : new CandidateAuthorizationEvidence(false, "Candidate is outside the navigation-only authorized boundary.");
    }

    internal static string? ResolveSemanticPage(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal))
            return null;
        if (Has(observation, BranchA) && Has(observation, BranchB))
            return RootPage;
        if (Has(observation, "A leaf marker"))
            return "ChildA";
        if (Has(observation, "B leaf marker"))
            return "ChildB";
        if (Has(observation, "Wrong parent marker"))
            return "OtherRoot";
        if (Has(observation, "Unresolved inventory marker"))
            return RootPage;
        return null;
    }

    private static U2OpenWorldSettingsFixture Create(string runId, U2FixtureVariant variant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var initial = variant == U2FixtureVariant.UnresolvedRoot ? "UnresolvedRoot" : "SettingsRoot";
        var staleSequences = variant == U2FixtureVariant.StaleChildObservation
            ? new Dictionary<long, long> { [3] = 2 }
            : null;
        var environment = new ScriptedEnvironment(
            "Launcher",
            initial,
            Screens(variant),
            observeSequenceOverrides: staleSequences);
        return new U2OpenWorldSettingsFixture(runId, environment);
    }

    private static IEnumerable<ScreenConfig> Screens(U2FixtureVariant variant)
    {
        yield return new ScreenConfig(
            "Launcher",
            "Launcher",
            [new ElementConfig("Home", null, null)]);
        yield return new ScreenConfig(
            "SettingsRoot",
            "Settings",
            [
                new ElementConfig(BranchA, null, TapTo("ChildA")),
                new ElementConfig(BranchB, null, TapTo("ChildB")),
                new ElementConfig(DangerousCandidate, false, null),
            ]);
        yield return new ScreenConfig(
            "UnresolvedRoot",
            "Settings",
            [new ElementConfig("Unresolved inventory marker", null, null)]);

        var childAReturn = variant == U2FixtureVariant.WrongParentReturn
            ? TapTo("OtherRoot")
            : TapTo("SettingsRoot");
        var childAElements = variant == U2FixtureVariant.AmbiguousParentReturn
            ? ImmutableArray.Create(
                new ElementConfig(RootPage, null, childAReturn),
                new ElementConfig(RootPage, null, childAReturn),
                new ElementConfig("A leaf marker", null, null),
                new ElementConfig(DeeperCandidate, null, TapTo("TooDeep")))
            : ImmutableArray.Create(
                new ElementConfig(RootPage, null, childAReturn),
                new ElementConfig("A leaf marker", null, null),
                new ElementConfig(DeeperCandidate, null, TapTo("TooDeep")));
        yield return new ScreenConfig("ChildA", "Settings", childAElements);
        yield return new ScreenConfig(
            "ChildB",
            "Settings",
            [
                new ElementConfig(RootPage, null, TapTo("SettingsRoot")),
                new ElementConfig("B leaf marker", null, null),
                new ElementConfig(DeeperCandidate, null, TapTo("TooDeep")),
            ]);
        yield return new ScreenConfig(
            "TooDeep",
            "Settings",
            [new ElementConfig("Out-of-scope depth 2 marker", null, null)]);
        yield return new ScreenConfig(
            "OtherRoot",
            "Settings",
            [new ElementConfig("Wrong parent marker", null, null)]);
    }

    private static TransitionConfig TapTo(string screen)
        => new(ScreenTransitionAction.Tap, screen);

    private static bool Has(Observation observation, string text)
        => observation.Elements.Any(element => string.Equals(element.Text, text, StringComparison.Ordinal));

    private enum U2FixtureVariant
    {
        Positive,
        UnresolvedRoot,
        AmbiguousParentReturn,
        WrongParentReturn,
        StaleChildObservation,
    }
}
