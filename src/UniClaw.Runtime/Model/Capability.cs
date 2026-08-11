namespace UniClaw.Runtime.Model;

/// <summary>
/// Immutable declarative capability contract — NOT mutable runtime state.
///
/// A Capability describes WHAT business/domain effect is possible, independent
/// of HOW UI execution occurs. It is a domain contract ("SetEnabled on
/// ConnectivitySetting"), not an execution procedure (no Tap, SetSwitch,
/// coordinates, page routes, element selectors).
///
/// Agent SELECTS and APPLIES capabilities. Capability DEFINITIONS are
/// declarative domain knowledge — Agent does not OWN the definitions.
///
/// Freeze: Capability ≠ ExecutionAction. Domain effect ≠ UI procedure.
/// </summary>
/// <param name="Name">Unique capability name (e.g. "SetEnabled").</param>
/// <param name="ApplicableToCategory">Domain category this capability applies to (e.g. "ConnectivitySetting").</param>
/// <param name="StateDimension">The state dimension this capability affects (e.g. "Enabled").</param>
public sealed record Capability(string Name, string ApplicableToCategory, string StateDimension)
{
    /// <summary>Creates a validated Capability.</summary>
    public static Capability Define(string name, string applicableToCategory, string stateDimension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicableToCategory);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDimension);
        return new Capability(name, applicableToCategory, stateDimension);
    }
}
