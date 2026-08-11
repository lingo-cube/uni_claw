using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Agent;

/// <summary>Stateless validation of an already selected semantic action.</summary>
internal static class ActionAuthorizer
{
    internal static SemanticActionResult? Validate(
        SemanticAction action,
        SemanticObject obj,
        Capability capability)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(capability);

        if (!action.IsValid)
            return new SemanticActionResult.Invalid("SemanticAction has null/empty required fields.");
        if (!string.Equals(action.ObjectIdentity, obj.Identity, StringComparison.Ordinal))
            return new SemanticActionResult.Invalid($"Action targets '{action.ObjectIdentity}' but object is '{obj.Identity}'.");
        if (!string.Equals(action.CapabilityName, capability.Name, StringComparison.Ordinal))
            return new SemanticActionResult.Invalid($"Action names capability '{action.CapabilityName}' but selected capability is '{capability.Name}'.");
        if (!string.Equals(capability.ApplicableToCategory, obj.Category, StringComparison.Ordinal))
            return new SemanticActionResult.Invalid($"Capability '{capability.Name}' applies to '{capability.ApplicableToCategory}', " + $"not '{obj.Category}'.");
        if (!string.Equals(action.StateDimension, capability.StateDimension, StringComparison.Ordinal))
            return new SemanticActionResult.Invalid($"Action targets dimension '{action.StateDimension}' " + $"but capability affects '{capability.StateDimension}'.");
        if (!obj.StateDimensions.Contains(action.StateDimension))
            return new SemanticActionResult.Invalid($"Object '{obj.Identity}' does not declare state dimension '{action.StateDimension}'.");
        return null;
    }
}
