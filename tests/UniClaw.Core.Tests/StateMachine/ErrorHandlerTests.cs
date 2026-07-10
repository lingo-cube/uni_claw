using UniClaw.Core.Domain;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// ErrorHandler wrapper tests (5 scenarios):
/// (1) normal pipeline execution
/// (2) pipeline-level fallback with injected throwing Func
/// (3) constructor injection with custom sub-components (executor hooks)
/// (4) Exception? parameter passes to ErrorRecoveryContext
/// (5) strategyCtx.RetryCount used (not classificationCtx.RetryCount) — D-G5
/// </summary>
public class ErrorHandlerTests
{
    private static ErrorClassificationContext DefaultClassificationCtx() => new(
        ErrorMessage: "network connection timeout",
        ExceptionType: "TaskCanceledException",
        RetryCount: 99,  // Noise field (D-G5: should NOT be used)
        MaxRetries: 3);

    private static StrategySelectionContext DefaultStrategyCtx() => new(
        RetryCount: 2,   // Authoritative field (D-G5: should be used)
        MaxRetries: 3,
        CanBacktrack: true,
        StackDepth: 3,
        CanSkip: true);

    /// <summary>
    /// (1) Normal pipeline execution — classify→select→execute produces expected result.
    /// </summary>
    [Fact(DisplayName = "错误编排: 正常3步流水线 → Timeout+Retry+RetryScheduled")]
    public void HandleError_NormalPipeline_ReturnsExecutorResult()
    {
        var classificationCtx = DefaultClassificationCtx();
        var strategyCtx = DefaultStrategyCtx();

        var handler = new ErrorHandler();
        var result = handler.HandleError(classificationCtx, strategyCtx);

        // ErrorClassifier: "timeout" in message → ErrorType.Timeout
        // ErrorStrategySelector: Timeout chain = [Retry, Continue, Backtrack], RetryCount=2 < MaxRetries=3 → Retry
        // RecoveryExecutor: DefaultRetry → ErrorRecoveryResult(Retry, RetryScheduled, 4.0)
        Assert.Equal(ErrorStrategy.Retry, result.Strategy);
        Assert.Equal(RecoveryOutcome.RetryScheduled, result.Outcome);
        Assert.Null(result.Description);
    }

    /// <summary>
    /// (2) Pipeline-level fallback — injected throwing classify Func causes pipeline catch.
    /// Returns ErrorRecoveryResult(Abort, Failure, 0, "Unhandled exception...").
    /// </summary>
    [Fact(DisplayName = "错误编排: classify步骤抛异常 → 管道兜底 Abort+Failure")]
    public void HandleError_ThrowingStep_PipelineFallback()
    {
        var classificationCtx = DefaultClassificationCtx();
        var strategyCtx = DefaultStrategyCtx();

        // Inject throwing classify Func → pipeline catch
        var handler = new ErrorHandler(
            classify: _ => throw new InvalidOperationException("Intentional test failure from classifier"),
            selectStrategy: new ErrorStrategySelector().SelectStrategy,
            execute: new RecoveryExecutor().Execute);

        var result = handler.HandleError(classificationCtx, strategyCtx);

        Assert.Equal(ErrorStrategy.Abort, result.Strategy);
        Assert.Equal(RecoveryOutcome.Failure, result.Outcome);
        Assert.Equal(0, result.BackoffDelaySeconds);
        Assert.Contains("Unhandled exception during error handling", result.Description!);
        Assert.Contains("InvalidOperationException", result.Description!);
    }

    /// <summary>
    /// (3) Constructor injection — custom executor hooks produce custom results.
    /// Demonstrates that injected sub-components are used instead of defaults.
    /// </summary>
    [Fact(DisplayName = "错误编排: 自定义executor hooks → 使用注入hooks")]
    public void HandleError_CustomExecutorHooks_UsedInsteadOfDefaults()
    {
        var classificationCtx = DefaultClassificationCtx();
        var strategyCtx = DefaultStrategyCtx();

        // Use RecoveryExecutor with custom retry hook
        var customExecutor = new RecoveryExecutor(
            retryHook: _ => new ErrorRecoveryResult(ErrorStrategy.Skip, RecoveryOutcome.Success, 0, "custom-retry-result"));

        var handler = new ErrorHandler(executor: customExecutor);
        var result = handler.HandleError(classificationCtx, strategyCtx);

        Assert.Equal(ErrorStrategy.Skip, result.Strategy);
        Assert.Equal(RecoveryOutcome.Success, result.Outcome);
        Assert.Equal("custom-retry-result", result.Description);
    }

    /// <summary>
    /// (4) Exception? parameter passes to ErrorRecoveryContext.Exception.
    /// When no Exception is provided, ErrorRecoveryContext.Exception is null.
    /// </summary>
    [Fact(DisplayName = "错误编排: Exception参数传入ErrorRecoveryContext")]
    public void HandleError_ExceptionParameter_PassesToRecoveryContext()
    {
        var classificationCtx = DefaultClassificationCtx();
        var strategyCtx = DefaultStrategyCtx();
        var testException = new InvalidOperationException("test-exception-details");

        // Inject capturing execute Func that records ErrorRecoveryContext
        ErrorRecoveryContext? capturedCtx = null;
        var handler = new ErrorHandler(
            classify: new ErrorClassifier().Classify,
            selectStrategy: new ErrorStrategySelector().SelectStrategy,
            execute: (strategy, ctx) =>
            {
                capturedCtx = ctx;
                return new ErrorRecoveryResult(strategy, RecoveryOutcome.Success, 0);
            });

        // With exception
        handler.HandleError(classificationCtx, strategyCtx, testException);
        Assert.NotNull(capturedCtx!.Exception);
        Assert.Equal("test-exception-details", capturedCtx.Exception!.Message);

        // Without exception (null default)
        capturedCtx = null;
        handler.HandleError(classificationCtx, strategyCtx);
        Assert.Null(capturedCtx!.Exception);
    }

    /// <summary>
    /// (5) D-G5: strategyCtx.RetryCount is used (not classificationCtx.RetryCount).
    /// ErrorRecoveryContext.RetryCount should come from strategyCtx.
    /// </summary>
    [Fact(DisplayName = "错误编排: RetryCount来自strategyCtx (D-G5)")]
    public void HandleError_RetryCount_UsesStrategyCtx_NotClassificationCtx()
    {
        // classificationCtx.RetryCount = 99 (noise field)
        // strategyCtx.RetryCount = 2 (authoritative)
        var classificationCtx = DefaultClassificationCtx();  // RetryCount = 99
        var strategyCtx = DefaultStrategyCtx();              // RetryCount = 2

        // Inject capturing execute Func
        ErrorRecoveryContext? capturedCtx = null;
        var handler = new ErrorHandler(
            classify: new ErrorClassifier().Classify,
            selectStrategy: new ErrorStrategySelector().SelectStrategy,
            execute: (strategy, ctx) =>
            {
                capturedCtx = ctx;
                return new ErrorRecoveryResult(strategy, RecoveryOutcome.Success, 0);
            });

        handler.HandleError(classificationCtx, strategyCtx);

        // D-G5: ErrorRecoveryContext.RetryCount should be 2 (from strategyCtx), NOT 99 (from classificationCtx)
        Assert.Equal(2, capturedCtx!.RetryCount);
        Assert.NotEqual(99, capturedCtx.RetryCount);
    }
}
