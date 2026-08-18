using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.PhysicalHost;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// RunExecutionCoordinator gate (dsh-runtime-agent-subagent-run-entry):
/// async accept → authoritative runId, immediate observability, REQUEST_REJECTED
/// semantics, ONE_ACTIVE_RUN_PER_DEVICE, reservation release, completed/failed
/// paths through the REAL Runtime.Agent entry over a deterministic
/// ScriptedEnvironment (fake on the test side; production Android composition is
/// separately proven by AndroidCompositionTests).
/// </summary>
public sealed class RunExecutionCoordinatorTests
{
    private static readonly PhysicalHostOptions TestOptions = new(
        AdbExecutable: "adb",
        Serial: null,
        TargetApplication: "settings",
        VisionSocketPath: "/tmp/uniclaw-vision-test.sock",
        DisplayWidth: 1080,
        DisplayHeight: 1920);

    private static readonly SemanticObject Wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");

    private static RunStartRequest Request(string device, bool desired = true)
        => new(
            new SemanticGoalInput("WifiConnectivity", "Enabled", desired),
            [Wifi],
            [SetEnabled],
            DeviceSelector.TryParse(device, out var selector) ? selector : throw new InvalidOperationException("bad selector"));

    /// <summary>Test-side device factory: DeviceSelector → deterministic scripted graph.</summary>
    private static RunGraphFactory ScriptedFactory(params (string DeviceKey, IEnvironment Environment)[] devices)
    {
        var map = devices.ToDictionary(d => d.DeviceKey, d => d.Environment, StringComparer.Ordinal);
        return selector =>
        {
            if (!map.TryGetValue(selector.Key, out var env))
            {
                throw new DeviceSelectorUnsupportedException(selector.Key, "not in test map");
            }

            var criteria = new ElementBindingCriteria(
                [Wifi],
                ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
                ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
            var pages = new PageAnalysisCriteria(
                "settings",
                ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
            var graph = PhysicalHostComposition.BuildRuntimeGraph(env, TestOptions, attach: null, criteria, pages);
            return new RunExecutionGraph(graph.Agent, env);
        };
    }

    /// <summary>WiFi off → SetSwitch(ON) → on (completes).</summary>
    private static ScriptedEnvironment CompletingEnvironment()
        => new(
            "settings", "Settings",
            [
                Screen("Settings", "Wi‑Fi", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "On", true)),
                Screen("On", "Wi‑Fi", true),
            ]);

    /// <summary>WiFi off, switch stuck (SetSwitch never changes the world → BudgetExhausted → Failed).</summary>
    private static ScriptedEnvironment StuckEnvironment()
        => new(
            "settings", "Settings",
            [Screen("Settings", "Wi‑Fi", false, null)]);

    private static ScreenConfig Screen(string name, string label, bool? value, TransitionConfig? transition = null)
        => new(
            name,
            "settings",
            [new ElementConfig(label, null, null, new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), "menuItem"),
             new ElementConfig("", value, transition, new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle")]);

    [Fact]
    public void StartRun_ReturnsRunIdImmediately_AndRunIsImmediatelyObservable()
    {
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            ScriptedFactory(("serial:test-1", CompletingEnvironment())));

        var accepted = coordinator.StartRun(Request("serial:test-1"));

        // DriverHost-owned runId, truthful accepted state (Idle), returned now.
        Assert.False(string.IsNullOrWhiteSpace(accepted.RunId));
        Assert.Equal(RunState.Idle, accepted.RunState);
        Assert.StartsWith("run-", accepted.RunId, StringComparison.Ordinal);

        // No race: registration happened synchronously BEFORE scheduling; the
        // run is immediately legitimate on every existing read surface.
        Assert.Contains(accepted.RunId, observability.RegisteredRunIds);
        var snapshot = observability.GetRunSnapshot(accepted.RunId);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, snapshot.RunState.Classification);
        var page = observability.GetRuntimeEvents(accepted.RunId);
        Assert.DoesNotContain(page.Diagnostics, d => d.Contains("No projected events", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompletedPath_SameRunId_ExistingSurfacesShowCompletedAndRunCompleted()
    {
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            ScriptedFactory(("serial:test-1", CompletingEnvironment())));

        var accepted = coordinator.StartRun(Request("serial:test-1"));

        // Await the coordinator-owned background execution (test handle only).
        await coordinator.Runs[accepted.RunId].Execution;

        var snapshot = observability.GetRunSnapshot(accepted.RunId);
        Assert.Equal(RunState.Completed, snapshot.RunState.Value);
        Assert.Equal("Settings", snapshot.CurrentSemanticPage.Value);

        var events = observability.GetRuntimeEvents(accepted.RunId).Events;
        Assert.Contains(events, e => e.Kind == RuntimeEventKind.RunCompleted);
        Assert.Contains(events, e => e.Kind == RuntimeEventKind.ActionDispatched);
        Assert.All(events, e => Assert.StartsWith($"evt-{accepted.RunId}-", e.EventId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedPath_AcceptedThenFailed_RpcAccept_ExistingSurfacesShowFailed()
    {
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            ScriptedFactory(("serial:test-1", StuckEnvironment())));

        var accepted = coordinator.StartRun(Request("serial:test-1")); // accepted (no rejection)
        await coordinator.Runs[accepted.RunId].Execution;

        var snapshot = observability.GetRunSnapshot(accepted.RunId);
        Assert.Equal(RunState.Failed, snapshot.RunState.Value);
        Assert.NotNull(snapshot.RunState.TruthSource);

        var events = observability.GetRuntimeEvents(accepted.RunId).Events;
        Assert.Contains(events, e => e.Kind == RuntimeEventKind.RunFailed);
    }

    [Fact]
    public void InvalidGoal_Rejected_NoRunCreated()
    {
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            ScriptedFactory(("serial:test-1", CompletingEnvironment())));

        var request = new RunStartRequest(
            new SemanticGoalInput("UnknownObject", "Enabled", true),
            [Wifi],
            [SetEnabled],
            DeviceSelector.TryParse("serial:test-1", out var s) ? s : null!);

        var ex = Assert.Throws<RequestRejectedException>(() => coordinator.StartRun(request));
        Assert.Contains("unknown object", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(observability.RegisteredRunIds); // no phantom run
    }

    [Fact]
    public void UnknownDevice_Rejected_NoRunCreated()
    {
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            ScriptedFactory(("serial:test-1", CompletingEnvironment())));

        var ex = Assert.Throws<RequestRejectedException>(() => coordinator.StartRun(Request("serial:not-in-map")));
        Assert.Contains("not supported", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(observability.RegisteredRunIds);
    }

    [Fact]
    public async Task SameDeviceExclusivity_SecondConcurrentRejected_ReleasedAfterTerminal()
    {
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            ScriptedFactory(("serial:test-1", CompletingEnvironment())));

        var runA = coordinator.StartRun(Request("serial:test-1"));

        var busy = Assert.Throws<RequestRejectedException>(() => coordinator.StartRun(Request("serial:test-1")));
        Assert.Contains("busy", busy.Reason, StringComparison.OrdinalIgnoreCase);

        await coordinator.Runs[runA.RunId].Execution; // terminal → reservation released

        var runC = coordinator.StartRun(Request("serial:test-1")); // accepted again; no leaked lock
        Assert.NotEqual(runA.RunId, runC.RunId);
        Assert.Equal(RunState.Idle, runC.RunState);
    }

    [Fact]
    public async Task DifferentDevices_IsolatedRuns_NoIdentityAliasing()
    {
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            ScriptedFactory(
                ("serial:test-1", CompletingEnvironment()),
                ("serial:test-2", CompletingEnvironment())));

        var runA = coordinator.StartRun(Request("serial:test-1"));
        var runB = coordinator.StartRun(Request("serial:test-2"));

        Assert.NotEqual(runA.RunId, runB.RunId);
        Assert.Equal(2, observability.RegisteredRunIds.Length);

        await Task.WhenAll(coordinator.Runs[runA.RunId].Execution, coordinator.Runs[runB.RunId].Execution);

        var snapshotA = observability.GetRunSnapshot(runA.RunId);
        var snapshotB = observability.GetRunSnapshot(runB.RunId);
        Assert.Equal(RunState.Completed, snapshotA.RunState.Value);
        Assert.Equal(RunState.Completed, snapshotB.RunState.Value);
        Assert.All(observability.GetRuntimeEvents(runA.RunId).Events, e => Assert.Equal(runA.RunId, e.RunId));
        Assert.All(observability.GetRuntimeEvents(runB.RunId).Events, e => Assert.Equal(runB.RunId, e.RunId));
    }

    [Fact]
    public async Task UnexpectedEnvironmentException_NoUnobservedFault_ReservationReleased()
    {
        // An environment whose observe throws drives the Agent entry to throw:
        // the coordinator must convert it to a truthful abnormal outcome, release
        // the reservation, and never leave an unobserved task fault.
        var throwing = new ThrowingEnvironment();
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            ScriptedFactory(("serial:test-1", throwing)));

        var accepted = coordinator.StartRun(Request("serial:test-1"));
        var task = coordinator.Runs[accepted.RunId].Execution;
        await task; // must settle (no unobserved fault)

        // Reservation released: the same device is immediately usable again.
        coordinator.StartRun(Request("serial:test-1"));
    }

    /// <summary>Deterministic IEnvironment whose observe always throws.</summary>
    private sealed class ThrowingEnvironment : IEnvironment
    {
        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("simulated environment failure");

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => throw new InvalidOperationException("simulated environment failure");
    }
}
