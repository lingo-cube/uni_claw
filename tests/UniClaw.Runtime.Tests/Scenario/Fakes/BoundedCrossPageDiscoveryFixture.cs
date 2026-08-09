using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-P3-CAND-008 Task 1.1 deterministic proof capability. The fixture scripts only external
/// pages, visible candidates, dispatch outcomes, fresh/stale Observations, and bounded criteria.
/// Agent inventory acceptance, semantic depth, selection, progress, and completion remain Task 2.1.
/// </summary>
internal sealed class BoundedCrossPageDiscoveryFixture
{
    internal const string DefaultRunId = "sc-p3-cand-008-fixture-run";
    internal const string ParentBranch = "Branch A";
    internal const string ChildBranch = "Branch C";

    private readonly ScriptedEnvironment _environment;
    private readonly ImmutableArray<DeviceAction> _actions;
    private readonly ImmutableArray<int> _depths;

    private BoundedCrossPageDiscoveryFixture(
        string runId,
        ScriptedEnvironment environment,
        ImmutableArray<DeviceAction> actions,
        ImmutableArray<int> depths)
    {
        RunId = runId;
        _environment = environment;
        _actions = actions;
        _depths = depths;
        InitialPlan = new Plan([new PlanStep("Bounded discovered branch", "Existing Tap mechanics")]);
        Goal = new Goal(
            observation => new GoalEvidence(
                observation.Elements.Any(element => element.Text == "Independent goal evidence"),
                "Goal completion remains independently evidence-controlled.",
                observation.SequenceNumber),
            EvaluateAuthorization,
            BranchInventoryEvaluator: EvaluateInventory);
    }

    internal string RunId { get; }

    internal Plan InitialPlan { get; }

    internal Goal Goal { get; }

    internal ScriptedEnvironment Environment => _environment;

    internal static BoundedCrossPageDiscoveryFixture Positive(string runId = DefaultRunId)
        => Create(runId, DiscoveryFixturePath.Positive);

    internal static BoundedCrossPageDiscoveryFixture Unresolved(string runId = DefaultRunId)
        => Create(runId, DiscoveryFixturePath.Unresolved);

    internal static BoundedCrossPageDiscoveryFixture DepthBound(string runId = DefaultRunId)
        => Create(runId, DiscoveryFixturePath.DepthBound);

    internal static BoundedCrossPageDiscoveryFixture DepthBoundRoute(string runId = DefaultRunId)
        => Create(runId, DiscoveryFixturePath.DepthBoundRoute);

    internal static BoundedCrossPageDiscoveryFixture ViewportSameContainer(string runId = DefaultRunId)
        => Create(runId, DiscoveryFixturePath.ViewportSameContainer);

    internal static BoundedCrossPageDiscoveryFixture StaleChild(string runId = DefaultRunId)
        => Create(runId, DiscoveryFixturePath.StaleChild);

    internal static BoundedCrossPageDiscoveryFixture StaleChildAfterStartup(string runId = DefaultRunId)
        => Create(runId, DiscoveryFixturePath.StaleChildAfterStartup);

    internal static BoundedCrossPageDiscoveryFixture ConflictingChild(string runId = DefaultRunId)
        => Create(runId, DiscoveryFixturePath.ConflictingChild);

    internal static BoundedCrossPageDiscoveryFixture ConflictingChildAfterStartup(string runId = DefaultRunId)
        => Create(runId, DiscoveryFixturePath.ConflictingChildAfterStartup);

    internal async Task<BoundedCrossPageFixtureEvidence> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var observations = ImmutableArray.CreateBuilder<Observation>();
        var inventories = ImmutableArray.CreateBuilder<BranchInventoryEvidence>();
        var dispatches = ImmutableArray.CreateBuilder<ActionResult>();

        var initial = await _environment.ObserveAsync(cancellationToken);
        observations.Add(initial);
        inventories.Add(EvaluateInventory([initial], _depths[0]));

        for (var index = 0; index < _actions.Length; index++)
        {
            dispatches.Add(await _environment.ExecuteAsync(_actions[index], cancellationToken));
            var observation = await _environment.ObserveAsync(cancellationToken);
            observations.Add(observation);
            inventories.Add(EvaluateInventory([observation], _depths[index + 1]));
        }

        return new BoundedCrossPageFixtureEvidence(
            RunId,
            InitialPlan,
            observations.ToImmutable(),
            inventories.ToImmutable(),
            dispatches.ToImmutable(),
            _environment.ActionHistory.ToImmutableArray());
    }

    internal static BranchInventoryEvidence EvaluateInventory(
        ImmutableArray<Observation> observations,
        int semanticDepth)
    {
        if (semanticDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(semanticDepth));
        if (observations.IsDefaultOrEmpty)
            return new BranchInventoryEvidence(null, "No accepted same-Container evidence is available.");

        var current = observations[^1];
        if (semanticDepth == 0 && Has(current, ParentBranch))
            return Inventory(ParentBranch, current.SequenceNumber, "P evidence proves complete inventory {A}.");
        if (semanticDepth == 1 && Has(current, ChildBranch))
            return Inventory(ChildBranch, current.SequenceNumber, "A evidence proves complete inventory {C}.");
        if (semanticDepth == 2 && Has(current, "Bounded leaf"))
        {
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                "C evidence positively proves an empty bounded inventory.");
        }
        if (semanticDepth >= 2 && Has(current, "Deeper branch D"))
        {
            return new BranchInventoryEvidence(
                null,
                "The semantic depth boundary does not prove a deeper required inventory.");
        }

        return new BranchInventoryEvidence(
            null,
            $"Evidence at seq={current.SequenceNumber} does not prove a complete inventory for depth={semanticDepth}.");
    }

    private static CandidateAuthorizationEvidence EvaluateAuthorization(
        Observation observation,
        ObservedElement candidate)
    {
        if (!observation.Elements.Contains(candidate))
            throw new ArgumentException("Candidate must be contained in the supplied Observation.", nameof(candidate));
        return candidate.Text is ParentBranch or ChildBranch
            ? new CandidateAuthorizationEvidence(true, "Fixture authorizes the bounded read-only branch candidate.")
            : new CandidateAuthorizationEvidence(null, "Fixture cannot authorize this candidate.");
    }

    private static BoundedCrossPageDiscoveryFixture Create(string runId, DiscoveryFixturePath path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var screens = Screens().ToArray();
        var initial = path switch
        {
            DiscoveryFixturePath.Positive or DiscoveryFixturePath.StaleChild
                or DiscoveryFixturePath.StaleChildAfterStartup
                or DiscoveryFixturePath.ConflictingChild
                or DiscoveryFixturePath.ConflictingChildAfterStartup => "ParentP",
            DiscoveryFixturePath.Unresolved => "UnresolvedP",
            DiscoveryFixturePath.DepthBound => "DepthBound",
            DiscoveryFixturePath.DepthBoundRoute => "ParentPDepth",
            DiscoveryFixturePath.ViewportSameContainer => "ParentViewport1",
            _ => throw new ArgumentOutOfRangeException(nameof(path)),
        };
        var actions = path switch
        {
            DiscoveryFixturePath.Positive => ImmutableArray.Create<DeviceAction>(
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0)),
            DiscoveryFixturePath.DepthBoundRoute => ImmutableArray.Create<DeviceAction>(
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0)),
            DiscoveryFixturePath.StaleChild or DiscoveryFixturePath.StaleChildAfterStartup
                or DiscoveryFixturePath.ConflictingChild
                or DiscoveryFixturePath.ConflictingChildAfterStartup
                => ImmutableArray.Create<DeviceAction>(new DeviceAction.Tap(0)),
            DiscoveryFixturePath.ViewportSameContainer
                => ImmutableArray.Create<DeviceAction>(new DeviceAction.ScrollForward()),
            _ => ImmutableArray<DeviceAction>.Empty,
        };
        var depths = path switch
        {
            DiscoveryFixturePath.Positive => ImmutableArray.Create(0, 1, 2),
            DiscoveryFixturePath.DepthBoundRoute => ImmutableArray.Create(0, 1, 2),
            DiscoveryFixturePath.StaleChild or DiscoveryFixturePath.StaleChildAfterStartup
                or DiscoveryFixturePath.ConflictingChild
                or DiscoveryFixturePath.ConflictingChildAfterStartup
                => ImmutableArray.Create(0, 1),
            DiscoveryFixturePath.DepthBound => ImmutableArray.Create(2),
            DiscoveryFixturePath.ViewportSameContainer => ImmutableArray.Create(0, 0),
            _ => ImmutableArray.Create(0),
        };
        var stale = path == DiscoveryFixturePath.StaleChild
            ? new Dictionary<long, long> { [2] = 1 }
            : path == DiscoveryFixturePath.StaleChildAfterStartup
                ? new Dictionary<long, long> { [3] = 2 }
            : null;
        var environment = new ScriptedEnvironment(
            initial,
            launchNextScreenName: null,
            screens,
            observeSequenceOverrides: stale);

        if (path is DiscoveryFixturePath.ConflictingChild or DiscoveryFixturePath.ConflictingChildAfterStartup)
        {
            var conflictSequence = path == DiscoveryFixturePath.ConflictingChild ? 2 : 3;
            environment = new ScriptedEnvironment(
                initial,
                launchNextScreenName: null,
                screens,
                observeOverrides: new Dictionary<long, (string, ImmutableArray<ObservedElement>)>
                {
                    [conflictSequence] = ("OtherApp", [new ObservedElement("Conflicting semantic page", null, 0)]),
                });
        }

        return new BoundedCrossPageDiscoveryFixture(runId, environment, actions, depths);
    }

    private static IEnumerable<ScreenConfig> Screens()
    {
        yield return new ScreenConfig(
            "ParentP",
            "Settings",
            [
                new ElementConfig(ParentBranch, null, TapTo("ChildA")),
                new ElementConfig("Optional candidate X", null, null),
            ]);
        yield return new ScreenConfig(
            "ChildA",
            "Settings",
            [new ElementConfig(ChildBranch, null, TapTo("ChildC"))]);
        yield return new ScreenConfig(
            "ChildC",
            "Settings",
            [new ElementConfig("Bounded leaf", null, null), new ElementConfig("Independent goal evidence", null, null)]);
        yield return new ScreenConfig(
            "UnresolvedP",
            "Settings",
            [new ElementConfig("Partial ambiguous branch evidence", null, null)]);
        yield return new ScreenConfig(
            "DepthBound",
            "Settings",
            [new ElementConfig("Deeper branch D", null, null)]);
        yield return new ScreenConfig(
            "ParentPDepth",
            "Settings",
            [new ElementConfig(ParentBranch, null, TapTo("ChildADepth"))]);
        yield return new ScreenConfig(
            "ChildADepth",
            "Settings",
            [new ElementConfig(ChildBranch, null, TapTo("ChildCDepth"))]);
        yield return new ScreenConfig(
            "ChildCDepth",
            "Settings",
            [new ElementConfig("Deeper branch D", null, null)]);
        yield return new ScreenConfig(
            "ParentViewport1",
            "Settings",
            [new ElementConfig(ParentBranch, null, null), new ElementConfig("More content", null, null)],
            new ViewportTransitionConfig("ParentViewport2"));
        yield return new ScreenConfig(
            "ParentViewport2",
            "Settings",
            [new ElementConfig(ParentBranch, null, null), new ElementConfig("Additional evidence", null, null)]);
    }

    private static BranchInventoryEvidence Inventory(string identity, long sequence, string reason)
        => new(ImmutableDictionary<string, long>.Empty.Add(identity, sequence), reason);

    private static bool Has(Observation observation, string text)
        => observation.Elements.Any(element => string.Equals(element.Text, text, StringComparison.Ordinal));

    internal static string? ResolveSemanticPage(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal))
            return null;
        if (Has(observation, ParentBranch))
            return "ParentP";
        if (Has(observation, ChildBranch))
            return "ChildA";
        if (Has(observation, "Bounded leaf") || Has(observation, "Deeper branch D"))
            return "ChildC";
        if (Has(observation, "Partial ambiguous branch evidence"))
            return "UnresolvedP";
        return null;
    }

    private static TransitionConfig TapTo(string screen)
        => new(ScreenTransitionAction.Tap, screen);

    private enum DiscoveryFixturePath
    {
        Positive,
        Unresolved,
        DepthBound,
        ViewportSameContainer,
        StaleChild,
        StaleChildAfterStartup,
        ConflictingChild,
        ConflictingChildAfterStartup,
        DepthBoundRoute,
    }
}

internal sealed record BoundedCrossPageFixtureEvidence(
    string RunId,
    Plan InitialPlan,
    ImmutableArray<Observation> Observations,
    ImmutableArray<BranchInventoryEvidence> Inventories,
    ImmutableArray<ActionResult> Dispatches,
    ImmutableArray<DeviceAction> ActionHistory);
