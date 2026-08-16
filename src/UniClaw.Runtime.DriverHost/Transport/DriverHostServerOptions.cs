namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Options for the DriverHost TCP transport server (loopback only, local and
/// deterministic — protocol baseline §9). Defaults keep the server bound to the
/// loopback interface with a bounded message size; tests use Port = 0 for an
/// ephemeral port.
/// </summary>
public sealed record DriverHostServerOptions
{
    /// <summary>Loopback interface only — this transport never listens on a remote interface.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>0 = bind an ephemeral port (read back via BoundPort).</summary>
    public int Port { get; init; } = 5177;

    /// <summary>Hard cap on one request/response line; oversized lines are rejected with bad_request.</summary>
    public int MaxMessageBytes { get; init; } = 1 << 20;

    /// <summary>Optional diagnostic sink (never part of the protocol).</summary>
    public Action<string>? Log { get; init; }
}
