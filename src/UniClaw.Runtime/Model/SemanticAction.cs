namespace UniClaw.Runtime.Model;

/// <summary>
/// Immutable semantic action — a desired business/domain effect, NOT a UI procedure.
///
/// SemanticAction expresses WHAT should change in the world:
///   "WifiConnectivity.Enabled = true"
///
/// It does NOT express HOW to achieve it:
///   no Index, Bounds, Text, Tap, SetSwitch, coordinates, page routes.
///
/// Agent is the sole authority that authorizes SemanticActions.
/// Traversal grounds/lowers authorized actions to ExecutionActions.
///
/// Freeze: SemanticAction ≠ ExecutionAction. Domain effect ≠ UI procedure.
/// SetEnabled(true) ≠ Tap(toggle). Set is idempotent; physical toggle is not.
/// </summary>
/// <param name="ObjectIdentity">The SemanticObject.Identity to act upon.</param>
/// <param name="CapabilityName">The Capability.Name being invoked.</param>
/// <param name="StateDimension">The state dimension to change (must match Capability.StateDimension).</param>
/// <param name="DesiredValue">The desired value for the state dimension.</param>
public sealed record SemanticAction(
    string ObjectIdentity,
    string CapabilityName,
    string StateDimension,
    bool DesiredValue)
{
    /// <summary>Validates that the action is well-formed (non-null/empty fields).</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ObjectIdentity)
        && !string.IsNullOrWhiteSpace(CapabilityName)
        && !string.IsNullOrWhiteSpace(StateDimension);
}
