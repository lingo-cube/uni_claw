namespace UniClaw.Core.StateMachine.Session;

/// <summary>
/// Session context read-only interface — Macro session state.
/// 只读属性暴露，GlobalState setter 只在 concrete class (per D-7)。
/// </summary>
public interface ISessionContext
{
    /// <summary>追踪ID</summary>
    string TraceId { get; }

    /// <summary>全局状态 (只读 via interface, setter on concrete class per D-7)</summary>
    GlobalState GlobalState { get; }

    /// <summary>设备经验级别</summary>
    string? DeviceExperience { get; }

    /// <summary>AI Provider</summary>
    string? AIProvider { get; }
}
