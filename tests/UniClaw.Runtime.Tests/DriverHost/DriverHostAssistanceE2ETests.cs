using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using UniClaw.Runtime.Capabilities.Brain;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.PhysicalHost;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// Cross-process Assistance E2E gate (dsh-assistance-provider-adapter A4):
/// the REAL DriverHost server + REAL RunExecutionCoordinator + REAL
/// AssistanceWireProvider (injected into the Agent) + REAL Node plugin bridge +
/// DETERMINISTIC consumer, over loopback TCP. The Agent hits a Contradicted
/// adjudication, consults the wire provider, the bridge resolves with
/// "re-observe", the Agent re-observes (external world transition via the
/// scripted environment), and the SAME goal completes. MODEL-FREE.
/// </summary>
public sealed class DriverHostAssistanceE2ETests : IDisposable
{
    private readonly UniClawDriverHostServer _server;

    private static readonly PhysicalHostOptions TestOptions = new(
        "adb", null, "settings", "/tmp/uniclaw-vision-test.sock", 1080, 1920);

    private const string ContradictingText = "CONTRADICTING_TEXT";

    public DriverHostAssistanceE2ETests()
    {
        var observability = new DriverHostObservability();
        var registry = new AssistancePendingRegistry();
        var wireProvider = new AssistanceWireProvider(registry);
        var coordinator = new RunExecutionCoordinator(observability, ScriptedFactory(wireProvider));
        _server = new UniClawDriverHostServer(
            new UniClawControlSurface(observability),
            new DriverHostServerOptions { Port = 0 },
            coordinator,
            registry);
        _server.Start();
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task NodeClient_BridgeResolvesConsult_AgentContinues_Completes()
    {
        var node = FindExecutable("node");
        Assert.True(node is not null, "node is required for the assistance E2E test; install Node.js and re-run.");

        var repoRoot = FindRepoRoot();
        var clientScript = Path.Combine(repoRoot, "dsh-plugin-uniclaw", "test", "e2e-assistance.mjs");
        Assert.True(File.Exists(clientScript), $"e2e-assistance.mjs missing: {clientScript}");

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
            $"e2e-assistance exited {process.ExitCode}\nstdout:\n{output}\nstderr:\n{stderr}");
        Assert.Contains("E2E_ASSISTANCE_OK", output, StringComparison.Ordinal);
        Assert.Contains("E2E_ASSISTANCE_RESOLVED_OK", output, StringComparison.Ordinal);
        Assert.DoesNotContain("E2E_FAIL", output, StringComparison.Ordinal);
    }

    /// <summary>Observation sequence: seq1 = Startup observe, seq2 = Agent initial
    /// observe (contradicting screen → fused belief Contradicted → consult),
    /// seq3 = the re-observe the advice triggers → external world transition to
    /// the clean screen (observeScreenTransitions) → continuity verified → SAME
    /// goal continues → SetSwitch → completed.</summary>
    private static RunGraphFactory ScriptedFactory(IAssistanceProvider wireProvider)
    {
        var env = new ScriptedEnvironment(
            "Settings",
            "Settings",
            [
                Screen("Settings", contradicting: true, off: true),
                Screen("SettingsClean", contradicting: false, off: true),
                Screen("On", contradicting: false, on: true),
            ],
            observeScreenTransitions: new Dictionary<long, string> { [3] = "SettingsClean" });

        return selector =>
        {
            if (selector.Key != "serial:test-1")
            {
                throw new DeviceSelectorUnsupportedException(selector.Key, "E2E supports only serial:test-1");
            }

            var wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
            var criteria = new ElementBindingCriteria(
                [wifi],
                ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
                ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
            var pages = new PageAnalysisCriteria(
                "settings",
                ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]),
                ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", [ContradictingText]));
            var semanticEnv = env.WithToggleLocalControl();
            var graph = PhysicalHostComposition.BuildRuntimeGraph(
                semanticEnv, TestOptions, attach: null, criteria, pages,
                launchIntentAction: null, resolveSemanticPage: _ => "Settings",
                assistanceProvider: wireProvider);
            return new RunExecutionGraph(graph.Agent, env);
        };
    }

    private static ScreenConfig Screen(string name, bool contradicting, bool off = false, bool on = false)
    {
        var elements = new List<ElementConfig>
        {
            new("Wi‑Fi", null, null, new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), "menuItem"),
            new("", on ? true : (off ? false : (bool?)null),
                on ? null : new TransitionConfig(ScreenTransitionAction.SetSwitch, "On", true),
                new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle"),
        };
        if (contradicting)
        {
            elements.Add(new ElementConfig(ContradictingText, null, null, new ElementBounds(0.1f, 0.9f, 0.5f, 0.95f), "text"));
        }

        return new ScreenConfig(name, "settings", [.. elements]);
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
