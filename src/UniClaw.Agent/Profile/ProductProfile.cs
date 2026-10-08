namespace UniClaw.Agent.Profile;

/// <summary>Product realization profile identity: who the realization is, which
/// frozen capability manifest it exposes, and which protocol versions it
/// speaks. Deliberately contains no provider or model selection (ADR-0041:
/// host bindings live in the realization binding, not in the product
/// profile).</summary>
public sealed record ProductProfile(
    string ProfileId,
    string ProfileVersion,
    CapabilityManifest Capabilities,
    string ProtocolVersion,
    string SchemaVersion);
