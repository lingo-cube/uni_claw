---
name: e2e-diagnose
description: E2E 测试全自动诊断 —— Haiku 模型编排 host-test-runner 执行 → trace-analyzer 深度诊断 → fsm-analyzer 状态机归因 → local-vision-analyzer 视觉问题分析 → 汇总运行报告（含耗时、问题、修复建议、职责边界）
model: haiku
metadata:
  author: uni-claw-ai-team
  version: "1.0"
  tags: [e2e, diagnosis, orchestration, trace, fsm, vision, report]
---

# E2E Diagnose Skill

编排完整的 E2E 测试 → 诊断 → 报告流程。使用 Haiku 模型做轻量编排，逐层委派专项 agent。

## When to Use

- 执行 E2E 集成测试并自动诊断
- 排查 enumerate / locate 场景失败根因
- 生成结构化的 E2E 运行报告（含 FSM 行为 + Vision 质量 + 性能指标）
- 定期 E2E 回归对比（跨 run diff）

## 架构概览

```
┌─────────────────────────────────────────────────────┐
│  e2e-diagnose SKILL (Haiku 编排)                     │
│                                                      │
│  Phase 1 ─→ host-test-runner skill (测试执行)        │
│                │                                     │
│  Phase 2 ─→ TraceTool CLI (指标提取, MCP 优先)       │
│                │                                     │
│  Phase 3a ─→ trace-analyzer (trace 诊断 + 问题分类)  │
│                │                                     │
│          ┌─────┴─────┐                               │
│          │ 按需派发    │                               │
│          └─────┬─────┘                               │
│     ┌──────────┼──────────┐                          │
│     ▼          │          ▼                          │
│  fsm-analyzer  │     local-vision                    │
│  (FSM 相关)    │     -analyzer                       │
│                │     (Vision 相关)                    │
│                ▼                                     │
│  Phase 4 ─→ 汇总运行报告 + 修复建议文档               │
└─────────────────────────────────────────────────────┘
```

## 分层工具优先级（强制执行）

| 数据类型 | 工具 | 禁用 |
|---------|------|------|
| Run 指标 / 产物 | **TraceTool CLI** (`trace diagnose`, `trace timeline`, `trace list`, `trace diff`) | 手写 Python 解析 trace.jsonl |
| C# 代码查询 | **MCP** (`csharper-mcp`, `cwm-roslyn-navigator`) | grep/find |
| Log 文本 / 简单 grep | Bash (只读) | — |
| E2E 执行 | **host-test-runner skill** 或 dotnet test | — |

---

## Phase 1 — 执行

使用 `host-test-runner` skill 启动模拟器 + 视觉服务 + 执行测试。

```
Skill(host-test-runner, "跑 <scenario> 单场景集成测试, provider=local")
```

可选：直接用 dotnet test（已有环境）

```bash
UNICLAW_INTEGRATION_SCOPES=<scope> UNICLAW_INTEGRATION_PROVIDER=local \
  dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=<scope>" -v:minimal
```

---

## Phase 2 — 指标提取（TraceTool CLI）

**不手写 Python 解析 trace.jsonl**。所有 run 数据提取走 TraceTool CLI：

```bash
BIN="src/UniClaw.TraceTool/bin/Debug/net10.0/UniClaw.TraceTool"

# 找到最新 run
$BIN trace list --dir artifacts/runs/integration/<scope> --format json

# 诊断
$BIN trace diagnose --run <runDir> --format json

# 性能
$BIN trace timeline --run <runDir> --threshold 3000 --format json

# 跨 run 对比（可选）
$BIN trace diff --run-a <runA> --run-b <runB> --format json
```

从 `result.json` 补充指标（status, completionReason, stepsConsumed, actionsAttempted, scrollsConsumed, discoveredEntries, visitedEntries, successCriteriaSatisfied）。

---

## Phase 3 — 分层诊断

**串行执行，逐层深入：**

```
Phase 3a: trace-analyzer (先跑，产出运行行为报告 + 问题分类)
                │
          ┌─────┴─────┐
          │ 问题分类    │
          └─────┬─────┘
                │
     ┌──────────┼──────────┐
     │          │          │
  FSM 相关   Vision 相关  仅 trace 可解释
     │          │          │
     ▼          ▼          ▼
fsm-analyzer  local-vision  直接汇总
 (按需派发)   -analyzer
              (按需派发)
                │
          ┌─────┴─────┐
          │ 汇总报告    │
          └───────────┘
```

### Phase 3a — trace-analyzer（必定执行）

```
Agent(subagent_type="trace-analyzer",
  prompt="诊断 run: <runDir>。
  使用 TraceTool CLI (diagnose + timeline + list)。
  输出：
  1. 运行行为报告（verification 决策 / 重复点击 / 错误事件 / 慢步）
  2. 问题分类标签（FSM / Vision / Host 验证 / 参数 / 仅 trace 可解释）
  3. 关键证据（analysis.jsonl 指纹序列 / trace 决策链 / run.log 异常）")
```

### Phase 3b — fsm-analyzer（按需派发）

**触发条件**（trace-analyzer 结论含以下任一）：
- completionReason 含 stuck / error_loop / FrameComplete=0 / max_steps
- 异常路由（ErrorHandling / PopupHandling 频繁触发）
- StateDecision 决策异常（verification 全部 unchanged / Branch 不推子节点）

**不触发**：纯 vision 问题 / 参数超限 / Host 验证层拒绝

```
Agent(subagent_type="fsm-analyzer",
  prompt="基于 trace-analyzer 的发现: <问题摘要>。
  分析 run <runDir> 的 FSM 行为。
  使用 MCP 查源码, TraceTool CLI 查 trace。")
```

### Phase 3c — local-vision-analyzer（按需派发）

**触发条件**（trace-analyzer 结论含以下任一）：
- 重复点击 / 副标题幻影项
- OCR 文本变体（空格/逗号/空文本）
- 坐标错位（点击落到错误 UI 行）
- 搜索框 / 系统 UI 误点击
- 同一 item 被多次识别（同排重复）
- isEndOfList 检测异常

**不触发**：纯 FSM 问题 / 参数超限

```
Agent(subagent_type="local-vision-analyzer",
  prompt="基于 trace-analyzer 的发现: <问题摘要>。
  分析 run <runDir> 的 vision 质量。
  检查 analysis.jsonl 的 items 类型/坐标/文本。")

---

## Phase 4 — 汇总报告

结构化输出：

```
═══════════════════════════════════════════════
  E2E Diagnosis Report — <scenarioId>
═══════════════════════════════════════════════

📋 Run Info
   runId / scenario / provider / model / device / duration

📊 Metrics
   steps / actions / scrolls / discovered / visited / completionReason

🔍 TraceTool
   verdict / cause / confidence / failingStep

🐛 Findings (按严重度排序)
   1. 🔴 <问题> — <trace-analyzer 归因>
   2. 🟡 <问题> — <fsm-analyzer / local-vision-analyzer 归因>
   ...

💡 Fix Suggestions
   引擎侧: <D-Gxx 建议>
   Vision 侧: <Vx 建议>
   Host 侧: <建议>

📂 Responsibility Boundaries
   引擎: <范围>
   Vision: <范围>
   Host 验证: <范围>
   FSM: <范围>

📁 Assets
   runDir / trace / analysis / screenshots
═══════════════════════════════════════════════
```

---

## 输出文档规范

Phase 4 的汇总报告按诊断结果分级存放：

| 结果 | 输出位置 | 条件 |
|------|---------|------|
| 有问题（Bug / 优化建议） | `docs/fix/YYYY-MM-DD-<scenarioId>-e2e-report.md` | Findings 非空 |
| 无问题（通过，无优化建议） | `/tmp/e2e-diagnose-YYYY-MM-DD-<scenarioId>.md` | Findings 为空 |

命名示例：
- 有问题：`docs/fix/2026-08-06-enumerate-settings-safely-e2e-report.md`
- 无问题：`/tmp/e2e-diagnose-2026-08-06-enumerate-settings-safely.md`

若同场景同日多次诊断，追加序号：`...-e2e-report-2.md`。

报告包含：Run Info / Metrics / Findings / Fix Suggestions / Responsibility Boundaries / Assets。

**关联文档**：
- 若产出 PRD，写入 `docs/prd/YYYY-MM-DD-<topic>-prd.md`
- 若产出修复方案，写入对应 `docs/fix/` 后同步到 `openspec/changes/<change>/`

---

## 职责边界声明

| 层 | 负责 | 工具 |
|----|------|------|
| 编排 | E2E 执行 → 诊断 → 报告 | 本 skill |
| 测试执行 | 模拟器 / 视觉服务 / dotnet test | host-test-runner skill |
| Run 数据 | 指标 / 决策 / 性能 | TraceTool CLI（不手写 Python） |
| 代码溯源 | C# 符号 / 调用链 / 诊断 | MCP（csharper-mcp / cwm-roslyn-navigator） |
| Trace 归因 | 运行行为 / 决策链 / 异常 | trace-analyzer agent |
| FSM 诊断 | 状态机正确性 / Handler 缺陷 | fsm-analyzer agent |
| Vision 诊断 | YOLO/OCR 质量 / item 类型 / 坐标 | local-vision-analyzer agent |
