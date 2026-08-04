using System.Text;
using UniClaw.Core.Observability;
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
/// does not re-run the ADB refresh, and submits the evidence to
/// <see cref="ITracePipeline"/> (relative paths; runId is injected at assembly
/// into <c>assets/{runId}/…</c>); on OnAfterStep it captures the post-step state
/// and submits after evidence the same way.
/// </summary>
public sealed class RunAssetHook : TraversalHookBase
{
    private readonly RunAssetSession _assets;
    private readonly IScreenCapture _capture;
    private readonly IObservableScreenStateProvider _screenState;
    private readonly StepCaptureStore _captureStore;
    private readonly ITracePipeline _pipeline;
    private StepAssetWriter? _step;
    private StepAssetWriter? _lastCompletedStep;

    public RunAssetHook(
        RunAssetSession assets,
        IScreenCapture capture,
        IObservableScreenStateProvider screenState,
        StepCaptureStore captureStore,
        ITracePipeline pipeline)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _screenState = screenState
                       ?? throw new ArgumentNullException(nameof(screenState));
        _captureStore = captureStore
                        ?? throw new ArgumentNullException(nameof(captureStore));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    /// <inheritdoc/>
    public override async Task OnBeforeStepAsync(ITraversalContext context)
    {
        try
        {
            var screenshot = await _capture.CaptureAsync();
            var state = await _screenState.RefreshAsync();
            _captureStore.SetBefore(state);
            _step = await _assets.BeginStepAsync(
                context.StepCount,
                state.HierarchyFingerprint ?? string.Empty);
            var step = _step;
            var uiXml = state.HierarchyXml ?? string.Empty;

            // Submit before screenshot
            _pipeline.Submit(new AssetSubmission(
                AssetCategories.Screenshot,
                screenshot.ToArray(),
                $"steps/{step.StepNumber:D4}/before.png"));

            // Submit before XML
            if (!string.IsNullOrEmpty(uiXml))
            {
                var xmlBytes = Encoding.UTF8.GetBytes(uiXml);
                _pipeline.Submit(new AssetSubmission(
                    AssetCategories.UiXml,
                    xmlBytes,
                    $"steps/{step.StepNumber:D4}/before.xml"));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // P3.1: hook exceptions no longer silently swallowed by FireAsync
            throw new ScenarioObservationException(
                "hook_before_step_failed",
                $"OnBeforeStepAsync failed at step {context.StepCount}: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public override async Task OnAfterStepAsync(ITraversalContext context)
    {
        try
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

            // Submit after screenshot
            _pipeline.Submit(new AssetSubmission(
                AssetCategories.Screenshot,
                screenshot.ToArray(),
                $"steps/{step.StepNumber:D4}/after.png"));

            // Submit after XML
            if (!string.IsNullOrEmpty(uiXml))
            {
                var xmlBytes = Encoding.UTF8.GetBytes(uiXml);
                _pipeline.Submit(new AssetSubmission(
                    AssetCategories.UiXml,
                    xmlBytes,
                    $"steps/{step.StepNumber:D4}/after.xml"));
            }

            _lastCompletedStep = _step;
            _step = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ScenarioObservationException(
                "hook_after_step_failed",
                $"OnAfterStepAsync failed at step {context.StepCount}: {ex.Message}");
        }
    }

    /// <summary>
    /// Replaces the last step's immediate post-step evidence with a stabilized
    /// capture. Android navigation may render after the engine hook returns, so
    /// scenario completion verification calls this after its stabilization wait.
    /// The capture runs here (submissions are data objects, not delegates) and
    /// the stabilized evidence is submitted to the pipeline for drain at run
    /// finalization.
    /// </summary>
    public async Task RefreshLastAfterAsync()
    {
        if (_lastCompletedStep is null)
            return;

        var step = _lastCompletedStep;
        var screenshot = await _capture.CaptureAsync();
        var state = await _screenState.RefreshAsync();
        var uiXml = state.HierarchyXml ?? string.Empty;

        _pipeline.Submit(new AssetSubmission(
            AssetCategories.Screenshot,
            screenshot.ToArray(),
            $"steps/{step.StepNumber:D4}/after.png"));
        if (!string.IsNullOrEmpty(uiXml))
        {
            var xmlBytes = Encoding.UTF8.GetBytes(uiXml);
            _pipeline.Submit(new AssetSubmission(
                AssetCategories.UiXml,
                xmlBytes,
                $"steps/{step.StepNumber:D4}/after.xml"));
        }
    }
}
