using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-P3-003 Task 1.1 的纯测试 Fixture：建立已有 Container local progress，
/// 驱动一次 targetless ScrollForward，并暴露 fresh/stale/identity-conflict evidence。
/// 不调用 Traversal/Agent viewport behavior。
/// </summary>
public sealed class ViewportMovementFixture
{
    public const string DefaultRunId = "sc-p3-003-fixture-run";

    private static readonly PlanStep ExistingProgressStep = new("Existing local progress", "Fixture marker");
    private static readonly DeviceAction ViewportAction = new DeviceAction.ScrollForward();
    private readonly ScriptedEnvironment _environment;
    private readonly Observation _before;
    private readonly ImmutableArray<PlanStep> _progressBefore;

    private ViewportMovementFixture(
        string runId,
        ScriptedEnvironment environment,
        RuntimeContainer activeContainer,
        Observation before,
        ImmutableArray<PlanStep> progressBefore)
    {
        RunId = runId;
        _environment = environment;
        ActiveContainer = activeContainer;
        _before = before;
        _progressBefore = progressBefore;
    }

    public string RunId { get; }

    public RuntimeContainer ActiveContainer { get; }

    public static Task<ViewportMovementFixture> ContinuousAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.ViewportContinuous());

    public static Task<ViewportMovementFixture> StaleAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.ViewportStale());

    public static Task<ViewportMovementFixture> PageChangedAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.ViewportPageChanged());

    public async Task<ViewportMovementEvidence> RunAsync(CancellationToken cancellationToken = default)
    {
        var dispatch = await _environment.ExecuteAsync(ViewportAction, cancellationToken);
        var after = await _environment.ObserveAsync(cancellationToken);
        return new ViewportMovementEvidence(
            RunId,
            _before,
            dispatch,
            after,
            _environment.ActionHistory.ToImmutableArray(),
            _progressBefore,
            ActiveContainer.ExecutedSteps,
            ResolveFixturePage(_before),
            ResolveFixturePage(after));
    }

    private static async Task<ViewportMovementFixture> CreateAsync(string runId, ScriptedEnvironment environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var before = await environment.ObserveAsync(CancellationToken.None);
        var container = new RuntimeContainer(
            "ScrollableList",
            observation => ResolveFixturePage(observation) == "ScrollableList",
            (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(before);
        if (container.ExecuteStep(ExistingProgressStep) is not TraversalStepResult.Succeeded)
            throw new InvalidOperationException("测试 Fixture 无法建立已有 Container local progress。");
        return new ViewportMovementFixture(runId, environment, container, before, container.ExecutedSteps);
    }

    private static string? ResolveFixturePage(Observation observation)
        => observation.Elements.Any(element => element.Text is "A" or "B" or "C" or "D" or "E" or "F")
            ? "ScrollableList"
            : observation.Elements.Any(element => element.Text == "Other semantic page")
                ? "OtherPage"
                : null;
}

public sealed record ViewportMovementEvidence(
    string RunId,
    Observation Before,
    ActionResult Dispatch,
    Observation After,
    ImmutableArray<DeviceAction> ActionHistory,
    ImmutableArray<PlanStep> ProgressBefore,
    ImmutableArray<PlanStep> ProgressAfter,
    string? SemanticPageBefore,
    string? SemanticPageAfter);
