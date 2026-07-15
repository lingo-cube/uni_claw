namespace UniClaw.Core.StateMachine.Session;

/// <summary>
/// Session context — Macro session state.
/// 4 个字段封装所有会话相关状态：追踪ID、全局状态机、设备配置。
/// </summary>
public sealed class SessionContext : ISessionContext
{
    // --- 4 private fields ---
    private readonly string _traceId;
    private readonly GlobalFSM _globalFsm = new();
    private string? _deviceExperience;
    private string? _aiProvider;

    /// <summary>构造 SessionContext</summary>
    public SessionContext(string traceId)
    {
        _traceId = traceId;
        // _globalFsm defaults to GlobalState.Idle (GlobalFSM ctor)
        _deviceExperience = null;
        _aiProvider = null;
    }

    // --- ISessionContext implementation ---

    /// <inheritdoc />
    public string TraceId => _traceId;

    /// <inheritdoc />
    /// <remarks>只读 — 所有状态变更走 GlobalStateMachine.TransitionTo() 或 InternalGlobalFSM.ForceState()</remarks>
    public GlobalState GlobalState => _globalFsm.CurrentState;

    /// <summary>
    /// 全局状态机公开接口 — 回调注册 (RegisterStateCallback 在具体类) 与转换查询 (CanTransitionTo, GetValidTransitions)。
    /// </summary>
    public IGlobalStateMachine GlobalStateMachine => _globalFsm;

    /// <summary>
    /// 全局状态机具体类型 — engine 内部访问 ForceState 恢复路径 (internal, 不暴露给外部)。
    /// </summary>
    internal GlobalFSM InternalGlobalFSM => _globalFsm;

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
