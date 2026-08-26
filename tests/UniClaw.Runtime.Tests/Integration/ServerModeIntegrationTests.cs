using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.PhysicalHost;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Tests.Integration;

/// <summary>
/// Production server-mode integration test (uniclaw-driverhost-production-server-mode).
///
/// Proves the frozen Protocol v1 surfaces are servable through the REAL production
/// composition — <see cref="PhysicalHostComposition.BuildDriverHostServer"/> — the exact
/// path the --serve entrypoint invokes. No parallel test-only server graph is built:
/// this drives the identical composition method the production entry uses.
///
/// Coverage:
///   server start → client connection → ping (serve identity)
///   → run.start (UNKNOWN device → deterministic request_rejected) [seam-live proof]
///   → run.start (serial:test-1 → RunAccepted → terminal event → terminal snapshot)
///     [approved successful production-composition path]
///   → read-only Surface B reachability (run.list / run.snapshot.get / run.events.after)
///   → clean server dispose (shutdown)
///
/// The deterministic ScriptedEnvironment is injected as the physical-world dependency
/// through the composition seam (RunGraphFactory? parameter). RuntimeAgent receives
/// only IEnvironment — it is unaware whether the environment is real or scripted.
/// </summary>
public sealed class ServerModeIntegrationTests
{
    private static readonly PhysicalHostOptions TestOptions = new(
        "adb", null, "settings", "/tmp/uniclaw-vision-test.sock", 1080, 1920);

    /// <summary>
    /// The production serve path: BuildDriverHostServer (entry-point composition)
    /// → Start → ping → run.start (UNKNOWN device → request_rejected) → surfaces → Dispose.
    /// Proves servability and seam liveness through the production composition.
    /// </summary>
    [Fact]
    public async Task ProductionServePath_StartsWithDriverHostServer_AndRespondsToPing()
    {
        using var server = PhysicalHostComposition.BuildDriverHostServer(
            TestOptions, new DriverHostServerOptions { Port = 0 });
        server.Start();

        try
        {
            Assert.True(server.IsListening, "production server should be listening");
            Assert.Equal(0, server.ActiveConnections);

            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync("127.0.0.1", server.BoundPort);

            var ping = Request(client, 1, "ping", null);
            Assert.Equal("dsh-uniclaw-driverhost", ping?["result"]?["service"]?.GetValue<string>());
            Assert.Equal(1, ping?["result"]?["protocolVersion"]?.GetValue<int>());

            // run.start round-trip: UNKNOWN device → deterministic request_rejected.
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

    /// <summary>
    /// The approved production-composition successful-run path:
    /// BuildDriverHostServer with injected deterministic RunGraphFactory
    /// → Start → ping → run.start (serial:test-1) → RunAccepted(runId)
    /// → run.events.after until truthful terminal event
    /// → run.snapshot.get → truthful terminal RunState
    /// → clean shutdown.
    ///
    /// Uses the SAME production BuildDriverHostServer composition method; the
    /// ScriptedEnvironment is injected only as the physical-world dependency.
    /// RuntimeAgent receives only IEnvironment — no awareness of scripted vs real.
    /// </summary>
    [Fact]
    public async Task ProductionCompositionPath_AcceptsRunStart_AndReachesTerminalOutcome()
    {
        // Production composition with injected deterministic factory — SAME method,
        // NOT a parallel test-only server graph.
        using var server = PhysicalHostComposition.BuildDriverHostServer(
            TestOptions,
            new DriverHostServerOptions { Port = 0 },
            runGraphFactory: ScriptedFactory());
        server.Start();

        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync("127.0.0.1", server.BoundPort);

            // ping → serve identity confirmed.
            var ping = Request(client, 1, "ping", null);
            Assert.Equal("dsh-uniclaw-driverhost", ping?["result"]?["service"]?.GetValue<string>());

            // run.start → RunAccepted(runId) immediately (async start; never blocks).
            var start = Request(client, 2, "run.start", JsonNode.Parse(
                "{\"goal\":{\"objectIdentity\":\"WifiConnectivity\",\"stateDimension\":\"Enabled\",\"desiredValue\":true}," +
                "\"objects\":[{\"identity\":\"WifiConnectivity\",\"category\":\"ConnectivitySetting\",\"stateDimensions\":[\"Enabled\"]}]," +
                "\"capabilities\":[{\"name\":\"SetEnabled\",\"applicableToCategory\":\"ConnectivitySetting\",\"stateDimension\":\"Enabled\"}]," +
                "\"device\":\"serial:test-1\"}"));

            var result = start?["result"];
            Assert.NotNull(result);
            Assert.True(result?["accepted"]?.GetValue<bool>(), $"run.start not accepted: {start?.ToJsonString()}");
            var runId = result?["runId"]?.GetValue<string>();
            Assert.False(string.IsNullOrEmpty(runId), $"missing runId: {start?.ToJsonString()}");

            // Poll run.events.after until a terminal event (RunCompleted or RunFailed).
            string? terminalKind = null;
            for (var attempt = 0; attempt < 40 && terminalKind is null; attempt++)
            {
                await Task.Delay(100);
                var events = Request(client, 3 + attempt, "run.events.after", JsonNode.Parse(
                    $"{{\"runId\":\"{runId}\",\"cursor\":{{\"runId\":\"{runId}\",\"lastSequence\":0}}}}"));
                var eventArray = events?["result"]?["events"] as JsonArray;
                if (eventArray is null || eventArray.Count == 0)
                {
                    continue;
                }

                foreach (var evt in eventArray)
                {
                    var kind = evt?["kind"]?.GetValue<string>();
                    if (kind is "RunCompleted" or "RunFailed")
                    {
                        terminalKind = kind;
                        break;
                    }
                }
            }

            Assert.True(terminalKind is not null,
                $"no terminal event (RunCompleted/RunFailed) found in {40} polls");

            // Brief delay to let the snapshot projection catch up after the terminal
            // event (the projector updates on a separate timing from the event store).
            await Task.Delay(200);

            // run.snapshot.get → truthful terminal RunState.
            var snapshot = Request(client, 99, "run.snapshot.get", JsonNode.Parse(
                $"{{\"runId\":\"{runId}\"}}"));
            // Diagnostic: dump the actual snapshot JSON to understand its structure.
            var snapshotJson = snapshot?.ToJsonString() ?? "(null)";
            var runStateField = snapshot?["result"]?["runState"];
            Assert.True(runStateField is not null,
                $"snapshot.runState is null. Full snapshot: {snapshotJson}");
            var runStateValue = runStateField?["value"]?.GetValue<string>();
            Assert.True(runStateValue is "completed" or "failed",
                $"snapshot runState={runStateValue} (expected completed or failed). Full snapshot: {snapshotJson}");
        }
        finally
        {
            server.Dispose();
        }
    }

    /// <summary>
    /// Deterministic ScriptedEnvironment factory: WiFi off → SetSwitch(ON) → on.
    /// Reuses the SAME deterministic scenario as DriverHostRunStartE2ETests.
    /// Injected as the physical-world dependency through the composition seam.
    /// </summary>
    private static RunGraphFactory ScriptedFactory()
    {
        var env = new ScriptedEnvironment(
            "settings", "Settings",
            [
                new ScreenConfig(
                    "Settings", "settings",
                    [new ElementConfig("Wi‑Fi", null, null, new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), "menuItem"),
                     new ElementConfig("", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "On", true), new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle")]),
                new ScreenConfig(
                    "On", "settings",
                    [new ElementConfig("Wi‑Fi", null, null, new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), "menuItem"),
                     new ElementConfig("", true, null, new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle")]),
            ]);

        return selector =>
        {
            if (selector.Key != "serial:test-1")
            {
                throw new DeviceSelectorUnsupportedException(selector.Key, "integration test supports only serial:test-1");
            }

            var wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
            var criteria = new ElementBindingCriteria(
                [wifi],
                ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
                ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
            var pages = new PageAnalysisCriteria(
                "settings",
                ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
            var semanticEnv = env.WithToggleLocalControl();
            var graph = PhysicalHostComposition.BuildRuntimeGraph(semanticEnv, TestOptions, attach: null, criteria, pages,
                resolveSemanticPage: _ => "Settings");
            return new RunExecutionGraph(graph.Agent, env);
        };
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
