using System.Diagnostics;
using System.Text;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;
using UniClaw.Runtime.Tests.Observability;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// Cross-process E2E gate (PLUG-F2): the real Node DSH plugin client connects
/// over loopback TCP to the real DriverHost server and exercises ping, snapshot,
/// events, evidence, and the deferred-control audit. The Node side asserts the
/// wire contract; this test asserts the client completed cleanly
/// (E2E_ALL_OK on stdout, exit code 0).
/// </summary>
public sealed class DriverHostPluginE2ETests : IDisposable
{
    private readonly UniClawDriverHostServer _server;

    public DriverHostPluginE2ETests()
    {
        var observability = new DriverHostObservability();
        observability.RegisterRun(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun(),
            CaptureBundle());
        _server = new UniClawDriverHostServer(new UniClawControlSurface(observability), new DriverHostServerOptions { Port = 0 });
        _server.Start();
    }

    public void Dispose() => _server.Dispose();

    private static TraceCaptureBundle CaptureBundle() => new()
    {
        CaptureSessionId = "session-e2e",
        Provenance = "Synthetic",
        FinalState = CaptureState.Persisted,
        Records =
        [
            new CaptureRecord { Order = 1, Kind = CaptureRecordKind.Observation, SequenceNumber = 7, FrameId = "frame-1" },
        ],
        Artifacts =
        [
            new CaptureArtifact { ArtifactId = "artifact-0001", FrameId = "frame-1", FileName = "shot.png", ByteCount = 128 },
        ],
    };

    [Fact]
    public async Task NodeClient_CompletesFullReadSurface_E2eAllOk()
    {
        var node = FindExecutable("node");
        Assert.True(node is not null,
            "node is required for the DSH plugin E2E test; install Node.js and re-run.");

        var repoRoot = FindRepoRoot();
        var clientScript = Path.Combine(repoRoot, "dsh-plugin-uniclaw", "test", "e2e-client.mjs");
        Assert.True(File.Exists(clientScript), $"e2e-client.mjs missing: {clientScript}");

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
        psi.ArgumentList.Add("--runId");
        psi.ArgumentList.Add(ReadOnlyObservabilityFixtures.RunId);

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
            $"e2e-client exited {process.ExitCode}\nstdout:\n{output}\nstderr:\n{stderr}");
        Assert.Contains("E2E_ALL_OK", output, StringComparison.Ordinal);
        Assert.DoesNotContain("E2E_FAIL", output, StringComparison.Ordinal);
    }

    private static string? FindExecutable(string name)
    {
        var candidates = new List<string>();
        var pathEnv = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            candidates.Add(Path.Combine(dir, name));
            if (OperatingSystem.IsWindows())
            {
                candidates.Add(Path.Combine(dir, name + ".exe"));
            }
        }
        return candidates.FirstOrDefault(File.Exists);
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
