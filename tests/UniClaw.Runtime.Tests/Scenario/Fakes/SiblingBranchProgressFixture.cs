using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-P3-CAND-004 Task 1.1 deterministic external world.
/// The fixture scripts only visible pages, action outcomes, transitions, and Observations.
/// It does not decide semantic identity, branch completion, subtree completion, or Goal success.
/// </summary>
public sealed class SiblingBranchProgressFixture
{
    public const string DefaultRunId = "sc-p3-cand-004-fixture-run";

    private readonly ScriptedEnvironment _environment;
    private readonly ImmutableArray<DeviceAction> _actions;

    private SiblingBranchProgressFixture(
        string runId,
        ScriptedEnvironment environment,
        ImmutableArray<DeviceAction> actions)
    {
        RunId = runId;
        _environment = environment;
        _actions = actions;
    }

    public string RunId { get; }

    /// <summary>
    /// Test-only access for Task 2.1 Agent wiring. The fixture remains the sole owner of the
    /// deterministic external-world script; production code receives it only through IEnvironment.
    /// </summary>
    internal ScriptedEnvironment Environment => _environment;

    public static SiblingBranchProgressFixture Complete(string runId = DefaultRunId)
        => Create(runId, BranchWorldPath.Complete);

    public static SiblingBranchProgressFixture AOnly(string runId = DefaultRunId)
        => Create(runId, BranchWorldPath.AOnly);

    public static SiblingBranchProgressFixture EarlyReturn(string runId = DefaultRunId)
        => Create(runId, BranchWorldPath.EarlyReturn);

    public static SiblingBranchProgressFixture RevisitA(string runId = DefaultRunId)
        => Create(runId, BranchWorldPath.RevisitA);

    public static SiblingBranchProgressFixture StaleParent(string runId = DefaultRunId)
        => Create(runId, BranchWorldPath.StaleParent);

    /// <summary>
    /// Stale parent-return evidence after Startup's readiness Observation and Agent's initial
    /// Observation have already consumed two sequence numbers.
    /// </summary>
    internal static SiblingBranchProgressFixture StaleParentAfterStartup(string runId = DefaultRunId)
        => Create(runId, BranchWorldPath.StaleParentAfterStartup);

    public static SiblingBranchProgressFixture WrongParent(string runId = DefaultRunId)
        => Create(runId, BranchWorldPath.WrongParent);

    public async Task<SiblingBranchWorldEvidence> RunAsync(CancellationToken cancellationToken = default)
    {
        var observations = ImmutableArray.CreateBuilder<Observation>();
        var dispatches = ImmutableArray.CreateBuilder<ActionResult>();

        observations.Add(await _environment.ObserveAsync(cancellationToken));
        foreach (var action in _actions)
        {
            dispatches.Add(await _environment.ExecuteAsync(action, cancellationToken));
            observations.Add(await _environment.ObserveAsync(cancellationToken));
        }

        return new SiblingBranchWorldEvidence(
            RunId,
            observations.ToImmutable(),
            dispatches.ToImmutable(),
            _environment.ActionHistory.ToImmutableArray());
    }

    private static SiblingBranchProgressFixture Create(string runId, BranchWorldPath path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var stale = path switch
        {
            BranchWorldPath.StaleParent => new Dictionary<long, long> { [4] = 3 },
            BranchWorldPath.StaleParentAfterStartup => new Dictionary<long, long> { [5] = 4 },
            _ => null,
        };
        var environment = new ScriptedEnvironment(
            "ParentP",
            launchNextScreenName: null,
            Screens(path == BranchWorldPath.WrongParent),
            observeSequenceOverrides: stale);
        return new SiblingBranchProgressFixture(runId, environment, Actions(path));
    }

    private static IEnumerable<ScreenConfig> Screens(bool wrongParentReturn)
    {
        yield return new ScreenConfig(
            "ParentP",
            "Settings",
            [
                new ElementConfig("Branch A", null, TapTo("ChildA")),
                new ElementConfig("Branch B", null, TapTo("ChildB")),
            ]);
        yield return new ScreenConfig(
            "ChildA",
            "Settings",
            [
                new ElementConfig("Complete A work", null, TapTo("ChildAComplete")),
                new ElementConfig("Return to Parent P", null, TapTo("ParentP")),
            ]);
        yield return new ScreenConfig(
            "ChildAComplete",
            "Settings",
            [
                new ElementConfig("A local effect", null, null),
                new ElementConfig(
                    "Return to Parent P",
                    null,
                    TapTo(wrongParentReturn ? "OtherParent" : "ParentP")),
            ]);
        yield return new ScreenConfig(
            "ChildB",
            "Settings",
            [
                new ElementConfig("Complete B work", null, TapTo("ChildBComplete")),
                new ElementConfig("Return to Parent P", null, TapTo("ParentP")),
            ]);
        yield return new ScreenConfig(
            "ChildBComplete",
            "Settings",
            [
                new ElementConfig("B local effect", null, null),
                new ElementConfig("Return to Parent P", null, TapTo("ParentP")),
            ]);
        yield return new ScreenConfig(
            "OtherParent",
            "Settings",
            [new ElementConfig("Conflicting parent", null, null)]);
    }

    private static TransitionConfig TapTo(string screen)
        => new(ScreenTransitionAction.Tap, screen);

    private static ImmutableArray<DeviceAction> Actions(BranchWorldPath path) => path switch
    {
        BranchWorldPath.Complete =>
        [
            new DeviceAction.Tap(0), // P → A
            new DeviceAction.Tap(0), // local A effect
            new DeviceAction.Tap(1), // A → P
            new DeviceAction.Tap(1), // P → B
            new DeviceAction.Tap(0), // local B effect
            new DeviceAction.Tap(1), // B → P
        ],
        BranchWorldPath.AOnly or BranchWorldPath.StaleParent
            or BranchWorldPath.StaleParentAfterStartup or BranchWorldPath.WrongParent =>
        [
            new DeviceAction.Tap(0),
            new DeviceAction.Tap(0),
            new DeviceAction.Tap(1),
        ],
        BranchWorldPath.EarlyReturn =>
        [
            new DeviceAction.Tap(0),
            new DeviceAction.Tap(1),
        ],
        BranchWorldPath.RevisitA =>
        [
            new DeviceAction.Tap(0),
            new DeviceAction.Tap(0),
            new DeviceAction.Tap(1),
            new DeviceAction.Tap(0),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown branch-world path."),
    };

    private enum BranchWorldPath
    {
        Complete,
        AOnly,
        EarlyReturn,
        RevisitA,
        StaleParent,
        StaleParentAfterStartup,
        WrongParent,
    }
}

/// <summary>Immutable test-only external-world evidence.</summary>
public sealed record SiblingBranchWorldEvidence(
    string RunId,
    ImmutableArray<Observation> Observations,
    ImmutableArray<ActionResult> Dispatches,
    ImmutableArray<DeviceAction> ActionHistory);
