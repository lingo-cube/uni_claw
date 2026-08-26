using System.Text.Json.Nodes;
using UniClaw.Runtime.ValidationHarness.Wire;

namespace UniClaw.Runtime.ValidationHarness.Emulator;

/// <summary>
/// Transport seam for the Emulator driver (design D2/D3). The driver speaks
/// ONLY this interface; nothing else touches the wire.
/// <see cref="SentRequestCount"/> is the exact wire-call counter the call log
/// and the boundary proof (D5.1) rely on to attest zero / exact start counts.
/// </summary>
public interface IEmulatorTransport
{
    /// <summary>Exact number of wire requests attempted (zero-proof evidence).</summary>
    long SentRequestCount { get; }

    /// <summary>Send one JSON-RPC request and return the parsed response object
    /// (carrying <c>result</c> or <c>error</c>).</summary>
    Task<JsonObject> SendAsync(string method, JsonObject? parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// Real loopback transport over the existing JSON-RPC wire (design D3: the
/// Emulator dials the same transport the DriverHost server hosts, so encoding
/// is exercised for real; the harness never bypasses the transport).
/// </summary>
public sealed class LoopbackEmulatorTransport : IEmulatorTransport
{
    private readonly int _port;
    private long _sent;
    private long _nextRequestId;

    /// <summary>Create a transport dialing the bound loopback port.</summary>
    public LoopbackEmulatorTransport(int port)
    {
        if (port <= 0)
            throw new ArgumentOutOfRangeException(nameof(port));
        _port = port;
    }

    /// <inheritdoc />
    public long SentRequestCount => Interlocked.Read(ref _sent);

    /// <inheritdoc />
    public async Task<JsonObject> SendAsync(string method, JsonObject? parameters, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        Interlocked.Increment(ref _sent);
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Interlocked.Increment(ref _nextRequestId),
            ["method"] = method,
        };
        if (parameters is not null)
            request["params"] = parameters;
        return await LoopbackWireClient.RequestAsync(_port, request.ToJsonString(), cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Harness-local wire copy of the strategy admission receipt — mirrors the
/// frozen <c>UniClawStrategyRunAdmissionDto</c> (<c>run.strategy.start</c>
/// returns an admission business result, not an RPC error, for Accept and
/// deterministic Reject alike). No new wire contract.
/// </summary>
public sealed record StrategyRunAdmissionView(
    bool Accepted,
    string? RunId,
    string? RunState,
    string? RejectionCode,
    string? RejectionReason)
{
    /// <summary>Parse the wire <c>result</c> object of one admission.</summary>
    public static StrategyRunAdmissionView FromWire(JsonObject? result)
    {
        if (result is null)
            throw new ArgumentException("run.strategy.start returned no result object on the wire.");
        var accepted = result["accepted"]?.GetValue<bool>()
            ?? throw new ArgumentException("the admission result lacks the 'accepted' flag.");
        return new StrategyRunAdmissionView(
            accepted,
            result["runId"]?.GetValue<string>(),
            result["runState"]?.GetValue<string>(),
            result["rejectionCode"]?.GetValue<string>(),
            result["rejectionReason"]?.GetValue<string>());
    }
}