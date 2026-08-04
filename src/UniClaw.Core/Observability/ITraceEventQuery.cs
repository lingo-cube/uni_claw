namespace UniClaw.Core.Observability;

/// <summary>
/// ITraceEventQuery — read-only event-stream query facet for trace consumers
/// (verify, diagnose, analyzers). Aligned with the existing read side of
/// <see cref="ITraceQuery"/>: it exposes the same read surface under a
/// role-named facet so the aggregated query context (<see cref="TraceQueries"/>)
/// can name the event stream independently of the span-tree surface.
///
/// Inherits the full read surface of <see cref="ITraceQuery"/> (ITraceService flat
/// reads — GetExecutions/GetTransitions/GetErrors/GetPageTransitions/GetAICalls —
/// plus the span-tree queries and JSON export). No write methods, no session
/// lifecycle methods: consumers SHALL read only through this facet and SHALL NOT
/// hold <see cref="ITraceStorage"/> or <see cref="ITraceRecorder"/> (ISP, design D-6).
/// Any <see cref="ITraceQuery"/> implementation also satisfies this interface.
/// </summary>
public interface ITraceEventQuery : ITraceQuery
{
}
