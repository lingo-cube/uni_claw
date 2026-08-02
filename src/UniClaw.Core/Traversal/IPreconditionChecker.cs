using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// Optional pre-step gate.  When set on the <see cref="StepContext"/>, the FSM
/// calls <see cref="CheckAsync"/> before entering Execute.  A false return
/// routes the step to ErrorHandling instead.
/// </summary>
public interface IPreconditionChecker
{
    /// <summary>
    /// Validate that the current page state is acceptable for the next action.
    /// </summary>
    /// <param name="context">The runtime context carrying current page analysis.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>true if the step should proceed; false to route to ErrorHandling.</returns>
    Task<bool> CheckAsync(
        TraversalRuntimeContext context,
        CancellationToken cancellationToken = default);
}
