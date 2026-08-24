using System.Collections.Immutable;

namespace UniClaw.Runtime.Tests.Dependencies;

/// <summary>
/// Test dependency manifest — the SINGLE declarative source of truth for what
/// every test suite needs before running.
///
/// This is test-side documentation (a model, not production code). A new
/// engineer answers "what does this test need?" by reading this manifest, not
/// by reading test implementation details.
///
/// Every dependency is classified so failures are distinguishable:
///   - <see cref="TestDependencyKind"/> — what kind of dependency it is
///   - <see cref="TestFailureClass"/> — how to classify a failure caused by it
/// </summary>
public static class TestDependencyManifest
{
    /// <summary>Kind of dependency a test suite requires.</summary>
    public enum TestDependencyKind
    {
        /// <summary>Only the Runtime assemblies and the deterministic fake world (no environment).</summary>
        DeterministicOnly,
        /// <summary>A real Android emulator (AVD) attached via adb.</summary>
        AndroidEmulator,
        /// <summary>A real physical Android device attached via adb.</summary>
        RealDevice,
        /// <summary>A Node.js runtime plus the dsh-plugin-uniclaw client scripts.</summary>
        NodeClient,
        /// <summary>A local vision service (Python, uniclaw_perception) on a unix socket.</summary>
        VisionService,
        /// <summary>Python3 (used by the vision-host behavioral proof test server).</summary>
        Python3,
        /// <summary>An external fixture APK installed on the device.</summary>
        FixtureApk,
        /// <summary>A repository asset file (images, XML dumps, JSON corpora).</summary>
        RepoAsset,
    }

    /// <summary>How a failure caused by this dependency is classified.</summary>
    public enum TestFailureClass
    {
        /// <summary>The test or production code is wrong (real regression).</summary>
        CodeFailure,
        /// <summary>The required environment (device/emulator/node/vision) is not available.</summary>
        EnvironmentUnavailable,
        /// <summary>A required external asset (APK/model/corpus/file) is missing.</summary>
        MissingDependency,
        /// <summary>Setup (install/permission/reset) failed before the test could run.</summary>
        SetupFailure,
    }

    /// <summary>One declared test dependency.</summary>
    public sealed record Dependency(
        TestDependencyKind Kind,
        string Requirement,
        TestFailureClass FailureIfMissing,
        string? Preparation = null,
        string? Cleanup = null);

    /// <summary>Declared dependency set for one test suite.</summary>
    public sealed record Suite(
        string Name,
        ImmutableArray<Dependency> Dependencies);

    /// <summary>The complete dependency inventory (see docs/decisions/test-environment-dependency-audit-report.md §1).</summary>
    public static ImmutableArray<Suite> All { get; } =
    [
        new("Evidence Specification tests (Evidence/*.cs)", [
            new(TestDependencyKind.DeterministicOnly, "Runtime assemblies only; ScriptedEnvironment fake world.", TestFailureClass.CodeFailure),
        ]),
        new("OpenWorld / Semantic / Strategy / Pre-terminal deterministic suites", [
            new(TestDependencyKind.DeterministicOnly, "Runtime assemblies + Semantic packages (in-repo); ScriptedEnvironment + fixture semantic capability.", TestFailureClass.CodeFailure),
        ]),
        new("Replay / Perception asset suites (GoldenRunReplay, ScenarioCatalog, LiveCalibration, RealImageClassifier)", [
            new(TestDependencyKind.RepoAsset, "tests/UniClaw.Runtime.Tests/Perception/Assets/** (images, JSON corpora, golden-run bundles).", TestFailureClass.MissingDependency),
            new(TestDependencyKind.RepoAsset, "tests/UniClaw.Runtime.Tests/Replay/Assets/** (XML dumps, JSON traces).", TestFailureClass.MissingDependency),
        ]),
        new("Vision host behavioral proofs (Vision/VisionHostBehavioralProofs.cs)", [
            new(TestDependencyKind.Python3, "python3 with http.server (stdlib only) for vh_test_server.py.", TestFailureClass.EnvironmentUnavailable),
            new(TestDependencyKind.VisionService, "A local vision service socket is NOT required — the test uses a stub server; production LocalVisionPerceptionSource is exercised via dependency injection.", TestFailureClass.CodeFailure),
        ]),
        new("DriverHost node E2E (DriverHostRunStartE2ETests, DriverHostAssistanceE2ETests)", [
            new(TestDependencyKind.NodeClient, "node (any recent LTS) on PATH + dsh-plugin-uniclaw/test/*.mjs client scripts.", TestFailureClass.EnvironmentUnavailable,
                Preparation: "npm install in dsh-plugin-uniclaw if needed (plain ESM, no build).", Cleanup: null),
        ]),
        new("Settings real-device suites (Settings*_RealDevice_Phase1-5, ExternalBoundary_RealDevice)", [
            new(TestDependencyKind.AndroidEmulator, "One online emulator (e.g. emulator-5554) running AOSP Android with com.android.settings (system app; NO APK needed).", TestFailureClass.EnvironmentUnavailable,
                Preparation: "avdmanager/emulator boot; adb wait-for-device; sys.boot_completed=1; com.android.settings reachable (adb shell am start -a android.settings.SETTINGS).", Cleanup: "adb emu kill"),
            new(TestDependencyKind.VisionService, "Local vision service on /tmp/uniclaw-capstone.sock (python -m uvicorn uniclaw_perception.server:app --uds ...).", TestFailureClass.EnvironmentUnavailable,
                Preparation: "python3 -m venv .venv-local-vision; pip install -r platforms/perception/requirements/runtime.txt; start uvicorn on the socket.", Cleanup: "stop the uvicorn process; rm the socket."),
        ]),
        new("External boundary real-device suite (ExternalBoundary_RealDevice)", [
            new(TestDependencyKind.AndroidEmulator, "One online emulator with com.android.settings AND com.android.permissioncontroller; the test drives an app-permission dialog that foregrounds the permission controller.", TestFailureClass.EnvironmentUnavailable,
                Preparation: "same emulator boot as Settings suites; app-permission state must be resettable (adb shell pm clear com.android.settings in-test).", Cleanup: "adb emu kill"),
            new(TestDependencyKind.VisionService, "Local vision service socket (OCR + structured evidence channels).", TestFailureClass.EnvironmentUnavailable),
        ]),
        new("Capstone real-emulator suite (CapstoneSingleAgentRunTests)", [
            new(TestDependencyKind.AndroidEmulator, "One online emulator with com.uniclaw.fixture APK installed (fixture app).", TestFailureClass.EnvironmentUnavailable),
            new(TestDependencyKind.FixtureApk, "tools/android-runtime-reality-fixture/build/fixture-debug.apk (built via scripts/build.sh, installed via adb install).", TestFailureClass.MissingDependency,
                Preparation: "bash tools/android-runtime-reality-fixture/scripts/build.sh && adb -s <serial> install -r build/fixture-debug.apk.", Cleanup: "adb uninstall com.uniclaw.fixture"),
            new(TestDependencyKind.VisionService, "Local vision service on /tmp/uniclaw-capstone.sock (OCR channel for goal evidence).", TestFailureClass.EnvironmentUnavailable),
        ]),
    ];

    /// <summary>Failure-class helper: classify a test failure by its dependency.</summary>
    public static TestFailureClass Classify(string suiteName, string failureMessage)
    {
        var suite = All.FirstOrDefault(s => s.Name == suiteName);
        if (suite is null)
            return TestFailureClass.CodeFailure;
        var lowered = failureMessage.ToLowerInvariant();
        if (lowered.Contains("no eligible online adb device", StringComparison.Ordinal)
            || lowered.Contains("device not found", StringComparison.Ordinal)
            || lowered.Contains("node is required", StringComparison.Ordinal)
            || lowered.Contains("socket", StringComparison.Ordinal) && lowered.Contains("connect", StringComparison.Ordinal))
        {
            return TestFailureClass.EnvironmentUnavailable;
        }
        if (lowered.Contains("missing", StringComparison.Ordinal)
            || lowered.Contains("no such file", StringComparison.Ordinal))
        {
            return TestFailureClass.MissingDependency;
        }
        return TestFailureClass.CodeFailure;
    }
}
