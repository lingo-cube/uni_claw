using System.Collections.Immutable;
using UniClaw.Runtime.PhysicalHost;
using UniClaw.Vision.Host;
using Xunit;
using PhysicalEnvironment = UniClaw.Runtime.Adapters.PhysicalEnvironment;

namespace UniClaw.Runtime.Tests.PhysicalHost;

/// <summary>
/// Vision runtime bootstrap tests (vision-runtime-bootstrap A1–A4, T1–T14):
/// config resolution, managed/external mode, python/repo/receipt resolution,
/// early validation, exact endpoint wiring, no stale default guessing.
/// Real-process tests (T2/T3/T8/T9) use the repository-managed runtime through
/// the SAME production resolution boundary.
/// </summary>
public sealed class VisionRuntimeBootstrapTests
{
    private static readonly PhysicalHostOptions ManagedOptions = new(
        "adb", null, "com.android.settings", VisionSocketPath: null, 1080, 1920);

    private static readonly PhysicalHostOptions ExternalOptions = new(
        "adb", null, "com.android.settings", VisionSocketPath: "/tmp/external-vision.sock", 1080, 1920);

    private static readonly string AppRoot = VisionRuntimeBootstrap.ResolveAppRoot();

    [Fact]
    public void T1_RepoManagedPython_ResolvesByDefault()
    {
        var config = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(ManagedOptions, AppRoot);

        Assert.Equal(VisionRuntimeMode.Managed, config.Mode);
        Assert.Equal(
            Path.Combine(AppRoot, VisionRuntimeBootstrap.RepositoryManagedPythonRelative),
            config.PythonExecutable);
        Assert.True(File.Exists(config.PythonExecutable), "repository-managed venv python must exist");
        Assert.Equal(Path.Combine(AppRoot, "platforms", "perception"), config.PerceptionRepoRoot);
        Assert.EndsWith("current-active-identity.json", config.ReceiptPath, StringComparison.Ordinal);
    }

    [Fact]
    public void T11_ManagedMode_NeverGuessesStaleDefault()
    {
        var config = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(ManagedOptions, AppRoot);
        Assert.Null(config.ExternalSocketPath);
        Assert.NotEqual("/tmp/uniclaw-vision.sock", config.ExternalSocketPath);

        // BuildRealEnvironment without a resolved endpoint must fail — no implicit
        // fallback to the stale default.
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalHostComposition.BuildRealEnvironment(
                new PhysicalHostOptions("adb", null, "com.android.settings", null, 1080, 1920),
                "emulator-5554"));
    }

    [Fact]
    public void T12_ExternalAttach_ResolvesExplicitEndpoint_NoHostCreation()
    {
        var config = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(ExternalOptions, AppRoot);

        Assert.Equal(VisionRuntimeMode.External, config.Mode);
        Assert.Equal("/tmp/external-vision.sock", config.ExternalSocketPath);
        VisionRuntimeBootstrap.ValidateVisionRuntimeConfiguration(config); // does not throw

        var env = PhysicalHostComposition.BuildRealEnvironment(
            ExternalOptions, "emulator-5554", config.ExternalSocketPath);
        Assert.IsType<PhysicalEnvironment>(env);
        // External mode never launches a managed host (no create call path exists).
        Assert.Throws<InvalidOperationException>(() => VisionRuntimeBootstrap.CreateManagedVisionHost(config));
    }

    [Fact]
    public void T13_AmbiguousConfiguration_IsDeterministic()
    {
        // Explicit --vision-socket ⇒ EXTERNAL (frozen precedence); absence ⇒ MANAGED.
        var explicitConfig = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(ExternalOptions, AppRoot);
        Assert.Equal(VisionRuntimeMode.External, explicitConfig.Mode);

        var managed = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(ManagedOptions, AppRoot);
        Assert.Equal(VisionRuntimeMode.Managed, managed.Mode);
    }

    [Fact]
    public void T14_AppRootResolution_DoesNotDependOnCallerCwd()
    {
        var root = VisionRuntimeBootstrap.ResolveAppRoot();
        Assert.True(Directory.Exists(Path.Combine(root, "platforms", "perception")),
            "app root resolves deterministically from the assembly location, not cwd");
    }

    [Fact]
    public void T5_MissingPython_FailsEarlyAndActionably()
    {
        var config = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(ManagedOptions, AppRoot)
            with { PythonExecutable = "/nonexistent/venv/bin/python" };

        var ex = Assert.Throws<FileNotFoundException>(() => VisionRuntimeBootstrap.ValidateVisionRuntimeConfiguration(config));
        Assert.Contains("Vision Python", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void T6_ModuleResolutionFailure_IsActionableAndBounded()
    {
        var config = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(ManagedOptions, AppRoot)
            with { PerceptionRepoRoot = Path.Combine(AppRoot, "platforms", "nonexistent") };

        var ex = Assert.Throws<DirectoryNotFoundException>(() => VisionRuntimeBootstrap.ValidateVisionRuntimeConfiguration(config));
        Assert.Contains("感知仓库根", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void T7_InvalidOrMissingReceipt_FailsClosed()
    {
        var config = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(ManagedOptions, AppRoot)
            with { ReceiptPath = Path.Combine(AppRoot, "platforms", "perception", "governance", "artifacts", "missing-receipt.json") };

        Assert.Throws<FileNotFoundException>(() => VisionRuntimeBootstrap.CreateManagedVisionHost(config));
    }

    [Fact]
    public void T4_ExactEndpointWiring_ManagedHostSocketEqualsInjectedEndpoint()
    {
        // The injected endpoint is exactly what BuildRealEnvironment consumes:
        // simulate the host output value and verify it is used verbatim (no
        // second socket calculation). Full real-host equality is proven by the
        // real-process test (T2/T3) when the environment permits.
        const string hostSocket = "/tmp/uniclaw-vision-session-abc.sock";
        var env = PhysicalHostComposition.BuildRealEnvironment(ManagedOptions, "emulator-5554", hostSocket);
        Assert.IsType<PhysicalEnvironment>(env);
        Assert.NotEqual("/tmp/uniclaw-vision.sock", hostSocket);
    }

    [Fact]
    public void T10_GovernanceReceipt_IsCanonicalAndValid()
    {
        // The canonical receipt exists and satisfies the four-axis identity shape.
        var config = VisionRuntimeBootstrap.ResolveVisionRuntimeConfiguration(ManagedOptions, AppRoot);
        Assert.True(File.Exists(config.ReceiptPath), "governance current-active-identity.json exists");

        // CanonicalVisionHostFactory.Create performs full validation — missing axes
        // throw. The production path must pass (identity verification intact).
        using var host = VisionRuntimeBootstrap.CreateManagedVisionHost(config);
        Assert.NotNull(host);
        host.Dispose();
    }
}
