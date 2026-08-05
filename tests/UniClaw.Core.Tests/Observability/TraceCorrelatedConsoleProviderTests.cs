using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// TraceCorrelatedConsoleProvider tests (trace-correlated-logging task 2.5):
/// 格式正则、level 标签（5 字符定宽）、无上下文 → "-" 占位、category 短名、
/// 异常堆栈仅 Error/Critical 输出、level 过滤在 LoggerFactory 层生效。
/// </summary>
public class TraceCorrelatedConsoleProviderTests
{
    /// <summary>
    /// 输出格式: [HH:mm:ss.fff] [t=...] [s=...] [LEVEL] ShortCategory: message
    /// 注意: INFO/WARN/CRIT 标签带尾随空格（5 字符定宽），\w{4,5} 只对
    /// TRACE/DEBUG/ERROR（纯词字符）成立 — 格式断言用例使用 Error 级别。
    /// </summary>
    private static readonly Regex FormatRegex = new(
        @"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[t=\S+\] \[s=\S+\] \[\w{4,5}\] \S+: .*$",
        RegexOptions.Compiled);

    /// <summary>重定向 Console.Error 到 StringWriter；用完后恢复原始 writer。</summary>
    private static (StringWriter captured, TextWriter original) CaptureStderr()
    {
        var captured = new StringWriter();
        var original = Console.Error;
        Console.SetError(captured);
        return (captured, original);
    }

    private static string[] LinesOf(StringWriter writer)
        => writer.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .ToArray();

    [Fact(DisplayName = "ConsoleProvider: 输出行匹配关联格式正则 [t=..] [s=..] [LEVEL]")]
    public void Format_WithRunAndSpanContext_MatchesLineRegex()
    {
        RunTraceContext.Instance.Push("run-42");
        EngineStepSpanContext.Instance.Push("span-7");
        try
        {
            var (captured, original) = CaptureStderr();
            try
            {
                using var factory = LoggerFactory.Create(
                    builder => builder.AddProvider(new TraceCorrelatedConsoleProvider()));
                var logger = factory.CreateLogger("TestCategory");
                logger.LogError("format message");
            }
            finally
            {
                Console.SetError(original);
            }

            var line = LinesOf(captured).Single(l => l.Contains("format message"));
            Assert.Matches(FormatRegex, line);
        }
        finally
        {
            RunTraceContext.Instance.Pop();
            EngineStepSpanContext.Instance.Pop();
        }
    }

    [Fact(DisplayName = "ConsoleProvider: 6 个 level 标签均为 5 字符定宽（INFO/WARN/CRIT 尾随空格）")]
    public void LevelLabels_AreFixedWidthFiveChars()
    {
        var (captured, original) = CaptureStderr();
        try
        {
            using var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddProvider(new TraceCorrelatedConsoleProvider());
            });
            var logger = factory.CreateLogger("TestCategory");
            logger.LogTrace("msg-trace");
            logger.LogDebug("msg-debug");
            logger.LogInformation("msg-info");
            logger.LogWarning("msg-warn");
            logger.LogError("msg-error");
            logger.LogCritical("msg-critical");
        }
        finally
        {
            Console.SetError(original);
        }

        var lines = LinesOf(captured);
        Assert.Contains("[TRACE]", LineOf(lines, "msg-trace"));
        Assert.Contains("[DEBUG]", LineOf(lines, "msg-debug"));
        Assert.Contains("[INFO ]", LineOf(lines, "msg-info"));
        Assert.Contains("[WARN ]", LineOf(lines, "msg-warn"));
        Assert.Contains("[ERROR]", LineOf(lines, "msg-error"));
        Assert.Contains("[CRIT ]", LineOf(lines, "msg-critical"));
    }

    [Fact(DisplayName = "ConsoleProvider: 无 RunTraceContext/SpanContext → [t=-] [s=-]")]
    public void NoContext_EmitsDashPlaceholders()
    {
        // 本测试不 Push 任何 context → 两处均输出 "-" 占位。
        var (captured, original) = CaptureStderr();
        try
        {
            using var factory = LoggerFactory.Create(
                builder => builder.AddProvider(new TraceCorrelatedConsoleProvider()));
            var logger = factory.CreateLogger("TestCategory");
            logger.LogError("no-context message");
        }
        finally
        {
            Console.SetError(original);
        }

        var line = LinesOf(captured).Single(l => l.Contains("no-context message"));
        Assert.Contains("[t=-] [s=-]", line);
    }

    [Fact(DisplayName = "ConsoleProvider: category 输出短名（去掉命名空间前缀）")]
    public void Category_EmitsShortNameOnly()
    {
        var (captured, original) = CaptureStderr();
        try
        {
            using var factory = LoggerFactory.Create(
                builder => builder.AddProvider(new TraceCorrelatedConsoleProvider()));
            var logger = factory.CreateLogger("UniClaw.Core.StateMachine.ErrorHandler");
            logger.LogError("short category message");
        }
        finally
        {
            Console.SetError(original);
        }

        var line = LinesOf(captured).Single(l => l.Contains("short category message"));
        Assert.Contains("ErrorHandler:", line);
        Assert.DoesNotContain("UniClaw.Core.StateMachine.ErrorHandler:", line);
    }

    [Fact(DisplayName = "ConsoleProvider: 异常堆栈仅 Error/Critical 输出，Info 不输出")]
    public void Exception_InfoLevelNoStack_ErrorLevelHasIndentedStack()
    {
        // 真实抛出 → StackTrace 非空；Info 级别不得渲染异常信息。
        Exception thrown;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        var (captured, original) = CaptureStderr();
        try
        {
            using var factory = LoggerFactory.Create(
                builder => builder.AddProvider(new TraceCorrelatedConsoleProvider()));
            var logger = factory.CreateLogger("TestCategory");
            // 显式 formatter 忽略 exception → 消息本身不含异常类型，
            // 验证 Info 级别 provider 不追加异常块。
            logger.Log(
                LogLevel.Information, 0, "info-with-exception", thrown,
                static (s, e) => s.ToString());
            logger.Log(
                LogLevel.Error, 0, "error-with-exception", thrown,
                static (s, e) => s.ToString());
        }
        finally
        {
            Console.SetError(original);
        }

        var lines = LinesOf(captured);

        var infoLine = lines.Single(l => l.Contains("info-with-exception"));
        Assert.DoesNotContain("InvalidOperationException", infoLine);

        // 主行（消息）+ 独立异常行（4 空格缩进），异常信息在 Error 级别追加输出。
        Assert.Contains(lines, l => l.Contains("error-with-exception") && l.Contains("[ERROR]"));
        Assert.Contains(lines, l => l.Contains("InvalidOperationException: boom"));

        // 堆栈帧以 4 空格缩进出现在 Error 输出中。
        Assert.Contains(lines, l => l.StartsWith("    ") && l.Contains(" at "));
    }

    [Fact(DisplayName = "ConsoleProvider: 最小级别过滤在 LoggerFactory 层生效（Warning 下过滤 Information）")]
    public void LevelFiltering_WarningMinimum_FiltersInformation()
    {
        var (captured, original) = CaptureStderr();
        try
        {
            using var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Warning);
                builder.AddProvider(new TraceCorrelatedConsoleProvider());
            });
            var logger = factory.CreateLogger("TestCategory");
            logger.LogInformation("filtered-info-msg");
            logger.LogError("filtered-error-msg");
        }
        finally
        {
            Console.SetError(original);
        }

        var lines = LinesOf(captured);
        Assert.Contains(lines, l => l.Contains("filtered-error-msg") && l.Contains("[ERROR]"));
        Assert.DoesNotContain(lines, l => l.Contains("filtered-info-msg"));
    }

    private static string LineOf(string[] lines, string marker)
        => lines.Single(l => l.Contains(marker, StringComparison.Ordinal));
}
