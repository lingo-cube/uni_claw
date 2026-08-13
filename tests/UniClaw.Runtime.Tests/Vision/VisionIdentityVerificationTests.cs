using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Sockets;
using UniClaw.Vision.Host;
using Xunit;

namespace UniClaw.Runtime.Tests.Vision;

/// <summary>
/// P4-D7 — Host expected-vs-observed deployment identity verification.
/// The Host is MECHANISM authority only: it compares expected facts
/// (from deployment composition) against observed /version facts and
/// fails closed on any axis mismatch. It never decides what should be
/// deployed.
/// </summary>
public sealed class VisionIdentityVerificationTests
{
    private static readonly string Script = "/tmp/vh_test_server.py";

    private static async Task<(Process Proc, string Socket)> Start(string mode)
    {
        var socket = $"/tmp/vh-id-{Guid.NewGuid():N}"[..25] + ".sock";
        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"{Script} {socket} {mode}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;
        if (await proc.StandardOutput.ReadLineAsync() != "READY")
            throw new InvalidOperationException("fixture not ready");
        await Task.Delay(200);
        return (proc, socket);
    }

    private static async Task Kill(Process proc, string socket)
    {
        if (!proc.HasExited) { proc.Kill(); await proc.WaitForExitAsync(); }
        proc.Dispose();
        try { File.Delete(socket); } catch { }
    }

    [Fact]
    public async Task IdentityMatch_NoThrow()
    {
        var (proc, socket) = await Start("normal");
        try
        {
            using var host = new VisionServiceHost(new VisionHostConfig
            {
                ExpectedIdentity = new ExpectedDeploymentIdentity
                {
                    ModelId = "0f72dd1cb7eb798dfc6aeba85076fac9b60631cd84ee1a0a61fdbe2ae08ef9c8",
                    RequiredSchemas = ImmutableArray.Create("uniclaw.localVisionEvidence.v1"),
                },
            });
            // Matching observed facts → verification passes without throwing.
            await VerifyObservedAsync(host, socket);
        }
        finally { await Kill(proc, socket); }
    }

    [Fact]
    public void HOST01_CanonicalFactory_MaterializesAllReceiptAxes_AndVerifiesSchema()
    {
        var receipt = Path.GetTempFileName();
        try
        {
            File.WriteAllText(receipt, """
                {"active":{"schemaVersion":"uniclaw.localVisionEvidence.v1","modelId":"model:1","configId":"config:1","pipelineRevision":"prev:1","deploymentId":"deploy:1"}}
                """);
            using var host = CanonicalVisionHostFactory.Create(receipt);
            host.VerifyIdentityAgainst("""
                {"modelId":"model:1","configId":"config:1","pipelineRevision":"prev:1","deploymentId":"deploy:1","supportedSchemas":["uniclaw.localVisionEvidence.v1"]}
                """);
        }
        finally { File.Delete(receipt); }
    }

    [Fact]
    public void HOST02_CanonicalFactory_RejectsIncompleteReceipt()
    {
        var receipt = Path.GetTempFileName();
        try
        {
            File.WriteAllText(receipt, """{"active":{"modelId":"model:1"}}""");
            Assert.Throws<InvalidDataException>(() => CanonicalVisionHostFactory.Create(receipt));
        }
        finally { File.Delete(receipt); }
    }

    [Fact]
    public void HOST08_ProductionComposition_UsesOnlyCanonicalFactory()
    {
        var root = FindRepositoryRoot();
        var hostSource = Path.Combine(root, "src", "UniClaw.Vision.Host");
        var directConstruction = Directory.EnumerateFiles(hostSource, "*.cs")
            .Where(path => !path.EndsWith("VisionServiceHost.cs", StringComparison.Ordinal)
                        && !path.EndsWith("CanonicalVisionHostFactory.cs", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(x => x.line.Contains("new VisionServiceHost", StringComparison.Ordinal)
                     || x.line.Contains("new VisionHostConfig", StringComparison.Ordinal))
            .ToArray();

        Assert.True(directConstruction.Length == 0,
            "Production Vision Host composition must go through CanonicalVisionHostFactory.");
    }

    // ── Axis mismatch proofs (DI-16): use the Host verification predicate
    //    directly with observed facts from each fixture mode. ────────────

    [Fact]
    public async Task DI16_ModelMismatch_FailsClosed()
    {
        var (proc, socket) = await Start("wrong-model");
        try
        {
            using var host = new VisionServiceHost(new VisionHostConfig
            {
                ExpectedIdentity = new ExpectedDeploymentIdentity { ModelId = new string('e', 64) },
            });
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                VerifyObservedAsync(host, socket));
        }
        finally { await Kill(proc, socket); }
    }

    [Fact]
    public async Task DI16_ConfigMismatch_FailsClosed()
    {
        var (proc, socket) = await Start("wrong-config");
        try
        {
            using var host = new VisionServiceHost(new VisionHostConfig
            {
                ExpectedIdentity = new ExpectedDeploymentIdentity { ConfigId = "config:expected" },
            });
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                VerifyObservedAsync(host, socket));
        }
        finally { await Kill(proc, socket); }
    }

    [Fact]
    public async Task DI16_PipelineMismatch_FailsClosed()
    {
        var (proc, socket) = await Start("wrong-pipeline");
        try
        {
            using var host = new VisionServiceHost(new VisionHostConfig
            {
                ExpectedIdentity = new ExpectedDeploymentIdentity
                { PipelineRevision = "prev:expected" },
            });
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                VerifyObservedAsync(host, socket));
        }
        finally { await Kill(proc, socket); }
    }

    [Fact]
    public async Task DI16_SchemaMismatch_FailsClosed()
    {
        var (proc, socket) = await Start("unsupported");
        try
        {
            using var host = new VisionServiceHost(new VisionHostConfig
            {
                ExpectedIdentity = new ExpectedDeploymentIdentity
                {
                    RequiredSchemas = ImmutableArray.Create("uniclaw.localVisionEvidence.v1"),
                },
            });
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                VerifyObservedAsync(host, socket));
        }
        finally { await Kill(proc, socket); }
    }

    /// <summary>Fetch /version from the fixture and run the Host's
    /// verification predicate against it (mechanism-under-test).</summary>
    private static async Task VerifyObservedAsync(VisionServiceHost host, string socket)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (ctx, ct) =>
            {
                var s = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.Unix,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Unspecified);
                await s.ConnectAsync(
                    new System.Net.Sockets.UnixDomainSocketEndPoint(socket), ct);
                return new NetworkStream(s, ownsSocket: true);
            },
        };
        using var client = new HttpClient(handler)
        { BaseAddress = new Uri("http://localhost"), Timeout = TimeSpan.FromSeconds(10) };
        var resp = await client.GetStringAsync("/version");
        host.VerifyIdentityAgainst(resp);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
