using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
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
    private readonly IStructuredUiHierarchySource? _structuredUi;
    private readonly string _foregroundApp;
    private readonly int _displayWidth;
    private readonly int _displayHeight;
    private readonly Action<PhysicalArtifactTap>? _artifactTap;
    private readonly IVisualControlStateReaderFactory? _visualControlFactory;
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
        int displayHeight,
        IStructuredUiHierarchySource? structuredUiSource = null,
        Action<PhysicalArtifactTap>? artifactTap = null,
        IVisualControlStateReaderFactory? visualControlFactory = null)
    {
        _screenshot = screenshot ?? throw new ArgumentNullException(nameof(screenshot));
        _perception = perception ?? throw new ArgumentNullException(nameof(perception));
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        _structuredUi = structuredUiSource;
        _foregroundApp = foregroundApp ?? throw new ArgumentNullException(nameof(foregroundApp));
        _displayWidth = displayWidth > 0 ? displayWidth
            : throw new ArgumentException("Display width must be positive.", nameof(displayWidth));
        _displayHeight = displayHeight > 0 ? displayHeight
            : throw new ArgumentException("Display height must be positive.", nameof(displayHeight));
        _artifactTap = artifactTap;
        _visualControlFactory = visualControlFactory;
    }

    /// <summary>Actions dispatched, in order.</summary>
    public IReadOnlyList<DeviceAction> ActionHistory => _actionHistory;

    /// <summary>Observations produced, in order.</summary>
    public IReadOnlyList<Observation> ObservationHistory => _observationHistory;

    /// <summary>Source provenance for the most recently produced observation.</summary>
    public IReadOnlyList<ObservationSourceMetadata> LastObservationSources { get; private set; } = [];

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

        // 1. Capture fresh screenshot (perception.capture stage)
        ScreenshotCapture capture;
        using (var captureSpan = RuntimeObservability.StartSpan(
            "PerceptionCapture", ObservabilityLayer.Capability, ObservabilityComponent.PerceptionCapture))
        {
            capture = await _screenshot.CaptureAsync(cancellationToken);
            RuntimeObservability.Complete(captureSpan, ObservabilityOutcome.Succeeded);
        }

        // 2. Create the frame-scoped Vision mechanism FIRST — it owns this
        //    capture's PerceptionFrame identity. Every downstream SwitchState
        //    read is validated against that SAME identity (stale evidence from
        //    a different capture fails closed — F4), so the observation's frame
        //    must be the reader's frame, not a second instance.
        ISwitchStateReader? switchReader = null;
        if (_visualControlFactory is not null)
        {
            try
            {
                using var encodedFrame = capture.ScreenshotData.Encode(
                    SkiaSharp.SKEncodedImageFormat.Png, 100);
                switchReader = _visualControlFactory.Create(
                    encodedFrame.ToArray(), capture.Width, capture.Height);
            }
            catch
            {
                // Optional visual enrichment is fail-closed and never blocks Vision.
                switchReader = null;
            }
        }
        var frame = switchReader?.Frame ?? new PerceptionFrame();

        // 3. Invoke perception for this frame (perception.vision stage — vision
        //    inference + enrich + optional structured acquisition)
        var elements = ImmutableArray.CreateBuilder<ObservedElement>();
        ImmutableArray<StructuredElementEvidence> structuredElements = [];
        var structuredAvailable = false;
        var candidates = ImmutableArray<PerceptionCandidate>.Empty;
        using (var visionSpan = RuntimeObservability.StartSpan(
            "PerceptionVision", ObservabilityLayer.Capability, ObservabilityComponent.PerceptionVision))
        {
            candidates = await _perception.AnalyzeAsync(
                capture.ScreenshotData, capture.Width, capture.Height, cancellationToken);

            // 4. Enrich candidates with Vision evidence
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];

            // Normalize provider aliases at the adapter boundary. The Runtime
            // semantic path consumes the canonical toggle type; raw detector
            // evidence remains available through the artifact tap.
            var perceptionType = NormalizeType(candidate.Type);

            // If toggle with valid bounds, read switch state
            bool? switchState = null;
            if (_visualControlFactory?.CanRead(perceptionType) == true
                && candidate.Bounds is { IsValid: true } bounds)
            {
                var rawState = switchReader is null
                    ? null
                    : await switchReader.ReadAsync(bounds, cancellationToken);
                // Validate frame match — stale evidence MUST fail closed
                switchState = switchReader is not null && frame is not null
                    ? SwitchStateValidation.ValidateFrameMatch(switchReader, frame, rawState)
                    : null;
            }

            elements.Add(new ObservedElement(
                candidate.Text,
                switchState,
                i,
                candidate.Bounds,
                perceptionType)
            {
                StabilizerHint = candidate.RowId,
            });
        }

        // 4.5 Capture structured Android UI evidence from the same external state.
        // This is optional: absence fails closed to an empty structured evidence stream.
        if (_structuredUi is not null)
        {
            try
            {
                structuredElements = await _structuredUi.CaptureAsync(
                    _displayWidth,
                    _displayHeight,
                    cancellationToken);
                structuredAvailable = !structuredElements.IsDefault && !structuredElements.IsEmpty;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Auxiliary structured acquisition is best-effort. Vision remains
                // the primary observation and must not fail because this source is unavailable.
                structuredElements = [];
            }
        }
        RuntimeObservability.Complete(visionSpan, ObservabilityOutcome.Succeeded);
        } // perception.vision stage

        // 5. Construct Observation — all evidence from frame F
        var observation = new Observation(
            elements.ToImmutable(),
            _foregroundApp,
            seq)
        {
            StructuredElements = structuredElements,
        };

        LastObservationSources =
        [
            new ObservationSourceMetadata(
                ObservationSourceTier.PrimaryVision,
                available: true,
                seq,
                $"capture:{seq}",
                _displayWidth,
                _displayHeight,
                "PhysicalEnvironment.screenshot/perception",
                "primary-vision"),
            new ObservationSourceMetadata(
                ObservationSourceTier.AuxiliaryStructured,
                structuredAvailable,
                seq,
                $"capture:{seq}",
                _displayWidth,
                _displayHeight,
                "PhysicalEnvironment.optional-structured",
                "auxiliary-structured")
        ];
        observation = observation with { Sources = LastObservationSources.ToImmutableArray() };

        // Optional mechanism-local evidence tap. It runs only after the
        // observation is complete and is strictly failure-isolated from the
        // IEnvironment lifecycle (including dispatch and observation output).
        if (_artifactTap is not null)
        {
            try
            {
                using var encoded = capture.ScreenshotData.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                _artifactTap(new PhysicalArtifactTap(
                    frame,
                    seq,
                    encoded.ToArray(),
                    capture.Width,
                    capture.Height,
                    candidates,
                    observation));
            }
            catch
            {
                // Artifact capture is evidence-only. A tap/provider fault must
                // never escape into Runtime observation semantics.
            }
        }

        // Evidence anchors (observability-evidence-anchors): the observe span
        // carries the observation sequence and frame token so FAILED spans can
        // jump to the frame/screenshot via AssetRef (candidate correlation,
        // never world truth).
        RuntimeObservability.SetTag(span, "observation.seq", seq.ToString());
        RuntimeObservability.SetTag(span, "observation.frame", $"capture:{seq}");

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
        RuntimeObservability.SetTag(span, "action.kind", action.GetType().Name);

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
/// Optional, narrow physical evidence report for one completed ObserveAsync
/// generation. The frame token is the exact token used by Vision enrichment.
/// This type is adapter-local and carries no Runtime authority.
/// </summary>
public sealed record PhysicalArtifactTap(
    PerceptionFrame Frame,
    long SequenceNumber,
    byte[] PngBytes,
    int Width,
    int Height,
    ImmutableArray<PerceptionCandidate> Candidates,
    Observation Observation);

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
/// <param name="RowId">Stable row identity from the perception stabilizer (null for new rows). DESIGN-SPEC D2.</param>
public sealed record PerceptionCandidate(
    string Text,
    string? Type,
    ElementBounds? Bounds,
    string? RowId = null);

/// <summary>
/// Adapter-private ADB dispatch target. Not a Runtime semantic port.
/// </summary>
public interface IAdbDispatchTarget
{
    Task<ActionResult> ExecuteAsync(
        AdbOperation operation,
        CancellationToken cancellationToken);
}
