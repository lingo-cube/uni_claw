using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Host.Safety;

namespace UniClaw.Host.Hooks;

/// <summary>
/// Pushes a per-step <see cref="SafetyCandidate"/> into the
/// run-scoped <see cref="SafetyExecutionContext"/> on OnBeforeStep and
/// restores the previous candidate on OnAfterStep.
/// The engine's <c>OperationDispatcher</c> dispatches actions through the
/// <c>SafeActionExecutor</c> decorator, which reads the AsyncLocal context in
/// <c>DecideAsync</c>. Without this hook, <c>DecideAsync</c> sees the
/// <c>"unscoped"</c> fallback candidate that denies by default and would block
/// the whole run. The candidate is derived from the engine's current frame —
/// the node's <see cref="Operation"/> (action + target) and the context path.
/// </summary>
public sealed class SafetyContextHook : TraversalHookBase
{
    private readonly ISafetyExecutionContext _context;
    private readonly string _runId;
    private readonly string _appPackage;
    private readonly string _entryPageIdentity;
    private readonly int _maxSteps;
    private readonly int _maxScrolls;
    private IDisposable? _scope;

    public SafetyContextHook(
        ISafetyExecutionContext context,
        string runId,
        string appPackage,
        string entryPageIdentity,
        int maxSteps,
        int maxScrolls)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _runId = runId;
        _appPackage = appPackage;
        _entryPageIdentity = entryPageIdentity;
        _maxSteps = maxSteps;
        _maxScrolls = maxScrolls;
    }

    /// <inheritdoc/>
    public override Task OnBeforeStepAsync(ITraversalContext context)
    {
        _scope?.Dispose();
        _scope = _context.Push(BuildCandidate(context));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnAfterStepAsync(ITraversalContext context)
    {
        _scope?.Dispose();
        _scope = null;
        return Task.CompletedTask;
    }

    private SafetyCandidate BuildCandidate(ITraversalContext context)
    {
        var currentNode = context.CurrentFrame as TraversalNode;
        var operation = currentNode?.Operation;
        var action = operation?.Action switch
        {
            OperationType.Click => "click",
            OperationType.Swipe => "scroll",
            OperationType.Back => "back",
            OperationType.InputText => "input",
            _ => "noop",
        };
        var coordinate = operation?.Target?.Value as Coordinate;
        var semantic = currentNode?.NodeType switch
        {
            NodeType.Container => "navigation_row",
            NodeType.LeafSwitch => "toggle",
            NodeType.LeafSlider => "slider",
            NodeType.LeafInfo => "readonly",
            _ when coordinate is not null => "navigation_row",
            _ => null,
        };
        var trustedNavigationTarget =
            semantic == "navigation_row"
            && operation?.Target?.By is TargetType.Text or TargetType.Coordinate;
        var pageIdentity = context.CurrentPath.LastOrDefault()
                           ?? _entryPageIdentity;
        var pagePath = context.CurrentPath.Count > 0
            ? string.Join("/", context.CurrentPath)
            : _entryPageIdentity;
        return new SafetyCandidate(
            action,
            operation?.Target?.Value?.ToString(),
            semantic,
            pageIdentity,
            pagePath,
            _appPackage,
            trustedNavigationTarget ? 0.99 : null,
            trustedNavigationTarget,
            false,
            context.NodeStack.Depth,
            Math.Max(0, _maxSteps - context.StepCount + 1),
            _maxScrolls,
            _runId,
            context.StepCount,
            string.Empty,
            "engine_hook");
    }
}
