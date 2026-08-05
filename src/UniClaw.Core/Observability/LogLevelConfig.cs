using Microsoft.Extensions.Logging;

namespace UniClaw.Core.Observability;

/// <summary>
/// Log level configuration from UNICLAW_LOG_LEVEL environment variable.
/// Valid values: trace, debug, information, warning, error, critical.
/// Default: Information. Unknown values default to Information (never throw).
/// </summary>
public static class LogLevelConfig
{
    public const string EnvVarName = "UNICLAW_LOG_LEVEL";

    public static LogLevel GetMinimumLevel()
    {
        var value = Environment.GetEnvironmentVariable(EnvVarName);
        return ParseLevel(value);
    }

    public static LogLevel ParseLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LogLevel.Information;

        return value.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" or "info" => LogLevel.Information,
            "warning" or "warn" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" or "crit" or "fatal" => LogLevel.Critical,
            _ => LogLevel.Information // unknown → default, never throw
        };
    }

    /// <summary>Validate and throw for config file parsing (fail-fast).</summary>
    public static LogLevel ParseLevelStrict(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LogLevel.Information;

        return value.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" or "info" => LogLevel.Information,
            "warning" or "warn" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" or "crit" or "fatal" => LogLevel.Critical,
            _ => throw new ArgumentException(
                $"Invalid log level '{value}'. Valid values: trace, debug, information, warning, error, critical.")
        };
    }
}
