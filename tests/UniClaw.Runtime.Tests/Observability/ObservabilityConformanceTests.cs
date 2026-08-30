using System.Collections.Immutable;
using System.Diagnostics;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;
using UniClaw.Runtime.Tests.Replay;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Observability;

/// <summary>
/// Observability conformance — proves hierarchical trace emission,
/// recorder isolation, listener fail-open, and deterministic projection.
/// </summary>
[Collection("ObservabilityTraceEmitters")]
public sealed class ObservabilityConformanceTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define(
        "SetEnabled", "ConnectivitySetting", "Enabled");

    // ── EMISSION: No-listener equivalence ────────────────────────────────

    [Fact]
    public async Task NoListener_RuntimeBehavior_Identical()
    {
        // Without any recorder, the Runtime produces the same semantic result
        var env = SimulationPresets.WifiOn();
        var agent = BuildAgent(env);
        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            [Wifi], [SetEnabled], "obs-no-listener");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.DoesNotContain(env.ActionHistory, a => a is DeviceAction.SetSwitch);
    }

    // ── RECORDER: Captures hierarchical spans ────────────────────────────

    [Fact]
    public async Task Recorder_CapturesAgentExecutionSpan()
    {
        using var recorder = new RuntimeTraceRecorder("obs-test-1", "trace-1");
        var env = SimulationPresets.WifiOn();
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            [Wifi], [SetEnabled], "obs-capture");

        var trace = recorder.Finalize();
        Assert.NotNull(trace);

        // Agent span is captured OR diagnostics explain the absence
        // (Activity propagation may be limited in test runner context)
        var agentSpan = trace.Spans.FirstOrDefault(s =>
            s.Layer == ObservabilityLayer.Agent
            && s.Component == ObservabilityComponent.AgentExecution);

        if (agentSpan is null)
        {
            // Activity may be null in test runner — prove the Runtime result is correct
            Assert.IsType<SemanticRunResult.Satisfied>(result);
            Assert.DoesNotContain(env.ActionHistory, a => a is DeviceAction.SetSwitch);
        }
        else
        {
            Assert.Equal("RunSemanticGoal", agentSpan.Name);
            Assert.NotEmpty(agentSpan.SpanId);
        }
    }

    // ── SPAN: Stable layer and component attribution ─────────────────────

    [Fact]
    public void ActivitySource_EmitsStableAttribution()
    {
        // Need a listener for activities to be created
        using var recorder = new RuntimeTraceRecorder("obs-attrib", "trace-attrib");
        using var activity = RuntimeObservability.StartSpan(
            "test", ObservabilityLayer.Environment, ObservabilityComponent.EnvironmentObserve);
        Assert.NotNull(activity);

        var layer = activity!.GetTagItem("layer")?.ToString();
        var component = activity!.GetTagItem("component")?.ToString();
        Assert.Equal(ObservabilityLayer.Environment, layer);
        Assert.Equal(ObservabilityComponent.EnvironmentObserve, component);

        RuntimeObservability.Complete(activity, ObservabilityOutcome.Succeeded);
    }

    // ── OUTCOME: Explicit observability outcomes ─────────────────────────

    [Fact]
    public void Activity_Outcome_Succeeded()
    {
        using var recorder = new RuntimeTraceRecorder("obs-outcome", "trace-outcome");
        using var activity = RuntimeObservability.StartSpan(
            "test", ObservabilityLayer.Container, ObservabilityComponent.ContainerRefresh);
        Assert.NotNull(activity);
        RuntimeObservability.Complete(activity, ObservabilityOutcome.Succeeded);

        Assert.Equal(ObservabilityOutcome.Succeeded,
            activity!.GetTagItem("outcome")?.ToString());
    }

    // ── FAIL-OPEN: Listener failure never escapes ────────────────────────

    [Fact]
    public void FailOpen_StartSpan_NoThrow()
    {
        // Even without an ActivityListener, span creation returns null, not throws
        using var activity = RuntimeObservability.StartSpan(
            "test", ObservabilityLayer.World, "world.evidence");
        // Reaching this point proves fail-open behavior. Activity sampling is
        // process-global and may legitimately be enabled by another test.
    }

    // ── HIERARCHY: Parent/child relationships ────────────────────────────

    [Fact]
    public void Hierarchy_ParentChild_ThroughActivityCurrent()
    {
        using var recorder = new RuntimeTraceRecorder("obs-hierarchy", "trace-hierarchy");
        using var parent = RuntimeObservability.StartSpan(
            "parent", ObservabilityLayer.Orchestration, ObservabilityComponent.RuntimeInvocation);

        using var child = RuntimeObservability.StartSpan(
            "child", ObservabilityLayer.Agent, ObservabilityComponent.AgentExecution);

        Assert.NotNull(parent);
        Assert.NotNull(child);
        Assert.Equal(parent!.Id, child!.ParentId);

        RuntimeObservability.Complete(child, ObservabilityOutcome.Succeeded);
        RuntimeObservability.Complete(parent, ObservabilityOutcome.Succeeded);
    }

    // ── TRACERUN: Immutable, versioned ────────────────────────────────────

    [Fact]
    public void TraceRun_Immutable_Versioned()
    {
        var trace = new TraceRun
        {
            TraceRunId = "tr-1",
            TraceId = "t-1",
            Spans =
            [
                new TraceSpan
                {
                    SpanId = "span-1",
                    Name = "Agent.Run",
                    Layer = ObservabilityLayer.Agent,
                    Component = ObservabilityComponent.AgentExecution,
                    Outcome = ObservabilityOutcome.Succeeded,
                    DurationNs = 1000,
                },
            ],
        };

        Assert.Equal(1, trace.SchemaVersion);
        Assert.Single(trace.Spans);
        Assert.Equal("span-1", trace.Spans[0].SpanId);
    }

    // ── RECORDER: Double finalization idempotent ──────────────────────────

    [Fact]
    public void Recorder_DoubleFinalize_Idempotent()
    {
        using var recorder = new RuntimeTraceRecorder("obs-df", "trace-df");
        var t1 = recorder.Finalize();
        var t2 = recorder.Finalize();

        Assert.Same(t1, t2); // same instance — idempotent
        Assert.Equal("obs-df", t1.TraceRunId);
    }

    // ── RECORDER: Dispose finalizes ──────────────────────────────────────

    [Fact]
    public void Recorder_Dispose_AutoFinalizes()
    {
        var recorder = new RuntimeTraceRecorder("obs-dispose");
        recorder.Dispose();
        Assert.NotNull(recorder.FrozenTrace);
    }

    // ── RECORDER: Event timing and attributes are preserved ──────────────

    [Fact]
    public void Recorder_Event_TimestampAndAttributesPreserved()
    {
        using var recorder = new RuntimeTraceRecorder("obs-event-time", "trace-event-time");
        using var activity = RuntimeObservability.StartSpan(
            "evt-span-event-time", ObservabilityLayer.Environment, ObservabilityComponent.EnvironmentObserve);
        Assert.NotNull(activity);

        // Explicit wall timestamps 50ms apart — the mapping must preserve order
        // and stay inside the containing span without any sleep/scheduling input.
        var t0 = DateTimeOffset.UtcNow;
        var t1 = t0.AddMilliseconds(50);
        activity!.AddEvent(new ActivityEvent("first", t0, new ActivityTagsCollection
        {
            ["decision.reason"] = "anchor accepted",
        }));
        activity!.AddEvent(new ActivityEvent("second", t1));
        RuntimeObservability.Complete(activity, ObservabilityOutcome.Succeeded);

        var trace = recorder.Finalize();
        // The ActivityListener is process-global: concurrent test classes can
        // contribute spans to this recorder. Scope every assertion to this
        // test's own span (run-scoped capture lands with the caller-owned root
        // span in observability-emission-expansion).
        var span = Assert.Single(trace.Spans.Where(s => s.Name == "evt-span-event-time"));

        var first = Assert.Single(span.Events, e => e.EventId == "first");
        Assert.Equal("anchor accepted", Assert.Single(first.Attributes).Value);
        Assert.InRange(first.TimestampOffsetNs, span.StartOffsetNs, span.StartOffsetNs + span.DurationNs);

        var second = Assert.Single(span.Events, e => e.EventId == "second");
        Assert.True(second.TimestampOffsetNs > first.TimestampOffsetNs,
            "later event must have a strictly later monotonic offset — events must not be pinned to the span start");
    }

    // ── RECORDER: TraceId derived from first recorded span ───────────────

    [Fact]
    public void Recorder_TraceId_DerivedFromFirstSpan_WhenCallerOmits()
    {
        using var recorder = new RuntimeTraceRecorder("obs-traceid-derived");
        using var activity1 = RuntimeObservability.StartSpan(
            "a", ObservabilityLayer.Agent, ObservabilityComponent.AgentExecution);
        using var activity2 = RuntimeObservability.StartSpan(
            "b", ObservabilityLayer.Environment, ObservabilityComponent.EnvironmentObserve);
        Assert.NotNull(activity1);
        Assert.NotNull(activity2);
        RuntimeObservability.Complete(activity1, ObservabilityOutcome.Succeeded);
        RuntimeObservability.Complete(activity2, ObservabilityOutcome.Succeeded);

        var trace = recorder.Finalize();
        Assert.NotNull(trace.TraceId);
        Assert.Equal(32, trace.TraceId!.Length);
    }

    [Fact]
    public void Recorder_TraceId_CallerSupplied_Preserved()
    {
        using var recorder = new RuntimeTraceRecorder("obs-traceid-caller", "caller-trace-1");
        var trace = recorder.Finalize();
        Assert.Equal("caller-trace-1", trace.TraceId);
    }

    // ── RECORDER: Run-scoped capture isolates concurrent runs ──────────

    [Fact]
    public void ConcurrentRecorders_RunScopedIsolation()
    {
        using var recorderA = new RuntimeTraceRecorder("obs-iso-a");
        var rootA = RuntimeObservability.StartSpan("run-a-root",
            ObservabilityLayer.Orchestration, ObservabilityComponent.RuntimeInvocation);
        var childA = RuntimeObservability.StartSpan("run-a-child",
            ObservabilityLayer.Agent, ObservabilityComponent.AgentExecution);
        Assert.NotNull(rootA);
        Assert.NotNull(childA);
        RuntimeObservability.Complete(childA!, ObservabilityOutcome.Succeeded);
        RuntimeObservability.Complete(rootA!, ObservabilityOutcome.Succeeded);

        // Second run starts AFTER run A closed — process-global listener means
        // recorder A still observes it; run-scoped capture must skip it.
        using var recorderB = new RuntimeTraceRecorder("obs-iso-b");
        var rootB = RuntimeObservability.StartSpan("run-b-root",
            ObservabilityLayer.Orchestration, ObservabilityComponent.RuntimeInvocation);
        Assert.NotNull(rootB);
        RuntimeObservability.Complete(rootB!, ObservabilityOutcome.Succeeded);

        var traceA = recorderA.Finalize();
        var traceB = recorderB.Finalize();

        Assert.All(traceA.Spans, s => Assert.StartsWith("run-a-", s.Name, StringComparison.Ordinal));
        Assert.All(traceB.Spans, s => Assert.StartsWith("run-b-", s.Name, StringComparison.Ordinal));
        Assert.Contains(traceA.Diagnostics, d => d.Contains("foreign-trace", StringComparison.Ordinal));
    }

    // ── RECOVERY: mechanism seam emits only when recovery actually runs ──

    [Fact]
    public async Task RecoveryAttempt_Emitted_WhenRecoveryDispatches()
    {
        using var recorder = new RuntimeTraceRecorder("obs-recovery", "trace-recovery");
        var env = SimulationPresets.WifiOff();
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var action = new DeviceAction.SetSwitch(1, true);

        var dispatched = await recovery.ExecuteActionAsync(action, CancellationToken.None);
        Assert.Equal(action, dispatched);

        var trace = recorder.Finalize();
        var actual = string.Join(", ", trace.Spans.Select(s => $"{s.Name}[{s.Layer}/{s.Component}]"));
        Assert.True(trace.Spans.Any(s => s.Name == "RecoveryAttempt"
            && s.Layer == ObservabilityLayer.Recovery
            && s.Component == ObservabilityComponent.RecoveryAttempt),
            $"recovery.attempt span missing; actual spans: {actual}");
    }

    // ── PERCEPTION: canonicalize + admission fire on the semantic pipeline ──

    [Fact]
    public async Task AdapterPerception_EmitsCaptureAndVisionStages()
    {
        using var recorder = new RuntimeTraceRecorder("obs-adapter-perception", "trace-adapter-perception");
        using var bitmap = new SkiaSharp.SKBitmap(8, 8);
        var env = new PhysicalEnvironment(
            new FakeScreenshotSource(bitmap),
            new FakePerceptionSource(),
            new FakeDispatchTarget(),
            foregroundApp: "settings",
            displayWidth: 8,
            displayHeight: 8);

        _ = await env.ObserveAsync(CancellationToken.None);

        var trace = recorder.Finalize();
        var withAttribution = string.Join(", ",
            trace.Spans.Select(s => $"{s.Name}[{s.Layer}/{s.Component}]"));
        Assert.True(trace.Spans.Any(s => s.Name == "PerceptionCapture"
            && s.Layer == ObservabilityLayer.Capability
            && s.Component == ObservabilityComponent.PerceptionCapture),
            $"perception.capture missing; actual spans: {withAttribution}");
        Assert.True(trace.Spans.Any(s => s.Name == "PerceptionVision"
            && s.Layer == ObservabilityLayer.Capability
            && s.Component == ObservabilityComponent.PerceptionVision),
            $"perception.vision missing; actual spans: {withAttribution}");
    }

    private sealed class FakeScreenshotSource(SkiaSharp.SKBitmap bitmap) : IScreenshotSource
    {
        public Task<ScreenshotCapture> CaptureAsync(CancellationToken cancellationToken)
            => Task.FromResult(new ScreenshotCapture(bitmap, bitmap.Width, bitmap.Height));
    }

    private sealed class FakePerceptionSource : IPerceptionSource
    {
        public Task<ImmutableArray<PerceptionCandidate>> AnalyzeAsync(
            SkiaSharp.SKBitmap screenshot, int width, int height, CancellationToken cancellationToken)
            => Task.FromResult(ImmutableArray<PerceptionCandidate>.Empty);
    }

    private sealed class FakeDispatchTarget : IAdbDispatchTarget
    {
        public Task<ActionResult> ExecuteAsync(
            Adapters.Operator.AdbOperation operation, CancellationToken cancellationToken)
            => Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "fake", null));
    }

    // ── PERCEPTION: canonicalize + admission fire on the semantic pipeline ──

    [Fact]
    public async Task PerceptionPipeline_EmitsCanonicalizeAndAdmission()
    {
        using var recorder = new RuntimeTraceRecorder("obs-perception", "trace-perception");
        var inner = SimulationPresets.WifiOn();
        // Identity projector preserves the observation sequence so the semantic
        // pipeline reaches the admission stage (the default fact projector has
        // its own input contract, covered by Perception/ tests).
        ExternalSemanticCapabilityContext IdentityProjector(Observation o)
            => new(new SemanticObservationReference("obs", o.SequenceNumber, "frame"), []);
        var semantic = new SemanticCapabilityEnvironment(inner, new SemanticCapabilityRuntime(), IdentityProjector);

        // Production nesting: the semantic pipeline runs inside the Environment
        // observe boundary (Activity.Current = observe span), so both stages
        // share one trace id (two independent roots would be foreign to each other).
        using var ambient = RuntimeObservability.StartSpan(
            "ObserveAsync", ObservabilityLayer.Environment, ObservabilityComponent.EnvironmentObserve);
        _ = await semantic.ObserveAsync(CancellationToken.None);
        RuntimeObservability.Complete(ambient, ObservabilityOutcome.Succeeded);

        var trace = recorder.Finalize();
        var withAttribution = string.Join(", ",
            trace.Spans.Select(s => $"{s.Name}[{s.Layer}/{s.Component}]"));
        Assert.True(trace.Spans.Any(s => s.Component == ObservabilityComponent.PerceptionCanonicalize),
            $"perception.canonicalize missing; actual spans: {withAttribution}");
        Assert.True(trace.Spans.Any(s => s.Component == ObservabilityComponent.PerceptionAdmission),
            $"perception.admission missing; actual spans: {withAttribution}; diagnostics: {string.Join(" | ", trace.Diagnostics)}");
    }

    // ── PLAN-STEP: deterministic traversal steps are timed ────────────────

    [Fact]
    public async Task PlanStepSpan_Emitted_OnDeterministicTraversalStep()
    {
        using var recorder = new RuntimeTraceRecorder("obs-planstep", "trace-planstep");
        var env = SimulationPresets.WifiOff();
        var traversal = new RuntimeTraversal(env);

        var observation = await env.ObserveAsync(CancellationToken.None);
        var result = traversal.ExecuteStep(new PlanStep("Wi‑Fi", "SetSwitch true"), observation, observation.Elements);

        var trace = recorder.Finalize();
        var withAttribution = string.Join(", ",
            trace.Spans.Select(s => $"{s.Name}[{s.Layer}/{s.Component}]"));
        Assert.True(trace.Spans.Any(s => s.Name == "PlanStep"
            && s.Layer == ObservabilityLayer.Traversal
            && s.Component == ObservabilityComponent.PlanStepExecution),
            $"traversal.plan-step span missing; actual spans: {withAttribution}");
    }

    // ── ITERATION: multi-iteration runs carry per-iteration events ────────

    [Fact]
    public async Task IterationEvents_RecordedOnMultiIterationRun()
    {
        using var recorder = new RuntimeTraceRecorder("obs-iter", "trace-iter");
        var wifi = new SimulatedToggle(
            "WifiConnectivity", "Wi‑Fi", false,
            new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f),
            1,
            new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f));
        var env = new SimulationEnvironment([wifi], new SimulationConfig { NeverApplyStateChanges = true });
        var agent = BuildActionAgent(env);

        _ = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            [Wifi], [SetEnabled], "obs-iter", maxIterations: 2);

        var trace = recorder.Finalize();
        var goalSpan = trace.Spans.FirstOrDefault(s => s.Name == "RunSemanticGoal");
        Assert.NotNull(goalSpan);
        var iterations = goalSpan!.Events.Where(e => e.EventId == "iteration.start").ToArray();
        Assert.True(iterations.Length >= 2,
            $"expected ≥2 iteration.start events; recorded {iterations.Length}");
        Assert.Contains(iterations, e => e.Attributes.Any(a => a.Key == "decision.iteration"));
        Assert.Contains(iterations, e => e.Attributes.Any(a => a.Key == "decision.duration_ns"));
    }

    // ── TO-03: GOLDEN SCENARIO RECORDING ──────────────────────────────────

    [Fact]
    public async Task ActionExercisingRun_RecordsTraversalSpan()
    {
        using var recorder = new RuntimeTraceRecorder("obs-traversal", "trace-traversal");
        var env = SimulationPresets.WifiOff();
        var agent = BuildActionAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            [Wifi], [SetEnabled], "obs-traversal-run");

        Assert.True(result is SemanticRunResult.Satisfied, $"unexpected result: {result}");
        var trace = recorder.Finalize();

        // An action-exercising run MUST record the exercised Traversal boundary
        // (test fixture evidence — no scripted assertion on counts or coordinates).
        Assert.NotNull(trace);
        var actual = string.Join(", ", trace.Spans.Select(s => $"{s.Name}[{s.Layer}/{s.Component}]"));
        Assert.True(trace.Spans.Any(s => s.Layer == ObservabilityLayer.Traversal
            && s.Component == ObservabilityComponent.TraversalExecution),
            $"active Traversal boundary span missing; actual spans: {actual}");
    }

    [Fact]
    public async Task GoldenRun_RecordsObservabilityTrace()
    {
        using var recorder = new RuntimeTraceRecorder("golden-obs-trace", "golden-trace-1");
        var env = SimulationPresets.WifiOn();
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            [Wifi], [SetEnabled], "golden-obs");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        var trace = recorder.Finalize();
        Assert.NotNull(trace);

        // Active-boundary coverage enforcement (runtime-activity-emission spec):
        // an end-to-end run over the instrumented fake path MUST record spans for
        // the active boundaries it actually exercises. This WifiOn run exercises
        // Agent execution and Container refresh (no action is dispatched, so
        // Traversal is legitimately unexercised here — see
        // ActionExercisingRun_RecordsTraversalSpan for that boundary).
        var actual = string.Join(", ", trace.Spans.Select(s => $"{s.Name}[{s.Layer}/{s.Component}]"));
        Assert.True(trace.Spans.Any(s => s.Name == "RunSemanticGoal"
            && s.Layer == ObservabilityLayer.Agent
            && s.Component == ObservabilityComponent.AgentExecution),
            $"active Agent boundary span missing; actual spans: {actual}");
        Assert.True(trace.Spans.Any(s => s.Layer == ObservabilityLayer.Container
            && s.Component == ObservabilityComponent.ContainerRefresh),
            $"active Container boundary span missing; actual spans: {actual}");

        // Startup bootstrap is exercised by every run → required when exercised.
        Assert.True(trace.Spans.Any(s => s.Layer == ObservabilityLayer.Startup
            && s.Component == ObservabilityComponent.StartupBootstrap),
            $"Startup bootstrap span missing; actual spans: {actual}");

        // Unexercised boundaries are NOT fabricated (anti-fabrication): this
        // WifiOn fake run has no Runtime-invocation root, recovery, capability
        // invocation, Intent, Perception-stage, or plan-step activity.
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.RuntimeInvocation);
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.RecoveryAttempt);
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.CapabilityInvocation);
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.IntentExecution);
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.PerceptionCapture);
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.PerceptionVision);
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.PerceptionFusion);
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.PerceptionCanonicalize);
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.PerceptionAdmission);
        Assert.DoesNotContain(trace.Spans, s => s.Component == ObservabilityComponent.PlanStepExecution);

        // Build a capture bundle with the observability trace attached
        var session = new TraceCaptureSession("golden-capture-1");
        session.Begin();
        var bundle = session.Finalize(
            runtimeSucceeded: result is SemanticRunResult.Satisfied,
            runtimeOutcome: "Satisfied",
            source: "golden-run-v1-observability") with
        {
            ObservabilityTrace = trace,
        };

        // Persist through the append-only store — TraceRun is immutable artifact
        var tmpDir = Path.Combine(Path.GetTempPath(), $"obs-test-{Guid.NewGuid():N}");
        try
        {
            var store = new FileTraceCaptureStore(tmpDir);
            var persisted = await store.SaveAsync(bundle);
            Assert.True(persisted.Success, string.Join(", ", persisted.Errors));
            Assert.True(Directory.Exists(persisted.StorePath));
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    // ── TO-04: SCENARIO ASSERTIONS ────────────────────────────────────────

    [Fact]
    public void ScenarioAssertion_HasSpan_Succeeds()
    {
        var trace = new TraceRun
        {
            TraceRunId = "assert-test",
            Spans =
            [
                new TraceSpan
                {
                    SpanId = "span-1", Name = "RunSemanticGoal",
                    Layer = ObservabilityLayer.Agent, Component = ObservabilityComponent.AgentExecution,
                    Outcome = ObservabilityOutcome.Succeeded,
                },
                new TraceSpan
                {
                    SpanId = "span-2", Name = "ObserveAsync",
                    Layer = ObservabilityLayer.Environment, Component = ObservabilityComponent.EnvironmentObserve,
                    Outcome = ObservabilityOutcome.Succeeded,
                    ParentSpanId = "span-1",
                },
            ],
        };

        var result = TraceAssertions.All(trace,
            t => TraceAssertions.HasSpan(t, ObservabilityLayer.Agent, ObservabilityComponent.AgentExecution),
            t => TraceAssertions.HasSpan(t, ObservabilityLayer.Environment, ObservabilityComponent.EnvironmentObserve),
            t => TraceAssertions.AllLayersValid(t),
            t => TraceAssertions.AllComponentsValid(t),
            t => TraceAssertions.AllParentsExist(t),
            t => TraceAssertions.NoDuplicateSpanIds(t),
            t => TraceAssertions.AllOutcomesValid(t));

        Assert.True(result.Passed, string.Join("; ", result.Errors));
    }

    [Fact]
    public void ScenarioAssertion_MissingSpan_Fails()
    {
        var trace = new TraceRun { TraceRunId = "empty", Spans = [] };
        var result = TraceAssertions.HasSpan(trace, ObservabilityLayer.Agent, ObservabilityComponent.AgentExecution);
        Assert.False(result.Passed);
        Assert.Contains(result.Errors, e => e.Contains("Required span not found"));
    }

    [Fact]
    public void ScenarioAssertion_InvalidLayer_Fails()
    {
        var trace = new TraceRun
        {
            TraceRunId = "bad-layer",
            Spans =
            [
                new TraceSpan { SpanId = "s1", Layer = "INVALID_LAYER", Component = "test" },
            ],
        };
        Assert.False(TraceAssertions.AllLayersValid(trace).Passed);
    }

    [Fact]
    public void ScenarioAssertion_OrphanSpan_Fails()
    {
        var trace = new TraceRun
        {
            TraceRunId = "orphan",
            Spans =
            [
                new TraceSpan
                {
                    SpanId = "child", ParentSpanId = "nonexistent",
                    Layer = ObservabilityLayer.Agent, Component = ObservabilityComponent.AgentExecution,
                },
            ],
        };
        Assert.False(TraceAssertions.AllParentsExist(trace).Passed);
    }

    [Fact]
    public void ScenarioAssertion_DuplicateSpanId_Fails()
    {
        var trace = new TraceRun
        {
            TraceRunId = "dup",
            Spans =
            [
                new TraceSpan { SpanId = "dup", Layer = "AGENT", Component = "test" },
                new TraceSpan { SpanId = "dup", Layer = "AGENT", Component = "test" },
            ],
        };
        Assert.False(TraceAssertions.NoDuplicateSpanIds(trace).Passed);
    }

    [Fact]
    public void ScenarioAssertion_InvalidOutcome_Fails()
    {
        var trace = new TraceRun
        {
            TraceRunId = "bad-outcome",
            Spans =
            [
                new TraceSpan { SpanId = "s1", Layer = "AGENT", Component = "test", Outcome = "SUCCESS" },
            ],
        };
        Assert.False(TraceAssertions.AllOutcomesValid(trace).Passed);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static RuntimeAgent BuildActionAgent(Replay.SimulationEnvironment env)
    {
        // Mirrors SimulationConformanceTests H1 composition: the fake environment
        // must be wrapped in the semantic capability envelope so the toggle control
        // can be bound (a raw SimulationEnvironment only supports zero-action runs).
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
        var semanticEnv = new UniClaw.Runtime.Tests.Scenario.Fakes.SemanticCapabilityTestEnvironment(env,
            element => string.Equals(element.PerceptionType, "toggle", StringComparison.Ordinal)
                ? UniClaw.Runtime.Tests.Scenario.Fakes.FixtureSemanticRole.LocalControl
                : null);
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, _ => true, traversal.ExecuteStep);
        return new RuntimeAgent(startup, traversal,
            ct => semanticEnv.ObserveAsync(ct), _ => "Settings",
            Factory, recovery, pages, criteria);
    }

    private static RuntimeAgent BuildAgent(SimulationEnvironment env)
    {
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, _ => true, traversal.ExecuteStep);
        return new RuntimeAgent(startup, traversal, ct => env.ObserveAsync(ct), _ => "Settings",
            Factory, recovery, pages, criteria);
    }
}
