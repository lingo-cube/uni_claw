using System.Runtime.InteropServices;
using SkiaSharp;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Runner;

namespace UniClaw.Host.Hooks;

/// <summary>
/// Writes per-step run artifacts on the engine lifecycle. On OnBeforeStep it
/// begins the next step asset, captures the pre-step screenshot and submits the
/// evidence to <see cref="ITracePipeline"/> (relative paths; runId is injected
/// at assembly into <c>assets/{runId}/…</c>); on OnAfterStep it captures the
/// post-step state and submits after evidence the same way. UIA hierarchy
/// evidence (before.xml / after.xml) was removed with the UIA pipeline
/// (delete-uia) — screenshots are the only per-step evidence.
/// </summary>
public sealed class RunAssetHook : TraversalHookBase
{
    private readonly RunAssetSession _assets;
    private readonly IScreenCapture _capture;
    private readonly ITracePipeline _pipeline;
    private StepAssetWriter? _step;
    private StepAssetWriter? _lastCompletedStep;

    public RunAssetHook(
        RunAssetSession assets,
        IScreenCapture capture,
        ITracePipeline pipeline)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    /// <inheritdoc/>
    public override async Task OnBeforeStepAsync(ITraversalContext context)
    {
        try
        {
            var screenshot = await CaptureScreenshotPngAsync();
            _step = await _assets.BeginStepAsync(
                context.StepCount,
                Fingerprint(screenshot));
            var step = _step;

            // Submit before screenshot
            _pipeline.Submit(new AssetSubmission(
                AssetCategories.Screenshot,
                screenshot.ToArray(),
                $"steps/{step.StepNumber:D4}/before.png"));
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

            var screenshot = await CaptureScreenshotPngAsync();
            var step = _step;

            // Submit after screenshot
            _pipeline.Submit(new AssetSubmission(
                AssetCategories.Screenshot,
                screenshot.ToArray(),
                $"steps/{step.StepNumber:D4}/after.png"));

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
    /// Raw RGBA → PNG 存盘编码 (raw-rgba-screenshot-pipeline D-5 存储边界)。
    /// C# 侧唯一一次像素操作; 输出标准 PNG, 下游 (文件管理器 / PIL / trace viewer) 无需感知,
    /// 文件名仍为 before.png / after.png。
    /// </summary>
    private static byte[] EncodeRawToPng(RawScreenBuffer raw)
    {
        using var bitmap = new SKBitmap(raw.Width, raw.Height,
            SKColorType.Rgba8888, SKAlphaType.Unpremul);
        // SkiaSharp 4.x SKBitmap.SetPixels 只接受 nint(IntPtr): pin 住 raw 字节数组
        // (Encode 为同步操作, pin 生命周期覆盖整个编码)。
        var handle = GCHandle.Alloc(raw.Pixels, GCHandleType.Pinned);
        try
        {
            bitmap.SetPixels(handle.AddrOfPinnedObject());
            return bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// 截图 → PNG 字节。UNICLAW_RAW_SCREEN_BUFFER=1 时走 raw 路径
    /// (CaptureRawAsync → EncodeRawToPng, 跳过设备端 PNG encode);
    /// 否则用现有 CaptureAsync (设备端 PNG 直出)。
    /// </summary>
    private async Task<byte[]> CaptureScreenshotPngAsync()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_RAW_SCREEN_BUFFER") == "1")
        {
            var raw = await _capture.CaptureRawAsync();
            return EncodeRawToPng(raw);
        }
        return await _capture.CaptureAsync();
    }

    /// <summary>
    /// Deterministic FNV-1a hash of the PNG bytes, hex-encoded — the step-level
    /// page identity for the evidence manifest. Replaces the removed UIA
    /// hierarchy fingerprint (delete-uia); screenshots are the only evidence.
    /// </summary>
    private static string Fingerprint(byte[] bytes)
    {
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            uint hash = offsetBasis;
            foreach (var b in bytes)
                hash = (hash ^ b) * prime;
            return hash.ToString("X8");
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
        var screenshot = await CaptureScreenshotPngAsync();

        _pipeline.Submit(new AssetSubmission(
            AssetCategories.Screenshot,
            screenshot.ToArray(),
            $"steps/{step.StepNumber:D4}/after.png"));
    }
}
