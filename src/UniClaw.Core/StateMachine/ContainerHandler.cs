using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// CompletionDetector — 5-priority detect_completion() 链，纯计算，无缓存 (D-3)。
/// </summary>
public sealed class CompletionDetector
{
    /// <summary>
    /// 检测容器遍历完成状态 — 5-priority chain。
    /// 纯计算，每次调用从头计算，不缓存结果。
    /// </summary>
    public CompletionResult DetectCompletion(CompletionContext ctx)
    {
        // Priority 1: TIMEOUT exceeded
        if (ctx.ElapsedMs > ctx.TimeoutMs)
            return new CompletionResult(
                IsComplete: true, Reason: CompletionReason.Timeout,
                SuggestedAction: FallbackAction.Back, ShouldBacktrack: true);

        // Priority 2: MAX_DEPTH reached
        if (ctx.CurrentDepth > ctx.MaxDepth)
            return new CompletionResult(
                IsComplete: true, Reason: CompletionReason.MaxDepth,
                SuggestedAction: FallbackAction.Back, ShouldBacktrack: true);

        // Priority 3: No children
        if (ctx.TotalChildren == 0)
            return new CompletionResult(
                IsComplete: true, Reason: CompletionReason.AllVisited,
                SuggestedAction: FallbackAction.Back, ShouldBacktrack: false);

        // Priority 4: All children visited
        if (ctx.VisitedChildCount >= ctx.TotalChildren)
            return new CompletionResult(
                IsComplete: true, Reason: CompletionReason.AllVisited,
                SuggestedAction: ctx.ExitConditionFallback, ShouldBacktrack: false);

        // Priority 5: Still processing (INCOMPLETE)
        return new CompletionResult(
            IsComplete: false, Reason: CompletionReason.Incomplete,
            SuggestedAction: FallbackAction.Skip, ShouldBacktrack: false);
    }
}

/// <summary>完成原因</summary>
public enum CompletionReason
{
    Timeout, MaxDepth, AllVisited, Incomplete
}

/// <summary>完成检测结果</summary>
public sealed record class CompletionResult(
    bool IsComplete,
    CompletionReason Reason,
    FallbackAction SuggestedAction,
    bool ShouldBacktrack);

/// <summary>
/// 完成检测上下文 — 纯输入，不持有可变状态。
/// </summary>
public sealed record class CompletionContext(
    double ElapsedMs,
    double TimeoutMs,
    int CurrentDepth,
    int MaxDepth,
    int TotalChildren,
    int VisitedChildCount,
    FallbackAction ExitConditionFallback);

/// <summary>
/// FallbackDecider — 纯计算优先级链，无缓存 (D-3)。
/// </summary>
public sealed class FallbackDecider
{
    /// <summary>
    /// 决定回退操作 — 纯计算，不缓存。
    /// </summary>
    public FallbackAction DecideFallback(CompletionResult completion, bool canContinue)
    {
        // Timeout or max depth → always BACK
        if (completion.Reason == CompletionReason.Timeout
            || completion.Reason == CompletionReason.MaxDepth)
            return FallbackAction.Back;

        // Complete with suggested action → use it
        if (completion.IsComplete && completion.Reason == CompletionReason.AllVisited)
            return completion.SuggestedAction;

        // Cannot continue → BACK
        if (!canContinue)
            return FallbackAction.Back;

        // Incomplete and can continue → SKIP
        return FallbackAction.Skip;
    }
}

/// <summary>
/// ContainerActionExecutor — Hook Dispatch 表 + 异常兜底到 BACK。
/// </summary>
public sealed class ContainerActionExecutor
{
    private readonly Dictionary<FallbackAction, Func<ContainerContext, ContainerActionResult>> _dispatchTable;

    /// <summary>
    /// 构造 ContainerActionExecutor — 注册 4 hooks。
    /// </summary>
    public ContainerActionExecutor(
        Func<ContainerContext, ContainerActionResult>? backHook = null,
        Func<ContainerContext, ContainerActionResult>? autoEscapeHook = null,
        Func<ContainerContext, ContainerActionResult>? skipHook = null,
        Func<ContainerContext, ContainerActionResult>? abortHook = null)
    {
        _dispatchTable = new Dictionary<FallbackAction, Func<ContainerContext, ContainerActionResult>>
        {
            [FallbackAction.Back] = backHook ?? DefaultBack,
            [FallbackAction.AutoEscape] = autoEscapeHook ?? DefaultAutoEscape,
            [FallbackAction.Skip] = skipHook ?? DefaultSkip,
            [FallbackAction.Abort] = abortHook ?? DefaultAbort,
        };
    }

    /// <summary>
    /// 执行回退动作 — Hook Dispatch 表查找 + 异常兜底到 BACK。
    /// </summary>
    public ContainerActionResult Execute(FallbackAction action, ContainerContext ctx)
    {
        try
        {
            if (_dispatchTable.TryGetValue(action, out var hook))
                return hook(ctx);
            return DefaultBack(ctx);
        }
        catch (Exception)
        {
            // Exception fallback to BACK (safest default)
            return DefaultBack(ctx);
        }
    }

    private static ContainerActionResult DefaultBack(ContainerContext ctx)
        => new ContainerActionResult(FallbackAction.Back, true, "Press back + pop frame");

    private static ContainerActionResult DefaultAutoEscape(ContainerContext ctx)
        => new ContainerActionResult(FallbackAction.AutoEscape, true, "Try sibling menu + fallback to back");

    private static ContainerActionResult DefaultSkip(ContainerContext ctx)
        => new ContainerActionResult(FallbackAction.Skip, true, "Skip remaining + pop frame + mark complete");

    private static ContainerActionResult DefaultAbort(ContainerContext ctx)
        => new ContainerActionResult(FallbackAction.Abort, false, "Abort traversal");
}

/// <summary>容器执行上下文</summary>
public sealed record class ContainerContext(
    string NodeId,
    int Depth,
    ITraversalContext TraversalContext);

/// <summary>容器执行结果</summary>
public sealed record class ContainerActionResult(
    FallbackAction Action,
    bool Success,
    string Description);

/// <summary>
/// ContainerHandler — 3-step HandleContainer() pipeline: detect → decide → execute。
/// Pipeline-level try/catch fallback returns ContainerActionResult(Back, false, ...).
/// Constructor injection: sub-component instances or Func delegates for testability.
/// </summary>
public sealed class ContainerHandler
{
    private readonly Func<CompletionContext, CompletionResult> _detect;
    private readonly Func<CompletionResult, bool, FallbackAction> _decide;
    private readonly Func<FallbackAction, ContainerContext, ContainerActionResult> _execute;

    /// <summary>
    /// 构造 ContainerHandler — 默认子组件或自定义注入。
    /// </summary>
    public ContainerHandler(
        CompletionDetector? detector = null,
        FallbackDecider? decider = null,
        ContainerActionExecutor? executor = null)
    {
        var d = detector ?? new CompletionDetector();
        var fd = decider ?? new FallbackDecider();
        var e = executor ?? new ContainerActionExecutor();
        _detect = d.DetectCompletion;
        _decide = fd.DecideFallback;
        _execute = e.Execute;
    }

    /// <summary>
    /// 构造 ContainerHandler — Func 注入 (用于测试管道级别兜底)。
    /// Sub-component classes are sealed; Func injection allows throwing/custom behavior for testability.
    /// </summary>
    public ContainerHandler(
        Func<CompletionContext, CompletionResult> detect,
        Func<CompletionResult, bool, FallbackAction> decide,
        Func<FallbackAction, ContainerContext, ContainerActionResult> execute)
    {
        _detect = detect;
        _decide = decide;
        _execute = execute;
    }

    /// <summary>
    /// 3-step pipeline: detect → decide → execute。
    /// Pipeline-level try/catch → ContainerActionResult(Back, false, "Unhandled exception...")。
    /// D-G4: Pipeline fallback Success=false (pipeline crashed); executor fallback Success=true (DefaultBack works)。
    /// </summary>
    public ContainerActionResult HandleContainer(
        CompletionContext completionCtx,
        bool canContinue,
        string nodeId,
        ITraversalContext traversalContext)
    {
        try
        {
            // Step 1: detect completion
            var completion = _detect(completionCtx);

            // Step 2: decide fallback action
            var fallbackAction = _decide(completion, canContinue);

            // Step 3: execute fallback action
            var containerCtx = new ContainerContext(nodeId, completionCtx.CurrentDepth, traversalContext);
            return _execute(fallbackAction, containerCtx);
        }
        catch (Exception ex)
        {
            // Pipeline-level fallback — any step exception → Back with Success=false
            // D-G4: Pipeline fallback differs from executor fallback
            //   Pipeline: Success=false (pipeline crashed, BACK is safest guess)
            //   Executor: Success=true (DefaultBack is a known-working action)
            return new ContainerActionResult(
                FallbackAction.Back, false,
                $"Unhandled exception during container handling: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
