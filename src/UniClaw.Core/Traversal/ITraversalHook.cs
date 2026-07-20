using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// ITraversalHook — 遍历引擎生命周期钩子。
/// 7 个层级：Run(2)、Step(2)、State(3) 包括 pause/resume。
/// P4-B1 设计，B2 使 OnPauseAsync/OnResumeAsync 功能化。
/// </summary>
public interface ITraversalHook
{
    // Run 级 — engine start/complete
    Task OnBeforeRunAsync(TraversalPlan plan, ITraversalContext context);
    Task OnAfterRunAsync(TraversalResult result);

    // Step 级 — before/after each engine step
    Task OnBeforeStepAsync(ITraversalContext context);
    Task OnAfterStepAsync(ITraversalContext context);

    // State 级 — errors
    Task OnErrorAsync(TraversalErrorContext error, ITraversalContext context);

    // State 级 — pause/resume (B2 functional)
    Task OnPauseAsync(ITraversalContext context);
    Task OnResumeAsync(ITraversalContext context);
}

/// <summary>
/// TraversalHookBase — 无操作抽象基类，实现类可选重写感兴趣的钩子。
/// </summary>
public abstract class TraversalHookBase : ITraversalHook
{
    /// <inheritdoc/>
    public virtual Task OnBeforeRunAsync(TraversalPlan plan, ITraversalContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnAfterRunAsync(TraversalResult result)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnBeforeStepAsync(ITraversalContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnAfterStepAsync(ITraversalContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnErrorAsync(TraversalErrorContext error, ITraversalContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnPauseAsync(ITraversalContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnResumeAsync(ITraversalContext context)
        => Task.CompletedTask;
}

/// <summary>
/// TraversalErrorContext — 引擎钩子错误上下文摘要类型。
/// 与 StateMachine.Error.ErrorContext 不同，这是专为 ITraversalHook.OnErrorAsync 设计的轻量摘要。
/// IsRecoverable: true = FSM 级（引擎继续），false = 引擎级致命（引擎终止）。
/// </summary>
public sealed record class TraversalErrorContext(
    string ErrorType,
    string Message,
    string? NodeId,
    bool IsRecoverable);
