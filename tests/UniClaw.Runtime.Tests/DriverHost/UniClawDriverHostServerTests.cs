using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Tests.Observability;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// Wire-level server tests over a raw TcpClient (PLUG-F2/F5/F10/F14/F16 gate
/// coverage): typed protocol errors, per-connection drain cursors, fresh-state
/// reconnect, and dispose/stop semantics. No DriverHost internals — the server
/// is exercised exactly as the DSH plugin will exercise it.
/// </summary>
public sealed class UniClawDriverHostServerTests : IDisposable
{
    private readonly UniClawDriverHostServer _server;
    private readonly DriverHostObservability _observability = new();

    public UniClawDriverHostServerTests()
    {
        _observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun());
        _server = new UniClawDriverHostServer(new UniClawControlSurface(_observability), new DriverHostServerOptions { Port = 0 });
        _server.Start();
    }

    public void Dispose() => _server.Dispose();

    private static string Rpc(int id, string method, string? paramsJson = null)
        => $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\"" +
           (paramsJson is null ? "}" : $",\"params\":{paramsJson}}}");

    private static string ParamsRunId(string runId) => $"{{\"runId\":\"{runId}\"}}";

    private async Task<(JsonNode? Id, JsonNode? Result, JsonNode? Error)> RequestAsync(string line)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", _server.BoundPort);
        var response = await SendRawAsync(client, line);
        var json = JsonNode.Parse(response) as JsonObject
                   ?? throw new InvalidOperationException("response is not a JSON object");
        return (json["id"]?.DeepClone(), json["result"]?.DeepClone(), json["error"]?.DeepClone());
    }

    private static async Task<string> SendRawAsync(TcpClient client, string line)
    {
        // The TcpClient owns the stream; disposing it here would close the
        // socket and break multi-request conversations on one connection.
        var stream = client.GetStream();
        var payload = Encoding.UTF8.GetBytes(line + "\n");
        await stream.WriteAsync(payload);
        await stream.FlushAsync();

        var buffer = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(one);
            if (read == 0) break;
            buffer.Add(one[0]);
            if (one[0] == (byte)'\n') break;
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    [Fact]
    public async Task Ping_ReturnsServiceIdentity()
    {
        var (id, result, error) = await RequestAsync(Rpc(1, "ping"));
        Assert.Null(error);
        Assert.Equal(1, id?.GetValue<int>());
        Assert.Equal("dsh-uniclaw-driverhost", result?["service"]?.GetValue<string>());
        Assert.Equal(1, result?["protocolVersion"]?.GetValue<int>());
        Assert.Equal("dsh-uniclaw-control-plane-protocol-baseline", result?["baselineChange"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunSnapshotGet_ReturnsClassifiedSnapshot()
    {
        var (_, result, error) = await RequestAsync(Rpc(2, "run.snapshot.get", ParamsRunId(ReadOnlyObservabilityFixtures.RunId)));
        Assert.Null(error);
        Assert.Equal(ReadOnlyObservabilityFixtures.RunId, result?["runId"]?.GetValue<string>());
        Assert.Equal("completed", result?["runState"]?["value"]?.GetValue<string>());
        Assert.Equal("directPublicProjection", result?["runState"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunSnapshotGet_UnknownRun_ReturnsUnknownShape()
    {
        var (_, result, error) = await RequestAsync(Rpc(3, "run.snapshot.get", ParamsRunId("no-such-run")));
        Assert.Null(error);
        Assert.Equal("no-such-run", result?["runId"]?.GetValue<string>());
        Assert.Equal("notCurrentlyAvailable", result?["runState"]?["classification"]?.GetValue<string>());
        Assert.Null(result?["runState"]?["value"]);
    }

    [Fact]
    public async Task RunTrapGet_NoTrap_ReturnsFoundFalse()
    {
        var (_, result, error) = await RequestAsync(Rpc(4, "run.trap.get", ParamsRunId(ReadOnlyObservabilityFixtures.RunId)));
        Assert.Null(error);
        Assert.False(result?["found"]?.GetValue<bool>());
        Assert.Null(result?["diagnostic"]);
    }

    [Fact]
    public async Task RunEventsAfter_ReturnsPageWithStableEventIds()
    {
        var (_, result, error) = await RequestAsync(Rpc(5, "run.events.after", ParamsRunId(ReadOnlyObservabilityFixtures.RunId)));
        Assert.Null(error);
        var events = result?["events"]?.AsArray();
        Assert.NotNull(events);
        Assert.NotEmpty(events);
        Assert.Equal(1, events![0]?["sequence"]?.GetValue<int>());
        Assert.StartsWith($"evt-{ReadOnlyObservabilityFixtures.RunId}-", events[0]?["eventId"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.NotNull(result?["nextCursor"]);
        Assert.False(result?["hasMore"]?.GetValue<bool>());
    }

    [Fact]
    public async Task RunEventsAfter_WithCursor_ReturnsOnlyNewerEvents()
    {
        var first = await RequestAsync(Rpc(6, "run.events.after", ParamsRunId(ReadOnlyObservabilityFixtures.RunId)));
        var lastSequence = first.Result!["nextCursor"]!["lastSequence"]!.GetValue<long>();

        var cursorJson = $"{{\"runId\":\"{ReadOnlyObservabilityFixtures.RunId}\",\"lastSequence\":{lastSequence}}}";
        var second = await RequestAsync(Rpc(7, "run.events.after", $"{{\"runId\":\"{ReadOnlyObservabilityFixtures.RunId}\",\"cursor\":{cursorJson}}}"));
        Assert.Null(second.Error);
        Assert.Empty(second.Result!["events"]!.AsArray());
        Assert.False(second.Result["hasMore"]?.GetValue<bool>());
    }

    [Fact]
    public async Task EvidenceGet_NoCatalog_ReturnsFoundFalse()
    {
        var evidenceJson = $"{{\"locator\":\"capture:session-1:record:1\",\"runId\":\"{ReadOnlyObservabilityFixtures.RunId}\"}}";
        var (_, result, error) = await RequestAsync(Rpc(8, "evidence.get", $"{{\"evidenceRef\":{evidenceJson}}}"));
        Assert.Null(error);
        Assert.False(result?["found"]?.GetValue<bool>());
        Assert.NotNull(result?["diagnostic"]);
    }

    [Fact]
    public async Task ControlSupport_Pause_IsDeferred()
    {
        var (_, result, error) = await RequestAsync(Rpc(9, "control.support", """{"operation":"pause"}"""));
        Assert.Null(error);
        Assert.Equal("pause", result?["operation"]?.GetValue<string>());
        Assert.False(result?["supported"]?.GetValue<bool>());
        Assert.Equal("DEFERRED_NO_KERNEL_CONTROL_BUYER", result?["reason"]?.GetValue<string>());
        Assert.NotEmpty(result?["evidence"]?.AsArray() ?? []);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsTypedError()
    {
        var (id, result, error) = await RequestAsync(Rpc(10, "explode"));
        Assert.Null(result);
        Assert.Equal(10, id?.GetValue<int>());
        Assert.Equal("unknown_method", error?["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequest()
    {
        var (_, result, error) = await RequestAsync("""{"jsonrpc":"2.0","id":11,"method":""");
        Assert.Null(result);
        Assert.Equal("bad_request", error?["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task MissingRunId_ReturnsBadRequest()
    {
        var (_, result, error) = await RequestAsync("""{"jsonrpc":"2.0","id":12,"method":"run.snapshot.get","params":{}}""");
        Assert.Null(result);
        Assert.Equal("bad_request", error?["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task MissingMethod_ReturnsBadRequest()
    {
        var (_, result, error) = await RequestAsync("""{"jsonrpc":"2.0","id":13}""");
        Assert.Null(result);
        Assert.Equal("bad_request", error?["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task OversizedMessage_ReturnsBadRequest()
    {
        var big = new string('x', 1024 * 1024 + 16);
        var (_, result, error) = await RequestAsync($"{{\"jsonrpc\":\"2.0\",\"id\":14,\"method\":\"ping\",\"params\":{{\"pad\":\"{big}\"}}}}");
        Assert.Null(result);
        Assert.Equal("bad_request", error?["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task DrainCursor_IsPerConnection()
    {
        // First drain on connection A consumes everything.
        using var clientA = new TcpClient();
        await clientA.ConnectAsync("127.0.0.1", _server.BoundPort);
        var responseA1 = await SendRawAsync(clientA, Rpc(1, "run.events.drain", ParamsRunId(ReadOnlyObservabilityFixtures.RunId)));
        var pageA1 = JsonNode.Parse(responseA1)!["result"]!;
        Assert.NotEmpty(pageA1["events"]!.AsArray());

        // Second drain on the SAME connection returns only new events (none).
        var responseA2 = await SendRawAsync(clientA, Rpc(2, "run.events.drain", ParamsRunId(ReadOnlyObservabilityFixtures.RunId)));
        var pageA2 = JsonNode.Parse(responseA2)!["result"]!;
        Assert.Empty(pageA2["events"]!.AsArray());

        // A NEW connection starts fresh: cursor state is per connection.
        using var clientB = new TcpClient();
        await clientB.ConnectAsync("127.0.0.1", _server.BoundPort);
        var responseB1 = await SendRawAsync(clientB, Rpc(1, "run.events.drain", ParamsRunId(ReadOnlyObservabilityFixtures.RunId)));
        var pageB1 = JsonNode.Parse(responseB1)!["result"]!;
        Assert.NotEmpty(pageB1["events"]!.AsArray());
    }

    [Fact]
    public async Task Dispose_StopsListening()
    {
        _server.Dispose();
        using var client = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(async () => await client.ConnectAsync("127.0.0.1", _server.BoundPort, CancellationToken.None));
    }

    [Fact]
    public async Task Start_IsIdempotent()
    {
        var bound = _server.BoundPort;
        _server.Start();
        Assert.Equal(bound, _server.BoundPort);
        var (_, result, error) = await RequestAsync(Rpc(1, "ping"));
        Assert.Null(error);
        Assert.Equal("dsh-uniclaw-driverhost", result?["service"]?.GetValue<string>());
    }
}
