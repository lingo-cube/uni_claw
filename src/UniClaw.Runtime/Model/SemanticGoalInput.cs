namespace UniClaw.Runtime.Model;

/// <summary>
/// Structured semantic desired outcome — Phase 5 input, NOT natural language.
///
/// Expresses WHAT should be true in the world:
///   "WifiConnectivity.Enabled = true"
///
/// Phase 6 (Intent Compilation) will produce this from natural language.
/// Phase 5 consumes this to drive the closed-loop semantic agent.
/// </summary>
/// <param name="ObjectIdentity">The SemanticObject.Identity to affect.</param>
/// <param name="StateDimension">The state dimension to achieve.</param>
/// <param name="DesiredValue">The desired boolean value.</param>
public sealed record SemanticGoalInput(
    string ObjectIdentity,
    string StateDimension,
    bool DesiredValue);
