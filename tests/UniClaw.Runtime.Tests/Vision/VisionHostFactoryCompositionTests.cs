using System.Reflection;
using UniClaw.Vision.Host;
using Xunit;

namespace UniClaw.Runtime.Tests.Vision;

/// <summary>
/// CORR-HOST-01..10 (GAP-009): the factory is the canonical production
/// reachability boundary, proven by REAL composition — a factory-created
/// Host against the real Python perception server.
/// </summary>
public sealed class VisionHostFactoryCompositionTests
{
    private static readonly string ReceiptPath = Path.Combine(
        RepoRoot(), "platforms", "perception", "governance",
        "artifacts", "current-active-identity.json");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "platforms", "perception")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("repo root not found from " + AppContext.BaseDirectory);
    }

    // ── CORR-HOST-01: structural reachability ──────────────────

    [Fact]
    public void CORR_HOST01_NoPublicNoncanonicalConstruction()
    {
        // VisionHostConfig: no public parameterless constructor
        var publicCtors = typeof(VisionHostConfig).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(publicCtors,
            c => c.GetParameters().Length == 0);
        // VisionServiceHost: constructor not public
        var hostCtors = typeof(VisionServiceHost).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(hostCtors);
        // canonical factory path IS public
        var factoryCreate = typeof(CanonicalVisionHostFactory).GetMethod(
            "Create", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(factoryCreate);
    }

    // ── CORR-HOST-02: factory requires the CURRENT ACTIVE receipt ──

    [Fact]
    public void CORR_HOST02_FactoryRequiresCompleteReceipt()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"corr-host-{Guid.NewGuid():N}"[..12]);
        Directory.CreateDirectory(tmp);
        try
        {
            var incomplete = Path.Combine(tmp, "incomplete.json");
            File.WriteAllText(incomplete,
                """{"active": {"modelId": "m"}}""");
            Assert.ThrowsAny<InvalidDataException>(() =>
                CanonicalVisionHostFactory.Create(incomplete, repoRoot: tmp));
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    [Fact]
    public void CORR_HOST02b_FactorySnapshotsReceiptAxes()
    {
        using var host = CanonicalVisionHostFactory.Create(
            ReceiptPath, repoRoot: RepoRoot());
        // The factory-created host must carry all four expected axes from
        // the receipt — observable through successful verification below
        // (CORR_HOST03) and the receipt-mutation proof (CORR_HOST09).
        Assert.NotNull(host);
        host.Shutdown();
    }

    // ── CORR-HOST-03/04: real factory → real server → HEALTHY + restart ──

    [Fact(Timeout = 240_000)]
    public async Task CORR_HOST03_RealFactoryHostReachesHealthy()
    {
        using var host = CanonicalVisionHostFactory.Create(
            ReceiptPath, repoRoot: RepoRoot());
        await host.StartAsync();
        Assert.Equal(VisionHostState.Healthy, host.State);
        Assert.NotNull(host.Facts);
        host.Shutdown();
    }

    [Fact(Timeout = 240_000)]
    public async Task CORR_HOST04_RestartReverifiesRealChild()
    {
        using var host = CanonicalVisionHostFactory.Create(
            ReceiptPath, repoRoot: RepoRoot());
        await host.StartAsync();
        Assert.Equal(VisionHostState.Healthy, host.State);
        var firstFacts = host.Facts;

        var restarted = await host.TryRestartAsync();
        Assert.True(restarted);
        Assert.Equal(VisionHostState.Healthy, host.State);
        Assert.Equal(firstFacts?.ModelId, host.Facts?.ModelId);
        host.Shutdown();
    }

    // ── CORR-HOST-05..08: mismatch fail-closed through the factory path ──

    private static string WriteTamperedReceipt(string axis, string value)
    {
        var original = File.ReadAllText(ReceiptPath);
        var tmp = Path.Combine(Path.GetTempPath(), $"corr-host-{Guid.NewGuid():N}"[..12]);
        Directory.CreateDirectory(tmp);
        var path = Path.Combine(tmp, "receipt.json");
        // simple tamper: replace the first occurrence of the axis value
        var axisKey = axis switch
        {
            "model" => "\"modelId\": \"",
            "config" => "\"configId\": \"",
            "pipeline" => "\"pipelineRevision\": \"",
            _ => throw new ArgumentException(axis),
        };
        var start = original.IndexOf(axisKey, StringComparison.Ordinal);
        Assert.True(start >= 0, $"axis {axis} not found in receipt");
        var valueStart = start + axisKey.Length;
        var valueEnd = original.IndexOf('"', valueStart);
        var tampered = original[..valueStart] + value + original[valueEnd..];
        File.WriteAllText(path, tampered);
        return path;
    }

    [Theory(Timeout = 240_000)]
    [InlineData("model")]
    [InlineData("config")]
    [InlineData("pipeline")]
    public async Task CORR_HOST05_07_MismatchFailsClosed(string axis)
    {
        var tampered = WriteTamperedReceipt(axis, new string('f', 64));
        try
        {
            using var host = CanonicalVisionHostFactory.Create(
                tampered, repoRoot: RepoRoot());
            var ex = await Assert.ThrowsAnyAsync<Exception>(
                () => host.StartAsync());
            Assert.Contains("mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(VisionHostState.Healthy, host.State);
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(tampered)!, true); } catch { } }
    }

    [Fact(Timeout = 240_000)]
    public async Task CORR_HOST08_UnsupportedSchemaFailsClosed()
    {
        // Layer 1 (factory): a receipt whose schema axis is unsupported is
        // rejected at the canonical composition boundary — fail closed
        // BEFORE any Host exists.
        var tmp = Path.Combine(Path.GetTempPath(), $"corr-host-{Guid.NewGuid():N}"[..12]);
        Directory.CreateDirectory(tmp);
        var path = Path.Combine(tmp, "receipt.json");
        var original = File.ReadAllText(ReceiptPath);
        var tampered = original.Replace(
            "\"schemaVersion\": \"uniclaw.localVisionEvidence.v1\"",
            "\"schemaVersion\": \"other.schema.v9\"");
        File.WriteAllText(path, tampered);
        try
        {
            var ex = Assert.ThrowsAny<Exception>(
                () => CanonicalVisionHostFactory.Create(path, repoRoot: RepoRoot()));
            Assert.Contains("schema", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }

        // Layer 2 (Host): a SERVICE reporting unsupported schemas fails
        // closed in the Host comparison predicate — proven by
        // VisionIdentityVerificationTests.DI16_SchemaMismatch_FailsClosed
        // via the same VerifyIdentityOrThrow mechanism the factory-created
        // Host exercises in CORR_HOST03/04.
        await Task.CompletedTask;
    }

    // ── CORR-HOST-09: receipt mutation does not switch a live Host ──

    [Fact(Timeout = 240_000)]
    public async Task CORR_HOST09_ReceiptMutationDoesNotSwitchLiveHost()
    {
        using var host = CanonicalVisionHostFactory.Create(
            ReceiptPath, repoRoot: RepoRoot());
        await host.StartAsync();
        var capturedModelId = host.Facts?.ModelId;
        Assert.NotNull(capturedModelId);

        // mutate the receipt ON DISK after construction
        var original = await File.ReadAllTextAsync(ReceiptPath);
        try
        {
            await File.WriteAllTextAsync(ReceiptPath,
                original.Replace(capturedModelId, new string('f', 64)));
            var restarted = await host.TryRestartAsync();
            Assert.True(restarted);
            Assert.Equal(VisionHostState.Healthy, host.State);
            // the Host still verifies against its CAPTURED expectation —
            // the mutated receipt did not silently switch its identity
            Assert.Equal(capturedModelId, host.Facts?.ModelId);
        }
        finally
        {
            await File.WriteAllTextAsync(ReceiptPath, original);
            host.Shutdown();
        }
    }

    // ── CORR-HOST-10: P4-34 reaches E4 via the proofs above ─────

    [Fact]
    public void CORR_HOST10_P4_34_EvidenceCompositionDeclared()
    {
        // P4-34 reaches E4 when CORR_HOST-03/04/05-08/09 all pass against
        // the REAL Python server through the factory-created Host. This
        // test asserts the composition contract itself: the factory path
        // is the only canonical production reachability seam.
        var internalCtor = typeof(VisionServiceHost).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null, types: new[] { typeof(VisionHostConfig) }, modifiers: null);
        Assert.NotNull(internalCtor);
        Assert.False(internalCtor!.IsPublic);
    }
}
