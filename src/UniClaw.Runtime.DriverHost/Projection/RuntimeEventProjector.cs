using System.Collections.Immutable;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Deterministic projection of Kernel-owned runtime facts into the audited
/// 18-family event vocabulary (design.md §3). Pure function: no I/O, no
/// timers, no mutation of its inputs, never throws on malformed input —
/// unparseable or unclassifiable input is recorded as a truthful diagnostic
/// and the corresponding event is NOT emitted.
///
/// Emission rules (hard):
/// <list type="bullet">
/// <item>Only kinds with <see cref="RuntimeEventKindMetadata.EmittableInSlice"/> == true are ever emitted.</item>
/// <item>No C-class kind is ever inferred from Reason strings, ordering, success, or dispatch.</item>
/// <item><see cref="RuntimeEventEnvelope.ObservationSequence"/> is an Observation.SequenceNumber
/// anchor — never the envelope <see cref="RuntimeEventEnvelope.Sequence"/>.</item>
/// <item><see cref="RuntimeEventEnvelope.CausationId"/> is populated only from a truthful
/// semantic relation; this slice has none, so it stays null.</item>
/// </list>
/// </summary>
public static class RuntimeEventProjector
{
    private const string GoalSpanName = "RunSemanticGoal";
    private const string RefreshSpanName = "RefreshSnapshot";
    private const string GoalTagKey = "goal";

    private const string ViewportExplorationPrefix = "viewport exploration ";
    private const string ViewportSourceSeqMarker = "source-seq=";

    /// <summary>Project one run's events + diagnostics + classification coverage.</summary>
    public static RuntimeEventProjection Project(
        TraceRun trace,
        AgentStateSnapshot agent,
        EvidenceCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(agent);

        var diagnostics = new List<string>();
        var events = new List<RuntimeEventEnvelope>();

        var runId = string.IsNullOrEmpty(agent.RunId) ? (trace.RunId ?? "") : agent.RunId;
        if (!string.IsNullOrEmpty(trace.RunId) && !string.IsNullOrEmpty(agent.RunId) && trace.RunId != agent.RunId)
        {
            diagnostics.Add(
                $"TraceRun.RunId '{trace.RunId}' differs from AgentStateSnapshot.RunId '{agent.RunId}'; " +
                $"projection anchored on AgentStateSnapshot.RunId.");
        }

        var correlationId = trace.TraceId;

        // Phase 1 — container reconcile evidence (span: container.refresh), span start order.
        foreach (var span in trace.Spans
                     .Where(s => s.Name == RefreshSpanName && s.Component == ObservabilityComponent.ContainerRefresh)
                     .OrderBy(s => s.StartOffsetNs))
        {
            events.Add(new RuntimeEventEnvelope
            {
                RunId = runId,
                Kind = RuntimeEventKind.ContainerReconciled,
                CorrelationId = correlationId,
                Payload = new ContainerReconciledPayload(
                    span.SpanId,
                    string.IsNullOrEmpty(span.Outcome) ? ObservabilityOutcome.Unknown : span.Outcome,
                    span.StartOffsetNs,
                    span.DurationNs),
            });
        }

        // Phase 2 — observation evidence from the accepted navigation evidence list.
        foreach (var observation in agent.NavigationEvidence)
        {
            var seq = observation.SequenceNumber;
            var evidenceRefs = ImmutableArray<EvidenceRef>.Empty;
            if (catalog?.TryGetObservationRef(seq, out var observationRef) == true)
            {
                evidenceRefs = [observationRef];
            }

            events.Add(new RuntimeEventEnvelope
            {
                RunId = runId,
                Kind = RuntimeEventKind.ObservationProduced,
                CorrelationId = correlationId,
                ObservationSequence = seq,
                EvidenceRefs = evidenceRefs,
                Payload = new ObservationProducedPayload(seq, observation.ForegroundApplication, observation.Elements.Length),
            });

            events.Add(new RuntimeEventEnvelope
            {
                RunId = runId,
                Kind = RuntimeEventKind.NavigationDecision,
                CorrelationId = correlationId,
                ObservationSequence = seq,
                EvidenceRefs = evidenceRefs,
                Payload = new NavigationDecisionPayload(seq, observation.ForegroundApplication, observation.Elements.Length),
            });
        }

        // Phase 3 — trace-order semantic events (dispatch, viewport decision, trap, recovery, goal evidence).
        foreach (var traceEvent in agent.Trace)
        {
            if (traceEvent.ActionId is not null && traceEvent.Action is not null)
            {
                var actionRefs = ImmutableArray<EvidenceRef>.Empty;
                if (catalog?.TryGetActionRef(traceEvent.ActionId, out var actionRef) == true)
                {
                    actionRefs = [actionRef];
                }

                events.Add(new RuntimeEventEnvelope
                {
                    RunId = runId,
                    Kind = RuntimeEventKind.ActionDispatched,
                    CorrelationId = correlationId,
                    EvidenceRefs = actionRefs,
                    Payload = new ActionDispatchedPayload(
                        traceEvent.ActionId,
                        traceEvent.StepId,
                        traceEvent.ContainerId,
                        DeviceActionText.Describe(traceEvent.Action)),
                });
            }
            else if (traceEvent.Reason is { } reason
                     && reason.StartsWith(ViewportExplorationPrefix, StringComparison.Ordinal))
            {
                if (TryParseViewportReason(reason, out var outcome, out var sourceSeq))
                {
                    events.Add(new RuntimeEventEnvelope
                    {
                        RunId = runId,
                        Kind = RuntimeEventKind.ViewportExplorationDecision,
                        CorrelationId = correlationId,
                        ObservationSequence = sourceSeq,
                        Payload = new ViewportExplorationDecisionPayload(outcome, sourceSeq, traceEvent.ContainerId, traceEvent.StepId),
                    });
                }
                else
                {
                    diagnostics.Add(
                        $"DecisionRecord.Reason matches '{ViewportExplorationPrefix}' prefix but could not be parsed " +
                        $"truthfully; ViewportExplorationDecision NOT emitted. Reason: '{reason}'");
                }
            }
            else if (traceEvent.TrapKind is { } trapKind && traceEvent.TrapScope is { } trapScope)
            {
                long? expected = null;
                long? observed = null;
                if (agent.LastTrap is { } trap
                    && trap.Kind == trapKind
                    && trap.Scope == trapScope)
                {
                    expected = trap.Expected;
                    observed = trap.Observed;
                }

                events.Add(new RuntimeEventEnvelope
                {
                    RunId = runId,
                    Kind = RuntimeEventKind.TrapRaised,
                    CorrelationId = correlationId,
                    ObservationSequence = observed ?? expected,
                    Payload = new TrapRaisedPayload(
                        trapKind,
                        trapScope,
                        expected,
                        observed,
                        traceEvent.ContainerId,
                        traceEvent.StepId),
                });
            }
            else if (traceEvent.RecoveryId is not null)
            {
                events.Add(new RuntimeEventEnvelope
                {
                    RunId = runId,
                    Kind = RuntimeEventKind.RecoveryStarted,
                    CorrelationId = correlationId,
                    Payload = new RecoveryStartedPayload(
                        traceEvent.RecoveryId,
                        traceEvent.Reason,
                        traceEvent.ContainerId,
                        traceEvent.StepId),
                });
            }
            else if (traceEvent.RunState == RunState.Completed && traceEvent.Reason is not null)
            {
                // Partial goal evidence: State=Completed + Reason only.
                events.Add(new RuntimeEventEnvelope
                {
                    RunId = runId,
                    Kind = RuntimeEventKind.GoalEvidenceProduced,
                    CorrelationId = correlationId,
                    Payload = new GoalEvidenceProducedPayload(traceEvent.Reason, IsPartial: true),
                });
            }
        }

        // Phase 4 — terminal state evidence (exactly one, when terminal).
        if (agent.State == RunState.Completed)
        {
            events.Add(new RuntimeEventEnvelope
            {
                RunId = runId,
                Kind = RuntimeEventKind.RunCompleted,
                CorrelationId = correlationId,
                Payload = new RunCompletedPayload(agent.Reason ?? "(no reason recorded)"),
            });
        }
        else if (agent.State == RunState.Failed)
        {
            events.Add(new RuntimeEventEnvelope
            {
                RunId = runId,
                Kind = RuntimeEventKind.RunFailed,
                CorrelationId = correlationId,
                Payload = new RunFailedPayload(agent.Reason ?? "(no reason recorded)"),
            });
        }

        return new RuntimeEventProjection
        {
            RunId = runId,
            Events = [.. events],
            Diagnostics = [.. diagnostics],
            ClassificationCoverage = [.. RuntimeEventKindTable.All],
        };
    }

    /// <summary>
    /// Parse the classified viewport-exploration Reason prefix:
    /// <c>viewport exploration {outcome}: source-seq={N}; {rest}</c>
    /// Returns false (and does NOT emit) unless outcome AND source-seq parse.
    /// </summary>
    internal static bool TryParseViewportReason(string reason, out string outcome, out long sourceSeq)
    {
        outcome = "";
        sourceSeq = 0;

        if (!reason.StartsWith(ViewportExplorationPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = reason.Substring(ViewportExplorationPrefix.Length);
        var colon = rest.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        outcome = rest[..colon].Trim();
        if (outcome.Length == 0)
        {
            return false;
        }

        var afterColon = rest[(colon + 1)..];
        var markerIndex = afterColon.IndexOf(ViewportSourceSeqMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        var seqText = afterColon[(markerIndex + ViewportSourceSeqMarker.Length)..].TrimStart();
        var end = seqText.Length;
        for (var i = 0; i < seqText.Length; i++)
        {
            if (seqText[i] == ';' || seqText[i] == ' ' || seqText[i] == ',')
            {
                end = i;
                break;
            }
        }

        return long.TryParse(seqText[..end], out sourceSeq);
    }
}

/// <summary>
/// Deterministic, coordinate-free action description (design.md §3 —
/// ActionDescription must never carry bounds/coordinates).
/// </summary>
public static class DeviceActionText
{
    /// <summary>Describes an action without coordinates or bounds.</summary>
    public static string Describe(DeviceAction action) => action switch
    {
        DeviceAction.LaunchApp launch => $"LaunchApp({launch.ApplicationId})",
        DeviceAction.Tap tap => $"Tap(index={tap.TargetElementIndex})",
        DeviceAction.SetSwitch setSwitch => $"SetSwitch(index={setSwitch.TargetElementIndex}, target={setSwitch.TargetState})",
        DeviceAction.ScrollForward => "ScrollForward()",
        _ => action.GetType().Name,
    };
}

/// <summary>Result of one projection (design.md §3).</summary>
public sealed record RuntimeEventProjection
{
    /// <summary>Run identity for this projection.</summary>
    public string RunId { get; init; } = "";

    /// <summary>Projected events (no EventId/Sequence — assigned by the append-only store).</summary>
    public ImmutableArray<RuntimeEventEnvelope> Events { get; init; } = [];

    /// <summary>Truthful projection diagnostics (gaps, parse failures, mismatch warnings).</summary>
    public ImmutableArray<string> Diagnostics { get; init; } = [];

    /// <summary>Full audited classification coverage (all 18 kinds with reasons).</summary>
    public ImmutableArray<RuntimeEventKindMetadata> ClassificationCoverage { get; init; } = [];
}
