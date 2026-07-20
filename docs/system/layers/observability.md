# Layers — Observability

> **Tier 3 · Layers**: Observability 层规格书。改 ITraceRecorder/ITraceStorage/ITraceService/TraceContext 时更新。
> 状态: Phase 2.2 完成 (trace-pipeline-three-layer change)
> 源码: `src/UniClaw.Core/Observability/`
> 约束: → constitution C-4 (Domain 零向上), D-17 (cross-cutting 定位), D-18 (TraceContext boundary), D-19 (三层 CQRS)
> 定位: **cross-cutting utility** — 被 StateMachine + Traversal 共同消费，非传统顶层 (→ D-17)

---

## 1. Type Inventory

### Enums (2)

| Enum | 值数 | 级别 | Cascade 影响 | Guard Test |
|------|------|------|-------------|-----------|
| `SpanType` | **11** | 火山 | operation_rules + trace_integrity 验证维度 | `SpanType_Has11Values` |
| `ErrorSeverity` | **5** | 丘陵 | ErrorRecord severity 分类, 日志级别映射 | 无 (Phase 3 Guard 候选) |

**SpanType (11 值 — 火山级)** (→ constitution/locked-enums.md):

```
DfsForward · DfsBacktrack · RestoreOp · SkipDangerous · PopupHandling
ContainerHandling · ErrorHandling · PageAnalysis · CacheOp · AICall · StateDecision
```

**ErrorSeverity (5 值 — 丘陵级)**:

```
Debug · Info · Warning · Error · Fatal
```

### Records (12)

| Record | 所在文件 | 核心字段 | 用途 |
|--------|---------|---------|------|
| **TraceContext** | TraceContext.cs | NodeId?, StepSpanId?, StepNumber?, TraceId? | 5 类型共享关联信封 (→ D-18) |
| **ExecutionRecord** | ITraceRecorder.cs | Action, Status, SpanType?, Context?, SpanId?, ChildNodeId?, ParentNodeId?, PageId?, TargetType?, TargetValue?, Depth?, DurationMs, Timestamp, Metadata? | 最重索引类型: DFS 树 + 动作记录 |
| **StateTransition** | ITraceRecorder.cs | FromState, ToState, Context?, FsmType?, Timestamp, Reason?, Metadata? | FSM 状态迁移 (FsmType 类型专属) |
| **ErrorRecord** | ITraceRecorder.cs | ErrorType, ErrorMessage, Severity, Context?, Timestamp, Metadata? | 错误记录 (ParentNodeId 已移除 → D-22) |
| **PageTransition** | ITraceRecorder.cs | FromPage, ToPage, TransitionType, Context?, DurationMs?, Timestamp, Metadata? | 页面导航 (DurationMs 类型专属) |
| **AICallRecord** | ITraceRecorder.cs | Capability, ProviderId, Success, LatencyMs, Context?, Tokens?, Timestamp | AI 调用追踪 (Tokens 类型专属) |
| **TraceSession** | ITraceRecorder.cs | TraceId, StartTime, EndTime?, Metadata? | 会话生命周期 (IsCompleted + GetDuration computed) |
| **TraversalTree** | TraceQueryResults.cs | Edges (ImmutableArray<TreeEdge>), RootNodeId | 树重建查询结果 |
| **TreeEdge** | TraceQueryResults.cs | Parent?, Child, Depth?, EntryStep? | DFS 树边 |
| **NodeSpans** | TraceQueryResults.cs | NodeId, Executions, Errors, PageTransitions, Transitions, AICalls | 某节点所有 span 聚合 |
| **NodeVisitTimeline** | TraceQueryResults.cs | NodeId, EntryStep?, ExitStep? | 节点访问时序 |
| **StepTimeline** | TraceQueryResults.cs | StepNumber, Executions, Transitions, Errors, PageTransitions, AICalls | 某步骤所有记录聚合 |
| **StepSpanGroup** | TraceQueryResults.cs | StepSpanId, Executions, Transitions, Errors, PageTransitions, AICalls | SpanId 分组聚合 |

### Interfaces (3 + 1)

| Interface | 方法数 | 角色 | 写/读 | 依赖注入 | Guard Test |
|-----------|-------|------|-------|---------|-----------|
| **ITraceRecorder** | **7** | 纯写契约 (async) | Write | 注入 ITraceStorage (接口) | `ITraceRecorder_Has7Methods` |
| **ITraceService** | 13 (1 prop + 12 method) | 纯读+查询 facade | Read | 消费 InMemoryTraceStorage (具体类 → D-19 ISP) | 无 |
| **ITraceStorage** | 14 (3 session + 5 write + 6 read) | 同步存储后端 | Both | 无外部依赖 | 无 |
| **IMetricsCollector** | 5 | 度量收集 | Write | 独立 | 无 |

### Classes (5)

| Class | 实现 | 依赖注入 | 用途 |
|-------|------|---------|------|
| **InMemoryTraceStorage** | ITraceStorage | 无 | 5 flat lists + 2 Dictionary indexes (_byNodeId, _bySpanType) + 2 concrete-only index methods (ISP → D-19) |
| **InMemoryTraceRecorder** | ITraceRecorder | ITraceStorage (接口) | 纯 async-over-sync wrapper, 7 方法全部 Task.CompletedTask 委托 (→ D-19 D-6) |
| **InMemoryTraceService** | ITraceService | InMemoryTraceStorage (具体类) | 查询实现: flat read 委托 storage, 6 查询方法用 indexes/LINQ |
| **FileTraceStorage** | ITraceStorage | IFileProvider (接口) | JSONL 文件存储后端 (D-95) |
| **PhysicalFileProvider** | IFileProvider | 无 | 真实文件系统委托 (System.IO) |

### Interfaces (4 + 1, + IFileProvider)

| Interface | 方法数 | 角色 | 写/读 | 依赖注入 |
|-----------|-------|------|-------|---------|
| **IFileProvider** | **6** | 纯文件抽象 | Both | 注入 FileTraceStorage (D-95) |

### Directory Layout

```
Observability/               ← src/UniClaw.Core/Observability/
  File/                      ← FileTraceStorage + IFileProvider (D-95)
    FileTraceStorage.cs       JSONL backend implementing ITraceStorage
    IFileProvider.cs           6-method file abstraction (EnsureDirectory, AppendLine, ReadAllText, ReadAllLines, FileExists, DirectoryExists)
    PhysicalFileProvider.cs    Real filesystem → System.IO
  InMemory/                   ← Phase 2.2 original (moved from Observability/ root)
    InMemoryTraceStorage.cs
    InMemoryTraceRecorder.cs
    InMemoryTraceService.cs
  IMetricsCollector.cs
  ITraceRecorder.cs
  ITraceService.cs
  ITraceStorage.cs
  TraceContext.cs
  TraceQueryResults.cs
  TraceSession.cs
```

---

## 2. TraceContext — 关联信封

TraceContext 是 **5 种 ITraceRecorder record 类型共享的关联字段封装** (→ D-18)。

**4-field boundary rule**: TraceContext 只含 **ALL 5 类型共享** 的字段:

| 字段 | 类型 | 语义 | 来源 |
|------|------|------|------|
| `NodeId` | string? | 事件发生节点 (NOT DFS parent) | ctx.CurrentFrame?.NodeId |
| `StepSpanId` | string? | 每引擎步骤分组键 (= StepStart 的 SpanId → D-20) | _currentStepSpanId |
| `StepNumber` | int? | 步骤序号 | ctx.StepCount |
| `TraceId` | string? | trace 会话标识 | _traceId |

**不在 TraceContext 的类型专属字段** (→ D-18 boundary):

| 类型专属字段 | 所属 Record | 原因 |
|-------------|-----------|------|
| FsmType | StateTransition | 仅 FSM 迁移有 FSM 类型 |
| SpanId | ExecutionRecord | 仅 execution 有唯一 span 标识 |
| ChildNodeId | ExecutionRecord | 仅 DfsForward 有子节点 |
| ParentNodeId | ExecutionRecord | 仅 DFS 树重建有 DFS 父 |
| PageId | ExecutionRecord | 仅 page analysis 有页面标识 |
| TargetType / TargetValue | ExecutionRecord | 仅 action execution 有目标 (→ D-21) |
| Depth | ExecutionRecord | 仅 DfsForward 有深度 |
| DurationMs | PageTransition | 仅页面过渡有耗时 |
| Tokens | AICallRecord | 仅 AI 调用有 token 计数 |

Guard: `TraceContext_Has4Fields` — 阻止意外添加类型专属字段。

Phase 3 扩展: VisitSpanId + ParentSpanId 加入 TraceContext (无需改任何 record type)。

---

## 3. Three-Layer CQRS Architecture

Observability 层采用 **CQRS at the interface level** — 写/读在接口层分离 (→ D-19):

```
TraceCoordinator ──writes──→ ITraceRecorder (7 async methods)
                              │
                              │ wraps (async-over-sync)
                              │
                              ▼
                         ITraceStorage (14 sync methods)
                              │
                              │ reads
                              │
                              ▼
ITraceService ←──reads──── InMemoryTraceStorage (concrete, + 2 index methods)

FileTraceStorage (same ITraceStorage contract) → IFileProvider → PhysicalFileProvider (System.IO)
                                │
                                │ JSONL write/read (sync)
                                │
                           trace/{traceId}/trace.jsonl
                           trace/{traceId}/session.json
```

### ITraceRecorder — 纯写契约 (7 methods)

| Method | 写入目标 | 返回 |
|--------|---------|------|
| `StartSessionAsync` | TraceSession | Task |
| `EndSessionAsync` | TraceSession (EndTime) | Task |
| `RecordExecutionAsync` | ExecutionRecord → _executions | Task |
| `RecordTransitionAsync` | StateTransition → _transitions | Task |
| `RecordErrorAsync` | ErrorRecord → _errors | Task |
| `RecordPageTransitionAsync` | PageTransition → _pageTransitions | Task |
| `RecordAICallAsync` | AICallRecord → _aiCalls | Task |

**SHALL NOT**: 含查询方法 (GetXxxAsync)、CurrentSession getter、ExportTraceAsync (→ D-19)。

### ITraceStorage — 同步存储后端 (14 methods)

| Category | Methods | 说明 |
|----------|---------|------|
| Session lifecycle (3) | SetSession, EndSession, CurrentSession | TraceSession 状态管理 |
| Sync write (5) | AddExecution, AddTransition, AddError, AddPageTransition, AddAICall | void, 直接写入 flat list + 更新 index |
| Sync read (6) | GetExecutions, GetTransitions, GetErrors, GetPageTransitions, GetAICalls, Export | IReadOnlyList 直接引用 (非拷贝) |

**设计原则 D-6**: 内存操作总是同步。async 层在 ITraceRecorder (消费侧契约)。

### ITraceService — 纯读+查询 facade (13 members)

| Category | Members | 说明 |
|----------|---------|------|
| Session (1) | CurrentSession property | TraceSession 状态 |
| Flat read (5) | GetExecutions, GetTransitions, GetErrors, GetPageTransitions, GetAICalls | 委托 _storage.GetXxx() |
| Node+Span queries (6) | ReconstructTree, GetNodeSpans, GetNodeVisitTimeline, GetStepTimeline, GetBySpanType, GetStepSpanGroup | indexes + LINQ via Context?.NodeId/StepNumber/StepSpanId |
| Export (1) | ExportTrace | 委托 _storage.Export() |

**SHALL NOT**: 含写方法或会话生命周期方法。

### ISP — 非对称依赖注入 (→ D-19 D-2b)

| 实现 | 注入 | 原因 |
|------|------|------|
| InMemoryTraceRecorder | **ITraceStorage (接口)** | 只需写方法, 不需 indexes |
| InMemoryTraceService | **InMemoryTraceStorage (具体类)** | 需要 GetByNodeId + GetBySpanType index 方法 (→ ISP: 不所有 ITraceStorage 实现都有内存 indexes) |

---

## 4. InMemoryTraceStorage — Indexes

5 flat lists + 2 incrementally-built Dictionary indexes:

| Index | Key | 覆盖 Record Type | Key 来源 | Null 行为 |
|-------|-----|------------------|---------|-----------|
| `_byNodeId` | `r.Context?.NodeId` | ExecutionRecord only | TraceContext.NodeId | Context=null 或 NodeId=null → 不索引 |
| `_bySpanType` | `r.SpanType` | ExecutionRecord only | ExecutionRecord.SpanType | SpanType=null → 不索引 |

**2 concrete-only index methods** (NOT on ITraceStorage → ISP):

| Method | 返回 | 用途 |
|--------|------|------|
| `GetByNodeId(string nodeId)` | List<ExecutionRecord> | InMemoryTraceService.GetNodeSpans + GetNodeVisitTimeline |
| `GetBySpanType(SpanType spanType)` | List<ExecutionRecord> | InMemoryTraceService.ReconstructTree + GetBySpanType |

---

## 5. TraceCoordinator Method Mapping

TraceCoordinator 是 Traversal 层组件 (定义在 TraversalEngine.cs:663), 消费 ITraceRecorder 接口:

| Method | 产出 Record | Context | SpanId | ChildNodeId | ParentNodeId | FsmType | Other |
|--------|-----------|---------|--------|-------------|-------------|---------|-------|
| `RecordStepStart` | ExecutionRecord | BuildCorrelation() (StepSpanId override) | ✅ = StepSpanId | — | — | — | StepSpanId 生命周期开始 |
| `RecordStepEnd` | ExecutionRecord | BuildCorrelation() | — | — | — | — | DurationMs from stopwatch; StepSpanId 释放 |
| `RecordPageAnalysis` | ExecutionRecord | BuildCorrelation() | ✅ | — | — | — | SpanType=PageAnalysis, Depth from ctx |
| `RecordActionExecution` (typed) | ExecutionRecord | BuildCorrelation() | ✅ | — | — | — | SpanType, TargetType, TargetValue (→ D-21) |
| `RecordSkipSpan` | ExecutionRecord | BuildCorrelation() | ✅ | ✅ matchResult.ChildNodeId | — | — | SpanType=DfsForward |
| `RecordDynamicLifecycle` | ExecutionRecord | BuildCorrelation() | ✅ | ✅ child.NodeId | ✅ parent.NodeId | — | SpanType=DfsForward |
| `RecordAICallSpan` | AICallRecord | BuildCorrelation() | — | — | — | — | Tokens?, LatencyMs |
| `RecordErrorSpan` | ErrorRecord | BuildCorrelation() | — | — | — | — | Severity, ErrorType |
| `RecordStateTransition` | StateTransition | BuildCorrelation() | — | — | — | ✅ "TraversalFSM" | Reason? |
| `RecordRootNodePushed` | StateTransition | **null** (before step loop) | — | — | — | ✅ "TraversalFSM" | 特例: 无 Context |
| `RecordPageTransition` | PageTransition | BuildCorrelation() | — | — | — | — | DurationMs (类型专属) |
| `RecordDecision` | ExecutionRecord | BuildCorrelation() | ✅ | — | — | — | SpanType=StateDecision |
| `RecordStateDecision` | ExecutionRecord | BuildCorrelation() | ✅ | — | — | — | SpanType=StateDecision |
| `RecordMetricsAsSpans` | — | stub (no-op) | — | — | — | — | Phase 3 |
| `RecordExecutionSpan` | — | stub (no-op) | — | — | — | — | Phase 3 |

**SpanId 生成**: `_spanCounter` 格式 `"{traceId}-{counter:D6}"`, 每次生成递增, 在 trace session 内唯一。

**StepSpanId 生命周期**: RecordStepStart → 赋值 _currentStepSpanId=SpanId → BuildCorrelation() 使用 → RecordStepEnd → 释放 _currentStepSpanId=null (→ D-20)。

**BuildCorrelation()**: 从 ITraversalContext? ctx 构造 TraceContext。ctx=null → 返回 null。RecordStepStart 用 `with` 表达式覆盖 StepSpanId 为刚生成的 SpanId。

**Log-and-Continue pattern**: 所有 Record 方法委托 `LogAndContinue(Action)` — 异静吞, 不传播 (→ patterns/dispatch-table.md)。

---

## 6. Dependency

```
Observability → Domain.Common (TargetType enum — ExecutionRecord.TargetType → D-17/D-21)
Observability → Domain.Vision (无引用)
Observability → Domain.Content (无引用)
Observability → Graph (无引用)
Observability → StateMachine (无引用)
Observability → Traversal (无引用)

StateMachine → Observability (ITraceRecorder 接口引用 — acknowledged upward, D-17)
Traversal → Observability (ITraceRecorder 接口引用 — acknowledged upward, D-17)
```

**Cross-cutting 定位**: Observability 被两层消费的向上引用是架构现实, 不是设计缺陷 (→ D-17)。

---

## 7. Guard Tests (ArchitectureGuardTests.cs)

| Guard Test | 验证内容 | CI-blocking |
|-----------|---------|------------|
| `SpanType_Has11Values` | SpanType enum 11 值锁定 | ✅ |
| `TraceContext_Has4Fields` | TraceContext 4 属性 (阻止类型专属字段) | ✅ |
| `ITraceRecorder_Has7Methods` | ITraceRecorder 7 方法 (阻止查询方法回归) | ✅ |

---

## 8. Phase 3 Roadmap

| Item | Description | Priority |
|------|-----------|---------|
| VisitSpanId + ParentSpanId | TraceContext 从 4→6 字段, 无需改 record type | P2 |
| IAsyncTraceStorage | async 存储后端 (DB/file), 不改 ITraceStorage | P3 |
| ErrorSeverity Guard test | 5 值锁定 | P3 |
| IMetricsCollector 实现 | 当前无实现, 接口已定义 | P3 |
| InMemoryTraceStorage TTL/size limits | 当前无限制 | P3 |
