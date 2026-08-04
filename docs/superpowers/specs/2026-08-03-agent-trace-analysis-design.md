# Agent × Trace Analyzer 集成设计

> 状态: draft | 日期: 2026-08-03 | 零代码改动 — 纯文档规范

## 1. 动机

TraceTool CLI (`src/UniClaw.TraceTool/`) 已实现 6 个子命令（list / timeline / diagnose / diff / report / interactive），全部支持 `--format json` 稳定契约（`schemaVersion: "1"`）+ 退出码语义（0/1/2/3）。但**Agent 如何使用这些能力进行自我分析**尚缺规范。

本文档定义 Agent 如何使用 TraceTool CLI 实现以下自我分析能力：

- **错误根因诊断**：Agent 运行场景后，自动分析为什么失败
- **性能剖析**：识别 AI 延迟瓶颈、慢 step、provider 差异
- **Trace 完善度评估**：判断 trace 是否完整、可信
- **可重放执行计划**：从成功 trace 提取确定性的 step 序列
- **跨运行回归检测**：对比两次运行，检测行为差异
- **自我迭代**：基于历史基线，自动发现异常并提出优化

## 2. 核心原则

### 2.1 Agent 通过子进程调用 TraceTool

```
Agent → subprocess: uni-claw trace <command> --format json <args>
Agent ← stdout: JSON (schemaVersion: "1")
Agent ← stderr: 日志/警告 (非 JSON)
Agent ← exit code: 0=成功, 1=diff检测到差异, 2=用法错误, 3=空trace
```

关键点：

- **stdout 只输出 JSON**：Agent 用 `2>/dev/null` 拿干净结果
- **stderr 走日志/警告**：Agent 选择性读取诊断信息
- **零代码改动**：TraceTool 已实现全部 6 个命令，Agent 只管调用
- **30 秒默认超时**：trace 加载是 I/O 密集型（JSONL 回放），非计算密集型

### 2.2 Agent 决策树

```
Run 完成
  │
  ├─ exit code = 0 (success)
  │   ├─ trace timeline --format json → 提取 replay plan → 存储供 CI 回归
  │   └─ trace diff vs 上一次成功运行 → 检测改善程度
  │
  ├─ exit code ≠ 0 (failure)
  │   ├─ trace diagnose --format json → 读取 verdict.cause
  │   │   ├─ error_loop         → 调整反循环阈值 / 检查滚动策略
  │   │   ├─ ai_call_failure    → 检查 provider 凭证 / 速率限制
  │   │   ├─ verification_mismatch → 检查预期页面 identity 是否正确
  │   │   ├─ timeline_gap > 30s → 检查 ADB 连接稳定性
  │   │   ├─ safety_denial      → 检查安全策略 / label-mapping
  │   │   └─ execution_failure  → 检查 ADB action 日志
  │   ├─ trace timeline --format json → 找到最慢 step → 深入调查
  │   └─ 同一 fingerprint 出现 3+ 次 → 自动提 issue
  │
  └─ exit code = 130 (cancelled)
      └─ trace diagnose --format json → 检查部分 trace 是否有用
```

## 3. Agent 工作流

### Workflow 1: 错误根因诊断

**场景**：Agent 运行场景后得到 failure/incomplete 状态。

**调用**：
```bash
uni-claw trace diagnose --format json {runDir}
```

**输出契约**（`DiagnoseResult`，定义于 `DiagnoseEngine.cs:17`）：

```json
{
  "schemaVersion": "1",
  "data": {
    "runId": "20260803T120000Z-abc12345",
    "status": "failure",
    "run": {
      "runId": "20260803T120000Z-abc12345",
      "taskId": "ci-run-1234",
      "purpose": "PR #42 验收",
      "system": { "sdkLevel": "35", "releaseVersion": "15", "buildFingerprint": "...", "codename": "VanillaIceCream", "arch": "arm64-v8a" },
      "machine": { "os": "macOS 15.6", "arch": "arm64", "runtime": ".NET 10.0.1", "hostname": "build-01" }
    },
    "verdict": {
      "cause": "error_loop",
      "failingStep": "Step at index 7",
      "summary": "Run failed: max_steps",
      "confidence": "medium"
    },
    "evidence": [
      { "type": "error", "description": "AdbCommandException: device offline", "stepNumber": "4" },
      { "type": "ai_call_failures", "description": "analyze_visual: 3 failures", "stepNumber": null },
      { "type": "timeline_gap", "description": "Large gap between step 4 and step 5", "stepNumber": "5" }
    ],
    "suggestions": [
      "Check AI provider credentials and rate limits."
    ],
    "artifactPaths": {
      "manifestPath": "/runs/.../manifest.json",
      "resultPath": "/runs/.../result.json",
      "tracePath": "/runs/.../trace/.../trace.jsonl",
      "screenshotPaths": ["/runs/.../steps/0004"]
    }
  }
}
```

**Agent 决策逻辑**：

| `verdict.cause` | Agent 处理 |
|---|---|
| `error_loop` | 检查 `evidence` 中连续 skip 次数 → 调整 `MaxScrolls` / `MaxSteps` 预算 |
| `ai_call_failure` | 统计 `evidence` 中 AI 失败数 → 检查 provider 凭证 / 切换模型 |
| `verification_mismatch` | 读 `verdict.failingStep` → 检查该 step 的预期页面 identity → 更新 scenario 定义 |
| `timeline_gap` | 检查 `evidence` 中时间线空洞 → 检查 ADB 连接 / 设备是否息屏 |
| `safety_denial` | 读 `safety-decisions.jsonl` → 检查安全策略规则 → 更新 label-mapping |
| `execution_failure` | 读 `verdict.failingStep` 对应 `steps/D4/after.png` → 视觉验证 |

### Workflow 2: 性能剖析

**场景**：Agent 需要了解运行效率 —— 哪些 step 最慢？AI 延迟分布如何？

**调用**：
```bash
uni-claw trace timeline --format json {runDir}
```

**输出契约**（`TraceCommands.cs:292`）：

```json
{
  "schemaVersion": "1",
  "data": {
    "runId": "20260803T120000Z-abc12345",
    "status": "success",
    "steps": [
      { "stepNumber": 1, "name": "step 1: scroll", "durationMs": 2140.5, "status": "ok", "aiCallCount": 1 },
      { "stepNumber": 2, "name": "step 2: click Wi-Fi", "durationMs": 3310.2, "status": "ok", "aiCallCount": 2 },
      { "stepNumber": 3, "name": "step 3: verify", "durationMs": 1200.0, "status": "ok", "aiCallCount": 0 }
    ],
    "aiLatency": [
      {
        "capability": "analyze_visual",
        "count": 18,
        "min": 850.0,
        "avg": 2140.5,
        "p50": 2100.0,
        "p95": 3310.2,
        "max": 4120.1
      }
    ]
  }
}
```

**Agent 决策逻辑**：

- `steps[].durationMs` 排序 → top-5 最慢 step → 读取对应 step 的 `analysis.json` + `before.png` 判断是否页面复杂度高
- `aiLatency[].p95` 与历史基线（`BaselineProfile.AiLatencyP95`）对比 → 超过 1.3× → provider 降级告警
- `steps[].aiCallCount` 异常高（> 平均 × 3）→ 该 step 可能反复重试 → 检查 `DecideNextAction` 是否反复返回相同 action
- 跨 provider 对比：`trace diff` 两次使用不同 model 的 run → `diff.aiComparisons[]` 直接给出延迟差异

### Workflow 3: Trace 完善度评估

**场景**：Agent 需要判断 trace 是否完整、诊断结论是否可信。

**方法**：此能力当前通过组合现有命令实现，无需新命令。

```
1. trace diagnose --format json {runDir}
   → 检查 evidence[] 是否有 "timeline_gap" / "ai_call_failures"
   → 检查 verdict.confidence 是否为 "low"（证据不足）

2. trace timeline --format json {runDir}
   → 检查 steps[] 是否为空（空 trace → 退出码 3）
   → 检查 steps[last].status 是否为 "error"（引擎中途崩溃）

3. 直接读取 status 字段（manifest.json / result.json）
   → cancelled → trace 可能不完整
   → running → trace 未最终化
```

**Agent 结论分级**：

| Trace 状态 | 条件 | 可信度 |
|---|---|---|
| 完整 | `steps[].length > 0`, 根 span `engine.run` 已关闭, 无 `timeline_gap`, `verdict.confidence != "low"` | 高 |
| 部分 | `steps[].length > 0` 但根 span 未关闭或存在 gap | 中 |
| 不完整 | `status == "cancelled"` 或 trace 为空 | 低 |
| 空 | 退出码 3, steps 为空 | 无法诊断 |

**Agent 行为**：

- 可信度 "高" → 直接按 verdict 采取修复动作
- 可信度 "中" → 采纳 verdict 但降低自动修复的激进程度（如只调整参数，不切换 provider）
- 可信度 "低/空" → 不自动修复，提示"trace 不完整，需手动排查"

### Workflow 4: 可重放执行计划

**场景**：Agent 从成功的 trace 中提取确定性的 action 序列，生成可重放计划。

**方法**：当前通过 `trace timeline --format json` 提取 step 序列。未来可新增专用 `trace replay-plan` 命令（见 §6）。

**提取算法**（Agent 端）：

```
1. trace timeline --format json {runDir}
2. 过滤 status == "success" 的 run
3. 读 steps[] → 每步一个 ReplayStep:
   - action: 从 span name 解析（"click Wi-Fi" → action=click, target=Wi-Fi）
   - 更深层的 action 信息需读 trace.jsonl 中 action.click / action.scroll span
4. 输出 ReplayPlan:
   - steps: 所有 step 的 action 序列
   - optimalStepCount: locate 模式下到 target_found 的 step 数
```

**用途**：

- CI 回归：每次 PR 重放已知成功的 action 序列，验证页面可达性
- Bug 复现：从 failure trace 提取到失败 step 的前缀路径，精确复现
- 性能基准：同一序列在不同 provider/model 下重放，对比延迟

### Workflow 5: 跨运行回归检测

**场景**：Agent 对比两次运行（如切换 model 前后、不同版本间）。

**调用**：
```bash
uni-claw trace diff --format json --run-a {runA} --run-b {runB}
```

**输出契约**（`RunDiffer.cs:25` + `TraceCommands.cs:476`）：

```json
{
  "schemaVersion": "1",
  "data": {
    "stepDiffs": [
      { "stepLabel": "Step 3", "presentInA": true, "presentInB": true, "difference": "Status: ok → error" },
      { "stepLabel": "Step 8", "presentInA": false, "presentInB": true, "difference": "Added in B" }
    ],
    "metricDiffs": [
      { "metric": "Steps Consumed", "valueA": 8, "valueB": 14, "delta": 6 },
      { "metric": "Duration (ms)", "valueA": 45000, "valueB": 63744, "delta": 18744 }
    ],
    "aiComparisons": [
      { "capability": "analyze_visual", "avgLatencyA": 2140.5, "avgLatencyB": 3120.3, "deltaMs": 979.8, "countA": 18, "countB": 22 }
    ],
    "conclusion": "Regression: run A was success, run B is failure.",
    "hasDifferences": true
  }
}
```

**Agent 决策逻辑**：

- `hasDifferences == true` → 退出码 1 → CI 可据此判断回归
- `metricDiffs[metric="Steps Consumed"].delta > 0` → 步骤数增加，检查是否引入了新页面
- `aiComparisons[].deltaMs > 500` → AI 延迟增加 > 500ms，检查 provider 负载
- `conclusion` 包含 "Regression" → 自动标记当前 commit 为可疑
- `stepDiffs[].difference` 包含 "status: ok → error" → 精确指出哪个 step 退化为错误

### Workflow 6: 自我迭代（基线对比）

**场景**：Agent 积累了足够的历史运行后（≥ 10 条 baseline 记录），基于历史基线评估当前运行是否正常。

**数据来源**：

```bash
# 历史基线文件（BaselineBuilder 自动追加）
artifacts/baselines/{scenarioId}.jsonl
# 每行一个 JSON: { itemsObserved, itemsVisited, itemsSkipped, stepsUsed, scrollCount, endOfListDetected, success, aiLatencyP50, aiLatencyP95 }
```

**Agent 比较逻辑**：

```
1. 读 BaselineProfile（已有 C# API: BaselineProfile.Load(scenarioId)）
   → ItemsVisitedP50 / P95, StepsUsedP50 / P95, AiLatencyP50 / P95

2. 读当前 run 的 result.json + trace timeline --format json
   → stepsUsed, aiLatencyP50

3. 对比:
   stepsUsed > p95 × 1.2        → "步骤数异常偏高（p95={p95}, 当前={current}），检查 step {p95+1} 至 {current}"
   aiLatencyP50 > p95 × 1.3     → "AI 延迟显著增加（p95={p95}, 当前={current}），provider 可能降级"
   success == false, 连续 3 次   → "连续 3 次失败，自动提 issue + 附 replay plan"
   visited / observed < p50 × 0.5 → "覆盖率异常偏低，scroll 策略可能未生效"
```

**Agent 行为**：

- 正常范围（p50 < current < p95）→ 不做特殊处理
- 警告范围（p95 < current < p95 × 1.5）→ 记录 observation，不做自动修复
- 异常范围（current > p95 × 1.5）→ 自动分析 + 提 issue

**当前限制**：
- BaselineProfile 仅在 ≥ 10 条记录后对 `EnumerateCompletionAnalyzer` 生效
- Agent 层面的基线对比逻辑需在 Agent 框架中实现，TraceTool 不内置（可后续加 `trace improve` 命令）

## 4. JSON 契约总览

| 命令 | 顶层 schema | `data` 类型 | 定义位置 |
|---|---|---|---|
| `trace list --format json` | `{ schemaVersion, data: { runs[] } }` | `RunEntry[]` | `TraceCommands.cs:156` |
| `trace timeline --format json` | `{ schemaVersion, data: { runId, status, steps[], aiLatency[] } }` | 匿名对象 | `TraceCommands.cs:292` |
| `trace diagnose --format json` | `{ schemaVersion, data: DiagnoseResult }` | `DiagnoseResult` | `DiagnoseEngine.cs:17` |
| `trace diff --format json` | `{ schemaVersion, data: RunDiff }` | `RunDiff` | `RunDiffer.cs:25` |
| `trace report --format json` | `{ schemaVersion, data: { runId, status, scenarioId, taskId, purpose, durationMs, steps[], diagnosis } }` | 匿名对象 | `TraceCommands.cs:560` |

退出码统一语义：

| 退出码 | 含义 | Agent 行为 |
|---|---|---|
| 0 | 成功 | 正常处理 JSON 输出 |
| 1 | diff 检测到行为差异 | 触发回归告警 |
| 2 | 用法错误 / run 目录不存在 | 检查路径是否正确 |
| 3 | 空 trace（0 span） | 标记"诊断不可用" |

## 5. 集成模式

### 5.1 Host 侧自动化（未来可选）

当前不实现，但预留钩子位置：`HostCommands.cs` 的 `RunScenarioAsync` 中 `FinalizeRunAssetsAsync` 之后，可加入：

```csharp
// 可选：自动调 TraceTool 分析（env gate: UNICLAW_SELF_ANALYSIS=1）
if (Environment.GetEnvironmentVariable("UNICLAW_SELF_ANALYSIS") == "1")
{
    var traceTool = ResolveTraceToolPath();  // UNICLAW_TRACE_TOOL env / dotnet run / PATH
    if (traceTool != null)
    {
        var result = await ProcessRunner.RunAsync(
            traceTool, ["diagnose", runDir, "--format", "json"],
            timeoutMs: 30_000);
        if (result.ExitCode == 0)
            File.WriteAllText(Path.Combine(runDir, "diagnosis.json"), result.Stdout);
    }
}
```

### 5.2 Agent 框架侧

Agent 框架（Claude Code / Codex / 自定义 orchestration）直接调 CLI：

```bash
# 错误诊断
DIAGNOSIS=$(uni-claw trace diagnose --format json /path/to/run 2>/dev/null)
CAUSE=$(echo "$DIAGNOSIS" | jq -r '.data.verdict.cause')

# 性能时间线
TIMELINE=$(uni-claw trace timeline --format json /path/to/run 2>/dev/null)
SLOWEST=$(echo "$TIMELINE" | jq '.data.steps | sort_by(.durationMs) | reverse | .[0]')

# 跨运行对比
uni-claw trace diff --format json --run-a /run/A --run-b /run/B
if [ $? -eq 1 ]; then
    echo "Regression detected!"
fi
```

### 5.3 Agent 完整诊断流程

```
1. trace list --format json --status failure --limit 5
   → 获取最近的 5 个失败 run

2. 对每个失败 run:
   a. trace diagnose --format json {runDir}
      → 分类失败原因
   b. trace timeline --format json {runDir}
      → 定位时间线异常
   c. 读 result.json → 检查 CompletionReason + FailureCause
   d. 读 issues.jsonl → 检查是否有已知 issue fingerprint

3. 聚合分析:
   - 同一 FailureCause 出现 3+ 次 → 模式识别 → 提 issue
   - 同一 StepNumber 反复失败 → 页面特定问题 → 附 before.png
   - AI call failure 集中出现 → provider 问题 → 建议切换

4. 自我迭代:
   - trace diff 当前 vs 上一次成功 run → 检测回归
   - 与 BaselineProfile 对比 → 检测指标异常
   - 生成优化建议 → Agent 决策是否自动应用
```

## 6. 未来增强（不在当前范围）

| 增强项 | 描述 | 优先级 |
|---|---|---|
| `trace replay-plan --format json {runDir}` | 专用命令：从 trace 提取确定性的 action 序列 | P1 |
| `trace improve --format json {runDir}` | 专用命令：基线对比 + 优化建议 | P1 |
| `trace completeness --format json {runDir}` | 专用命令：span type 覆盖度检查 | P2 |
| Replay plan → engine 反馈 | `uniclaw run --replay-plan {file}` 模式 | P2 |
| 自动 GitHub issue | `gh issue create --title ... --body-file ...` 集成 | P2 |
| 跨 scenario 关联分析 | 同一 device 上多个 scenario 的关联失败模式 | P3 |
| 性能回归自动告警 | p95 超过阈值时自动通知 | P3 |

## 7. 测试策略

- **Agent 决策逻辑**：用现有的 trace fixture（`tests/UniClaw.TraceTool.Tests/` + `tests/UniClaw.Core.Tests/Fixtures/Traces/`）模拟各种失败场景，验证 Agent 的 cause → action 映射正确
- **JSON 契约**：`JsonContractTests.cs` 已验证 schema 稳定性
- **退出码**：`CliTests.cs` 已验证所有 6 个子命令的退出码语义
- **端到端**：实际运行场景 → trace diagnose → 手动验证 verdict 与实际原因一致

## 8. 现有资产引用

| 资产 | 路径 | 用途 |
|---|---|---|
| DiagnoseEngine | `src/UniClaw.TraceTool/DiagnoseEngine.cs` | 诊断规则引擎，产出 DiagnoseResult |
| RunDiffer | `src/UniClaw.TraceTool/RunDiffer.cs` | 跨运行结构化 diff |
| TraceRunLoader | `src/UniClaw.TraceTool/TraceRunLoader.cs` | JSONL → ITraceQuery 回放 |
| TraceRun | `src/UniClaw.TraceTool/TraceRun.cs` | Manifest + Result + Trace + Steps 聚合 |
| TraceCommands | `src/UniClaw.TraceTool/Commands/TraceCommands.cs` | CLI handler + JSON 输出 |
| BaselineBuilder | `src/UniClaw.Host/Analysis/BaselineBuilder.cs` | 运行时聚合 → baselines/*.jsonl |
| BaselineProfile | `src/UniClaw.Host/Analysis/BaselineProfile.cs` | 基线 p50/p95 百分位 |
| EnumerateCompletionAnalyzer | `src/UniClaw.Host/Analysis/EnumerateCompletionAnalyzer.cs` | 实时完成度分析 |
| ErrorLoopAnalyzer | `src/UniClaw.Host/Analysis/ErrorLoopAnalyzer.cs` | 错误循环检测 |
| SpanTypes | `src/UniClaw.Core/Observability/SpanTypes.cs` | span type 目录 (22 种) |
| ITraceQuery | `src/UniClaw.Core/Observability/ITraceQuery.cs` | span 树查询接口 |
| RunManifest | `src/UniClaw.Host/Artifacts/RunAssets.cs:40` | 运行清单 (含 Purpose/TaskId/SystemInfo/MachineInfo) |
| RunResult | `src/UniClaw.Host/Artifacts/RunAssets.cs:65` | 运行结果 |
| RunSystemInfo / RunMachineInfo | `src/UniClaw.Host/Artifacts/RunAssets.cs:87,94` | 系统/机器信息 |
| trace-analyzer design | `docs/superpowers/specs/2026-08-03-trace-analyzer-design.md` | TraceTool 设计文档 |
| trace-analyzer-cli spec | `openspec/specs/trace-analyzer-cli/spec.md` | CLI 行为 spec |
| trace-run-aggregate spec | `openspec/specs/trace-run-aggregate/spec.md` | TraceRun 聚合 spec |
| run-metadata-enrichment spec | `openspec/specs/run-metadata-enrichment/spec.md` | 元数据增强 spec |
