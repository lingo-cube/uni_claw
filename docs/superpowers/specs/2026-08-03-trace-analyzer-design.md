# Trace 分析工具设计（UniClaw.TraceTool）

> 状态: draft | 日期: 2026-08-03

## 1. 动机

运行链路打通后，产物（trace.jsonl / result.json / manifest.json / steps/）已经丰富，但**从 trace 快速分析问题**的能力缺失：

- 排查失败 run 要手动翻 JSONL + 拼接 step 与 AI 调用
- 性能问题（慢 step、AI 延迟）无时间线视图
- 跨运行对比（回归检测）无工具化支持
- 作为 Agent 的辅助工具时，需要结构化、可解析的输出而非冗长表格

目标：**C# CLI + TUI**（`src/UniClaw.TraceTool/` 新项目），人机两用——人看表格/TUI，agent 消费 JSON。

## 2. 架构

```
run 目录
  trace/*.jsonl ─→ FileTraceStorage.ReadBack() ─→ InMemoryTraceStorage ─→ InMemoryTraceService(ITraceQuery)
  result.json   ─→ TraceRun.RunResult          ─→ TraceRun 聚合
  manifest.json ─→ RunManifest (含新增元数据)   ─→ 各子命令消费
  steps/...     ─→ StepAsset[] (懒加载, 仅 interactive/report 需要)
```

关键点：

- **磁盘只读一次，全部回放到内存**；`TraceRun` 是唯一入口（`TraceRun.LoadAsync(runDir)`），子命令不直接碰文件
- 全部实现落在新项目 `src/UniClaw.TraceTool/` 内，**不改 Core 接口**（ITraceRecorder / ITraceService 零改动）
- 复用现有分析器（`CompletionMonitor`、`ErrorLoopAnalyzer`、`VerificationAnalyzer` 的 FailingStep/FailureCause 分类），不另造一套规则

### 2.1 项目结构

```
src/UniClaw.TraceTool/
├── UniClaw.TraceTool.csproj   → net10.0 console app, 引用 UniClaw.Core
├── Program.cs                 → CLI entry (System.CommandLine)
├── TraceRunLoader.cs          → Gap 1: 回放 FileTraceStorage → InMemoryTraceService
├── TraceRun.cs                → Gap 2: trace + result.json + manifest + step assets 聚合
├── RunDiffer.cs               → Gap 3: 两次运行结构化 diff
├── Commands/
│   ├── TimelineCommand.cs     → 性能时间线
│   ├── DiagnoseCommand.cs     → 自动诊断
│   ├── DiffCommand.cs         → 跨运行对比
│   ├── ListCommand.cs         → run 发现（agent 入口）
│   └── ReportCommand.cs       → Markdown/Mermaid 导出
└── Tui/
    └── TraceBrowser.cs        → TUI 交互浏览器 (Terminal.Gui)
```

### 2.2 核心类型

```
TraceRun (聚合一个完整 run)
├── RunResult          (从 result.json)
├── RunManifest        (从 manifest.json)
├── ITraceQuery        (从 trace.jsonl → InMemoryTraceService)
├── StepAsset[]        (从 steps/D4/analysis.json 等)
├── RunSystemInfo?     (系统版本, 从 manifest)
├── RunMachineInfo?    (机器信息, 从 manifest)
└── Screenshot(path)   (steps/D4/before.png 路径引用)

TraceRunLoader
├── LoadAsync(runDir) → TraceRun
└── 内部: FileTraceStorage(disk) → replay → InMemoryTraceStorage(mem) → InMemoryTraceService

RunDiffer
├── Diff(runA, runB) → RunDiff
└── RunDiff { StepDiffs[], MetricDiffs[], Conclusion }
```

## 3. CLI 入口（6 子命令）

```
uni-claw trace list        [--dir artifacts/runs] [--status failure] [--task-id <id>] [--limit 10]
uni-claw trace timeline    --run <path> [--threshold 10]
uni-claw trace diagnose    --run <path>
uni-claw trace diff        --run-a <a> --run-b <b>
uni-claw trace report      --run <path> [--format md|mermaid] [--out report.md]
uni-claw trace interactive --run <path>
```

所有命令支持 `--format json`（agent 消费）。默认人读格式为表格。

### 3.1 timeline — 性能时间线

输出两张表：

1. **Step 时间线表**：每行一个 step — `step号 | 类型 | 耗时 | AI调用数 | 截图`，慢 step 按阈值标红
2. **AI 延迟分布**：按 capability 分组的 `min / avg / p50 / p95 / max`，一眼看出瓶颈 capability

数据来源：`GetSpansByType("engine.step")` + 子 span `ai.call` 的 `LatencyMs`。

### 3.2 diagnose — 故障诊断

不引入新分析逻辑，复用现有分析器，输出故障根因：

```
Run: 20260803T120000Z-abc12345  →  status=failure
├─ FailureCause: no_actions_advised     (来自 VerificationAnalyzer)
├─ FailingStep: #3 scroll_down_loop
├─ 证据: skipped 7 次连续 > visited×4   (ErrorLoopAnalyzer 规则)
├─ AI 调用: screen_safety 拒绝 5 次     (AICallRecord.Success=false)
└─ 建议: 检查 safety gate 规则 / 换 label
```

规则引擎 = `CompletionMonitor` + `ErrorLoopAnalyzer` 现有规则 + 少量新规则（ai_call_failures 聚合、时间线空洞检测），全部在 TraceTool 项目内实现。

### 3.3 diff — 跨运行对比

结构化输出：

- **Step 级**：B 相比 A 新增 / 缺失 / 重排的 step
- **指标级**：steps / scrolls / actions / duration 差值
- **AI 级**：capability 分布、平均延迟变化
- **结论**：一行话（如 "B 少滚了 3 次，耗时 -22%"）

### 3.4 report — 导出

Markdown 报告（时间线表 + 诊断摘要）或 Mermaid 时序图（span 树 → 图），供贴 issue / 归档。

### 3.5 list — run 发现（agent 入口）

列出可用 run 目录 + 状态 + 耗时 + taskId。支持 `--status` / `--task-id` 过滤。Agent 流程：`list` → 挑一个 failure run → `diagnose --json` → 按 `artifactPaths` 深入读文件。三步定位原因。

### 3.6 interactive — TUI 浏览器

见 §5。

## 4. Agent 快速诊断接口

### 4.1 `--format json` 输出契约

`diagnose --run <path> --format json` 输出稳定 JSON schema（含 `schemaVersion`）：

```json
{
  "schemaVersion": "1",
  "runId": "20260803T120000Z-abc12345",
  "status": "failure",
  "run": {
    "runId": "20260803T120000Z-abc12345",
    "taskId": "ci-run-1234",
    "purpose": "PR #42 验收",
    "system": { "sdk": "35", "release": "15", "arch": "arm64-v8a" },
    "machine": { "os": "macOS 15.6", "arch": "arm64", "runtime": ".NET 10.0.1" }
  },
  "verdict": {
    "cause": "error_loop_stuck",
    "failingStep": 3,
    "summary": "连续 7 次 skip 同一 step（> visited×4），陷入滚动循环",
    "confidence": 0.92
  },
  "evidence": [
    { "type": "ai_call_failure", "capability": "screen_safety", "count": 5 },
    { "type": "span", "spanId": "s-42", "detail": "LatencyMs=12400 > p95" }
  ],
  "suggestions": ["检查 safety gate 规则", "检查 label-mapping 是否覆盖该页面"],
  "artifactPaths": { "result": "...", "trace": "...", "screenshot": "steps/D4/before.png" }
}
```

设计要点：

- **stdout 只输出 JSON**，日志/警告走 stderr — agent 用 `2>/dev/null` 拿干净结果
- `evidence` 有上限（默认 5 条），防止单次输出爆炸
- 非 TTY 时自动禁用表格装饰（Spectre.Console 的 `AnsiConsole` vs `Console` 检测）

### 4.2 退出码契约（脚本可分支）

| 码 | 含义 |
|----|------|
| 0 | 成功（diagnose: 诊断完成） |
| 1 | diff 检测到行为差异（agent 可 `if ! uni-claw trace diff ...; then` 判断回归） |
| 2 | 用法错误 / run 目录不存在 |
| 3 | 空 trace / 无 span |

## 5. TUI 交互模型（Terminal.Gui）

```
┌────────────────────────────┬──────────────────────────────────────┐
│ Steps (左)                 │ Detail (右)                          │
│ ────────────────────────── │ ──────────────────────────────────── │
│ #1  分析页面    1.2s  AI×2 │ Step #3  scroll_down_loop  3.8s     │
│ #2  点击目标    2.5s  AI×1 │                                    │
│ #3  滚动到底    3.8s  AI×5 │  AI 调用:                           │
│ #4  再次滚动    6.1s  AI×7 │   screen_safety   ×3  1.2s avg     │
│ #5  验证完成    0.4s  AI×0 │   decide_next     ×2  2.1s avg     │
│                           │  截图: steps/D4/before.png (Enter)  │
└────────────────────────────┴──────────────────────────────────────┘
       底部状态栏: ↑↓ 选择  Enter 详情  T 时间线  R 报告  Q 退出
```

- 左列：step 列表，按耗时高亮慢项；右列：选中 step 的 AI 调用明细 + 截图路径
- 数据来源 = `TraceRun` 聚合（trace + result.json + steps/analysis.json 交叉引用）
- 截图打开：调 `open <path>`（macOS）/ xdg-open（Linux），不内嵌图像
- TUI 层薄，逻辑都在 `TraceRun` 聚合里；TUI 的结论与 `diagnose --json` 共享同一规则引擎，避免两套结论来源

## 6. 错误处理

| 场景 | 行为 |
|------|------|
| run 目录不存在 / 无 trace 文件 | 退出码 2，stderr 提示 |
| JSONL 损坏（单行坏） | 跳过坏行 + 警告计数，不崩溃（FileTraceStorage 已有此语义） |
| result.json 缺失 | 降级：TraceRun 只带 trace，标注 "no result.json" |
| TUI 无终端 | interactive 命令检测 `TERM=dumb` 拒绝启动 |
| 空 trace（0 span） | 提示 "no spans found"，退出码 3 |

## 7. 运行实例元数据增强（run 产物侧）

现状：manifest.json 已有身份字段（RunId、ScenarioId、Policy、DeviceSerial、AndroidIdentity、ProviderId、Mode），但缺少**预期目的、任务关联、系统版本、机器信息**。

### 7.1 新增字段（扩展 `RunManifestInput` + `RunManifest`，全部 optional 向后兼容）

```csharp
// 输入侧新增（RunManifestInput）
string? Purpose     // 预期目的: 自由文本, 如 "PR #42 验收" / "locate 回归"
string? TaskId      // 任务关联: CI job id / agent session id, 如 "ci-run-1234"
RunSystemInfo?  SystemInfo   // 系统版本
RunMachineInfo? MachineInfo  // 机器信息

// 结构化子记录
public sealed record class RunSystemInfo(
    string? SdkLevel,          // "35"
    string? ReleaseVersion,    // "15"
    string? BuildFingerprint,  // google/sdk_gphone64...
    string? Codename,          // "VanillaIceCream"
    string? Arch);             // "arm64-v8a"

public sealed record class RunMachineInfo(
    string Os,                 // "macOS 15.6" (RuntimeInformation)
    string Arch,               // "arm64"
    string Runtime,            // ".NET 10.0.x"
    string Hostname);          // Environment.MachineName
```

### 7.2 采集与注入

| 字段 | 来源 | 注入方式 |
|------|------|----------|
| Purpose | 运行意图 | Host 运行命令 CLI 选项 `--purpose`（RunScenarioAsync 选项层）/ env `UNICLAW_RUN_PURPOSE` |
| TaskId | CI 任务 | Host 运行命令 CLI 选项 `--task-id` / env `UNICLAW_TASK_ID`（CI 里自动注入） |
| SystemInfo | ADB `getprop`（模拟器模式）；local 模式为 null | Host 在 `RunScenarioAsync` 里采集，进 `RunManifestInput` |
| MachineInfo | `RuntimeInformation` + `Environment.MachineName` | Host 采集，零外部依赖 |

关键决策：

- `RunSystemInfo` 采集逻辑放 **Host 生产代码**（`src/UniClaw.Host/`），不放测试项目——真实运行也带系统版本，不只是集成测试
- 标准化计划中的 `EmulatorInfoDetector` 概念迁移到 Host，测试项目复用
- **不再单独写 emulator-info.json**——从 manifest 读，避免双源

### 7.3 TraceTool 消费

- `TraceRun` 聚合读取 manifest 的这些字段
- `diagnose --json` 输出带 run 上下文（见 §4.1 的 `run` 字段）
- `list` 支持 `--task-id` / `--status` 过滤——agent 可按任务找 run
- 诊断结论可带系统上下文：如 "该 failure 仅出现在 API 35"（diff 时成为对比维度）

### 7.4 兼容性

- 新字段全部 optional：旧 run 目录（无这些字段）TraceRun 显示 `"unknown"`，不破坏
- manifest schemaVersion 不动（1），字段可选即兼容；录制/回读路径不变

## 8. 测试策略

- **TraceRunLoader / RunDiffer**：纯单元测试，用 `tests/UniClaw.TraceTool.Tests/` 内嵌的最小 JSONL fixture（从现有 snapshot 提取）
- **CLI 行为**：System.CommandLine 的测试钩子（invoke + capture output），覆盖 6 个子命令 happy path + 错误路径
- **TUI**：不自动化，手动验证（TUI 层薄，逻辑都在 TraceRun 聚合里）
- **规则引擎**：用现有 snapshot 的 trace（`tests/UniClaw.Core.Tests/Fixtures/Traces/` 已有真实失败 trace）做输入，断言 diagnose 输出分类

## 9. 范围边界

- ✅ 本设计内：TraceTool 新项目、manifest 元数据增强（Host 侧）
- ⏸️ 不含（后续）：集成测试标准化（`glittery-launching-clover.md`，独立计划）、端到端链路验证、资产收集用于训练
- TraceTool 与集成测试标准化**互补但独立**——它读的是产物目录，不依赖基线；标准化产出的系统版本上下文会作为 diagnose 的可选增强
