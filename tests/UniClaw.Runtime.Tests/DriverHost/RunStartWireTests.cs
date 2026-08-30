using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.PhysicalHost;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// Wire-level run.start gate (dsh-runtime-agent-subagent-run-entry) over a raw
/// TcpClient — the server is exercised exactly as the DSH plugin exercises it.
/// Proves: additive dispatch, RunAccepted(runId) async shape, typed
/// request_rejected (no phantom run), bad_request for malformed payloads, and
/// that the frozen read-only methods keep working on a server that also carries
/// the execution seam (R10 / T8).
/// </summary>
[Collection("ObservabilityTraceEmitters")]
public sealed class RunStartWireTests : IDisposable
{
    private readonly UniClawDriverHostServer _server;
    private readonly DriverHostObservability _observability = new();
    private readonly RunExecutionCoordinator _coordinator;

    private static readonly PhysicalHostOptions TestOptions = new(
        "adb", null, "settings", "/tmp/uniclaw-vision-test.sock", 1080, 1920);

    public RunStartWireTests()
    {
        _observability = new DriverHostObservability();
        _coordinator = new RunExecutionCoordinator(_observability, ScriptedFactory(("serial:test-1", CompletingEnvironment())));
        _server = new UniClawDriverHostServer(
            new UniClawControlSurface(_observability),
            new DriverHostServerOptions { Port = 0 },
            _coordinator);
        _server.Start();
    }

    public void Dispose() => _server.Dispose();

    // NOTE: the transport is newline-delimited JSON-RPC — the params MUST be a
    // single physical line (embedded newlines would break the line framing).
    private const string ValidRunStartParams =
        "{\"goal\":{\"objectIdentity\":\"WifiConnectivity\",\"stateDimension\":\"Enabled\",\"desiredValue\":true}," +
        "\"objects\":[{\"identity\":\"WifiConnectivity\",\"category\":\"ConnectivitySetting\",\"stateDimensions\":[\"Enabled\"]}]," +
        "\"capabilities\":[{\"name\":\"SetEnabled\",\"applicableToCategory\":\"ConnectivitySetting\",\"stateDimension\":\"Enabled\"}]," +
        "\"device\":\"serial:test-1\"}";

    private static string Rpc(int id, string method, string? paramsJson = null)
        => $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\"" +
           (paramsJson is null ? "}" : $",\"params\":{paramsJson}}}");

    private async Task<(JsonNode? Id, JsonNode? Result, JsonNode? Error)> RequestAsync(string line)
        => await RawRequestAsync(_server.BoundPort, line);

    private static async Task<(JsonNode? Id, JsonNode? Result, JsonNode? Error)> RawRequestAsync(int port, string line)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
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

        var json = JsonNode.Parse(Encoding.UTF8.GetString(buffer.ToArray())) as JsonObject
                   ?? throw new InvalidOperationException("response is not a JSON object");
        return (json["id"]?.DeepClone(), json["result"]?.DeepClone(), json["error"]?.DeepClone());
    }

    [Fact]
    public async Task RunStart_AcceptsAndReturnsRunId_AsyncShape_NoBlocking()
    {
        var (id, result, error) = await RequestAsync(Rpc(1, "run.start", ValidRunStartParams));

        Assert.Null(error);
        Assert.Equal(1, id?.GetValue<int>());
        Assert.Equal(true, result?["accepted"]?.GetValue<bool>());
        var runId = result?["runId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(runId));
        Assert.Equal("Idle", result?["runState"]?.GetValue<string>());

        // DriverHost-owned runId immediately legitimate on the existing surfaces.
        var runs = await RequestAsync(Rpc(2, "run.list"));
        Assert.Contains(runs.Result?["runIds"]?.AsArray() ?? [], n => n?.GetValue<string>() == runId);

        var snapshot = await RequestAsync(Rpc(3, "run.snapshot.get", $"{{\"runId\":\"{runId}\"}}"));
        // Truthful accepted/live state at read time (never the "unknown run" read);
        // the exact value may already be terminal for the deterministic scripted run.
        Assert.Equal("directPublicProjection", snapshot.Result?["runState"]?["classification"]?.GetValue<string>());
        Assert.NotNull(snapshot.Result?["runState"]?["value"]);

        var events = await RequestAsync(Rpc(4, "run.events.after", $"{{\"runId\":\"{runId}\"}}"));
        Assert.NotNull(events.Result?["events"]);
    }

    [Fact]
    public async Task RunStart_UnknownDevice_RequestRejected_NoPhantomRun()
    {
        var paramsJson = ValidRunStartParams.Replace("serial:test-1", "serial:not-in-map", StringComparison.Ordinal);
        var (id, result, error) = await RequestAsync(Rpc(1, "run.start", paramsJson));

        Assert.Null(result);
        Assert.Equal(1, id?.GetValue<int>());
        Assert.Equal("request_rejected", error?["code"]?.GetValue<string>());
        Assert.Contains("not supported", error?["message"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var runs = await RequestAsync(Rpc(2, "run.list"));
        Assert.Empty(runs.Result?["runIds"]?.AsArray() ?? []);
    }

    [Fact]
    public async Task RunStart_InvalidGoal_RequestRejected_NoPhantomRun()
    {
        // Replace ONLY the goal's objectIdentity — the objects catalog must keep
        // its declared identity so the request is genuinely "unknown object".
        var paramsJson = ValidRunStartParams.Replace(
            "\"objectIdentity\":\"WifiConnectivity\"", "\"objectIdentity\":\"UnknownObject\"", StringComparison.Ordinal);
        var (_, _, error) = await RequestAsync(Rpc(1, "run.start", paramsJson));

        Assert.Equal("request_rejected", error?["code"]?.GetValue<string>());
        Assert.Contains("unknown object", error?["message"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var runs = await RequestAsync(Rpc(2, "run.list"));
        Assert.Empty(runs.Result?["runIds"]?.AsArray() ?? []);
    }

    [Fact]
    public async Task RunStart_BusyDevice_RequestRejected_NoSecondRun()
    {
        // Gate the first run's first observe so the run stays ACTIVE while the
        // second request arrives — deterministic ONE_ACTIVE_RUN_PER_DEVICE proof
        // over the wire (no timing dependence on scripted-run completion speed).
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new RunExecutionCoordinator(
            _observability,
            GatedFactory(("serial:test-1", CompletingEnvironment(), gate)));
        using var gatedServer = new UniClawDriverHostServer(
            new UniClawControlSurface(_observability),
            new DriverHostServerOptions { Port = 0 },
            coordinator);
        gatedServer.Start();
        try
        {
            var (_, _, error1) = await RawRequestAsync(gatedServer.BoundPort, Rpc(1, "run.start", ValidRunStartParams));
            Assert.Null(error1);

            var second = await RawRequestAsync(gatedServer.BoundPort, Rpc(2, "run.start", ValidRunStartParams));
            Assert.Equal("request_rejected", second.Error?["code"]?.GetValue<string>());
            Assert.Contains("busy", second.Error?["message"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

            var runs = await RawRequestAsync(gatedServer.BoundPort, Rpc(3, "run.list"));
            Assert.Single(runs.Result?["runIds"]?.AsArray() ?? []);
        }
        finally
        {
            gate.TrySetResult();
            gatedServer.Dispose();
        }
    }

    [Fact]
    public async Task RunStart_MalformedPayload_BadRequest()
    {
        var (_, _, error) = await RequestAsync(Rpc(1, "run.start", "{\"goal\":{}}"));

        Assert.Equal("bad_request", error?["code"]?.GetValue<string>());
        var runs = await RequestAsync(Rpc(2, "run.list"));
        Assert.Empty(runs.Result?["runIds"]?.AsArray() ?? []);
    }

    [Fact]
    public async Task RunStart_NoExecutionSeam_RequestRejected()
    {
        using var bareServer = new UniClawDriverHostServer(
            new UniClawControlSurface(new DriverHostObservability()),
            new DriverHostServerOptions { Port = 0 });
        bareServer.Start();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", bareServer.BoundPort);
            var stream = client.GetStream();
            var payload = Encoding.UTF8.GetBytes(Rpc(1, "run.start", ValidRunStartParams) + "\n");
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

            var json = JsonNode.Parse(Encoding.UTF8.GetString(buffer.ToArray())) as JsonObject;
            Assert.Equal("request_rejected", json?["error"]?["code"]?.GetValue<string>());
        }
        finally
        {
            bareServer.Dispose();
        }
    }

    [Fact]
    public async Task FrozenReadOnlyMethods_StillWork_OnServerWithExecutionSeam()
    {
        // T8: the 8 frozen methods keep exact semantics on a server that also
        // carries the execution seam (run.start is purely additive).
        var (id, result, error) = await RequestAsync(Rpc(1, "ping"));
        Assert.Null(error);
        Assert.Equal(1, id?.GetValue<int>());
        Assert.Equal("dsh-uniclaw-driverhost", result?["service"]?.GetValue<string>());

        var support = await RequestAsync(Rpc(2, "control.support", "{\"operation\":\"start\"}"));
        Assert.Equal(true, support.Result?["supported"]?.GetValue<bool>());
        Assert.Equal("AUTHORIZED_RUN_START_ENTRY", support.Result?["reason"]?.GetValue<string>());

        foreach (var op in new[] { "pause", "resume", "stop", "abort" })
        {
            var deferred = await RequestAsync(Rpc(3, "control.support", $"{{\"operation\":\"{op}\"}}"));
            Assert.Equal(false, deferred.Result?["supported"]?.GetValue<bool>());
            Assert.Equal("DEFERRED_NO_KERNEL_CONTROL_BUYER", deferred.Result?["reason"]?.GetValue<string>());
        }
    }

    /// <summary>WiFi off → SetSwitch(ON) → on (deterministic completed path).</summary>
    private static ScriptedEnvironment CompletingEnvironment()
        => new(
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

    private static RunGraphFactory ScriptedFactory(params (string DeviceKey, ScriptedEnvironment Environment)[] devices)
    {
        var map = devices.ToDictionary(d => d.DeviceKey, d => d.Environment, StringComparer.Ordinal);
        return selector =>
        {
            if (!map.TryGetValue(selector.Key, out var env))
            {
                throw new DeviceSelectorUnsupportedException(selector.Key, "not in test map");
            }

            var wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
            var criteria = new ElementBindingCriteria(
                [wifi],
                System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
                System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
            var pages = new PageAnalysisCriteria(
                "settings",
                System.Collections.Immutable.ImmutableDictionary<string, System.Collections.Immutable.ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
            var graph = PhysicalHostComposition.BuildRuntimeGraph(env, TestOptions, attach: null, criteria, pages);
            return new RunExecutionGraph(graph.Agent, env);
        };
    }

    /// <summary>Factory whose environment blocks its FIRST observe until the gate
    /// completes — keeps the run active deterministically for exclusivity proofs.</summary>
    private static RunGraphFactory GatedFactory(
        params (string DeviceKey, ScriptedEnvironment Environment, TaskCompletionSource Gate)[] devices)
    {
        var map = devices.ToDictionary(d => d.DeviceKey, d => d, StringComparer.Ordinal);
        return selector =>
        {
            if (!map.TryGetValue(selector.Key, out var entry))
            {
                throw new DeviceSelectorUnsupportedException(selector.Key, "not in test map");
            }

            var gated = new GatedEnvironment(entry.Environment, entry.Gate);
            var wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
            var criteria = new ElementBindingCriteria(
                [wifi],
                System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
                System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
            var pages = new PageAnalysisCriteria(
                "settings",
                System.Collections.Immutable.ImmutableDictionary<string, System.Collections.Immutable.ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
            var graph = PhysicalHostComposition.BuildRuntimeGraph(gated, TestOptions, attach: null, criteria, pages);
            return new RunExecutionGraph(graph.Agent, gated);
        };
    }

    /// <summary>IEnvironment wrapper: the first ObserveAsync waits on the gate;
    /// all subsequent calls delegate to the inner environment unchanged.</summary>
    private sealed class GatedEnvironment : IEnvironment
    {
        private readonly IEnvironment _inner;
        private readonly TaskCompletionSource _gate;
        private int _firstObserve;

        public GatedEnvironment(IEnvironment inner, TaskCompletionSource gate)
        {
            _inner = inner;
            _gate = gate;
        }

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _firstObserve, 1) == 0)
            {
                await _gate.Task.WaitAsync(cancellationToken);
            }

            return await _inner.ObserveAsync(cancellationToken);
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => _inner.ExecuteAsync(action, cancellationToken);
    }
}
