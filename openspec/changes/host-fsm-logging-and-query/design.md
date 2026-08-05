## Context

日志基础设施已完成：`TraceCorrelatedFileProvider` 写入 `trace/{runId}/run.log`，
`TraceCorrelatedConsoleProvider` 同步输出 stderr。每条日志行自带 `[t=<runId>] [s=<spanId>]` 关联。
日志级别由 `UNICLAW_LOG_LEVEL` 环境变量控制，默认 `Information`。

当前埋点仅有 run 启动/结束（Host）、step 启动（Engine，Debug 级别）、非法状态转换被拒（FSM，Warning）、
dispatch 异常（FSM，Error）、错误分类（ErrorHandler，Info）。**默认 Info 级别下，引擎的核心运行路径完全不可见。**

需求来自 [host-fsm-logging-and-query PRD](../../docs/prd/host-fsm-logging-and-query.md)。

## Goals / Non-Goals

**Goals:**
- 默认 Info 级别下，run.log 包含完整的操作→分析→状态链路
- 新增 ILogger 注入遵循现有 NullLogger 可选模式，不破坏已有调用方
- 日志消费者（skill + agent）能用标准 grep 命令快速定位

**Non-Goals:**
- 不改变日志基础设施（Provider/格式/落盘路径）
- 不加 Debug/Trace 级别细粒度日志（需要调试时用户自行设 `UNICLAW_LOG_LEVEL=Debug`）
- 不修改 trace.jsonl 或 result.json 的格式
- 不引入结构化日志框架变更（保持 `Microsoft.Extensions.Logging`）

## Decisions

### D-1: ILogger 注入模式——可选 ctor 参数 + NullLogger 默认

**选择**：遵循 `TraversalFSM` / `ErrorHandler` / `TraversalEngine` 已有模式：
```csharp
public SafeActionExecutor(
    ...existing params...,
    ILogger<SafeActionExecutor>? logger = null)
{
    _logger = logger ?? NullLogger<SafeActionExecutor>.Instance;
}
```

**备选**：强制注入（无默认值）→ 拒绝。会破坏所有已有的 `new SafeActionExecutor(...)` 调用。

**备选**：静态 Logger → 拒绝。无法按 run 隔离，也无法用 DI 统一管理。

### D-2: 日志级别——全部 Info，deny 用 Warning

- 正常操作结果、页面分析、FSM 转换、引擎终止 → **Info**（默认可见）
- 安全门拒绝 → **Warning**（表示"引擎想做但被规则阻止"，不同于 Error）
- 异常/崩溃 → 保持已有 **Error**（`TraversalFSM.StepAsync` 的 catch 块）

**理由**：Info 是默认级别，用户不设任何 env 就能看到完整链路。Warning 级别让 deny 可以从 Info 噪音中独立 grep。

### D-3: 页面分析日志——仅缓存 miss 时记录

`InvalidatingPageAnalysisCache` 在缓存命中时直接返回 `_cached`，不产生日志。
只在缓存 miss（实际模型调用）后记录。

**理由**：缓存命中的分析不会被引擎重评估，记录没有信息量，反而刷屏
（同一页面上的多个 step 会重复触发分析）。

### D-4: 消费者分层——skill 做 grep / agent 做交叉引用

- **skill**：grep 命令列表（按 component / level / spanId），不做归因
- **agent**：用 spanId 跨 trace.jsonl 和 run.log 做交叉引用，结合 L2 状态机知识做归因

**理由**：skill 是操作手册（操作者不需要理解状态机），agent 是诊断引擎（需要 L1-L4 全层知识）。
分工与已有 trace.jsonl 消费一致——skill 看摘要，agent 做深度。

### D-5: 不记录 Hook 生命周期（Debug 级别除外）

Hook 调用（`OnBeforeStep` / `OnAfterStep` / `OnError`）不在 Info 级别记录。
已有代码中 `TraversalEngine` 的 `_logger.LogDebug` 可覆盖此需求。

**理由**：Hook 是内部机制，排障时很少需要逐 hook 跟踪。需要时设 `UNICLAW_LOG_LEVEL=Debug`。

## Risks / Trade-offs

| 风险 | 缓解 |
|------|------|
| 日志量在长 run 中增长（enumerate 模式可能有数百 step） | 每 step 约 3-5 行日志，100 step = 300-500 行 ≈ 20KB；file provider 使用 StreamWriter 缓冲写入，性能影响可忽略 |
| 页面分析日志在短轮询场景刷屏 | D-3 的缓存 miss-only 策略 + `InvalidatingPageAnalysisCache` 内置缓存确保同一页面只记一次分析 |
| 日志格式变更（破坏已有脚本） | 不改变现有日志行的格式；新增的日志行使用新的 Category 名称（`SafeActionExecutor`、`InvalidatingPageAnalysisCache`） |

## Migration Plan

1. 新增 ILogger 参数到 `SafeActionExecutor` 和 `InvalidatingPageAnalysisCache`——可选，已有调用方无感
2. 在 `HostCommands.CreateRunServices` 通过 `loggerFactory.CreateLogger<T>()` 注入
3. 对于不使用 `HostCommands` 组合根的调用方（如单元测试），`NullLogger<T>.Instance` 默认生效，无日志输出
4. 无回滚需求——新增行为，不影响已有功能
