using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Observability;

/// <summary>
/// Deterministic fixtures for dsh-kernel-read-only-observability tests.
/// Built from public models only — no live Agent, no Container internals.
/// </summary>
public static class ReadOnlyObservabilityFixtures
{
    public const string RunId = "run-1";
    public const string TraceId = "trace-1";

    /// <summary>A completed run with dispatch, navigation, and a viewport-exploration decision.</summary>
    public static AgentStateSnapshot CompletedRun()
        => new()
        {
            RunId = RunId,
            State = RunState.Completed,
            Reason = "goal satisfied: WifiConnectivity.Enabled=true",
            Belief = new WorldBelief("Settings", 1.0f, "observed WifiConnectivity.Enabled=true", 7),
            LastTrap = null,
            Trace =
            [
                new TraceEvent(RunId) { RunState = RunState.Idle },
                new TraceEvent(RunId) { RunState = RunState.Initializing },
                new TraceEvent(RunId)
                {
                    ContainerId = "Settings",
                    Reason = "navigation decision: Settings (anchor 'Wi‑Fi')",
                },
                new TraceEvent(RunId) { RunState = RunState.Running },
                new TraceEvent(RunId)
                {
                    ContainerId = "Settings",
                    StepId = "step-1",
                    ActionId = "Action-1",
                    Action = new DeviceAction.SetSwitch(1, true),
                },
                new TraceEvent(RunId)
                {
                    ContainerId = "Settings",
                    StepId = "step-2",
                    Reason = "viewport exploration exhausted: source-seq=7; bounds evidence satisfied",
                },
                new TraceEvent(RunId)
                {
                    RunState = RunState.Completed,
                    Reason = "goal satisfied: WifiConnectivity.Enabled=true",
                },
            ],
            NavigationEvidence =
            [
                new Observation([new ObservedElement("Wi‑Fi", true, 1)], "Settings", 1),
                new Observation([new ObservedElement("Wi‑Fi", true, 1)], "Settings", 7),
            ],
        };

    /// <summary>A failed run with a trap and recovery start.</summary>
    public static AgentStateSnapshot FailedRunWithTrapAndRecovery()
        => new()
        {
            RunId = RunId,
            State = RunState.Failed,
            Reason = "trap: StateMismatch (observed=false, expected=true)",
            Belief = new WorldBelief("Settings", 0.4f, "observed WifiConnectivity.Enabled=false", 7),
            LastTrap = new Trap(
                TrapKind.StateMismatch,
                TrapScope.Agent,
                expected: 3,
                observed: 7,
                "agent",
                "observed=false expected=true",
                new DeviceAction.SetSwitch(1, true)),
            Trace =
            [
                new TraceEvent(RunId) { RunState = RunState.Running },
                new TraceEvent(RunId)
                {
                    ContainerId = "Settings",
                    StepId = "step-1",
                    ActionId = "Action-1",
                    Action = new DeviceAction.SetSwitch(1, true),
                },
                new TraceEvent(RunId)
                {
                    ContainerId = "Settings",
                    StepId = "step-2",
                    TrapKind = TrapKind.StateMismatch,
                    TrapScope = TrapScope.Agent,
                    Reason = "trap: StateMismatch (observed=false, expected=true)",
                },
                new TraceEvent(RunId)
                {
                    RecoveryId = "recovery-1",
                    ContainerId = "Settings",
                    Reason = "recovery started: observe settings page",
                },
                new TraceEvent(RunId) { RunState = RunState.Failed, Reason = "trap: StateMismatch (observed=false, expected=true)" },
            ],
            NavigationEvidence =
            [
                new Observation([new ObservedElement("Wi‑Fi", false, 1)], "Settings", 7),
            ],
        };

    /// <summary>TraceRun with one goal span, one refresh span, one lowered-action span.</summary>
    public static TraceRun CompletedTrace()
        => new()
        {
            TraceRunId = "trace-run-1",
            TraceId = TraceId,
            RunId = RunId,
            Spans =
            [
                new TraceSpan
                {
                    SpanId = "s1",
                    Name = "RunSemanticGoal",
                    Layer = "AGENT",
                    Component = "agent.execution",
                    StartOffsetNs = 0,
                    DurationNs = 100,
                    Outcome = "SUCCEEDED",
                    Attributes = [new TraceSpanAttribute { Key = "goal", Value = "WifiConnectivity.Enabled=true" }],
                },
                new TraceSpan
                {
                    SpanId = "s2",
                    Name = "RefreshSnapshot",
                    Layer = "CONTAINER",
                    Component = "container.refresh",
                    StartOffsetNs = 10,
                    DurationNs = 5,
                    Outcome = "SUCCEEDED",
                },
                new TraceSpan
                {
                    SpanId = "s3",
                    Name = "LoweredAction",
                    Layer = "TRAVERSAL",
                    Component = "traversal.execution",
                    StartOffsetNs = 20,
                    DurationNs = 8,
                    Outcome = "SUCCEEDED",
                },
            ],
        };

    /// <summary>Empty trace — spans may legitimately be absent (no listener / structural gaps).</summary>
    public static TraceRun EmptyTrace()
        => new() { TraceRunId = "trace-empty", TraceId = TraceId, RunId = RunId };
}
