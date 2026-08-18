using System.Diagnostics;
using System.Text;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.PhysicalHost;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// Cross-process run.start E2E gate (dsh-runtime-agent-subagent-run-entry §32):
/// a REAL DriverHost server wired with the REAL RunExecutionCoordinator + REAL
/// Runtime.Agent entry over a deterministic ScriptedEnvironment factory, driven
/// by the REAL Node plugin client over loopback TCP. No mocked JSON-RPC
/// dispatcher: run.start dispatch, wire serialization, coordinator, observability
/// registration, and the Agent semantic entry all execute for real. The Node side
/// asserts the full accept → immediate-visibility → completed path and prints
/// E2E_RUN_START_OK.
/// </summary>
public sealed class DriverHostRunStartE2ETests : IDisposable
{
    private readonly UniClawDriverHostServer _server;

    private static readonly PhysicalHostOptions TestOptions = new(
        "adb", null, "settings", "/tmp/uniclaw-vision-test.sock", 1080, 1920);

    public DriverHostRunStartE2ETests()
    {
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(observability, ScriptedFactory());
        _server = new UniClawDriverHostServer(
            new UniClawControlSurface(observability),
            new DriverHostServerOptions { Port = 0 },
            coordinator);
        _server.Start();
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task NodeClient_StartsRealAgentRun_AndObservesCompletion_ThroughExistingSurfaces()
    {
        var node = FindExecutable("node");
        Assert.True(node is not null, "node is required for the run.start E2E test; install Node.js and re-run.");

        var repoRoot = FindRepoRoot();
        var clientScript = Path.Combine(repoRoot, "dsh-plugin-uniclaw", "test", "e2e-run-start.mjs");
        Assert.True(File.Exists(clientScript), $"e2e-run-start.mjs missing: {clientScript}");

        var psi = new ProcessStartInfo
        {
            FileName = node,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(clientScript);
        psi.ArgumentList.Add("--host");
        psi.ArgumentList.Add("127.0.0.1");
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(_server.BoundPort.ToString());

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("failed to start node process");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutTask = process.StandardOutput.ReadToEndAsync().ContinueWith(t => stdout.Append(t.Result));
        var stderrTask = process.StandardError.ReadToEndAsync().ContinueWith(t => stderr.Append(t.Result));
        await process.WaitForExitAsync();
        await Task.WhenAll(stdoutTask, stderrTask);

        var output = stdout.ToString();
        Assert.True(process.ExitCode == 0,
            $"e2e-run-start exited {process.ExitCode}\nstdout:\n{output}\nstderr:\n{stderr}");
        Assert.Contains("E2E_RUN_START_OK", output, StringComparison.Ordinal);
        Assert.DoesNotContain("E2E_FAIL", output, StringComparison.Ordinal);
        Assert.Contains("E2E_SNAPSHOT_COMPLETED_OK", output, StringComparison.Ordinal);
        Assert.Contains("E2E_EVENTS_COMPLETED_OK", output, StringComparison.Ordinal);
    }

    /// <summary>WiFi off → SetSwitch(ON) → on: the deterministic completed path.</summary>
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
                throw new DeviceSelectorUnsupportedException(selector.Key, "E2E supports only serial:test-1");
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

    private static string? FindExecutable(string name)
    {
        var pathEnv = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "dsh-plugin-uniclaw")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root (dsh-plugin-uniclaw) not found from " + AppContext.BaseDirectory);
    }
}
