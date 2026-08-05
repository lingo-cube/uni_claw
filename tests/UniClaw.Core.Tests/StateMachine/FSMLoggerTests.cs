using Microsoft.Extensions.Logging;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// FSM/ErrorHandler 日志测试 (trace-correlated-logging task 3.4):
/// FSM step 异常 → LogError 带 [t=runId] 与异常类型；无效转换 → LogWarning；
/// ErrorHandler 分类信息 → LogInformation；管道兜底 → LogError；
/// 默认构造 → NullLogger 不抛异常。
/// 用 InMemoryLoggerProvider 捕获日志行（测试内部私有类，无共享状态）。
/// </summary>
public sealed class FSMLoggerTests
{
    // ── Task 3.4-1: FSM step 异常 ────────────────────────────────────────────

    [Fact(DisplayName = "FSM日志: StepAsync 异常 → LogError 带 [t=runId] 与异常类型")]
    public async Task StepAsync_HandlerThrows_LogsRunContextAndExceptionType()
    {
        RunTraceContext.Instance.Push("run-abc-123");
        try
        {
            var provider = new InMemoryLoggerProvider();
            using var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddProvider(provider);
            });
            var logger = factory.CreateLogger<TraversalFSM>();
            var ctx = new TraversalRuntimeContext("test-trace");
            var fsm = new TraversalFSM(ctx, logger);
            fsm.TransitionTo(TraversalState.PreconditionCheck);

            var stepCtx = new StepContext(
                Context: ctx,
                StateMachine: fsm,
                Brain: null!,
                ScreenState: null!,
                Action: null!,
                ChildMgr: null!,
                NodeRegistry: null!,
                Trace: null!,
                SnapshotMgr: null!,
                Stack: null!,
                PreconditionChecker: new ThrowingPreconditionChecker());

            var next = await fsm.StepAsync(stepCtx);

            Assert.Equal(TraversalState.ErrorHandling, next);
            Assert.Contains(provider.Lines, l =>
                l.Contains("[t=run-abc-123]")
                && l.Contains("InvalidOperationException")
                && l.Contains("Step dispatch failed"));
        }
        finally
        {
            RunTraceContext.Instance.Pop();
        }
    }

    // ── Task 3.4-2: FSM DomainValidation ─────────────────────────────────────

    [Fact(DisplayName = "FSM日志: 无效转换 → LogWarning 带被拒转换信息")]
    public void TransitionTo_InvalidTransition_LogsWarningWithRejectedTransition()
    {
        var provider = new InMemoryLoggerProvider();
        using var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });
        var logger = factory.CreateLogger<TraversalFSM>();
        var fsm = new TraversalFSM(new TraversalRuntimeContext("test-trace"), logger);

        // NodeSelect → PopupHandling 不在转换矩阵 → DomainValidationException → LogWarning
        var result = fsm.TransitionTo(TraversalState.PopupHandling, nodeId: "n1");

        Assert.False(result.IsSuccess);
        Assert.Contains(provider.Lines, l =>
            l.Contains("[WARN ]")
            && l.Contains("rejected")
            && l.Contains("NodeSelect")
            && l.Contains("PopupHandling"));
    }

    // ── Task 3.4-3: ErrorHandler 分类信息 ────────────────────────────────────

    [Fact(DisplayName = "ErrorHandler日志: 分类完成 → LogInformation 带 errorType/strategy/retryCount")]
    public void HandleError_LogsClassificationInformation()
    {
        var provider = new InMemoryLoggerProvider();
        using var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });
        var logger = factory.CreateLogger<ErrorHandler>();
        var handler = new ErrorHandler(logger: logger);

        var result = handler.HandleError(
            new ErrorClassificationContext(ErrorMessage: "connection timed out"),
            new StrategySelectionContext(
                RetryCount: 1, MaxRetries: 3,
                CanBacktrack: false, StackDepth: 1, CanSkip: true));

        Assert.Equal(ErrorStrategy.Retry, result.Strategy);
        Assert.Contains(provider.Lines, l =>
            l.Contains("[INFO ]")
            && l.Contains("Error classified")
            && l.Contains("Timeout")
            && l.Contains("strategy=Retry")
            && l.Contains("retry=1"));
    }

    // ── Task 3.4-4: ErrorHandler 管道兜底 ────────────────────────────────────

    [Fact(DisplayName = "ErrorHandler日志: classify 抛异常 → LogError 带 pipeline fallback 与异常类型")]
    public void HandleError_ClassifyThrows_LogsPipelineFallbackError()
    {
        var provider = new InMemoryLoggerProvider();
        using var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });
        var logger = factory.CreateLogger<ErrorHandler>();
        var handler = new ErrorHandler(
            classify: ctx => throw new InvalidOperationException("classify exploded"),
            selectStrategy: (type, ctx) => ErrorStrategy.Abort,
            execute: (strategy, ctx) => new ErrorRecoveryResult(strategy, RecoveryOutcome.Success, 0),
            logger: logger);

        var result = handler.HandleError(
            new ErrorClassificationContext(ErrorMessage: "boom"),
            new StrategySelectionContext(
                RetryCount: 0, MaxRetries: 3,
                CanBacktrack: false, StackDepth: 1, CanSkip: true));

        Assert.Equal(ErrorStrategy.Abort, result.Strategy);
        Assert.Equal(RecoveryOutcome.Failure, result.Outcome);
        Assert.Contains(provider.Lines, l =>
            l.Contains("[ERROR]") && l.Contains("pipeline fallback"));
        Assert.Contains(provider.Lines, l =>
            l.Contains("InvalidOperationException"));
    }

    // ── Task 3.4-5: 默认构造 NullLogger ──────────────────────────────────────

    [Fact(DisplayName = "默认构造: FSM/ErrorHandler 无 logger → NullLogger 默认，不抛异常")]
    public void DefaultConstructor_NoLogger_UsesNullLogger()
    {
        var fsm = new TraversalFSM(new TraversalRuntimeContext("test-trace"));
        var next = fsm.StepAsync().GetAwaiter().GetResult(); // 走 NullLogger 路径
        Assert.Equal(TraversalState.Branch, next);

        var handler = new ErrorHandler();
        var result = handler.HandleError(
            new ErrorClassificationContext(ErrorMessage: "boom"),
            new StrategySelectionContext(
                RetryCount: 0, MaxRetries: 3,
                CanBacktrack: false, StackDepth: 1, CanSkip: true));
        Assert.Equal(ErrorStrategy.Continue, result.Strategy); // Unknown → 默认链首 Continue
    }

    // ── Test helpers ─────────────────────────────────────────────────────────

    private sealed class ThrowingPreconditionChecker : IPreconditionChecker
    {
        public Task<bool> CheckAsync(
            TraversalRuntimeContext context,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("precondition gate exploded");
    }

    /// <summary>
    /// 捕获日志行的内存 LoggerProvider — 行格式对齐 TraceCorrelatedLogger:
    /// [HH:mm:ss.fff] [t=...] [s=...] [LEVEL] ShortCategory: message（异常行缩进）。
    /// </summary>
    private sealed class InMemoryLoggerProvider : ILoggerProvider
    {
        public List<string> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, this);
        public void Dispose() { }

        private sealed class InMemoryLogger : ILogger
        {
            private readonly string _category;
            private readonly InMemoryLoggerProvider _provider;

            public InMemoryLogger(string category, InMemoryLoggerProvider provider)
            {
                _category = category;
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var msg = formatter(state, exception);
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var traceId = RunTraceContext.Instance.Current ?? "-";
                var spanId = EngineStepSpanContext.Instance.CurrentSpanId ?? "-";
                var levelLabel = logLevel switch
                {
                    LogLevel.Trace => "TRACE",
                    LogLevel.Debug => "DEBUG",
                    LogLevel.Information => "INFO ",
                    LogLevel.Warning => "WARN ",
                    LogLevel.Error => "ERROR",
                    LogLevel.Critical => "CRIT ",
                    _ => "NONE "
                };
                var shortCat = _category.Split('.').Last();
                var line = $"[{timestamp}] [t={traceId}] [s={spanId}] [{levelLabel}] {shortCat}: {msg}";
                _provider.Lines.Add(line);
                if (exception != null && (logLevel == LogLevel.Error || logLevel == LogLevel.Critical))
                    _provider.Lines.Add($"    {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
