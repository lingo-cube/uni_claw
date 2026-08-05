using Microsoft.Extensions.Logging;

namespace UniClaw.Core.Observability;

public sealed class TraceCorrelatedFileProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private bool _closed;

    public TraceCorrelatedFileProvider(string filePath)
    {
        _filePath = filePath;
        // Ensure directory exists
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() => Close();

    public void Flush()
    {
        lock (_lock)
        {
            _writer?.Flush();
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            if (_closed) return;
            _closed = true;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }

    internal void WriteLine(string line)
    {
        lock (_lock)
        {
            if (_closed) return;
            _writer ??= new StreamWriter(_filePath, append: true) { AutoFlush = false };
            _writer.WriteLine(line);
        }
    }

    // Internal for PostCloseWriteDetection test
    internal bool IsClosed
    {
        get { lock (_lock) { return _closed; } }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly TraceCorrelatedFileProvider _provider;
        private readonly string _category;

        public FileLogger(TraceCorrelatedFileProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var traceId = RunTraceContext.Instance.Current ?? "-";
            var spanId = EngineStepSpanContext.Instance.CurrentSpanId ?? "-";
            var levelLabel = LevelToLabel(logLevel);
            var shortCategory = _category.Split('.').Last();
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

            var line = $"[{timestamp}] [t={traceId}] [s={spanId}] [{levelLabel}] {shortCategory}: {message}";
            _provider.WriteLine(line);

            if (exception != null && (logLevel == LogLevel.Error || logLevel == LogLevel.Critical))
            {
                _provider.WriteLine($"    {exception.GetType().Name}: {exception.Message}");
                if (exception.StackTrace != null)
                {
                    foreach (var frame in exception.StackTrace.Split('\n'))
                        _provider.WriteLine($"    {frame.TrimEnd()}");
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
