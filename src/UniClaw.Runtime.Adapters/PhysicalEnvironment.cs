using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Adapters.Perception.Vision;
using UniClaw.Runtime.Capabilities.Perception.Vision;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;

namespace UniClaw.Runtime.Adapters;

/// <summary>
/// Concrete production IEnvironment composition root.
///
/// Composes the graduated Perception/Vision and Operator mechanisms
/// behind the existing IEnvironment Runtime boundary.
///
/// ObserveAsync: screenshot → perception → Vision enrichment → Observation
/// ExecuteAsync:  DeviceAction → AdbOperation → ADB executor → ActionResult
///
/// Owns external integration mechanics ONLY.
/// Owns NO semantic belief, business intent, capability selection,
/// goal completion, or recovery authority.
///
/// Interop points (marked as extension seams):
///   - Screenshot capture → IScreenshotSource
///   - Perception invocation → IPerceptionSource
///   - ADB execution → IAdbDispatchTarget
///
/// These are adapter-private interfaces for test substitution and
/// future live-device connection. They are NOT Runtime semantic ports.
/// </summary>
public sealed class PhysicalEnvironment : IEnvironment
{
    private readonly IScreenshotSource _screenshot;
    private readonly IPerceptionSource _perception;
    private readonly IAdbDispatchTarget _dispatch;
    private readonly string _foregroundApp;
    private readonly int _displayWidth;
    private readonly int _displayHeight;
    private readonly List<DeviceAction> _actionHistory = [];
    private readonly List<Observation> _observationHistory = [];
    private long _sequenceNumber;

    /// <summary>
    /// Creates the production composition.
    /// </summary>
    /// <param name="screenshot">Screenshot capture source.</param>
    /// <param name="perception">Perception pipeline source (YOLO/OCR/fusion).</param>
    /// <param name="dispatch">ADB dispatch target.</param>
    /// <param name="foregroundApp">Expected foreground application package.</param>
    /// <param name="displayWidth">Current device display width in pixels.</param>
    /// <param name="displayHeight">Current device display height in pixels.</param>
    public PhysicalEnvironment(
        IScreenshotSource screenshot,
        IPerceptionSource perception,
        IAdbDispatchTarget dispatch,
        string foregroundApp,
        int displayWidth,
        int displayHeight)
    {
        _screenshot = screenshot ?? throw new ArgumentNullException(nameof(screenshot));
        _perception = perception ?? throw new ArgumentNullException(nameof(perception));
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        _foregroundApp = foregroundApp ?? throw new ArgumentNullException(nameof(foregroundApp));
        _displayWidth = displayWidth > 0 ? displayWidth
            : throw new ArgumentException("Display width must be positive.", nameof(displayWidth));
        _displayHeight = displayHeight > 0 ? displayHeight
            : throw new ArgumentException("Display height must be positive.", nameof(displayHeight));
    }

    /// <summary>Actions dispatched, in order.</summary>
    public IReadOnlyList<DeviceAction> ActionHistory => _actionHistory;

    /// <summary>Observations produced, in order.</summary>
    public IReadOnlyList<Observation> ObservationHistory => _observationHistory;

    /// <summary>
    /// Production observe lifecycle — one external-world sampling generation.
    ///
    /// Frame F → PerceptionFrame F → perception candidates → Vision enrichment
    /// → ObservedElement[] → Observation. All must belong to the SAME capture F.
    /// </summary>
    public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        using var span = RuntimeObservability.StartSpan(
            "ObserveAsync", ObservabilityLayer.Environment, ObservabilityComponent.EnvironmentObserve);
        cancellationToken.ThrowIfCancellationRequested();
        var seq = ++_sequenceNumber;

        // 1. Capture fresh screenshot
        var capture = await _screenshot.CaptureAsync(cancellationToken);

        // 2. Create frame identity — all downstream work is scoped to this frame
        var frame = new PerceptionFrame();

        // 3. Invoke perception for this frame
        var candidates = await _perception.AnalyzeAsync(
            capture.ScreenshotData, capture.Width, capture.Height, cancellationToken);

        // 4. Create frame-scoped Vision mechanisms
        var switchReader = new ImageSwitchStateProvider(
            capture.ScreenshotData, capture.Width, capture.Height);

        // 5. Enrich candidates with Vision evidence
        var elements = ImmutableArray.CreateBuilder<ObservedElement>();
        for (int i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];

            // Normalize type: "switch" → "toggle" (adapter boundary)
            var perceptionType = NormalizeType(candidate.Type);

            // If toggle with valid bounds, read switch state
            bool? switchState = null;
            if (perceptionType == "toggle" && candidate.Bounds is { IsValid: true } bounds)
            {
                var rawState = await switchReader.ReadAsync(bounds, cancellationToken);
                // Validate frame match — stale evidence MUST fail closed
                switchState = SwitchStateValidation.ValidateFrameMatch(
                    switchReader, frame, rawState);
            }

            elements.Add(new ObservedElement(
                candidate.Text,
                switchState,
                i,
                candidate.Bounds,
                perceptionType));
        }

        // 6. Construct Observation — all evidence from frame F
        var observation = new Observation(
            elements.ToImmutable(),
            _foregroundApp,
            seq);

        _observationHistory.Add(observation);
        return observation;
    }

    /// <summary>
    /// Production execute lifecycle.
    ///
    /// Already-authorized DeviceAction → AdbOperation → ADB executor → ActionResult.
    /// Operator performs NO semantic capability selection.
    /// </summary>
    public async Task<ActionResult> ExecuteAsync(
        DeviceAction action, CancellationToken cancellationToken)
    {
        using var span = RuntimeObservability.StartSpan(
            "ExecuteAsync", ObservabilityLayer.Environment, ObservabilityComponent.EnvironmentExecute);
        cancellationToken.ThrowIfCancellationRequested();
        _actionHistory.Add(action);

        // Translate to ADB operation — fail closed on invalid input
        var op = DeviceActionTranslator.Translate(action, _displayWidth, _displayHeight);
        if (op is null)
        {
            return new ActionResult(
                ActionResultOutcome.Rejected,
                action.ToString(),
                "Adapter: translation failed — invalid target or missing spatial evidence.");
        }

        // Dispatch through ADB
        return await _dispatch.ExecuteAsync(op, cancellationToken);
    }

    private static string NormalizeType(string? rawType) => rawType switch
    {
        "switch" => "toggle",
        "checkbox" => "toggle",
        null => "menuItem",
        "" => "menuItem",
        _ => rawType,
    };
}

/// <summary>
/// Adapter-private screenshot capture source. Not a Runtime semantic port.
/// </summary>
public interface IScreenshotSource
{
    Task<ScreenshotCapture> CaptureAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Immutable screenshot capture result for one frame.
/// </summary>
/// <param name="ScreenshotData">Raw screenshot pixel data (format determined by provider).</param>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
public sealed record ScreenshotCapture(
    SkiaSharp.SKBitmap ScreenshotData,
    int Width,
    int Height);

/// <summary>
/// Adapter-private perception pipeline source. Not a Runtime semantic port.
/// </summary>
public interface IPerceptionSource
{
    Task<ImmutableArray<PerceptionCandidate>> AnalyzeAsync(
        SkiaSharp.SKBitmap screenshot,
        int width,
        int height,
        CancellationToken cancellationToken);
}

/// <summary>
/// Raw perception candidate — translated from YOLO/OCR/fusion output.
/// Adapter-internal type. Does NOT cross Runtime boundary.
/// </summary>
/// <param name="Text">OCR text (may be empty for non-text elements).</param>
/// <param name="Type">Raw provider type label (e.g., "toggle", "menuItem", "switch").</param>
/// <param name="Bounds">Normalized [0,1]×[0,1] bounding box, or null if unavailable.</param>
public sealed record PerceptionCandidate(
    string Text,
    string? Type,
    ElementBounds? Bounds);

/// <summary>
/// Adapter-private ADB dispatch target. Not a Runtime semantic port.
/// </summary>
public interface IAdbDispatchTarget
{
    Task<ActionResult> ExecuteAsync(
        AdbOperation operation,
        CancellationToken cancellationToken);
}
