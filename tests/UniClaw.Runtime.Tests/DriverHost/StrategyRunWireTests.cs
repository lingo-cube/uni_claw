using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Strategy;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>Raw TCP proof for the additive run.strategy.start Goal-plane operation.</summary>
[Collection("ObservabilityTraceEmitters")]
public sealed class StrategyRunWireTests : IDisposable
{
    private readonly DriverHostObservability _observability = new();
    private readonly RunExecutionCoordinator _coordinator;
    private readonly UniClawDriverHostServer _server;

    public StrategyRunWireTests()
    {
        _coordinator = new RunExecutionCoordinator(
            _observability,
            _ => StrategyTestSupport.CreateGraph(),
            strategyCompiler: StrategyTestSupport.ExploreCompiler());
        _server = new UniClawDriverHostServer(
            new UniClawControlSurface(_observability),
            new DriverHostServerOptions { Port = 0 },
            execution: _coordinator,
            strategyExecution: _coordinator);
        _server.Start();
    }

    public void Dispose() => _server.Dispose();

    private static string StrategyParams(string strategyId)
        => $$"""
           {
             "strategy": {
               "strategyId": "{{strategyId}}",
               "contractVersion": 1,
               "objective": { "kind": "exploreScope" },
               "scope": {
                 "applicationIdentity": "SampleApplication",
                 "semanticRoot": "SampleRoot",
                 "maximumDepth": 1
               },
               "exploration": "exhaustiveWithinScope",
               "constraints": {
                 "allowedInteractionCategories": ["navigableContainer"],
                 "prohibitedEffects": ["stateMutation", "externalBoundaryCrossing"]
               },
               "completion": { "kind": "exhaustiveCoverageWithinScope" },
               "adaptation": {
                 "allowedAdaptations": ["reconcileBelief", "reviseExecutionHypothesis"]
               }
             },
             "device": "serial:sample-device"
           }
           """.ReplaceLineEndings(string.Empty);

    private static string Rpc(int id, string method, string? parameters = null)
        => $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\""
           + (parameters is null ? "}" : $",\"params\":{parameters}}}");

    [Fact]
    public async Task StrategyStart_AcceptsOneRunAndRejectsIdentityReuse()
    {
        var first = await RequestAsync(Rpc(1, "run.strategy.start", StrategyParams("wire-strategy-once")));

        Assert.Null(first.Error);
        Assert.True(first.Result?["accepted"]?.GetValue<bool>());
        var runId = first.Result?["runId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(runId));
        Assert.Equal("Idle", first.Result?["runState"]?.GetValue<string>());
        Assert.Contains(runId!, _observability.RegisteredRunIds);

        var second = await RequestAsync(Rpc(2, "run.strategy.start", StrategyParams("wire-strategy-once")));
        Assert.Null(second.Error);
        Assert.False(second.Result?["accepted"]?.GetValue<bool>());
        Assert.Equal("duplicateStrategy", second.Result?["rejectionCode"]?.GetValue<string>());
        Assert.Null(second.Result?["runId"]);
        Assert.Single(_observability.RegisteredRunIds);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var state = _observability.GetRunSnapshot(runId!).RunState.Value;
            if (state is RunState.Completed or RunState.Failed)
                break;
            await Task.Delay(5);
        }

        var snapshot = _observability.GetRunSnapshot(runId!);
        Assert.Equal(RunState.Completed, snapshot.RunState.Value);
        var events = _observability.GetRuntimeEvents(runId!).Events;
        Assert.NotEmpty(events);
        Assert.All(events, runtimeEvent => Assert.Equal(runId, runtimeEvent.RunId));
        Assert.Contains(events, runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.RunCompleted);
    }

    [Fact]
    public async Task ControlSupport_AdvertisesStrategyStartWithoutChangingRunStart()
    {
        var strategy = await RequestAsync(Rpc(
            1,
            "control.support",
            "{\"operation\":\"run.strategy.start\"}"));
        var existing = await RequestAsync(Rpc(
            2,
            "control.support",
            "{\"operation\":\"start\"}"));

        Assert.Null(strategy.Error);
        Assert.True(strategy.Result?["supported"]?.GetValue<bool>());
        Assert.Equal("AUTHORIZED_STRATEGY_START_ENTRY", strategy.Result?["reason"]?.GetValue<string>());
        Assert.True(existing.Result?["supported"]?.GetValue<bool>());
        Assert.Equal("AUTHORIZED_RUN_START_ENTRY", existing.Result?["reason"]?.GetValue<string>());
    }

    private async Task<(JsonNode? Result, JsonNode? Error)> RequestAsync(string line)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", _server.BoundPort);
        var stream = client.GetStream();
        var payload = Encoding.UTF8.GetBytes(line + "\n");
        await stream.WriteAsync(payload);
        await stream.FlushAsync();

        var bytes = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(one);
            if (read == 0 || one[0] == (byte)'\n')
                break;
            bytes.Add(one[0]);
        }

        var response = JsonNode.Parse(Encoding.UTF8.GetString(bytes.ToArray()))!.AsObject();
        return (response["result"]?.DeepClone(), response["error"]?.DeepClone());
    }
}
