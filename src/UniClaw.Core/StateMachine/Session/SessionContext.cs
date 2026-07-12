namespace UniClaw.Core.StateMachine.Session;

/// <summary>
/// Session context — Macro session state.
/// 4 个字段封装所有会话相关状态：追踪ID、全局状态、设备配置。
/// </summary>
public sealed class SessionContext : ISessionContext
{
    // --- 4 private fields ---
    private readonly string _traceId;
    private GlobalState _globalState;
    private string? _deviceExperience;
    private string? _aiProvider;

    /// <summary>构造 SessionContext</summary>
    public SessionContext(string traceId)
    {
        _traceId = traceId;
        _globalState = GlobalState.Idle;
        _deviceExperience = null;
        _aiProvider = null;
    }

    // --- ISessionContext implementation ---

    /// <inheritdoc />
    public string TraceId => _traceId;

    /// <inheritdoc />
    /// <remarks>Getter on interface, setter on concrete class per D-7</remarks>
    public GlobalState GlobalState
    {
        get => _globalState;
        set => _globalState = value;
    }

    /// <inheritdoc />
    public string? DeviceExperience
    {
        get => _deviceExperience;
        set => _deviceExperience = value;
    }

    /// <inheritdoc />
    public string? AIProvider
    {
        get => _aiProvider;
        set => _aiProvider = value;
    }
}
