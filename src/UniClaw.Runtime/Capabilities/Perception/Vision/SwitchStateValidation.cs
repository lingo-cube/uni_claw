namespace UniClaw.Runtime.Capabilities.Perception.Vision;

/// <summary>
/// Production composition invariant: stale-frame evidence MUST NOT enter
/// a fresh Observation.
///
/// The perception adapter calls <see cref="ValidateFrameMatch"/> before
/// attaching any reader result to an ObservedElement. A frame mismatch
/// fails closed (returns null), converting potentially-trusted ON/OFF
/// evidence into safe UNKNOWN.
///
/// This is NOT an optional test-harness check — it is the mandatory
/// production composition rule for every ISwitchStateReader consumer.
/// </summary>
public static class SwitchStateValidation
{
    /// <summary>
    /// Validates that the reader's frame matches the current observation
    /// frame before evidence is attached.
    ///
    /// Production composition invariant:
    ///   reader.Frame == currentFrame → result passes through
    ///   reader.Frame != currentFrame → fail closed (null)
    ///   readResult is null → null (UNKNOWN preserved)
    /// </summary>
    /// <param name="reader">The frame-scoped switch state reader.</param>
    /// <param name="currentFrame">The frame currently being observed.</param>
    /// <param name="readResult">The raw reader result to validate.</param>
    /// <returns>readResult if frames match; null otherwise.</returns>
    public static bool? ValidateFrameMatch(
        ISwitchStateReader reader,
        PerceptionFrame currentFrame,
        bool? readResult)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(currentFrame);

        // UNKNOWN is already safe — pass through
        if (readResult is null)
            return null;

        // Stale frame → fail closed. A trusted ON/OFF from a stale frame
        // MUST NOT enter the current Observation as evidence.
        if (reader.Frame != currentFrame)
            return null;

        return readResult;
    }
}
