---
name: trace-analysis
description: Trace 故障排查 playbook —— 用 TraceTool CLI 对失败 run 做根因诊断、性能时间线与跨 run 回归对比。先按分层掌握状态机 + trace 设计，再执行排查；支持裸 trace 路径、trace 完整性自评、运行日志补证与自我反思。与 trace-collection（采集）/ trace-visualization（可视化）互补，本 skill 是消费侧。
metadata:
  author: uni-claw-ai-team
  version: "1.1"
  tags: [trace, diagnose, state-machine, offline-analysis]
---

# Trace Analysis Skill

对 UniClaw run 产物（`{outputRoot}/{scenarioId}/{runId}/`）做离线故障排查。**先分层掌握，再下结论**——不理解 span 语义与状态机机制，就无法解释 diagnose 的 verdict。输入可以是 run 目录、run 目录内的 trace.jsonl 路径、或**裸 trace 文件**（三级解析，见下）。

## When to Use

- 排查失败 run（cause / failingStep / evidence）
- 性能分析（engine.step 耗时、AI 延迟分布、时间线空洞）
- 跨 run 回归对比（diff，退出码 1 = 行为差异）
- Agent 消费 trace 的结构化输出（`--format json`）
- 拿到外部/裸 trace 文件需要分析（无 run 目录上下文）

## 分层掌握（固定顺序 L1 → L2 → L3 → L4）

任务开始时先做**记忆读取 + 刷新检查**（见记忆系统），由检查结论决定读什么：

- 来源文档**未更新**的层 → 记忆为准，**可跳过整层重读**（深度需要时按需精读对应节）
- 来源文档**已更新**的层 → 必须重读该层 + 重精简记忆
- 记忆缺失/损坏 → 按 L1–L4 全量加载
- 机制结论只引 L2 语义（记忆条目标注了来源层），不臆测

| 层 | 内容 | 文档 |
|----|------|------|
| **L1 观测层** | span 语义：SpanType 11 值、span 树（engine.run → engine.step → ai.call → ai.analyze）、TraceContext 信封、三层 CQRS | `docs/system/layers/observability.md` + `openspec/specs/trace-foundation`、`trace-span`、`span-type`、`trace-service`、`trace-storage`、`file-trace-storage` |
| **L2 运行层** | 状态机机制：TraversalState/GlobalState、entry.visited/entry.skipped 产生时机、CompletionReason 来源、error loop 根源（状态不前进） | `docs/system/layers/state-machine.md` + `docs/system/layers/traversal.md` + `openspec/specs/traversal-fsm`、`traversal-engine`、`step-orchestrator`、`completion-monitor`、`error-handler` |
| **L3 产物层** | run 目录布局与字段：manifest.json / result.json / trace/{runId}/trace.jsonl / steps/D4/、TracePath 双格式 | `src/UniClaw.Host/Artifacts/RunAssets.cs` + `openspec/specs/run-metadata-enrichment` + 真实产物样例 |
| **L4 分析层** | TraceTool 契约：CLI 6 子命令、--format json、退出码、DiagnoseEngine 规则来源 | `openspec/changes/trace-analyzer/design.md` + `openspec/specs/trace-analyzer-cli`、`trace-run-aggregate` |

## 排查工作流（五步：定位 → 诊断 → 取证 → 自评 → 反思）

```bash
BIN=src/UniClaw.TraceTool/bin/Debug/net10.0/UniClaw.TraceTool
# 若 bin 未构建：dotnet run --project src/UniClaw.TraceTool -- trace <subcommand> ...
```

### 1. 定位 run（L3）——三级解析

输入形态任意，统一解析为 run 目录：

| 输入 | 处理 |
|------|------|
| run 目录（含 manifest.json 那层） | 直接用 |
| trace.jsonl 路径（在真实 run 目录内） | 向上定位含 manifest.json 的目录 |
| 裸 trace 文件（无 run 目录） | **临时 run 目录构造**（实测可行）：复制 trace 到 `/tmp/trace-analysis-$$/trace/{id}/trace.jsonl`，按下方模板写最小 result.json（RunResult 字段全必填，`tracePath` 指向复制后的位置），分析完清理 `/tmp/trace-analysis-$$` |

```bash
mkdir -p /tmp/trace-analysis-$$/trace/{id}
cp <trace.jsonl> /tmp/trace-analysis-$$/trace/{id}/trace.jsonl
echo '{"schemaVersion":"1","runId":"{id}","status":"failure","completionReason":"external_trace","discoveredEntries":0,"visitedEntries":0,"skippedEntries":0,"failedEntries":0,"actionsAttempted":0,"actionsSucceeded":0,"safetyAllowed":0,"safetyDenied":0,"stepsConsumed":0,"scrollsConsumed":0,"durationMs":0,"tracePath":"trace/{id}/trace.jsonl","issueFingerprints":[],"successCriteriaSatisfied":false,"successEvidence":[],"updatedAt":"2026-08-04T00:00:00+00:00"}' > /tmp/trace-analysis-$$/result.json
```

发现 run 用 `$BIN trace list --dir artifacts/runs [--status failure] [--task-id <id>] [--limit N] --format json`（递归扫描任意深度）。

### 2. 根因诊断（L4 + L1/L2 解读）

`$BIN trace diagnose --run <runDir> --format json`
- 提取 verdict（cause/failingStep/summary/confidence）、evidence、suggestions、artifactPaths
- 解读：cause 透传 result.json completionReason（L3）；`error_loop_stuck` = ErrorLoopAnalyzer 命中 stuck_in_error_loop（≥5 连续全跳过）/ skip_rate_too_high（skipped>visited×4）——对照 L2 状态不前进机制

### 3. 深入取证（含日志补证）

- 性能：`timeline --run <dir> --threshold <ms>`（L1 ai.call 延迟语义）
- 回归：`diff --run-a <a> --run-b <b>`（有基线时；退出码 1 = 行为差异）
- 产物：按 artifactPaths 用 Read 打开 manifest/result/screenshot（L3）
- **运行日志（evidence 不足或需验证机制时）**：
  - `{runDir}/analysis.jsonl`（D-197：每次页面分析快照——matcher/OCR 排查关键证据）
  - Host 运行日志（位置按 `docs/system/layers/host.md` 约定；未知时用 `find artifacts -name "*.log"` 定位）
  - ADB 只读日志：`adb shell logcat -d` / `dumpsys dropbox --print`（**禁止 `-c` 清日志、禁止 kill/重启设备**）
  - 日志证据在结论中单独标注来源；只读命令

### 4. trace 完整性自评（每个诊断必做）

| 检查项 | 完整 | 部分（降级声明） | 不完整（低置信） |
|--------|------|------------------|------------------|
| 有 span（退出码 3 = 无 span） | 有 span | — | 无 span：早期 run 或埋点缺失 |
| manifest / result | 都在 | 缺一（字段显示 "unknown"） | 都缺（外部 trace） |
| result vs trace 覆盖 | 一致 | 有 result 无 span（埋点缺失）/ 有 span 无 result（中断 run） | — |
| 时间线空洞 | 无 >30s gap | 有 timeline_gap evidence | — |
| steps/D4 截图产物 | 有 | screenshotPaths 空（无法截图取证） | — |

- 部分/不完整时结论必须声明"证据不足，置信度受限"，并列出可补充来源（运行日志、重跑）
- CLI 输出与自评矛盾（如退出码 3 但 trace 非空）→ 按 L1 语义排查（execution 记录不是 span），不臆测

### 5. 反思自改进（触发时必做）

触发条件（任一）：confidence=low 且 evidence 空；完整性不完整；结论被用户纠正或后续证据证伪。

回顾（加载了哪些层/用了哪些命令/依据）→ 识别缺口（知识/数据/方法）→ 改进（补加载层文档 / 补读日志证据）→ **重跑诊断** → 输出反思摘要（学到什么、下次改进点，如"下次 visited=0 的 target 失败先查 analysis.jsonl 的 matcher 结果"）。

边界：CLI 能力不足 → 回报顶层统筹，不自行修改产品代码；改进只体现在流程与知识加载。

## 记忆系统（自建 · 精简 · 定时刷新）

执行 trace 分析时同步维护自建记忆 `.claude/agents/trace-analyzer-memory/`（git 跟踪）：

- **任务开始**：读 `INDEX.md` → `knowledge.md`（分层知识蒸馏）→ `lessons.md`（案例经验）；做**刷新检查**并**自行判断读不读**——文档更新时间取 `git log -1 --format=%ci` 与 fs mtime 中更新者；文档比记忆新 → 重读该层 + 重精简；文档未更新 → 跳过整层重读（深度需要时按需精读），记忆为准
- **任务结束**：新验证的事实/方法/局限追加 lessons.md（日期 + 来源 + ≤3 句；同主题合并、重复不追加、错误纠正删除）
- 记忆是层文档的精简蒸馏，**不替代**层文档；与文档冲突以文档为准
- 唯一可写的持久位置是记忆目录；不写源码/层文档/run 目录

## CLI 契约（关键）

- **退出码**：0 成功 / 1 diff 差异 / 2 用法错误或 run 不存在 / 3 空 trace（无 span；早期 run 只有 execution 记录时也报 3，L1 语义下 execution 不是 span）
- `--format json`：stdout 纯 JSON（schemaVersion "1"），日志在 stderr；非 TTY 自动转 JSON
- `diagnose` 的 run 上下文带 taskId/purpose/system（ADB getprop）/machine——用于关联 CI/设备
- `--run` 只接受 run 目录——**三级解析负责把任何输入形态归一成 run 目录**，绝不把 trace.jsonl 直接当 --run 传

## 禁止事项

- 不修改真实 run 目录（只读消费者；临时 run 目录仅限 /tmp，用后清理）
- 不手写 JSONL 解析——trace 读取一律走 TraceTool CLI
- 不臆测状态机行为——机制解释必须可溯源到 L2 文档
- 日志命令只读——不清理、不修改、不 kill 运行中进程/设备
