using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Environment;
using UniClaw.Semantic.Settings;

namespace UniClaw.Runtime.ValidationHarness.SettingsBinding;

/// <summary>
/// Harness-local composition for the production Settings semantic capability.
///
/// <see cref="Wrap"/> decorates a raw environment so every observation carries
/// the admitted semantic evidence the production
/// <see cref="SettingsSemanticCapability"/> emitted for it — the input
/// <see cref="SettingsStrategyBinding"/> adapts (spec "SettingsStrategyBinding
/// adapts without inventing" + design D6).
///
/// Unlike the test-only SettingsSemanticCapabilityTestEnvironment, this
/// composition does NOT stamp source metadata: physical environments already
/// carry real source metadata, so the raw environment is wrapped directly.
/// </summary>
public static class SettingsBindingComposition
{
    /// <summary>
    /// Wraps a raw environment with the production Settings semantic capability
    /// (manifest "uni-claw.settings.semantic" v1). The inner environment is
    /// never modified and never receives capability internals.
    /// </summary>
    public static IEnvironment Wrap(IEnvironment raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return new SemanticCapabilityEnvironment(
            raw,
            new SemanticCapabilityRuntime(new SettingsSemanticCapability()));
    }
}