## Why

全仓库无 `ILogger` 使用，日志为裸 `Console.Error.WriteLine`（Core 5 处、Host 12 处、TraceTool 13 处），无统一格式、无级别、无 trace 关联。状态机异常（`TraversalFSM` step catch 路由、`ErrorHandler` 分类与 pipeline fallback）**只进 trace 事件流、控制台零文本输出**——排查时不可见；stderr 易失，run 结束后（常是数分钟/小时后）日志已不可查，trace-analyzer"运行日志补充取证"无地址可查（P3.1 教训）。trace/span 关联基础设施已就位（TraceId = runId、`EngineStepSpanContext` AsyncLocal、`TraceSpanScope` 唯一 span 生命周期封装），缺的是日志出口本身。

## What Changes

- **Core 引入标准 `ILogger` 抽象**（`Microsoft.Extensions.Logging.Abstractions`，仅接口包）：新增 `TraceCorrelatedConsoleProvider`（stderr）+ `TraceCorrelatedFileProvider`（`trace/{runId}/run.log`），统一行格式 `[HH:mm:ss.fff] [t={runId}] [s={spanId}] [LVL] {Category}: {message}`；级别门控 `UNICLAW_LOG_LEVEL`（默认 information）。
- **全 span 覆盖**：`EngineStepSpanContext` 栈化（Push/Pop/栈顶读取），span 上下文同步点收敛在 `TraceSpanScope`（构造 push / Dispose pop）——任何 span 区域内日志可取当前 span id，**SourceGen Emitter 零改动**；`TraversalEngine` 显式 `Set`/`Reset`（:282/:541）删除；ai.call parent 语义从"仅 engine.step"扩展为"当前最内层 span"（D-3，S1-S6 需回归确认）。
- **新增 `RunTraceContext`**（AsyncLocal runId）：Host run 边界 push/pop，引擎/FSM 内日志带 runId 不经参数传递。
- **补充日志点（重点：状态机异常）**：L1 FSM step catch 路由、L2 `ErrorHandler` 分类结果、L3 pipeline fallback 完整异常、L4 引擎 step 开/关（Debug 门控）、L5/L6 Host run 开始/结束、L7 资产提交失败（与 `asset_write_failed` issue 同步）、L8 run 终态。
- **存储与对外告知**：`result.json` 新增 `RunLogPath` 字段（`"runLogPath": "trace/{runId}/run.log"`，对称 `TracePath` 先例，schemaVersion 不 bump——缺字段读侧回退默认）；`RunLayoutV2` 增加布局解析辅助；`integration.config.json` 新增 `logging.level`（测试装配注入 `UNICLAW_LOG_LEVEL`）。V2 布局增补 `trace/{runId}/run.log` 行（技术事实同步 unified PRD §3.1）。
- **注入形态**：记录日志的类可选构造注入 `ILogger<T>? = null`（NullLogger 缺省）——既有测试零波及；组合根 LoggerFactory 装配双 provider。
- 日志不进 trace.jsonl（TraceFields 45-key frozen catalog 不动）；日志与 trace 靠 id 关联而非合并。

## Capabilities

### New Capabilities

- `trace-correlated-logging`: Core 统一日志出口（`ILogger` + 双 provider + 栈式 span 通道 + `RunTraceContext`）+ 状态机/引擎/Host 关键路径日志点 + 级别配置；日志行携带 TraceId/spanId，与 trace 事件流交叉关联。

### Modified Capabilities

- `run-layout-v2`: V2 布局增补 `trace/{runId}/run.log`（trace 侧、流式追加文本诊断、非资产管线产物）；`RunLayoutV2` 增加 run.log 布局解析辅助。
- `run-metadata-enrichment`: `RunResult` 新增 `RunLogPath` 字段（result.json 输出 `runLogPath` 相对路径；读侧缺失回退默认，V1 run 解析为"无日志"）。

## Impact

- **Core**（`src/UniClaw.Core/`）：csproj 新增 `Microsoft.Extensions.Logging.Abstractions`；`Observability/` 新增 `TraceCorrelatedConsoleProvider`/`TraceCorrelatedFileProvider`/`RunTraceContext`；`EngineStepSpanContext` 栈化；`TraceSpanScope` 构造/Dispose 同步 span 上下文；`TraversalEngine` 删除显式 Set/Reset 并加 step 级 Debug 日志；`TraversalFSM`/`ErrorHandler` 接入 `ILogger<T>?` 异常日志点。S1-S6 span-tree 快照门需回归（D-3 parent 语义）。
- **Host**（`src/UniClaw.Host/`）：组合根创建 LoggerFactory（console + file 双 provider），run 边界 push/pop `RunTraceContext` + 文件 provider 生命周期（finally Flush+Close）；`HostCommands` run 开始/结束/资产失败/终态日志点；`RunAssets.cs` `RunResult` 加 `RunLogPath` 并 finalize 写出。
- **TraceTool**（`src/UniClaw.TraceTool/`）：`TraceRunLoader` 解析链加 `result?.RunLogPath ?? "trace/{runId}/run.log"` 回退；`RunLayoutV2` 布局辅助。CLI 提示格式不变。
- **测试**：`tests/UniClaw.Host.Tests/Integration/integration.config.json` 加 `logging` 段 + loader 校验；Core.Tests 新增 provider/通道/FSM 异常日志测试。
- 既有测试零构造签名变化（可选注入默认参数）；`TraceFields` 45-key catalog 不变。
- 设计源：`docs/prd/2026-08-04-trace-correlated-logging-prd.md`（12 条决策 D-1~D-12，实现/归档映射 log.md D-222~D-233）。
