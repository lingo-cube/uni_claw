using UniClaw.Core.Observability;

namespace UniClaw.TraceTool;

/// <summary>
/// Config-driven query assembly. Builds <see cref="TraceQueries"/> from a loaded
/// <see cref="TraceRun"/>. MVP: CLI params are the config (positional args);
/// assembly function shape retained for future --backend/--config swaps.
/// </summary>
public static class TraceQueryAssembly
{
    /// <summary>
    /// Assemble TraceQueries from a loaded run. The trace event query is the
    /// run's ITraceQuery (InMemoryTraceService declares the ITraceEventQuery
    /// marker — D-6); the asset query is a V2-aware FileAssetQuery.
    /// </summary>
    public static TraceQueries Assemble(TraceRun run)
    {
        // ITraceQuery implementations satisfy ITraceEventQuery (empty marker interface)
        ITraceEventQuery events = (ITraceEventQuery)run.Trace;

        var runId = run.RunId;
        var schemaVersion = run.Manifest?.SchemaVersion ?? "1";
        IAssetQuery assets = new FileAssetQuery(run.RunDir, runId, schemaVersion);

        return new TraceQueries(events, assets);
    }
}
