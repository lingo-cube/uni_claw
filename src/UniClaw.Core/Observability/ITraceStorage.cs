using System.Text.Json;

namespace UniClaw.Core.Observability;

/// <summary>
/// ITraceStorage — shared synchronous storage backend for ITraceRecorder (write wrapper)
/// and ITraceService (read+query facade). CQRS at the interface level.
/// Write methods are synchronous (void return). Read methods return IReadOnlyList (direct access).
/// D-6: In-memory operations are always synchronous. Async layer on ITraceRecorder (consumer contract).
/// </summary>
public interface ITraceStorage
{
    // ── Session lifecycle (2 + 1 getter) ──────────────────

    /// <summary>Set the current trace session</summary>
    void SetSession(TraceSession session);

    /// <summary>End the current session (sets EndTime)</summary>
    void EndSession();

    /// <summary>Current trace session (null if not started)</summary>
    TraceSession? CurrentSession { get; }

    // ── Synchronous write (5) ─────────────────────────────

    /// <summary>Add an execution record</summary>
    void AddExecution(ExecutionRecord record);

    /// <summary>Add a state transition</summary>
    void AddTransition(StateTransition transition);

    /// <summary>Add an error record</summary>
    void AddError(ErrorRecord record);

    /// <summary>Add a page transition</summary>
    void AddPageTransition(PageTransition transition);

    /// <summary>Add an AI call record</summary>
    void AddAICall(AICallRecord record);

    // ── Synchronous read (6) ──────────────────────────────

    /// <summary>Get all execution records</summary>
    IReadOnlyList<ExecutionRecord> GetExecutions();

    /// <summary>Get all state transitions</summary>
    IReadOnlyList<StateTransition> GetTransitions();

    /// <summary>Get all error records</summary>
    IReadOnlyList<ErrorRecord> GetErrors();

    /// <summary>Get all page transitions</summary>
    IReadOnlyList<PageTransition> GetPageTransitions();

    /// <summary>Get all AI call records</summary>
    IReadOnlyList<AICallRecord> GetAICalls();

    /// <summary>Export trace data as JSON string</summary>
    string Export();
}
