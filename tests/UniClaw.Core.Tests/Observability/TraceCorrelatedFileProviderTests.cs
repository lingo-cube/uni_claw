using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// TraceCorrelatedFileProvider tests (trace-correlated-logging task 2.5):
/// 文件输出格式、目录自动创建、Flush/Close 幂等、Close 后写入 no-op。
/// </summary>
public sealed class TraceCorrelatedFileProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"uniclaw-file-provider-{Guid.NewGuid():N}");

    private static readonly Regex FormatRegex = new(
        @"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[t=\S+\] \[s=\S+\] \[\w{4,5}\] \S+: .*$",
        RegexOptions.Compiled);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static (TraceCorrelatedFileProvider provider, ILogger logger) CreateLogging(string path)
    {
        var provider = new TraceCorrelatedFileProvider(path);
        var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        return (provider, factory.CreateLogger("FileTest"));
    }

    [Fact(DisplayName = "FileProvider: 日志写入文件且行格式匹配关联正则")]
    public void Write_ProducesFormattedLineInFile()
    {
        var path = Path.Combine(_root, "run.log");
        var (provider, logger) = CreateLogging(path);

        logger.LogError("file format message");
        provider.Close();

        Assert.True(System.IO.File.Exists(path));
        var line = System.IO.File.ReadAllText(path)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(l => l.Contains("file format message"));
        Assert.Matches(FormatRegex, line);
    }

    [Fact(DisplayName = "FileProvider: 目标目录不存在时自动创建")]
    public void Write_NonExistentDirectory_AutoCreated()
    {
        var path = Path.Combine(_root, "a", "b", "run.log");
        var (provider, logger) = CreateLogging(path);

        logger.LogError("nested dir message");
        provider.Close();

        Assert.True(System.IO.File.Exists(path));
        Assert.Contains("nested dir message", System.IO.File.ReadAllText(path));
    }

    [Fact(DisplayName = "FileProvider: Close 两次 + Close 后 Flush — 幂等不抛异常")]
    public void CloseAndFlush_AreIdempotent()
    {
        var path = Path.Combine(_root, "run.log");
        var (provider, logger) = CreateLogging(path);

        logger.LogError("idempotent message");
        provider.Close();
        provider.Close();
        provider.Flush(); // close 之后 flush — no-op，不抛异常

        Assert.True(System.IO.File.Exists(path));
        Assert.Contains("idempotent message", System.IO.File.ReadAllText(path));
    }

    [Fact(DisplayName = "FileProvider: Close 后写入为 no-op — 文件内容不变")]
    public void Write_AfterClose_IsNoOp()
    {
        var path = Path.Combine(_root, "run.log");
        var (provider, logger) = CreateLogging(path);

        logger.LogError("before-close message");
        provider.Close();
        var before = System.IO.File.ReadAllText(path);

        logger.LogError("after-close message"); // no-op，不抛异常

        Assert.Equal(before, System.IO.File.ReadAllText(path));
        Assert.DoesNotContain("after-close message", before);
    }
}
