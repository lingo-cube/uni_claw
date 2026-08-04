## Why

运行链路打通后，产物（trace.jsonl / result.json / manifest.json / steps/）已经丰富，但**从 trace 快速分析问题**的能力缺失：排查失败 run 要手动翻 JSONL、性能问题无时间线视图、跨运行回归无工具化对比，Agent 也无法结构化消费 trace 数据。

## What Changes

- **新增 `src/UniClaw.TraceTool/` 项目**（C# CLI + TUI）：
  - `list` — run 发现（`--status` / `--task-id` 过滤）
  - `timeline` — 性能时间线（step 表 + AI 延迟分布）
  - `diagnose` — 故障根因推断（复用 CompletionMonitor / ErrorLoopAnalyzer / VerificationAnalyzer 规则）
  - `diff` — 跨运行结构化对比
  - `report` — Markdown / Mermaid 导出
  - `interactive` — Terminal.Gui TUI 浏览器
- **全部命令支持 `--format json`**：stdout 纯 JSON + 稳定 schema + 退出码契约（0/1/2/3），供 Agent / 脚本快速消费
- **运行实例元数据增强（Host 侧）**：`RunManifestInput` / `RunManifest` 新增可选字段 `Purpose`、`TaskId`、`RunSystemInfo`（ADB getprop 采集）、`RunMachineInfo`（RuntimeInformation 采集）——预期目的、任务关联、系统版本、机器信息全部入 manifest，向后兼容

## Capabilities

### New Capabilities

- `trace-analyzer-cli`: TraceTool CLI 的 6 个子命令行为、`--format json` 输出契约与退出码
- `trace-run-aggregate`: `TraceRun` / `TraceRunLoader` / `RunDiffer` 核心类型——磁盘 trace 回放、产物聚合、跨运行 diff
- `run-metadata-enrichment`: manifest 新增 Purpose / TaskId / RunSystemInfo / RunMachineInfo 元数据的采集与注入

### Modified Capabilities

（无 — 现有 spec 的 REQUIREMENTS 不变；manifest 字段新增不改变任何既有需求语义）

## Impact

- **新增**：`src/UniClaw.TraceTool/`（console app, 引用 UniClaw.Core）+ `tests/UniClaw.TraceTool.Tests/`
- **修改**：`src/UniClaw.Host/Artifacts/RunAssets.cs`（RunManifestInput / RunManifest 新增可选字段 + RunSystemInfo / RunMachineInfo 记录）、`src/UniClaw.Host/Commands/HostCommands.cs`（采集注入 + CLI 选项）、`src/UniClaw.sln` 或解决方案文件
- **依赖**：Spectre.Console（CLI 渲染）、Terminal.Gui（TUI）、System.CommandLine（CLI 解析）
- **兼容性**：manifest 新字段全部 optional——旧 run 目录 / 旧读取路径零破坏；Core 接口（ITraceRecorder / ITraceService）零改动
- **复用**：FileTraceStorage 回读、InMemoryTraceService、CompletionMonitor / ErrorLoopAnalyzer / VerificationAnalyzer、AssetRedactor 脱敏
