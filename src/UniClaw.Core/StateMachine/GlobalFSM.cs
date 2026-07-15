using System.Collections.Immutable;
using UniClaw.Core.Domain;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// GlobalFSM — 8 GlobalState × 修正转换矩阵。
/// Error 不能直接转换到 Traversing (必须经过 Recovering → Initializing → Traversing)。
/// Completed/Terminated 是锁定态，无出转换。
/// 支持 register_state_callback + transition_history。
/// </summary>
public sealed class GlobalFSM : IGlobalStateMachine
{
    /// <summary>
    /// 修正转换矩阵 — Error 不能直接到 Traversing。
    /// </summary>
    public static readonly IReadOnlyDictionary<GlobalState, ImmutableArray<GlobalState>> TransitionMatrix =
        new Dictionary<GlobalState, ImmutableArray<GlobalState>>
        {
            [GlobalState.Idle] = ImmutableArray.Create(GlobalState.Initializing),
            [GlobalState.Initializing] = ImmutableArray.Create(GlobalState.Traversing, GlobalState.Error),
            [GlobalState.Traversing] = ImmutableArray.Create(GlobalState.Paused, GlobalState.Error, GlobalState.Completed),
            [GlobalState.Paused] = ImmutableArray.Create(GlobalState.Traversing, GlobalState.Terminated),
            [GlobalState.Error] = ImmutableArray.Create(GlobalState.Recovering, GlobalState.Terminated),
            [GlobalState.Recovering] = ImmutableArray.Create(GlobalState.Initializing, GlobalState.Terminated),
            [GlobalState.Completed] = ImmutableArray<GlobalState>.Empty, // Locked
            [GlobalState.Terminated] = ImmutableArray<GlobalState>.Empty, // Locked
        };

    /// <summary>当前状态</summary>
    public GlobalState CurrentState { get; internal set; } = GlobalState.Idle;

    /// <summary>转换历史 (chronological log)</summary>
    private readonly List<TransitionRecord> _transitionHistory = new();

    /// <summary>状态回调</summary>
    private readonly Dictionary<GlobalState, List<Action<StateTransitionEventArgs>>> _callbacks = new();

    /// <summary>
    /// 转换到目标状态 — 严格矩阵校验。
    /// </summary>
    public StateTransitionResult TransitionTo(GlobalState targetState, string? reason = null)
    {
        // Terminal states: no outgoing transitions
        if (CurrentState == GlobalState.Completed || CurrentState == GlobalState.Terminated)
            throw new DomainValidationException("transition", $"{CurrentState}→{targetState}");

        if (!TransitionMatrix.TryGetValue(CurrentState, out var allowedTargets)
            || !allowedTargets.Contains(targetState))
        {
            throw new DomainValidationException("transition", $"{CurrentState}→{targetState}");
        }

        var fromState = CurrentState;
        CurrentState = targetState;

        // Record transition history
        _transitionHistory.Add(new TransitionRecord(fromState, targetState, reason, DateTimeOffset.UtcNow));

        // Invoke callbacks
        InvokeCallbacks(fromState, targetState, reason);

        return StateTransitionResult.Success();
    }

    /// <summary>
    /// 强制设置状态 — 内部状态恢复专用 (语义是"撤销到中断前状态"，不是"状态转换")。
    /// 绕过矩阵校验，不触发 callback (恢复不是消费者应感知的事件)，但记录历史 (可审计)。
    /// 仅通过 SessionContext.InternalGlobalFSM 供 PopupHandler/StateRestorer 恢复路径使用。
    /// </summary>
    internal void ForceState(GlobalState targetState)
    {
        var fromState = CurrentState;
        CurrentState = targetState;
        _transitionHistory.Add(new TransitionRecord(fromState, targetState, "force_restore", DateTimeOffset.UtcNow));
        // 不触发 callback — 恢复不是状态转换，消费者不应感知
    }

    /// <inheritdoc/>
    public bool IsComplete() => CurrentState == GlobalState.Completed || CurrentState == GlobalState.Terminated;

    /// <inheritdoc/>
    public bool CanTransitionTo(GlobalState targetState)
    {
        if (CurrentState == GlobalState.Completed || CurrentState == GlobalState.Terminated)
            return false;
        return TransitionMatrix.TryGetValue(CurrentState, out var allowed)
            && allowed.Contains(targetState);
    }

    /// <inheritdoc/>
    public IEnumerable<GlobalState> GetValidTransitions()
    {
        if (CurrentState == GlobalState.Completed || CurrentState == GlobalState.Terminated)
            return Enumerable.Empty<GlobalState>();
        return TransitionMatrix.TryGetValue(CurrentState, out var allowed)
            ? allowed : Enumerable.Empty<GlobalState>();
    }

    /// <summary>
    /// 注册状态回调 — 进入该状态时调用。异常不传播（catch + log）。
    /// </summary>
    public void RegisterStateCallback(GlobalState state, Action<StateTransitionEventArgs> callback)
    {
        if (!_callbacks.ContainsKey(state))
            _callbacks[state] = new List<Action<StateTransitionEventArgs>>();
        _callbacks[state].Add(callback);
    }

    /// <summary>
    /// 获取转换历史 — 只读视图。
    /// </summary>
    public IReadOnlyList<TransitionRecord> GetTransitionHistory()
        => _transitionHistory.AsReadOnly();

    private void InvokeCallbacks(GlobalState fromState, GlobalState toState, string? reason)
    {
        if (!_callbacks.TryGetValue(toState, out var callbacks))
            return;

        var args = new StateTransitionEventArgs(fromState, toState, reason, DateTimeOffset.UtcNow);
        foreach (var callback in callbacks)
        {
            try
            {
                callback(args);
            }
            catch (Exception)
            {
                // Catch + log, do NOT propagate — callback failure must not disrupt FSM
            }
        }
    }
}

/// <summary>
/// 转换记录 — from, to, reason, timestamp。
/// </summary>
public sealed record class TransitionRecord(
    GlobalState FromState,
    GlobalState ToState,
    string? Reason,
    DateTimeOffset Timestamp);
