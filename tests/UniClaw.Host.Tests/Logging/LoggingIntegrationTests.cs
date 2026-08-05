using Microsoft.Extensions.Logging;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Host.Tests.Logging;

/// <summary>
/// LoggingIntegrationTests (trace-correlated-logging task 4.5) — 无设备集成测试:
/// 组合根 LoggerFactory 装配（Console + File 双 sink）、run 边界 RunTraceContext
/// Push/Pop、带 run/span 上下文的输出格式、文件 provider 异常路径 finally 关闭。
/// 完整模拟器链路测试受设备门槛限制，此处验证 Host 侧日志装配与上下文契约。
/// </summary>
public sealed class LoggingIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"uniclaw-host-logging-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact(DisplayName = "Host日志: 组合根模式 — Console+File 双 provider 装配不抛异常")]
    public void LoggerFactoryAssembly_ConsoleAndFileProviders_NoException()
    {
        // 对齐 HostCommands 组合根: 先 Console sink，run id 已知后追加 file sink，
        // 所有 logger 在双 sink 挂载后创建。
        var fileProvider = new TraceCorrelatedFileProvider(Path.Combine(_root, "run.log"));
        using var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new TraceCorrelatedConsoleProvider());
            builder.AddProvider(fileProvider);
        });

        var logger = factory.CreateLogger("UniClaw.Host.LoggingTest");
        logger.LogInformation("factory assembly ok"); // 不抛异常即通过

        Assert.NotNull(logger);
        Assert.True(File.Exists(Path.Combine(_root, "run.log")));
    }

    [Fact(DisplayName = "Host日志: run 边界 — Push 后 Current 匹配，Pop 后为 null")]
    public void RunTraceContext_RunBoundary_PushPop()
    {
        Assert.Null(RunTraceContext.Instance.Current);

        RunTraceContext.Instance.Push("run-boundary-1");
        try
        {
            Assert.Equal("run-boundary-1", RunTraceContext.Instance.Current);
        }
        finally
        {
            RunTraceContext.Instance.Pop();
        }

        Assert.Null(RunTraceContext.Instance.Current);
    }

    [Fact(DisplayName = "Host日志: 带 run+span 上下文的输出含 [t=..] [s=..]")]
    public void LogFormat_WithRunAndSpanContext_EmitsCorrelatedLine()
    {
        var captured = new StringWriter();
        var original = Console.Error;
        Console.SetError(captured);
        try
        {
            RunTraceContext.Instance.Push("test-run-123");
            EngineStepSpanContext.Instance.Push("span-456");
            try
            {
                using var factory = LoggerFactory.Create(
                    builder => builder.AddProvider(new TraceCorrelatedConsoleProvider()));
                var logger = factory.CreateLogger("UniClaw.Host");
                logger.LogError("test message");
            }
            finally
            {
                RunTraceContext.Instance.Pop();
                EngineStepSpanContext.Instance.Pop();
            }
        }
        finally
        {
            Console.SetError(original);
        }

        Assert.Contains("[t=test-run-123] [s=span-456]", captured.ToString());
        Assert.Contains("test message", captured.ToString());
    }

    [Fact(DisplayName = "Host日志: 文件 provider 异常路径 — finally 关闭后内容完整")]
    public void FileProvider_ExceptionPath_FinallyCloseFlushesContent()
    {
        var path = Path.Combine(_root, "trace", "run-1", "run.log");
        var provider = new TraceCorrelatedFileProvider(path);
        try
        {
            using var factory = LoggerFactory.Create(
                builder => builder.AddProvider(provider));
            var logger = factory.CreateLogger("UniClaw.Host");
            logger.LogError("content before exception");

            // 模拟 run 中途异常路径: 不显式关闭 provider，由 finally 兜底。
            throw new InvalidOperationException("simulated run failure");
        }
        catch (InvalidOperationException)
        {
            // 预期异常 — 吞掉，验证 finally 关闭路径。
        }
        finally
        {
            provider.Close();
        }

        Assert.True(File.Exists(path));
        Assert.Contains("content before exception", File.ReadAllText(path));
    }
}
