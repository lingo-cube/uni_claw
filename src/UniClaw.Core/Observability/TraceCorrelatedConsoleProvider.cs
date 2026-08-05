using Microsoft.Extensions.Logging;

namespace UniClaw.Core.Observability;

public sealed class TraceCorrelatedConsoleProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TraceCorrelatedLogger(categoryName);
    public void Dispose() { }

    private sealed class TraceCorrelatedLogger : ILogger
    {
        private readonly string _category;
        private static readonly object _lock = new();

        public TraceCorrelatedLogger(string category) { _category = category; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true; // gating is at LoggerFactory level

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // Format and write the log line with lock
            var message = formatter(state, exception);
            var traceId = RunTraceContext.Instance.Current ?? "-";
            var spanId = EngineStepSpanContext.Instance.CurrentSpanId ?? "-";
            var levelLabel = LevelToLabel(logLevel);
            var shortCategory = _category.Split('.').Last();
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

            var line = $"[{timestamp}] [t={traceId}] [s={spanId}] [{levelLabel}] {shortCategory}: {message}";

            lock (_lock)
            {
                Console.Error.WriteLine(line);
                if (exception != null && (logLevel == LogLevel.Error || logLevel == LogLevel.Critical))
                {
                    Console.Error.WriteLine($"    {exception.GetType().Name}: {exception.Message}");
                    if (exception.StackTrace != null)
                    {
                        foreach (var frame in exception.StackTrace.Split('\n'))
                            Console.Error.WriteLine($"    {frame.TrimEnd()}");
                    }
                }
            }
        }

        private static string LevelToLabel(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            _ => "NONE "
        };
    }
}
