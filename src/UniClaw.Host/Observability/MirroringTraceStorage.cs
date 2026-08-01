using UniClaw.Core.Observability;

namespace UniClaw.Host.Observability;

/// <summary>
/// Keeps the queryable in-memory trace as the authoritative read model while
/// mirroring every lifecycle/write operation to a durable run-asset backend.
/// </summary>
public sealed class MirroringTraceStorage : ITraceStorage
{
    private readonly ITraceStorage _primary;
    private readonly ITraceStorage _mirror;

    public MirroringTraceStorage(ITraceStorage primary, ITraceStorage mirror)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
    }

    public TraceSession? CurrentSession => _primary.CurrentSession;

    public void SetSession(TraceSession session)
    {
        _primary.SetSession(session);
        _mirror.SetSession(session);
    }

    public void EndSession()
    {
        _primary.EndSession();
        _mirror.EndSession();
    }

    public void AddExecution(ExecutionRecord record)
    {
        _primary.AddExecution(record);
        _mirror.AddExecution(record);
    }

    public void AddTransition(StateTransition transition)
    {
        _primary.AddTransition(transition);
        _mirror.AddTransition(transition);
    }

    public void AddError(ErrorRecord record)
    {
        _primary.AddError(record);
        _mirror.AddError(record);
    }

    public void AddPageTransition(PageTransition transition)
    {
        _primary.AddPageTransition(transition);
        _mirror.AddPageTransition(transition);
    }

    public void AddAICall(AICallRecord record)
    {
        _primary.AddAICall(record);
        _mirror.AddAICall(record);
    }

    public IReadOnlyList<ExecutionRecord> GetExecutions() =>
        _primary.GetExecutions();

    public IReadOnlyList<StateTransition> GetTransitions() =>
        _primary.GetTransitions();

    public IReadOnlyList<ErrorRecord> GetErrors() =>
        _primary.GetErrors();

    public IReadOnlyList<PageTransition> GetPageTransitions() =>
        _primary.GetPageTransitions();

    public IReadOnlyList<AICallRecord> GetAICalls() =>
        _primary.GetAICalls();

    public string Export() => _primary.Export();
}
