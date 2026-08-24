using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.PhysicalHost;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// Android composition validation (dsh-runtime-agent-subagent-run-entry §33):
/// DeviceSelector → the CURRENT production Android composition, without requiring
/// a live device in CI (composition is construction-only; no IO is performed).
/// The production mapping itself is NOT faked — it is the real
/// PhysicalHostComposition.CreateAndroidRunGraphFactory seam over the real
/// AdbScreenshotSource / LocalVisionPerceptionSource / AdbDispatchTarget stack.
/// </summary>
public sealed class AndroidCompositionTests
{
    private static readonly PhysicalHostOptions Options = new(
        AdbExecutable: "adb",
        Serial: null,
        TargetApplication: "com.android.settings",
        VisionSocketPath: "/tmp/uniclaw-vision.sock",
        DisplayWidth: 1080,
        DisplayHeight: 1920);

    [Fact]
    public void SerialSelector_BuildsCurrentAndroidExecutionGraph_NoIo()
    {
        var factory = PhysicalHostComposition.CreateAndroidRunGraphFactory(Options);
        Assert.True(DeviceSelector.TryParse("serial:emulator-5554", out var selector));

        var graph = factory(selector);

        // The REAL production composition: Agent over a PhysicalEnvironment
        // (AdbScreenshotSource + LocalVisionPerceptionSource + AdbDispatchTarget).
        Assert.IsType<RuntimeAgent>(graph.Agent);
        Assert.IsType<PhysicalEnvironment>(graph.Environment);
        Assert.IsAssignableFrom<IEnvironment>(graph.Environment);
        // Agent receives only IEnvironment — zero ADB/Android/DSH awareness by contract.
    }

    [Fact]
    public void AliasSelector_Unsupported_FirstSliceIsSerialOnly()
    {
        var factory = PhysicalHostComposition.CreateAndroidRunGraphFactory(Options);
        Assert.True(DeviceSelector.TryParse("my-emulator", out var selector));

        var ex = Assert.Throws<DeviceSelectorUnsupportedException>(() => factory(selector));
        Assert.Contains("serial", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildDriverHostServer_WiresReadSurface_ExecutionSeam_AndPing()
    {
        using var server = PhysicalHostComposition.BuildDriverHostServer(Options, new DriverHostServerOptions { Port = 0 });
        server.Start();
        try
        {
            // The production host composition is wired: read-only surface +
            // RunExecutionCoordinator + Android factory, one listener.
            Assert.True(server.IsListening);
            Assert.Equal(0, server.ActiveConnections);

            // run.start reaches the coordinator through the seam; an unknown
            // (non-serial) device is deterministically rejected — proving the
            // execution seam is live without needing a physical device.
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync("127.0.0.1", server.BoundPort);
            var stream = client.GetStream();
            var line = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"run.start\",\"params\":{" +
                       "\"goal\":{\"objectIdentity\":\"WifiConnectivity\",\"stateDimension\":\"Enabled\",\"desiredValue\":true}," +
                       "\"objects\":[{\"identity\":\"WifiConnectivity\",\"category\":\"ConnectivitySetting\",\"stateDimensions\":[\"Enabled\"]}]," +
                       "\"capabilities\":[{\"name\":\"SetEnabled\",\"applicableToCategory\":\"ConnectivitySetting\",\"stateDimension\":\"Enabled\"}]," +
                       "\"device\":\"my-emulator\"}}\n";
                    var payload = System.Text.Encoding.UTF8.GetBytes(line);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();

            var buffer = new List<byte>();
            var one = new byte[1];
            while (stream.Read(one, 0, 1) > 0)
            {
                buffer.Add(one[0]);
                if (one[0] == (byte)'\n') break;
            }

            var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray())) as System.Text.Json.Nodes.JsonObject;
            Assert.Equal("request_rejected", json?["error"]?["code"]?.GetValue<string>());
            Assert.Contains("not supported", json?["error"]?["message"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            server.Stop();
        }
    }
}
