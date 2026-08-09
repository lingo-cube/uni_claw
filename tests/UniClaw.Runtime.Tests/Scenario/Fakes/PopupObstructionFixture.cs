using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-P3-002 Task 1.1 的纯测试 Fixture：组合 ScriptedEnvironment 与真实 Container 局部状态表面，
/// 只负责建立已有 local progress、驱动确定性的外部 Popup 世界序列并暴露证据快照。
/// 不调用 Agent/Traversal Popup handling，不实现 Task 2.1 Runtime behavior。
/// </summary>
public sealed class PopupObstructionFixture
{
    public const string DefaultRunId = "sc-p3-002-fixture-run";

    private static readonly PlanStep ExistingProgressStep = new("Existing local progress", "Fixture marker");
    private static readonly DeviceAction DismissAction = new DeviceAction.Tap(0);

    private readonly ScriptedEnvironment _environment;
    private readonly Observation _initialObservation;
    private readonly ImmutableArray<PlanStep> _progressBefore;

    private PopupObstructionFixture(
        string runId,
        ScriptedEnvironment environment,
        RuntimeContainer activeContainer,
        Observation initialObservation,
        ImmutableArray<PlanStep> progressBefore)
    {
        RunId = runId;
        _environment = environment;
        ActiveContainer = activeContainer;
        _initialObservation = initialObservation;
        _progressBefore = progressBefore;
    }

    /// <summary>确定性 replay 输入标识；Fixture 不把 RunId 注入生产 Runtime。</summary>
    public string RunId { get; }

    /// <summary>Popup 前已绑定且已有 local progress 的同一个 Container 实例。</summary>
    public RuntimeContainer ActiveContainer { get; }

    public static Task<PopupObstructionFixture> ContinuousAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.PopupDismissContinuous());

    public static Task<PopupObstructionFixture> DismissRejectedAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.PopupDismissRejected());

    public static Task<PopupObstructionFixture> PageChangedAsync(string runId = DefaultRunId)
        => CreateAsync(runId, ScriptedEnvironmentVariants.PopupDismissPageChanged());

    /// <summary>
    /// 驱动纯 Fake 世界序列：外部 Popup 出现 → 单次 dismiss dispatch → fresh Observe。
    /// Container 不参与 handling；前后 progress 快照只用于证明 Fixture 可观察该状态。
    /// </summary>
    public async Task<PopupObstructionEvidence> RunAsync(CancellationToken cancellationToken = default)
    {
        var obstruction = await _environment.ObserveAsync(cancellationToken);
        var dispatch = await _environment.ExecuteAsync(DismissAction, cancellationToken);
        var afterDismiss = await _environment.ObserveAsync(cancellationToken);

        return new PopupObstructionEvidence(
            RunId,
            _initialObservation,
            obstruction,
            dispatch,
            afterDismiss,
            _environment.ActionHistory.ToImmutableArray(),
            _progressBefore,
            ActiveContainer.ExecutedSteps);
    }

    private static async Task<PopupObstructionFixture> CreateAsync(string runId, ScriptedEnvironment environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var initialObservation = await environment.ObserveAsync(CancellationToken.None);
        var container = new RuntimeContainer(
            "NetworkSettings",
            ScenarioIdentity.IdentityRule("NetworkSettings"),
            (_, _, _) => new TraversalStepResult.Succeeded());

        container.Bind(initialObservation);
        var progressResult = container.ExecuteStep(ExistingProgressStep);
        if (progressResult is not TraversalStepResult.Succeeded)
            throw new InvalidOperationException("测试 Fixture 无法建立既有 Container local progress。");

        return new PopupObstructionFixture(
            runId,
            environment,
            container,
            initialObservation,
            container.ExecutedSteps);
    }
}

/// <summary>SC-P3-002 Task 1.1 的不可变测试证据快照。</summary>
public sealed record PopupObstructionEvidence(
    string RunId,
    Observation InitialObservation,
    Observation ObstructionObservation,
    ActionResult DismissDispatch,
    Observation PostDismissObservation,
    ImmutableArray<DeviceAction> ActionHistory,
    ImmutableArray<PlanStep> ProgressBefore,
    ImmutableArray<PlanStep> ProgressAfter);
