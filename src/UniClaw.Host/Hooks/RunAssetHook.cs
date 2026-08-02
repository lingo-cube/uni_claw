using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Runner;

namespace UniClaw.Host.Hooks;

/// <summary>
/// Writes per-step run artifacts on the engine lifecycle. On OnBeforeStep it
/// begins the next step asset, captures the pre-step screenshot + hierarchy,
/// shares the hierarchy via <see cref="StepCaptureStore"/> so page analysis
/// does not re-run the ADB refresh, and submits the evidence write to
/// <see cref="StepAssetSink"/>; on OnAfterStep it captures the post-step state
/// and submits after evidence the same way.
/// </summary>
public sealed class RunAssetHook : TraversalHookBase
{
    private readonly RunAssetSession _assets;
    private readonly IScreenCapture _capture;
    private readonly IObservableScreenStateProvider _screenState;
    private readonly StepCaptureStore _captureStore;
    private readonly StepAssetSink _sink;
    private StepAssetWriter? _step;
    private StepAssetWriter? _lastCompletedStep;

    public RunAssetHook(
        RunAssetSession assets,
        IScreenCapture capture,
        IObservableScreenStateProvider screenState,
        StepCaptureStore captureStore,
        StepAssetSink sink)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _screenState = screenState
                       ?? throw new ArgumentNullException(nameof(screenState));
        _captureStore = captureStore
                        ?? throw new ArgumentNullException(nameof(captureStore));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    /// <inheritdoc/>
    public override async Task OnBeforeStepAsync(ITraversalContext context)
    {
        var screenshot = await _capture.CaptureAsync();
        var state = await _screenState.RefreshAsync();
        _captureStore.SetBefore(state);
        _step = await _assets.BeginStepAsync(
            context.StepCount,
            state.HierarchyFingerprint ?? string.Empty);
        var step = _step;
        var uiXml = state.HierarchyXml ?? string.Empty;
        _sink.Submit(token => step.WriteBeforeAsync(screenshot, uiXml, token));
    }

    /// <inheritdoc/>
    public override async Task OnAfterStepAsync(ITraversalContext context)
    {
        if (_step is null)
            return;

        // Skip after-capture when no real action ran: the before XML is still
        // valid in the store, so page state is identical to the before capture.
        if (_captureStore.TryGetBefore(out _))
        {
            _lastCompletedStep = _step;
            _step = null;
            return;
        }

        var screenshot = await _capture.CaptureAsync();
        var state = await _screenState.RefreshAsync();
        var step = _step;
        var uiXml = state.HierarchyXml ?? string.Empty;
        _sink.Submit(token => step.WriteAfterAsync(screenshot, uiXml, token));
        _lastCompletedStep = _step;
        _step = null;
    }

    /// <summary>
    /// Replaces the last step's immediate post-step evidence with a stabilized
    /// capture. Android navigation may render after the engine hook returns, so
    /// scenario completion verification calls this after its stabilization wait.
    /// The write is submitted to the sink and drained at run finalization.
    /// </summary>
    public void RefreshLastAfterAsync()
    {
        if (_lastCompletedStep is null)
            return;

        var step = _lastCompletedStep;
        _sink.Submit(async token =>
        {
            var screenshot = await _capture.CaptureAsync(token);
            var state = await _screenState.RefreshAsync(
                cancellationToken: token);
            await step.WriteAfterAsync(
                screenshot,
                state.HierarchyXml ?? string.Empty,
                token);
        });
    }
}
