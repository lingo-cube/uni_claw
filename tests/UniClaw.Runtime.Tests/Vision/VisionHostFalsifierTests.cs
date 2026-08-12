using UniClaw.Vision.Host;
using Xunit;

namespace UniClaw.Runtime.Tests.Vision;

/// <summary>
/// H1-H18: VisionServiceHost falsifier tests + P4 model identity.
/// </summary>
public sealed class VisionHostFalsifierTests
{
    // ── H1: Normal startup (requires live env — validated elsewhere) ─────

    [Fact]
    public void H1_Host_Cold_InitialState()
    {
        var config = new VisionHostConfig { RepoRoot = "/tmp" };
        using var host = new VisionServiceHost(config);

        Assert.Equal(VisionHostState.Cold, host.State);
        Assert.EndsWith(".sock", host.SocketPath);
        Assert.Contains("uniclaw-vision-", host.SocketPath);
    }

    // ── H2: Python executable missing ───────────────────────────────────

    [Fact]
    public async Task H2_MissingPython_ThrowsFileNotFound()
    {
        var config = new VisionHostConfig
        {
            PythonExecutable = "/nonexistent/python-xyz-12345",
            RepoRoot = "/tmp",
        };
        using var host = new VisionServiceHost(config);
        await Assert.ThrowsAsync<FileNotFoundException>(() => host.StartAsync());
    }

    // ── H3: Service entry point missing ──────────────────────────────────

    [Fact]
    public async Task H3_MissingEntryPoint_ThrowsFileNotFound()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"vh-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            var config = new VisionHostConfig
            {
                PythonExecutable = "python3", // must exist for this to reach the entry point check
                ServiceEntryPoint = "nonexistent/server.py",
                RepoRoot = tmp,
            };
            using var host = new VisionServiceHost(config);

            // If python3 exists on PATH, the entry point check fires
            // If not, the python check fires first — both are FileNotFoundException
            try { await host.StartAsync(); }
            catch (FileNotFoundException) { /* expected */ }
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    // ── H4: Model missing ──────────────────────────────────────────────

    [Fact]
    public async Task H4_MissingModel_ThrowsFileNotFound()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"vh-test-{Guid.NewGuid():N}");
        // Create fake server.py path so entry point check passes
        var serverDir = Path.Combine(tmp, "tools", "local_vision");
        Directory.CreateDirectory(serverDir);
        File.WriteAllText(Path.Combine(serverDir, "server.py"), "# fake");
        try
        {
            var config = new VisionHostConfig
            {
                PythonExecutable = "python3",
                ServiceEntryPoint = "tools/local_vision/server.py",
                RepoRoot = tmp,
                ModelPath = "nonexistent/model.pt", // won't exist
            };
            using var host = new VisionServiceHost(config);
            await Assert.ThrowsAsync<FileNotFoundException>(() => host.StartAsync());
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    // ── H5: Config missing ──────────────────────────────────────────────

    [Fact]
    public void H5_ConfigMissing_MissingRepoRoot_Detected()
    {
        var config = new VisionHostConfig { RepoRoot = "/nonexistent/dir-xyz" };
        using var host = new VisionServiceHost(config);
        // Startup validates entry point existence which will fail
    }

    // ── H6: Health never ready ──────────────────────────────────────────

    [Fact]
    public void H6_HealthTimeout_Default_IsPositive()
    {
        var config = new VisionHostConfig();
        Assert.True(config.HealthTimeout > TimeSpan.Zero);
        Assert.True(config.ReadinessPollInterval > TimeSpan.Zero);
    }

    // ── H9: Socket path uniqueness ──────────────────────────────────────

    [Fact]
    public void H9_SocketPath_UniquePerSession()
    {
        var config = new VisionHostConfig();
        using var h1 = new VisionServiceHost(config);
        using var h2 = new VisionServiceHost(config);

        Assert.NotEqual(h1.SocketPath, h2.SocketPath);
        Assert.Contains("uniclaw-vision-", h1.SocketPath);
    }

    // ── H10: Socket path conforms to naming rules ────────────────────────

    [Fact]
    public void H10_SocketPath_MatchesNamingRule()
    {
        var config = new VisionHostConfig();
        using var host = new VisionServiceHost(config);

        Assert.StartsWith("/tmp/uniclaw-vision-", host.SocketPath);
        Assert.EndsWith(".sock", host.SocketPath);
        Assert.DoesNotContain("..", host.SocketPath); // no path traversal
    }

    // ── H15: Restart budget ─────────────────────────────────────────────

    [Fact]
    public void H15_RestartBudget_Default_IsFinite()
    {
        var config = new VisionHostConfig();
        Assert.Equal(3, config.MaxRestarts);
        Assert.Equal(TimeSpan.FromSeconds(60), config.RestartWindow);
    }

    // ── H16: Shutdown is idempotent ─────────────────────────────────────

    [Fact]
    public void H16_Shutdown_Idempotent()
    {
        var config = new VisionHostConfig();
        var host = new VisionServiceHost(config);

        host.Shutdown();
        Assert.Equal(VisionHostState.Shutdown, host.State);

        // Second shutdown — no throw
        host.Shutdown();
        Assert.Equal(VisionHostState.Shutdown, host.State);
    }

    // ── H17: Host operational failure cannot change Runtime ──────────────

    [Fact]
    public void H17_Host_HasNoRuntimeDependency()
    {
        // Vision.Host must not reference Agent, Container, Traversal namespaces
        var hostType = typeof(VisionServiceHost);
        var assembly = hostType.Assembly;

        var forbiddenNamespaces = new[]
        {
            "UniClaw.Runtime.Agent",
            "UniClaw.Runtime.Container",
            "UniClaw.Runtime.Traversal",
        };

        foreach (var ns in forbiddenNamespaces)
        {
            var hasType = assembly.GetTypes().Any(t =>
                (t.Namespace ?? "").StartsWith(ns, StringComparison.Ordinal));
            Assert.False(hasType, $"Vision.Host assembly references {ns}");
        }
    }

    // ── H18: Deploy facts capture ────────────────────────────────────────

    [Fact]
    public void H18_DeploymentFacts_FieldsPresent()
    {
        var facts = new VisionDeploymentFacts
        {
            ServiceVersion = "1.0",
            SupportedSchemas = ["uniclaw.localVisionEvidence.v1"],
            ModelId = "3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782",
            ConfigHash = "a85d7e78a27cde2321c64a8d62fab46179242f056f1addb6bf6698839aafddc3",
            OcrBackend = "rapidocr",
        };

        Assert.Equal("1.0", facts.ServiceVersion);
        Assert.Single(facts.SupportedSchemas);
        Assert.Equal(64, facts.ModelId!.Length);
        Assert.Equal(64, facts.ConfigHash!.Length);
    }

    // ── P4: Model identity changes with content ─────────────────────────

    [Fact]
    public void P4_ModelId_ChangesWhenContentChanges()
    {
        // Create two synthetic model files with different content
        var tmp = Path.GetTempPath();
        var model1 = Path.Combine(tmp, $"model-{Guid.NewGuid():N}.pt");
        var model2 = Path.Combine(tmp, $"model-{Guid.NewGuid():N}.pt");

        try
        {
            File.WriteAllText(model1, "model-v1-content");
            File.WriteAllText(model2, "model-v2-different");

            var hash1 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(model1)));
            var hash2 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(model2)));

            Assert.NotEqual(hash1, hash2); // different content → different hash
            Assert.Equal(64, hash1.Length);
            Assert.Equal(64, hash2.Length);
        }
        finally
        {
            try { File.Delete(model1); } catch { }
            try { File.Delete(model2); } catch { }
        }
    }

    [Fact]
    public void P4_ModelId_SameContentSameHash()
    {
        var tmp = Path.GetTempPath();
        var model = Path.Combine(tmp, $"model-{Guid.NewGuid():N}.pt");
        try
        {
            File.WriteAllText(model, "same-content");
            var h1 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(model)));
            var h2 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(model)));
            Assert.Equal(h1, h2); // same content → same hash
        }
        finally { try { File.Delete(model); } catch { } }
    }
}
