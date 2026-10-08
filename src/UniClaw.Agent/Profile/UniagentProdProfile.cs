namespace UniClaw.Agent.Profile;

/// <summary>Product realization profile. A profile is the allowlisted capability
/// surface; it deliberately does not contain provider or model selection
/// (PRF-001/ADR-0041: provider bindings live in the host realization binding,
/// not in the product profile).</summary>
public static class UniagentProdProfile
{
    /// <summary>Stable profile identity; a config file claiming another id is
    /// drift and fails closed.</summary>
    public const string ProfileId = "uniagent-prod";

    /// <summary>Protocol-compatibility version of the profile identity;
    /// bumped only with an explicit protocol change.</summary>
    public const string ProfileVersion = "1";

    /// <summary>The compiled identity served as the handshake authority.</summary>
    public static ProductProfile Current => new(
        ProfileId,
        ProfileVersion,
        CapabilityManifest.ProductHeadless,
        ProductProtocolVersions.ProtocolVersion,
        ProductProtocolVersions.SchemaVersion);
}
