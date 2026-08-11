using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Capabilities.Perception.Vision;

/// <summary>
/// Opaque frame identity token. Created once per immutable perception capture.
/// A reader bound to one frame MUST NOT produce trusted evidence for another.
/// Two frames are never equal — each capture is a distinct immutable moment.
/// </summary>
public sealed record PerceptionFrame
{
    private readonly Guid _id = Guid.NewGuid();
}

/// <summary>
/// Frame-scoped perception port: reads the visual ON/OFF state of a
/// toggle/switch region from ONE immutable perception frame.
///
/// The reader is bound to a single frame at construction. Regions passed
/// to ReadAsync MUST belong to the same frame. Stale-frame use (bounds
/// from frame F1 passed to a reader bound to frame F2) is detectable
/// via <see cref="Frame"/> identity comparison.
///
/// This is a PURE PERCEPTION port — it owns NO Runtime state, NO semantic
/// belief, NO capability selection, NO action authorization, and NO goal
/// completion authority.
///
/// The contract returns qualitative three-state evidence:
///   true  = visually ON
///   false = visually OFF
///   null  = UNKNOWN / insufficient evidence / not a recognizable switch
///   null  = invalid bounds
///
/// No confidence, model identity, or provider metadata crosses this boundary.
/// </summary>
public interface ISwitchStateReader
{
    /// <summary>
    /// The immutable frame this reader is bound to. Bounds passed to
    /// <see cref="ReadAsync"/> MUST originate from this frame.
    /// </summary>
    PerceptionFrame Frame { get; }

    /// <summary>
    /// Reads the visual toggle/switch state for the given normalized bounds.
    /// </summary>
    /// <param name="switchBounds">Normalized [0,1]×[0,1] toggle region in
    /// canonical full-screenshot frame (top-left origin). Must be valid.
    /// Must belong to <see cref="Frame"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>true=ON, false=OFF, null=UNKNOWN/ambiguous/invalid.</returns>
    ValueTask<bool?> ReadAsync(
        ElementBounds switchBounds,
        CancellationToken cancellationToken = default);
}
