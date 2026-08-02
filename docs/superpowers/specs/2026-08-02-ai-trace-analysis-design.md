# AI 集成 Trace 分析接口设计

> 状态: draft | 日期: 2026-08-02

## 1. 动机

硬编码完成判定规则难以泛化——不同场景的条目数、页面结构、迭代模式各不相同。将 `ITraceQuery` 暴露为 Agent Tool，让 AI Agent 根据场景上下文自适应判断是否终止。

## 2. 架构

```
┌─ Host ────────────────────────────────────────────┐
│                                                    │
│  CompletionMonitor                                  │
│    │                                                │
│    ├── 规则层（仅 1 条）                             │
│    │   skipped 连续 ≥ 5 → AgentToolTrigger.Warn     │
│    │                                                │
│    └── AI Agent 层（关键决策点）                     │
│         触发点: 每 10 步 / 检测到 endOfList /        │
│                 每完成 5 个 entry.visited            │
│          │                                          │
│          ▼                                          │
│    Agent (spawn via ITraceQuery tools)              │
│      tools:                                         │
│        ├─ query_spans_by_type(spanType)             │
│        ├─ query_child_spans(parentSpanId)            │
│        ├─ query_span(spanId)                        │
│        └─ plan_context()                            │
│                                                     │
│      → 返回 CompletionVerdict                       │
│         { verdict: "terminate"|"continue",           │
│           reason: "...", confidence: 0.0-1.0 }      │
└────────────────────────────────────────────────────┘
```

## 3. Tool 定义

### 3.1 query_spans_by_type

```json
{
  "name": "query_spans_by_type",
  "description": "Query all trace spans of a specific type from the current engine run. Span types include: entry.observed (discovered items), entry.visited (clicked items), entry.skipped (safety-denied items), engine.step (step metrics), action.click/scroll/back (ADB operations), ai.call (model invocations).",
  "parameters": {
    "spanType": "string — e.g. 'entry.observed', 'entry.visited', 'engine.step'"
  },
  "returns": {
    "count": "int — total number of matching spans",
    "items": "array of { spanId, parentSpanId, spanName, startTime, endTime, durationMs, attributes }"
  }
}
```

### 3.2 query_child_spans

```json
{
  "name": "query_child_spans",
  "description": "Get all child spans nested under a parent span. Use this to understand what happened within a specific step or entry — e.g. query child spans of an 'entry.visited' span to see its action.click and ai.analyze children.",
  "parameters": {
    "parentSpanId": "string — parent span id from a previous query"
  },
  "returns": {
    "count": "int",
    "items": "array of child TraceSpan records"
  }
}
```

### 3.3 query_span

```json
{
  "name": "query_span",
  "description": "Get a single span by its id. Useful for checking the status or attributes of a specific span.",
  "parameters": {
    "spanId": "string"
  },
  "returns": "TraceSpan record or null"
}
```

### 3.4 plan_context

```json
{
  "name": "plan_context",
  "description": "Get the current plan's metadata — scenario mode, target, depth limits, expected pages. Provides the Agent with the scenario's intended goal so it can judge completion accordingly.",
  "parameters": {},
  "returns": {
    "scenarioId": "string",
    "mode": "enumerate_first_level | locate_one_item",
    "maxDepth": "int",
    "maxSteps": "int",
    "entryApp": "string",
    "completionType": "Exhaustive | TargetFound | Timeout | MaxSteps"
  }
}
```

## 4. Agent System Prompt

```
You are a test completion analyst. A traversal engine is executing a
scenario on an Android emulator. Your job is to decide whether the run
has completed its goal, based on trace span data.

Current scenario:
  {plan_context output}

Available tools:
  query_spans_by_type — count and list spans of a given type
  query_child_spans   — get children of a parent span
  query_span          — get a single span by id

Decision criteria by scenario mode:

**enumerate_first_level:**
  Goal: visit every first-level Settings entry, record what's inside,
  then return.  The run is complete when:
  - All discovered entries have been visited or skipped
  - End-of-list is detected (engine.step with scroll.end_reached=true)
  - The engine has returned to the root page
  
  The run is stuck if:
  - Same entry is visited > 2 times (loop)
  - > 5 consecutive entry.skipped without any entry.visited
  - No new entry.observed in the last 10 steps

**locate_one_item:**
  Goal: find and click a specific target.  The run is complete when:
  - An entry.visited with name matching the target appears
  - Followed by a page_transition
  
  The run is stuck if:
  - All visible entries have been visited without finding target
  - Engine is scrolling without finding new entries (scroll.end_reached
    without matching target)

**Return format:**
{
  "verdict": "terminate" | "continue",
  "reason": "brief explanation",
  "confidence": 0.0-1.0
}
```

## 5. 触发策略

| 触发条件 | 频率 | 说明 |
|---------|------|------|
| 每 10 步 | ~10s | 常规检查 |
| `scroll.end_reached` 出现 | 即时 | 关键信号 |
| `entry.visited` 每 +5 | ~15-30s | 进度里程碑 |
| `entry.skipped` 连续 ≥ 5 | 即时 | 异常预警 |

## 6. 成本控制

| 策略 | 说明 |
|------|------|
| 规则层先行 | `skipped ≥ 5` 先触发 Warn，AI 无需处理 |
| 调用上限 | 每 run 最多 10 次 Agent 调用 |
| 模型选择 | 用 haiku（低延迟、低成本） |
| 无网络 fallback | Agent 不可用时降级为规则引擎（仅 Halt 终止） |

## 7. 与规则引擎的关系

```
Phase 1 (现在):   规则引擎 → 仅 Halt（pending==0 && endOfList）
Phase 2:          规则引擎 + Agent → 规则处理低置信度场景，Agent 处理复杂判定
Phase 3 (未来):   纯 Agent → 规则完全由 Agent 替代，保留 skipped≥5 作为 kill-switch
```

## 8. 文件清单

| 文件 | 内容 |
|------|------|
| `Host/Analysis/AgentCompletionAnalyzer.cs` | Agent 调用封装 |
| `Host/Analysis/AgentTraceTools.cs` | Tool 函数实现（适配 Agent SDK tool schema） |
| `Host/Analysis/CompletionMonitor.cs` | 调度器（触发规则 + Agent 调用） |
| `Host/Analysis/RuleEngine.cs` | 规则引擎（skipped 检测 + Halt） |
