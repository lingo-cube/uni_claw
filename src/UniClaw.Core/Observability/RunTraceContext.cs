namespace UniClaw.Core.Observability;

/// <summary>
/// RunTraceContext — AsyncLocal channel for the current run id.
/// Host pushes at run boundary; engine/FSM code reads Current without parameter plumbing.
/// Pattern follows EngineStepSpanContext (AsyncLocal + static singleton).
/// No run context → Current returns null → log line shows "-".
/// </summary>
public sealed class RunTraceContext
{
    private static readonly AsyncLocal<string?> _current = new();

    /// <summary>Static singleton — composition root and tests share the same instance.</summary>
    public static RunTraceContext Instance { get; } = new();

    private RunTraceContext() { }

    /// <summary>Current run id; null when no run is active.</summary>
    public string? Current => _current.Value;

    /// <summary>Push a run id onto the context (run boundary entry).</summary>
    public void Push(string runId) => _current.Value = runId;

    /// <summary>Pop the run id (run boundary exit).</summary>
    public void Pop() => _current.Value = null;
}
