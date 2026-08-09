using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-P3-CAND-007 Task 1.1 deterministic proof fixture. It exercises only the approved
/// evidence value, Container-owned retention, and Fake world; Agent decision behavior is Task 2.1.
/// </summary>
public sealed class ViewportExplorationFixture
{
    public const string DefaultRunId = "sc-p3-cand-007-fixture-run";

    private static readonly DeviceAction ScrollAction = new DeviceAction.ScrollForward();
    private readonly ScriptedEnvironment _environment;
    private readonly ImmutableArray<PlanStep> _progressBefore;

    private ViewportExplorationFixture(
        string runId,
        ScriptedEnvironment environment,
        RuntimeContainer container,
        ImmutableArray<PlanStep> progressBefore)
    {
        RunId = runId;
        _environment = environment;
        Container = container;
        _progressBefore = progressBefore;
    }

    public string RunId { get; }

    public RuntimeContainer Container { get; }

    public static Task<ViewportExplorationFixture> PositiveAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.ViewportExplorationPositive());

    public static Task<ViewportExplorationFixture> AmbiguousSameAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.ViewportExplorationAmbiguousSame());

    public static Task<ViewportExplorationFixture> RejectedAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.ViewportExplorationRejected());

    public static Task<ViewportExplorationFixture> StaleAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.ViewportExplorationStale());

    public static Task<ViewportExplorationFixture> PageChangedAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.ViewportExplorationPageChanged());

    public async Task<ViewportExplorationFixtureEvidence> RunAsync(
        int maximumMovements = 2,
        CancellationToken cancellationToken = default)
    {
        if (maximumMovements < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMovements));

        var decisions = ImmutableArray.CreateBuilder<ViewportExplorationEvidence>();
        var dispatches = ImmutableArray.CreateBuilder<ActionResult>();
        bool? lastContinuityAccepted = null;
        var decision = Evaluate(Container.ViewportExplorationObservations);
        decisions.Add(decision);

        for (var movement = 0; movement < maximumMovements && decision.ContinueExploration is true; movement++)
        {
            var dispatch = await _environment.ExecuteAsync(ScrollAction, cancellationToken);
            dispatches.Add(dispatch);
            if (dispatch.Outcome == ActionResultOutcome.Rejected)
                break;

            var observation = await _environment.ObserveAsync(cancellationToken);
            var semanticPage = ResolveFixturePage(observation);
            lastContinuityAccepted = Container.TryVerifyViewportContinuity(
                observation,
                semanticPage,
                "Settings");
            if (lastContinuityAccepted is not true)
                break;

            decision = Evaluate(Container.ViewportExplorationObservations);
            decisions.Add(decision);
        }

        return new ViewportExplorationFixtureEvidence(
            RunId,
            Container.ViewportExplorationObservations,
            decisions.ToImmutable(),
            dispatches.ToImmutable(),
            _environment.ActionHistory.ToImmutableArray(),
            _environment.ObservationHistory.ToImmutableArray(),
            _progressBefore,
            Container.ExecutedSteps,
            lastContinuityAccepted);
    }

    public static ViewportExplorationEvidence Evaluate(ImmutableArray<Observation> evidence)
    {
        if (evidence.IsDefaultOrEmpty)
            return new ViewportExplorationEvidence(null, "No accepted same-Container Observation evidence.");

        var current = evidence[^1];
        if (current.Elements.Any(element => element.Text == "End of list"))
        {
            return new ViewportExplorationEvidence(
                false,
                $"Positive bounded end evidence observed at seq={current.SequenceNumber}.");
        }

        if (evidence.Length == 1
            && current.Elements.Any(element => element.Text == "More content"))
        {
            return new ViewportExplorationEvidence(
                true,
                $"Initial accepted evidence explicitly indicates more content at seq={current.SequenceNumber}.");
        }

        if (evidence.Length > 1)
        {
            var previousContent = ContentEvidence(evidence[^2]);
            var currentContent = ContentEvidence(current);
            if (current.Elements.Any(element => element.Text == "More content")
                && currentContent.Except(previousContent, StringComparer.Ordinal).Any())
            {
                return new ViewportExplorationEvidence(
                    true,
                    $"Fresh accepted evidence adds bounded relevant content at seq={current.SequenceNumber}.");
            }
        }

        return new ViewportExplorationEvidence(
            null,
            $"Accepted evidence at seq={current.SequenceNumber} proves neither continuation nor exhaustion.");
    }

    private static async Task<ViewportExplorationFixture> CreateAsync(
        string runId,
        ScriptedEnvironment environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var initial = await environment.ObserveAsync(CancellationToken.None);
        var container = new RuntimeContainer(
            "ScrollableList",
            observation => ResolveFixturePage(observation) == "ScrollableList",
            (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(initial);
        container.ExecuteStep(new PlanStep("Existing local progress", "Fixture marker"));
        return new ViewportExplorationFixture(runId, environment, container, container.ExecutedSteps);
    }

    private static ImmutableHashSet<string> ContentEvidence(Observation observation)
        => observation.Elements
            .Where(element => element.Text.Length == 1)
            .Select(element => element.Text)
            .ToImmutableHashSet(StringComparer.Ordinal);

    private static string? ResolveFixturePage(Observation observation)
        => observation.Elements.Any(element => element.Text is "A" or "B" or "C" or "D" or "E")
            ? "ScrollableList"
            : observation.Elements.Any(element => element.Text == "Other semantic page")
                ? "OtherPage"
                : null;
}

public sealed record ViewportExplorationFixtureEvidence(
    string RunId,
    ImmutableArray<Observation> AcceptedObservations,
    ImmutableArray<ViewportExplorationEvidence> Decisions,
    ImmutableArray<ActionResult> Dispatches,
    ImmutableArray<DeviceAction> ActionHistory,
    ImmutableArray<Observation> EnvironmentObservations,
    ImmutableArray<PlanStep> ProgressBefore,
    ImmutableArray<PlanStep> ProgressAfter,
    bool? LastContinuityAccepted);
