using System.Collections.Immutable;
using UniClaw.Core.Domain;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// ErrorClassifier — 6 ErrorType 优先级链，substring matching (case-insensitive, not regex)。
/// </summary>
public sealed class ErrorClassifier
{
    /// <summary>
    /// 分类异常 → ErrorType。优先级链 (substring matching, case-insensitive)。
    /// </summary>
    public ErrorType Classify(ErrorClassificationContext ctx)
    {
        var msg = (ctx.ErrorMessage ?? "").ToLowerInvariant();
        var exType = ctx.ExceptionType ?? "";

        // Priority 1: CRASH — app crash / process kill
        if (msg.Contains("crash") || msg.Contains("fatal") || msg.Contains("process killed")
            || exType.Contains("Crash") || exType.Contains("Fatal"))
            return ErrorType.Crash;

        // Priority 2: PERMISSION — permission denied / access restricted
        if (msg.Contains("permission") || msg.Contains("access denied") || msg.Contains("not allowed")
            || exType.Contains("Permission") || exType.Contains("Unauthorized"))
            return ErrorType.Permission;

        // Priority 3: TIMEOUT — timeout / timed out / deadline
        if (msg.Contains("timeout") || msg.Contains("timed out") || msg.Contains("deadline")
            || exType.Contains("Timeout") || exType.Contains("TaskCanceled"))
            return ErrorType.Timeout;

        // Priority 4: NETWORK — network / connection / socket / host unreachable
        if (msg.Contains("network") || msg.Contains("connection") || msg.Contains("socket")
            || msg.Contains("host unreachable") || msg.Contains("refused")
            || exType.Contains("Network") || exType.Contains("Socket") || exType.Contains("Http"))
            return ErrorType.Network;

        // Priority 5: UI_ELEMENT — element not found / stale / not clickable
        if (msg.Contains("element not found") || msg.Contains("stale element")
            || msg.Contains("not clickable") || msg.Contains("not visible")
            || exType.Contains("Element") || exType.Contains("NoSuchElement"))
            return ErrorType.UiElement;

        // Priority 6: Exception type fallback
        if (!string.IsNullOrEmpty(exType))
        {
            // Map common exception types to ErrorType
            if (exType.Contains("NullReference") || exType.Contains("Argument"))
                return ErrorType.Crash;
            if (exType.Contains("OperationCanceled"))
                return ErrorType.Timeout;
        }

        // Priority 7: UNKNOWN — catch-all
        return ErrorType.Unknown;
    }
}

/// <summary>错误类型枚举 (6 值)</summary>
public enum ErrorType
{
    Crash, Permission, Timeout, Network, UiElement, Unknown
}

/// <summary>错误分类上下文</summary>
public sealed record class ErrorClassificationContext(
    string? ErrorMessage = null,
    string? ExceptionType = null,
    int RetryCount = 0,
    int MaxRetries = 3);

/// <summary>
/// ErrorStrategySelector — 6 ErrorType × 策略优先链 + 适用性检查。
/// </summary>
public sealed class ErrorStrategySelector
{
    /// <summary>策略优先链 (每 ErrorType 一个)</summary>
    public static readonly IReadOnlyDictionary<ErrorType, ImmutableArray<ErrorStrategy>> StrategyChains =
        new Dictionary<ErrorType, ImmutableArray<ErrorStrategy>>
        {
            [ErrorType.Crash] = ImmutableArray.Create(ErrorStrategy.Abort),
            [ErrorType.Permission] = ImmutableArray.Create(ErrorStrategy.Abort, ErrorStrategy.Backtrack),
            [ErrorType.Timeout] = ImmutableArray.Create(ErrorStrategy.Retry, ErrorStrategy.Continue, ErrorStrategy.Backtrack),
            [ErrorType.Network] = ImmutableArray.Create(ErrorStrategy.Retry, ErrorStrategy.Backtrack, ErrorStrategy.Abort),
            [ErrorType.UiElement] = ImmutableArray.Create(ErrorStrategy.Skip, ErrorStrategy.Retry, ErrorStrategy.Backtrack),
            [ErrorType.Unknown] = ImmutableArray.Create(ErrorStrategy.Continue, ErrorStrategy.Skip, ErrorStrategy.Abort),
        };

    /// <summary>
    /// 选择恢复策略 — 优先链 + 适用性检查。
    /// </summary>
    public ErrorStrategy SelectStrategy(ErrorType errorType, StrategySelectionContext ctx)
    {
        if (!StrategyChains.TryGetValue(errorType, out var chain))
            return ErrorStrategy.Abort; // Default fallback

        foreach (var strategy in chain)
        {
            if (IsApplicable(strategy, ctx))
                return strategy;
        }

        return ErrorStrategy.Abort; // Terminal fallback
    }

    private bool IsApplicable(ErrorStrategy strategy, StrategySelectionContext ctx)
    {
        return strategy switch
        {
            ErrorStrategy.Retry => ctx.RetryCount < ctx.MaxRetries,
            ErrorStrategy.Backtrack => ctx.CanBacktrack && ctx.StackDepth > 1,
            ErrorStrategy.Skip => ctx.CanSkip,
            ErrorStrategy.Continue => true, // Always applicable
            ErrorStrategy.Abort => true,    // Always applicable
            _ => true
        };
    }
}

/// <summary>恢复策略枚举 (5 值)</summary>
public enum ErrorStrategy
{
    Retry, Backtrack, Skip, Continue, Abort
}

/// <summary>策略选择上下文</summary>
public sealed record class StrategySelectionContext(
    int RetryCount,
    int MaxRetries,
    bool CanBacktrack,
    int StackDepth,
    bool CanSkip);

/// <summary>
/// RecoveryExecutor — Hook Dispatch 表 (5 hooks) + 指数退避 (RETRY: min(2^retry, 10)) + 异常兜底到 ABORT。
/// </summary>
public sealed class RecoveryExecutor
{
    private readonly Dictionary<ErrorStrategy, Func<ErrorRecoveryContext, ErrorRecoveryResult>> _dispatchTable;

    /// <summary>
    /// 构造 RecoveryExecutor — 注册 5 hooks。
    /// </summary>
    public RecoveryExecutor(
        Func<ErrorRecoveryContext, ErrorRecoveryResult>? retryHook = null,
        Func<ErrorRecoveryContext, ErrorRecoveryResult>? backtrackHook = null,
        Func<ErrorRecoveryContext, ErrorRecoveryResult>? skipHook = null,
        Func<ErrorRecoveryContext, ErrorRecoveryResult>? continueHook = null,
        Func<ErrorRecoveryContext, ErrorRecoveryResult>? abortHook = null)
    {
        _dispatchTable = new Dictionary<ErrorStrategy, Func<ErrorRecoveryContext, ErrorRecoveryResult>>
        {
            [ErrorStrategy.Retry] = retryHook ?? DefaultRetry,
            [ErrorStrategy.Backtrack] = backtrackHook ?? DefaultBacktrack,
            [ErrorStrategy.Skip] = skipHook ?? DefaultSkip,
            [ErrorStrategy.Continue] = continueHook ?? DefaultContinue,
            [ErrorStrategy.Abort] = abortHook ?? DefaultAbort,
        };
    }

    /// <summary>
    /// 执行恢复策略 — Hook Dispatch + 异常兜底到 ABORT。
    /// </summary>
    public ErrorRecoveryResult Execute(ErrorStrategy strategy, ErrorRecoveryContext ctx)
    {
        try
        {
            if (_dispatchTable.TryGetValue(strategy, out var hook))
                return hook(ctx);
            return DefaultAbort(ctx);
        }
        catch (Exception)
        {
            // Exception fallback to ABORT
            return DefaultAbort(ctx);
        }
    }

    /// <summary>
    /// 计算指数退避延迟 — min(2^retry_count, 10) 秒。
    /// </summary>
    public static double CalculateBackoffDelay(int retryCount)
        => Math.Min(Math.Pow(2, retryCount), 10);

    private static ErrorRecoveryResult DefaultRetry(ErrorRecoveryContext ctx)
        => new ErrorRecoveryResult(ErrorStrategy.Retry, RecoveryOutcome.RetryScheduled,
            CalculateBackoffDelay(ctx.RetryCount));

    private static ErrorRecoveryResult DefaultBacktrack(ErrorRecoveryContext ctx)
        => new ErrorRecoveryResult(ErrorStrategy.Backtrack, RecoveryOutcome.Success, 0);

    private static ErrorRecoveryResult DefaultSkip(ErrorRecoveryContext ctx)
        => new ErrorRecoveryResult(ErrorStrategy.Skip, RecoveryOutcome.Success, 0);

    private static ErrorRecoveryResult DefaultContinue(ErrorRecoveryContext ctx)
        => new ErrorRecoveryResult(ErrorStrategy.Continue, RecoveryOutcome.Success, 0);

    private static ErrorRecoveryResult DefaultAbort(ErrorRecoveryContext ctx)
        => new ErrorRecoveryResult(ErrorStrategy.Abort, RecoveryOutcome.Failure, 0);
}

/// <summary>恢复结果枚举</summary>
public enum RecoveryOutcome
{
    Success, Failure, RetryScheduled
}

/// <summary>错误恢复上下文</summary>
public sealed record class ErrorRecoveryContext(
    ErrorType ErrorType,
    int RetryCount,
    Exception? Exception = null);

/// <summary>错误恢复结果</summary>
public sealed record class ErrorRecoveryResult(
    ErrorStrategy Strategy,
    RecoveryOutcome Outcome,
    double BackoffDelaySeconds);
