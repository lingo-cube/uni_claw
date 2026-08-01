using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Host.Artifacts;

namespace UniClaw.Host.Hooks;

/// <summary>
/// Writes per-step run artifacts on the engine lifecycle. On OnBeforeStep it
/// begins the next step asset, captures the pre-step screenshot + hierarchy and
/// writes before/analysis evidence; on OnAfterStep it captures the post-step
/// state and writes after evidence. The hook must obtain screenshot bytes and
/// hierarchy itself — <see cref="PageAnalysis"/> carries no screenshot bytes and
/// the engine context exposes only node/path state, so the step evidence is
/// captured via <see cref="IScreenCapture"/> + <see cref="IObservableScreenStateProvider"/>.
/// </summary>
public sealed class RunAssetHook : TraversalHookBase
{
    private readonly RunAssetSession _assets;
    private readonly IScreenCapture _capture;
    private readonly IObservableScreenStateProvider _screenState;
    private StepAssetWriter? _step;
    private StepAssetWriter? _lastCompletedStep;

    public RunAssetHook(
        RunAssetSession assets,
        IScreenCapture capture,
        IObservableScreenStateProvider screenState)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _screenState = screenState
                       ?? throw new ArgumentNullException(nameof(screenState));
    }

    /// <inheritdoc/>
    public override async Task OnBeforeStepAsync(ITraversalContext context)
    {
        var screenshot = await _capture.CaptureAsync();
        var state = await _screenState.RefreshAsync();
        _step = await _assets.BeginStepAsync(
            context.StepCount,
            state.HierarchyFingerprint ?? string.Empty);
        await _step.WriteBeforeAsync(screenshot, state.HierarchyXml ?? string.Empty);
    }

    /// <inheritdoc/>
    public override async Task OnAfterStepAsync(ITraversalContext context)
    {
        if (_step is null)
            return;
        var screenshot = await _capture.CaptureAsync();
        var state = await _screenState.RefreshAsync();
        await _step.WriteAfterAsync(screenshot, state.HierarchyXml ?? string.Empty);
        _lastCompletedStep = _step;
        _step = null;
    }

    /// <summary>
    /// Replaces the last step's immediate post-step evidence with a stabilized
    /// capture. Android navigation may render after the engine hook returns, so
    /// scenario completion verification calls this after its stabilization wait.
    /// </summary>
    public async Task RefreshLastAfterAsync(
        CancellationToken cancellationToken = default)
    {
        if (_lastCompletedStep is null)
            return;

        var screenshot = await _capture.CaptureAsync(cancellationToken);
        var state = await _screenState.RefreshAsync(
            cancellationToken: cancellationToken);
        await _lastCompletedStep.WriteAfterAsync(
            screenshot,
            state.HierarchyXml ?? string.Empty,
            cancellationToken);
    }
}
