using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-P3-CAND-005 Task 1.1 deterministic external-world fixture.
/// It scripts one bounded parent, A's observable effect, external Launcher drift, recovered-world
/// true/false/unobservable evidence, and remaining B navigation. It does not decide branch validity,
/// progress contribution, resume, escalation, Goal evidence, or final RunState.
/// </summary>
public sealed class RecoveryProgressResumeFixture
{
    public const string DefaultRunId = "sc-p3-cand-005-fixture-run";

    private readonly ScriptedEnvironment _environment;

    private RecoveryProgressResumeFixture(
        string runId,
        ScriptedEnvironment environment,
        Plan plan)
    {
        RunId = runId;
        _environment = environment;
        Plan = plan;
    }

    public string RunId { get; }

    public Plan Plan { get; }

    internal ScriptedEnvironment Environment => _environment;

    public static RecoveryProgressResumeFixture Survived(string runId = DefaultRunId)
        => Create(runId, RecoveredEffectEvidence.Survived);

    public static RecoveryProgressResumeFixture Contradicted(string runId = DefaultRunId)
        => Create(runId, RecoveredEffectEvidence.Contradicted);

    public static RecoveryProgressResumeFixture Unobservable(string runId = DefaultRunId)
        => Create(runId, RecoveredEffectEvidence.Unobservable);

    internal static RecoveryProgressResumeFixture AgentSurvived(
        bool includeCriterion = true,
        string runId = DefaultRunId)
        => Create(runId, RecoveredEffectEvidence.Survived, agentLifecycle: true, includeCriterion);

    internal static RecoveryProgressResumeFixture AgentContradicted(string runId = DefaultRunId)
        => Create(runId, RecoveredEffectEvidence.Contradicted, agentLifecycle: true);

    internal static RecoveryProgressResumeFixture AgentUnobservable(string runId = DefaultRunId)
        => Create(runId, RecoveredEffectEvidence.Unobservable, agentLifecycle: true);

    public async Task<RecoveryProgressWorldEvidence> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var observations = ImmutableArray.CreateBuilder<Observation>();
        var dispatches = ImmutableArray.CreateBuilder<ActionResult>();

        observations.Add(await _environment.ObserveAsync(cancellationToken));

        dispatches.Add(await _environment.ExecuteAsync(new DeviceAction.Tap(0), cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));

        dispatches.Add(await _environment.ExecuteAsync(new DeviceAction.SetSwitch(0, true), cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));

        dispatches.Add(await _environment.ExecuteAsync(new DeviceAction.Tap(1), cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));

        // Deterministic external drift. ScriptedEnvironment changes to Launcher before Observation #5.
        observations.Add(await _environment.ObserveAsync(cancellationToken));

        dispatches.Add(await _environment.ExecuteAsync(
            new DeviceAction.LaunchApp("Settings"),
            cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));

        dispatches.Add(await _environment.ExecuteAsync(new DeviceAction.Tap(1), cancellationToken));
        observations.Add(await _environment.ObserveAsync(cancellationToken));

        var branchAEntry = Plan.Steps[0];
        var criterionOutcome = branchAEntry.BranchEffectEvidenceEvaluator?.Invoke(observations[5]);

        return new RecoveryProgressWorldEvidence(
            RunId,
            observations.ToImmutable(),
            dispatches.ToImmutable(),
            _environment.ActionHistory.ToImmutableArray(),
            criterionOutcome);
    }

    private static RecoveryProgressResumeFixture Create(
        string runId,
        RecoveredEffectEvidence recoveredEffect,
        bool agentLifecycle = false,
        bool includeCriterion = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var environment = new ScriptedEnvironment(
            initialScreenName: agentLifecycle ? "Launcher" : "ParentP",
            launchNextScreenName: agentLifecycle ? "ParentP" : "RecoveredParentP",
            Screens(recoveredEffect),
            observeScreenTransitions: agentLifecycle
                ? new Dictionary<long, string>
                {
                    [6] = "Launcher",
                    [7] = "RecoveredParentP",
                }
                : new Dictionary<long, string> { [5] = "Launcher" });
        var plan = new Plan(
        [
            new PlanStep(
                "Branch A",
                "Tap",
                includeCriterion ? EvaluateBranchAEffect : null),
            new PlanStep("A external effect", "SetSwitch true"),
            new PlanStep("Return to Parent P", "Tap"),
            new PlanStep("Branch B", "Tap"),
            new PlanStep("Complete B work", "Tap"),
            new PlanStep("Return to Parent P", "Tap"),
        ]);
        return new RecoveryProgressResumeFixture(runId, environment, plan);
    }

    private static IEnumerable<ScreenConfig> Screens(RecoveredEffectEvidence recoveredEffect)
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
                new ElementConfig(
                    "A external effect",
                    false,
                    new TransitionConfig(ScreenTransitionAction.SetSwitch, "ChildAComplete", true)),
                new ElementConfig("Return to Parent P", null, TapTo("ParentAfterA")),
            ]);
        yield return new ScreenConfig(
            "ChildAComplete",
            "Settings",
            [
                new ElementConfig("A external effect", true, null),
                new ElementConfig("Return to Parent P", null, TapTo("ParentAfterA")),
            ]);
        yield return new ScreenConfig(
            "ParentAfterA",
            "Settings",
            [
                new ElementConfig("Branch A", null, TapTo("ChildA")),
                new ElementConfig("Branch B", null, TapTo("ChildB")),
                new ElementConfig("A external effect", true, null),
            ]);
        yield return new ScreenConfig(
            "RecoveredParentP",
            "Settings",
            RecoveredParentElements(recoveredEffect));
        yield return new ScreenConfig("Launcher", "Launcher", []);
        yield return new ScreenConfig(
            "ChildB",
            "Settings",
            [
                new ElementConfig("Complete B work", null, TapTo("ChildBComplete")),
                new ElementConfig("Return to Parent P", null, TapTo("RecoveredParentP")),
            ]);
        yield return new ScreenConfig(
            "ChildBComplete",
            "Settings",
            [
                new ElementConfig("B local effect", null, null),
                new ElementConfig("Return to Parent P", null, TapTo("RecoveredParentP")),
            ]);
    }

    private static ImmutableArray<ElementConfig> RecoveredParentElements(
        RecoveredEffectEvidence recoveredEffect)
    {
        var elements = ImmutableArray.CreateBuilder<ElementConfig>();
        elements.Add(new ElementConfig("Branch A", null, TapTo("ChildA")));
        elements.Add(new ElementConfig("Branch B", null, TapTo("ChildB")));
        if (recoveredEffect != RecoveredEffectEvidence.Unobservable)
        {
            elements.Add(new ElementConfig(
                "A external effect",
                recoveredEffect == RecoveredEffectEvidence.Survived,
                null));
        }
        return elements.ToImmutable();
    }

    private static bool? EvaluateBranchAEffect(Observation observation)
    {
        var matches = observation.Elements
            .Where(element => string.Equals(
                element.Text,
                "A external effect",
                StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0].SwitchState : null;
    }

    private static TransitionConfig TapTo(string screen)
        => new(ScreenTransitionAction.Tap, screen);

    private enum RecoveredEffectEvidence
    {
        Survived,
        Contradicted,
        Unobservable,
    }
}

/// <summary>Immutable test-only external-world and criterion evidence.</summary>
public sealed record RecoveryProgressWorldEvidence(
    string RunId,
    ImmutableArray<Observation> Observations,
    ImmutableArray<ActionResult> Dispatches,
    ImmutableArray<DeviceAction> ActionHistory,
    bool? CriterionOutcome);
