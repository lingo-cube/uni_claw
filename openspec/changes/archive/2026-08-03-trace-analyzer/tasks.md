## 1. 项目脚手架

- [x] 1.1 创建 `src/UniClaw.TraceTool/UniClaw.TraceTool.csproj`（net10.0 console app，引用 UniClaw.Core；包引用 System.CommandLine、Spectre.Console、Terminal.Gui）
- [x] 1.2 创建 `tests/UniClaw.TraceTool.Tests/UniClaw.TraceTool.Tests.csproj`（xunit，引用 TraceTool + Core）
- [x] 1.3 将两个新项目接入 solution（uni-claw.sln）
- [x] 1.4 Program.cs 骨架：`uni-claw trace` 根命令 + 6 个子命令注册（list/timeline/diagnose/diff/report/interactive），`--format` 全局选项
- [x] 1.5 从既有 snapshot 提取最小 JSONL fixture 到测试项目（trace.jsonl + manifest.json + result.json 三件套，含失败 run 与成功 run 各一份）

## 2. Host 元数据增强（run-metadata-enrichment）

- [x] 2.1 `RunAssets.cs` 新增 `RunSystemInfo` / `RunMachineInfo` sealed records（字段见 spec）
- [x] 2.2 `RunManifestInput` 新增可选 `Purpose` / `TaskId` / `SystemInfo` / `MachineInfo`；`RunManifest` 同增并透传（BuildManifest）
- [x] 2.3 Host 新增采集器：`RunMachineInfoCollector`（RuntimeInformation + Environment.MachineName，零外部依赖）+ `AdbSystemInfoCollector`（getprop 采集 RunSystemInfo，失败返回 null）
- [x] 2.4 `HostCommands.RunScenarioAsync` 注入：读取 `--purpose` / `--task-id`（CLI 选项）+ `UNICLAW_RUN_PURPOSE` / `UNICLAW_TASK_ID`（env 兜底）→ RunManifestInput
- [x] 2.5 Host 运行命令 CLI 注册 `--purpose` / `--task-id` 选项

## 3. TraceRun 聚合层（trace-run-aggregate）

- [x] 3.1 `TraceRunLoader.LoadAsync(runDir)`：FileTraceStorage 回放 → InMemoryTraceService（ITraceQuery）
- [x] 3.2 `TraceRun` 聚合：RunResult / RunManifest / ITraceQuery / StepAsset[]（懒加载）/ 元数据暴露（缺失 → "unknown"）
- [x] 3.3 `RunDiffer.Diff(runA, runB)` → RunDiff（StepDiffs / MetricDiffs / AI 对比 / Conclusion 一行话）
- [x] 3.4 规则引擎：复用 CompletionMonitor / ErrorLoopAnalyzer / VerificationAnalyzer（Host 引用）+ 新增聚合规则（ai_call_failures 分组、时间线空洞检测）

## 4. CLI 命令

- [x] 4.1 `list`：扫描 run 目录 + `--status` / `--task-id` / `--limit` 过滤，表格/JSON 双输出
- [x] 4.2 `timeline`：engine.step 时间线表 + AI 延迟分布（capability 分组 min/avg/p50/p95/max）+ `--threshold` 高亮
- [x] 4.3 `diagnose`：规则引擎 → verdict（cause/failingStep/summary/confidence）+ evidence + suggestions + artifactPaths
- [x] 4.4 `diff`：RunDiffer → 差异表格/JSON + 行为差异时退出码 1
- [x] 4.5 `report`：Markdown（时间线 + 诊断摘要）/ Mermaid 时序图导出（`--out`）

## 5. JSON 契约 + 退出码（trace-analyzer-cli）

- [x] 5.1 `--format json` 输出器：schemaVersion + stdout 纯 JSON / stderr 日志分离
- [x] 5.2 evidence 上限（默认 5 条）+ 非 TTY 自动去装饰（Spectre.Console 检测）
- [x] 5.3 退出码契约统一入口：0/1/2/3（用法错误、run 不存在、空 trace、diff 差异）

## 6. TUI（interactive）

- [x] 6.1 Terminal.Gui 布局：左 step 列表（慢项高亮）+ 右详情面板（AI 调用明细 + 截图路径）
- [x] 6.2 键位：↑↓ 选择 / Enter 详情 / T 时间线 / R 报告 / Q 退出
- [x] 6.3 截图系统打开（open / xdg-open）+ `TERM=dumb` 拒绝启动（退出 2）
- [x] 6.4 TUI 复用 diagnose 规则引擎（同一结论源）

## 7. 测试

- [x] 7.1 TraceRunLoader / TraceRun / RunDiffer 单元测试（fixture 驱动，含坏行跳过、result.json 缺失降级）
- [x] 7.2 CLI 测试：6 个子命令 happy path + 错误路径（System.CommandLine invoke + capture）
- [x] 7.3 JSON 契约测试：schemaVersion / stdout-stderr 分离 / evidence 上限 / 退出码
- [x] 7.4 diagnose 规则引擎测试：用现有失败 trace snapshot 断言 cause 分类
- [x] 7.5 Host 元数据测试：Purpose/TaskId/SystemInfo/MachineInfo 写入 manifest + env 注入 + 旧 manifest 兼容（null 容忍）

## 8. 验证与文档

- [x] 8.1 `dotnet build` 全 solution 0 错误；Core + Host 既有测试全绿（回归）
- [x] 8.2 真实 run 目录端到端冒烟：list → timeline → diagnose --format json
- [x] 8.3 更新 docs/system/decisions/log.md（D-184+：TraceTool 独立项目、JSON 契约优先、元数据进 manifest、规则复用）
- [x] 8.4 更新 docs/validation/unit_test_status.md（validation-documentation skill 规范）

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| src/UniClaw.TraceTool/ | docs/superpowers/specs/2026-08-03-trace-analyzer-design.md |
| src/UniClaw.Host/ | docs/system/layers/host.md |
| openspec/changes/trace-analyzer/ | design.md（本 change 决策 D1–D7） |
