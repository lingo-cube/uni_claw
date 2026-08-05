using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// RunLayoutV2Tests — run.log 布局常量与纯路径解析辅助 (trace-correlated-logging
/// 任务 5.2): 事件流空间同目录 {runDir}/trace/{runId}/run.log, 相对路径供
/// result.json runLogPath 使用。纯函数 — 无 I/O, 无状态。
/// </summary>
public class RunLayoutV2Tests
{
    [Fact(DisplayName = "run.log 常量与路径解析: 事件流空间同目录 trace/{runId}/run.log")]
    public void RunLogHelpers_ComposeLayoutPaths()
    {
        Assert.Equal("run.log", RunLayoutV2.RunLogFileName);

        Assert.Equal(
            Path.Combine("runs", "trace", "run-1", "run.log"),
            RunLayoutV2.RunLogFilePath("runs", "run-1"));

        Assert.Equal(
            "trace/run-1/run.log",
            RunLayoutV2.RunLogRelativePath("run-1"));
    }

    [Fact(DisplayName = "run.log 与 trace.jsonl 同事件流空间 (同 runId 目录)")]
    public void RunLogAndTrace_ShareEventStreamDirectory()
    {
        var runId = "20260804T000000000Z-deadbeef";
        var fullLog = RunLayoutV2.RunLogFilePath("/runs/root", runId);
        var fullTrace = RunLayoutV2.TraceFilePath("/runs/root", runId);

        Assert.Equal(Path.GetDirectoryName(fullTrace), Path.GetDirectoryName(fullLog));
        Assert.Equal("run.log", Path.GetFileName(fullLog));
    }
}
