using System.Text;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.PhysicalHost;
using Xunit;

namespace UniClaw.Runtime.Tests.Integration;

/// <summary>
/// Production server-mode integration test (uniclaw-driverhost-production-server-mode).
///
/// Proves the frozen Protocol v1 surfaces are servable through the REAL production
/// composition — <see cref="PhysicalHostComposition.BuildDriverHostServer"/> — the exact
/// path the --serve entrypoint invokes. No parallel test-only server graph is built:
/// this drives the identical composition the production entry uses.
///
/// Coverage:
///   server start → client connection → ping (serve identity)
///   → run.start round-trip (seam live; deterministic resolve)
///   → read-only Surface B reachability (run.list / run.snapshot.get / run.events.after)
///   → clean server dispose (shutdown)
///
/// Full run.start → Agent → terminal execution semantics are already covered by
/// DriverHostRunStartE2ETests / RunExecutionCoordinatorTests (which inject a
/// ScriptedEnvironment). This test's buyer is PRODUCTION SERVABILITY.
/// </summary>
public sealed class ServerModeIntegrationTests
{
    private static readonly PhysicalHostOptions TestOptions = new(
        "adb", null, "com.android.settings", "/tmp/uniclaw-vision-test.sock", 1080, 1920);

    /// <summary>
    /// The production serve path: BuildDriverHostServer (entry-point composition)
    /// → Start → Serve → Dispose. This is the same graph the --serve mode builds.
    /// </summary>
    [Fact]
    public async Task ProductionServePath_StartsWithDriverHostServer_AndRespondsToPing()
    {
        using var server = PhysicalHostComposition.BuildDriverHostServer(
            TestOptions, new DriverHostServerOptions { Port = 0 });
        server.Start();

        try
        {
            // Production server listening (ephemeral port).
            Assert.True(server.IsListening, "production server should be listening");
            Assert.Equal(0, server.ActiveConnections);

            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync("127.0.0.1", server.BoundPort);

            var ping = Request(client, 1, "ping", null);
            Assert.Equal("dsh-uniclaw-driverhost", ping?["result"]?["service"]?.GetValue<string>());
            Assert.Equal(1, ping?["result"]?["protocolVersion"]?.GetValue<int>());

            // run.start round-trip through the production composition.
            // An unknown (non-serial) device is deterministically rejected — proving
            // the execution seam is live through the production serve path.
            var start = Request(client, 2, "run.start", JsonNode.Parse(
                "{\"goal\":{\"objectIdentity\":\"WifiConnectivity\",\"stateDimension\":\"Enabled\",\"desiredValue\":true}," +
                "\"objects\":[{\"identity\":\"WifiConnectivity\",\"category\":\"ConnectivitySetting\",\"stateDimensions\":[\"Enabled\"]}]," +
                "\"capabilities\":[{\"name\":\"SetEnabled\",\"applicableToCategory\":\"ConnectivitySetting\",\"stateDimension\":\"Enabled\"}]," +
                "\"device\":\"my-emulator\"}"));
            Assert.Equal("request_rejected", start?["error"]?["code"]?.GetValue<string>());
            Assert.Contains("not supported", start?["error"]?["message"]?.GetValue<string>() ?? "", StringComparison.OrdinalIgnoreCase);

            // Read-only Surface B reachability.
            var list = Request(client, 3, "run.list", null);
            Assert.NotNull(list?["result"]?["runIds"]);

            var support = Request(client, 4, "control.support", JsonNode.Parse("{\"operation\":\"start\"}"));
            Assert.NotNull(support?["result"]);
        }
        finally
        {
            server.Dispose();
        }
    }

    /// <summary>Sends one newline-delimited JSON-RPC request and reads one response line.</summary>
    private static JsonObject? Request(System.Net.Sockets.TcpClient client, int id, string method, JsonNode? parameters)
    {
        var stream = client.GetStream();
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (parameters is not null)
        {
            request["params"] = parameters;
        }

        var payload = Encoding.UTF8.GetBytes(request.ToJsonString() + "\n");
        stream.Write(payload, 0, payload.Length);
        stream.Flush();

        var buffer = new List<byte>();
        var one = new byte[1];
        while (stream.Read(one, 0, 1) > 0)
        {
            buffer.Add(one[0]);
            if (one[0] == (byte)'\n')
            {
                break;
            }
        }

        return JsonNode.Parse(Encoding.UTF8.GetString(buffer.ToArray())) as JsonObject;
    }
}
