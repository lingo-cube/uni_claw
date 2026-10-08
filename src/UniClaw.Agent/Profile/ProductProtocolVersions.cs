namespace UniClaw.Agent.Profile;

/// <summary>AGT-002 product protocol constants. These values are handshake
/// data, not host session state.</summary>
public static class ProductProtocolVersions
{
    /// <summary>Wire protocol identity exchanged and verified at handshake.</summary>
    public const string ProtocolVersion = "uniclaw.agent.protocol.v1";

    /// <summary>Generated schema artifact identity; its hash is pinned in the
    /// handshake stamp.</summary>
    public const string SchemaVersion = "uniclaw.agent.schema.v1";
}
