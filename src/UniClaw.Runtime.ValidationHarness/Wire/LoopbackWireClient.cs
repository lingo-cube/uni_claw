using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace UniClaw.Runtime.ValidationHarness.Wire;

/// <summary>
/// Loopback JSON-RPC wire client for the Tier-A harness (mirror of the existing
/// E2E loopback client pattern; duplicated inside the harness project per design
/// D-risk "Duplicated wire-client code"). It dials the REAL DriverHost TCP
/// transport over 127.0.0.1 and returns the parsed response — the harness never
/// bypasses the transport, so encoding is exercised for real.
/// </summary>
public static class LoopbackWireClient
{
    /// <summary>Send one newline-delimited JSON-RPC line and read one response line.</summary>
    /// <param name="port">Bound loopback port of the in-process host.</param>
    /// <param name="requestLine">Single-line JSON-RPC request (method + params).</param>
    /// <param name="cancellationToken">Optional cancellation.</param>
    /// <returns>The parsed JSON-RPC response object (result or error).</returns>
    public static async Task<JsonObject> RequestAsync(
        int port,
        string requestLine,
        CancellationToken cancellationToken = default)
    {
        if (port <= 0)
            throw new ArgumentOutOfRangeException(nameof(port));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestLine);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port, timeout.Token).ConfigureAwait(false);
        await using var stream = client.GetStream();
        var payload = Encoding.UTF8.GetBytes(requestLine + "\n");
        await stream.WriteAsync(payload, timeout.Token).ConfigureAwait(false);
        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

        var bytes = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(one, timeout.Token).ConfigureAwait(false);
            if (read == 0 || one[0] == (byte)'\n')
                break;
            bytes.Add(one[0]);
        }

        if (bytes.Count == 0)
            throw new InvalidOperationException("Loopback wire client received an empty response line.");

        var parsed = JsonNode.Parse(Encoding.UTF8.GetString(bytes.ToArray()));
        return parsed as JsonObject
            ?? throw new InvalidOperationException("Loopback wire client received a non-object response.");
    }
}