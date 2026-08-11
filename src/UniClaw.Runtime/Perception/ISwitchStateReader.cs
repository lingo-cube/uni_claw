using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Perception;

/// <summary>
/// Stateless perception port: reads the visual ON/OFF state of a toggle/switch
/// region from an immutable perception frame.
///
/// This is a PURE PERCEPTION port — it owns NO Runtime state, NO semantic
/// belief, NO capability selection, NO action authorization, and NO goal
/// completion authority.
///
/// The contract returns qualitative three-state evidence:
///   true  = visually ON
///   false = visually OFF
///   null  = UNKNOWN / insufficient evidence / not a recognizable switch
///
/// Reader instances are bound to one immutable fresh perception frame.
/// No confidence, model identity, or provider metadata crosses this boundary.
/// </summary>
public interface ISwitchStateReader
{
    /// <summary>
    /// Reads the visual toggle/switch state for the given normalized bounds.
    /// </summary>
    /// <param name="switchBounds">Normalized [0,1]×[0,1] toggle region in
    /// canonical full-screenshot frame (top-left origin). Must be valid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>true=ON, false=OFF, null=UNKNOWN/ambiguous/invalid.</returns>
    ValueTask<bool?> ReadAsync(
        ElementBounds switchBounds,
        CancellationToken cancellationToken = default);
}
